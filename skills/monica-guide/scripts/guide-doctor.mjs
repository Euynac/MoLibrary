import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { GuideError, exists, loadProjectConfig, loadState, normalizePath, run, semverChannel, stateFilePath, workspaceKey } from './guide-shared.mjs';
import { discoverChannelReleaseTag, loadCatalog, loadReleaseArtifacts, loadReleaseIndex, resolveProfileClosure, resolveRequiredSkillClosure, resolveRelease, skillsCliSpec } from './guide-catalog.mjs';
import { assertProjectReferenceRelease, profileRepositoryIssues, workspaceDetection } from './guide-detect.mjs';
import { instructionDiagnostics } from './guide-plan.mjs';
import { retainedGlobalSkillTransactions } from './guide-install-transaction.mjs';
import { findSourceResolver, resolveCachedSource, verifyLocalSource } from './guide-source.mjs';
import { buildSourceManifest, compareManagedSkillRecords, verifyDiscoveryPayload, verifyLocalSkillSource } from './guide-installation.mjs';

function check(id, status, message, details = undefined, remediation = undefined) {
  return { id, status, message, ...(details === undefined ? {} : { details }), ...(remediation ? { remediation } : {}) };
}

function skillLocations(agent, name) {
  if (agent === 'claude-code') return [path.join(os.homedir(), '.claude', 'skills', name)];
  return [path.join(os.homedir(), '.agents', 'skills', name), path.join(os.homedir(), '.codex', 'skills', name)];
}

function offlineDiscoveryPayload(agent) {
  const roots = agent === 'claude-code'
    ? [path.join(os.homedir(), '.claude', 'skills')]
    : [path.join(os.homedir(), '.agents', 'skills'), path.join(os.homedir(), '.codex', 'skills')];
  const payload = [];
  const seen = new Set();
  for (const root of roots) {
    if (!exists(root)) continue;
    for (const entry of fs.readdirSync(root, { withFileTypes: true })) {
      if (!entry.isDirectory() || seen.has(entry.name)) continue;
      seen.add(entry.name);
      payload.push({ name: entry.name, path: path.join(root, entry.name), scope: 'global' });
    }
  }
  return payload;
}

function discoveryCheck(agent, skills, manifest, cliSpec, offline, canonicalSkills) {
  if (offline) {
    try {
      const payload = offlineDiscoveryPayload(agent);
      verifyDiscoveryPayload(payload, skills, manifest);
      const extras = payload.map((entry) => entry.name).filter((name) => canonicalSkills.has(name) && !skills.includes(name)).sort();
      if (extras.length) return check(`skill-discovery:${agent}`, 'error', `${agent} has catalog-managed Monica skills outside the recorded global set: ${extras.join(', ')}.`, { extras }, 'Run update to adopt and reinstall them at the active immutable release.');
      return check(`skill-discovery:${agent}`, 'ok', `${agent} installed skill contents match the verified immutable manifest (offline).`);
    } catch (error) {
      return check(`skill-discovery:${agent}`, 'error', error.message, error.details, 'Run a full update to reinstall unhealthy managed skills and verify the complete global set.');
    }
  }
  const executable = process.env.MONICA_GUIDE_NPX || 'npx';
  const result = run(executable, ['--yes', cliSpec, 'ls', '-g', '-a', agent, '--json']);
  if (result.status !== 0) return check(`skill-discovery:${agent}`, 'error', `${agent} global discovery could not be queried with skills ls --json.`);
  let payload;
  try { payload = JSON.parse(result.stdout); } catch { return check(`skill-discovery:${agent}`, 'error', `${agent} returned invalid skills ls --json output.`); }
  if (!Array.isArray(payload)) return check(`skill-discovery:${agent}`, 'error', `${agent} skills ls --json did not return a top-level array.`);
  try {
    verifyDiscoveryPayload(payload, skills, manifest);
    const extras = payload.map((entry) => entry?.name).filter((name) => canonicalSkills.has(name) && !skills.includes(name)).sort();
    if (extras.length) return check(`skill-discovery:${agent}`, 'error', `${agent} has catalog-managed Monica skills outside the recorded global set: ${extras.join(', ')}.`, { extras }, 'Run update to adopt and reinstall them at the active immutable release.');
    return check(`skill-discovery:${agent}`, 'ok', `${agent} reports every selected Monica skill with immutable manifest-matching content.`);
  } catch (error) {
    return check(`skill-discovery:${agent}`, 'error', error.message, error.details, 'Run a full update to reinstall unhealthy managed skills and verify the complete global set.');
  }
}

function bindingCheck(binding, closure, release, profile, resolverPath, sourceSkillError) {
  if (!closure?.source?.required && !binding) return check('source-binding', 'ok', 'This profile does not require an exact Monica source binding.');
  if (!binding) return check('source-binding', 'error', 'The selected profile requires an exact Monica source binding.', undefined, 'Preview the source intent or init with an exact cached/local source.');
  if (!exists(binding.sourcePath)) return check('source-binding', 'error', 'The stored Monica source path is unavailable.', { sourcePath: binding.sourcePath });
  if (binding.commit !== release?.commit) return check('source-binding', 'error', `Source commit ${binding.commit} does not match expected release commit ${release?.commit || 'unresolved'}.`);
  if (binding.verificationState !== 'verified' || !['exact_commit', 'exact_tag'].includes(binding.resolutionKind)) {
    return check('source-binding', 'error', 'Stored source provenance is not verified and exact.');
  }
  if (profile === 'framework-contributor' || binding.provenance === 'local-git') {
    try {
      const verified = verifyLocalSource(binding.sourcePath, {
        access: profile === 'framework-contributor' ? 'read-write' : 'read-only',
        expectedCommit: release.commit,
      });
      if (verified.dirty) return check('source-binding', 'warning', 'Writable Monica framework source has local changes.', { sourcePath: binding.sourcePath, commit: binding.commit });
    } catch (error) {
      return check('source-binding', 'error', error.message, error.details);
    }
  } else if (binding.provenance?.resolver === 'inspect-dependency-source') {
    try {
      const refreshed = resolveCachedSource({ exactRef: release.commit, access: 'read-only', resolverPath });
      if (path.resolve(refreshed.sourcePath) !== path.resolve(binding.sourcePath)) {
        return check('source-binding', 'warning', 'The verified cache now resolves this release to a different local path; run update to refresh state.', { storedPath: binding.sourcePath, resolvedPath: refreshed.sourcePath });
      }
    } catch (error) {
      return check('source-binding', 'error', error.message, error.details);
    }
  } else {
    if (binding.provenance?.resolver !== 'inspect-dependency-source') return check('source-binding', 'error', 'Stored source provenance is not a supported local Git or inspect-dependency-source binding.');
  }
  if (sourceSkillError) return check('source-binding', 'error', sourceSkillError.message, sourceSkillError.details);
  if (profile !== 'framework-contributor' && binding.access !== 'read-only') return check('source-binding', 'error', `${profile} cannot persist writable Monica source access.`);
  return check('source-binding', 'ok', `Exact Monica source is bound at ${binding.commit}.`, { sourcePath: binding.sourcePath, access: binding.access });
}

export async function inspectEnvironment(options = {}) {
  const workspace = normalizePath(options.workspace);
  if (!exists(workspace) || !fs.statSync(workspace).isDirectory()) throw new GuideError('workspace_unavailable', `Workspace is not a directory: ${workspace}.`);
  const statePath = stateFilePath(options.state);
  const state = loadState(statePath);
  const recoveryTransactions = retainedGlobalSkillTransactions(statePath);
  const project = loadProjectConfig(workspace);
  const bootstrapInfo = loadCatalog({ catalogPath: options.catalog, indexPath: options.index });
  const detection = workspaceDetection(workspace);
  const profile = options.profile || project.config?.profile || detection.repository.candidateProfile;
  const profileConfirmed = Boolean(options.profile || project.config?.profile);
  const repositoryIssues = profileRepositoryIssues(workspace, profile, detection.repository);
  const channel = options.channel || project.config?.channel || semverChannel(detection.frameworkVersion.version) || 'stable';
  const capabilities = [...new Set(options.capabilities?.length ? options.capabilities : (project.config?.capabilities || detection.repository.capabilities || []))].sort();
  const key = workspaceKey(workspace, detection.repository.identity);
  let targetCatalog = bootstrapInfo.catalog;
  let targetCatalogDigest = bootstrapInfo.catalogDigest;
  let index = bootstrapInfo.index;
  let releaseIndex = null;
  let releaseArtifacts = null;
  let installManifest = null;
  let release = null;
  let releaseError = detection.versionError ? new GuideError(detection.versionError.code, detection.versionError.message, detection.versionError.details) : null;
  if (!releaseError) {
    try {
      if (channel === 'source') {
        release = resolveRelease(index, {
          channel,
          frameworkVersion: detection.frameworkVersion.version,
          sourceRef: options.sourceRef || project.config?.expectedCatalogRelease?.commit,
        });
        const binding = state.sourceBindings[key];
        if (!binding || binding.commit !== release.commit || !exists(binding.sourcePath)) throw new GuideError('source_binding_unavailable', 'Source-channel diagnosis requires the stored exact source binding.');
        const sourceCatalogInfo = loadCatalog({ catalogPath: path.join(binding.sourcePath, '.monica', 'agent-skill-catalog.json'), indexPath: bootstrapInfo.indexPath });
        targetCatalog = sourceCatalogInfo.catalog;
        targetCatalogDigest = sourceCatalogInfo.catalogDigest;
        installManifest = buildSourceManifest(binding.sourcePath, targetCatalog);
        release.catalogDigest = targetCatalogDigest;
        release.skillDigests = installManifest.skillDigests;
        release.skillRevisions = installManifest.skillRevisions;
        release.skillLastChangedIn = installManifest.skillLastChangedIn;
      } else {
        let contractTag = options.releaseTag
          || project.config?.expectedCatalogRelease?.indexTag
          || project.config?.expectedCatalogRelease?.tag;
        if (!contractTag) {
          if (options.offline) throw new GuideError('offline_release_index_unavailable', `No initialized repository release or explicit immutable ${channel} release tag is available offline.`);
          contractTag = await discoverChannelReleaseTag(channel, options.fetchImplementation);
        }
        releaseIndex = await loadReleaseIndex({
          releaseTag: contractTag,
          releaseIndexPath: options.releaseIndex,
          releaseIndexUrl: options.releaseIndexUrl,
          offline: options.offline,
          state,
          statePath,
          fetchImplementation: options.fetchImplementation,
        });
        if (releaseIndex) index = releaseIndex.index;
        release = resolveRelease(index, { channel, frameworkVersion: detection.frameworkVersion.version });
        releaseArtifacts = await loadReleaseArtifacts({
          release,
          releaseCatalogPath: options.releaseCatalog,
          releaseManifestPath: options.releaseManifest,
          offline: options.offline,
          state,
          statePath,
          fetchImplementation: options.fetchImplementation,
        });
        targetCatalog = releaseArtifacts.catalog;
        targetCatalogDigest = releaseArtifacts.catalogDigest;
        installManifest = releaseArtifacts.manifest;
      }
      assertProjectReferenceRelease(detection.frameworkVersion, release);
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      releaseError = error;
    }
  }
  let closure = null;
  let closureError = null;
  try { if (profile) closure = resolveProfileClosure(targetCatalog, profile, capabilities); }
  catch (error) { closureError = error; }
  let skillChanges = [];
  let skillMetadataError = null;
  if (release && installManifest && closure) {
    const recordedManagedSkills = Object.keys(state.managedSkills);
    const retiredManagedSkills = recordedManagedSkills.filter((name) => {
      const entry = targetCatalog.skills[name];
      return !entry || entry.ownership !== 'monica' || entry.managed === false;
    });
    if (retiredManagedSkills.length) {
      skillMetadataError = {
        code: 'managed_skill_missing_from_release',
        message: `The target catalog no longer manages recorded Monica skills: ${retiredManagedSkills.join(', ')}. Resolve their diagnostic aliases or remove them explicitly before switching releases.`,
        details: { skills: retiredManagedSkills },
      };
    }
    try {
      const managedRoots = [...new Set([...recordedManagedSkills, ...closure.selected])]
        .filter((name) => targetCatalog.skills[name]?.ownership === 'monica' && targetCatalog.skills[name]?.managed !== false);
      const managedSkillNames = resolveRequiredSkillClosure(targetCatalog, managedRoots);
      skillChanges = compareManagedSkillRecords(state.managedSkills, managedSkillNames, release, installManifest);
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      skillMetadataError ||= { code: error.code, message: error.message, details: error.details };
    }
  }
  let sourceSkillError = null;
  const sourceBinding = state.sourceBindings[key] || null;
  if (sourceBinding && installManifest) {
    try { verifyLocalSkillSource(sourceBinding.sourcePath, targetCatalog, installManifest); }
    catch (error) {
      if (!(error instanceof GuideError)) throw error;
      sourceSkillError = { code: error.code, message: error.message, details: error.details };
    }
  }
  return {
    schemaVersion: 1,
    workspace,
    statePath,
    recoveryTransactions,
    repository: detection.repository,
    candidateProfile: detection.repository.candidateProfile,
    profile,
    profileConfirmed,
    repositoryIssues,
    channel,
    capabilities,
    frameworkVersion: detection.frameworkVersion,
    versionError: detection.versionError,
    projectConfig: project.config,
    activeRelease: state.activeRelease,
    targetRelease: release,
    releaseError: releaseError ? { code: releaseError.code || 'release_error', message: releaseError.message, details: releaseError.details } : null,
    releaseIndexSource: releaseIndex?.source || null,
    releaseArtifactSource: releaseArtifacts?.source || (channel === 'source' ? 'exact-source-binding' : null),
    installManifest,
    closure,
    closureError: closureError ? { code: closureError.code || 'closure_error', message: closureError.message } : null,
    skillChanges,
    skillMetadataError,
    sourceBinding,
    sourceSkillError,
    contributionPreference: state.contributionPreferences[key] || 'ask',
    instructionState: instructionDiagnostics(workspace, targetCatalog, project.config),
    nestedInstructions: detection.nestedInstructions,
    catalog: {
      version: targetCatalog.catalogVersion,
      digest: targetCatalogDigest,
      path: releaseArtifacts?.cachePaths?.catalog || (channel === 'source' ? state.sourceBindings[key]?.sourcePath : bootstrapInfo.catalogPath),
      indexPath: bootstrapInfo.indexPath,
      skillsCliSpec: skillsCliSpec(targetCatalog),
      managedSkills: Object.entries(targetCatalog.skills)
        .filter(([, entry]) => entry.ownership === 'monica' && entry.managed !== false)
        .map(([name]) => name)
        .sort(),
    },
    catalogAliases: targetCatalog.aliases || {},
    state,
    key,
  };
}

export async function doctor(options = {}) {
  const environment = await inspectEnvironment(options);
  const checks = [];
  const repository = environment.repository;
  if (repository.identity || environment.candidateProfile) checks.push(check('repository', 'ok', repository.reason, { identity: repository.identity, confidence: repository.confidence }));
  else checks.push(check('repository', 'warning', repository.reason, undefined, 'Choose a profile explicitly; Monica Guide will not guess for a non-Monica repository.'));
  if (environment.repositoryIssues.length) {
    checks.push(check(
      'repository-prerequisites',
      'error',
      environment.repositoryIssues.map((issue) => issue.message).join(' '),
      { issues: environment.repositoryIssues },
      'Select the writable canonical Git root with a canonical origin or upstream remote.',
    ));
  } else if (['framework-contributor', 'docs-contributor'].includes(environment.profile)) {
    checks.push(check('repository-prerequisites', 'ok', `${environment.profile} repository identity, Git root, and writability prerequisites are satisfied.`));
  }

  if (!environment.profile) checks.push(check('profile', 'error', 'No Monica profile is selected.'));
  else if (!environment.profileConfirmed) checks.push(check('profile', 'warning', `Candidate profile ${environment.profile} is not confirmed.`, undefined, `Rerun init with --profile ${environment.profile} after confirmation.`));
  else checks.push(check('profile', 'ok', `Profile ${environment.profile} is confirmed.`));

  if (environment.versionError) checks.push(check('framework-version', 'error', environment.versionError.message, environment.versionError.details));
  else if (environment.frameworkVersion.version) checks.push(check('framework-version', 'ok', `Resolved Monica ${environment.frameworkVersion.version} from ${environment.frameworkVersion.tier}.`));
  else checks.push(check('framework-version', 'warning', 'No exact Monica framework version was detected.'));

  if (environment.releaseError) checks.push(check('immutable-release', 'error', environment.releaseError.message, environment.releaseError.details));
  else if (environment.targetRelease) checks.push(check('immutable-release', 'ok', `Resolved immutable release ${environment.targetRelease.id} at ${environment.targetRelease.commit}.`));

  if (environment.projectConfig?.expectedCatalogRelease && environment.targetRelease
    && environment.projectConfig.expectedCatalogRelease.id !== environment.targetRelease.id) {
    checks.push(check('project-release', 'error', `Repository expects ${environment.projectConfig.expectedCatalogRelease.id}, but resolution selected ${environment.targetRelease.id}.`));
  } else if (environment.projectConfig) checks.push(check('project-release', 'ok', `Repository configuration expects ${environment.projectConfig.expectedCatalogRelease?.id || 'an unresolved release'}.`));
  else checks.push(check('project-release', 'warning', 'Repository has not been initialized by Monica Guide.'));

  if (environment.activeRelease && environment.targetRelease && environment.activeRelease.id !== environment.targetRelease.id) {
    checks.push(check('global-release', 'error', `Global release ${environment.activeRelease.id} conflicts with repository release ${environment.targetRelease.id}.`, undefined, 'Explicitly switch the one global release or upgrade the project.'));
  } else if (environment.activeRelease) checks.push(check('global-release', 'ok', `Global Monica skills are recorded at ${environment.activeRelease.id}.`));
  else checks.push(check('global-release', 'warning', 'No active global Monica skill release is recorded.'));

  if (environment.skillMetadataError) {
    checks.push(check(
      'managed-skill-versions',
      'error',
      environment.skillMetadataError.message,
      environment.skillMetadataError.details,
      environment.skillMetadataError.code === 'managed_skill_missing_from_release'
        ? 'Resolve the catalog alias or explicitly remove the obsolete global skill and state record before retrying update.'
        : 'Run a full verified update after resolving the reported skill metadata contract error.',
    ));
  } else {
    for (const entry of environment.skillChanges) {
      const target = entry.target.revision === null
        ? `source@${environment.targetRelease?.commit || 'unknown'}`
        : `r${entry.target.revision} (${entry.target.lastChangedIn})`;
      const installed = !entry.installed || entry.installed.digest === null
        ? 'unknown'
        : entry.installed.revision === null
          ? `source@${environment.activeRelease?.commit || 'unknown'}`
          : `r${entry.installed.revision} (${entry.installed.lastChangedIn})`;
      const status = entry.changeState === 'unchanged'
        ? 'ok'
        : entry.changeState === 'unknown' ? 'error' : 'warning';
      const remediation = entry.changeState === 'unknown'
        ? 'Run a full verified update; migrated or incomplete metadata cannot be treated as current.'
        : entry.changeState === 'unchanged' ? undefined : 'Preview update to verify and reconcile this skill at the target release.';
      checks.push(check(
        `skill-version:${entry.name}`,
        status,
        `${entry.name}: installed ${installed}; target ${target}; ${entry.changeState}.`,
        entry,
        remediation,
      ));
    }
  }

  if (environment.recoveryTransactions.length) {
    checks.push(check(
      'global-skill-recovery',
      'error',
      'Private evidence from a prior global-skill transaction requires review before another mutation.',
      { transactions: environment.recoveryTransactions },
      'Inspect each retained transaction status. Remove committed or compensated cleanup remnants only after confirming their reported outcome; recover or reconcile incomplete and interrupted transactions before applying again.',
    ));
  } else {
    checks.push(check('global-skill-recovery', 'ok', 'No retained global-skill recovery transaction was found.'));
  }

  if (environment.closureError) checks.push(check('profile-closure', 'error', environment.closureError.message));
  else if (environment.closure) {
    checks.push(check('profile-closure', 'ok', `Profile closure selects ${environment.closure.selected.length} Monica skills.`, {
      required: environment.closure.required,
      recommended: environment.closure.recommended,
      conditional: environment.closure.conditional,
      external: environment.closure.external,
    }));
    if (environment.targetRelease) checks.push(bindingCheck(
      environment.sourceBinding,
      environment.closure,
      environment.targetRelease,
      environment.profile,
      options.sourceResolver,
      environment.sourceSkillError,
    ));
  }

  const instructionIssue = (code) => environment.instructionState.issues.find((issue) => issue.code === code);
  if (environment.instructionState.blockStatus === 'malformed') {
    checks.push(check('managed-instructions', 'error', 'Root AGENTS.md contains malformed or duplicate Monica Guide markers.'));
  } else if (environment.instructionState.blockStatus === 'absent') {
    checks.push(check(
      'managed-instructions',
      environment.projectConfig ? 'error' : 'warning',
      environment.projectConfig ? 'Configured repository is missing the root Monica Guide managed block.' : 'Root AGENTS.md has no Monica Guide managed block.',
    ));
  } else {
    checks.push(check('managed-instructions', 'ok', 'Root AGENTS.md has one structurally valid Monica Guide managed block.'));
  }
  if (environment.projectConfig) {
    const versionIssue = instructionIssue('instruction_block_version_mismatch');
    checks.push(versionIssue
      ? check('managed-instruction-version', versionIssue.severity, versionIssue.message, versionIssue.details)
      : check('managed-instruction-version', 'ok', `Managed instruction block version matches catalog version ${environment.instructionState.expectedVersion}.`));
    if (environment.instructionState.blockStatus === 'valid') {
      const bodyIssue = instructionIssue('instruction_body_mismatch');
      checks.push(bodyIssue
        ? check('managed-instruction-body', bodyIssue.severity, bodyIssue.message, bodyIssue.details)
        : check('managed-instruction-body', 'ok', 'Managed AGENTS.md body matches the configured profile template.'));
    }
  }
  const claudeIssue = environment.instructionState.issues.find((issue) => issue.code.startsWith('claude_') || issue.code === 'duplicate_claude_import');
  if (claudeIssue || environment.projectConfig) {
    checks.push(claudeIssue
      ? check('claude-import', claudeIssue.severity, claudeIssue.message, claudeIssue.details)
      : check('claude-import', 'ok', environment.instructionState.claude.required
        ? 'Root CLAUDE.md imports @AGENTS.md exactly once.'
        : 'Root Claude import state matches the configured agent targets.'));
  }
  if (environment.nestedInstructions.length) checks.push(check('nested-instructions', 'warning', 'Nested instruction files exist and were not rewritten.', environment.nestedInstructions));
  else checks.push(check('nested-instructions', 'ok', 'No nested instruction files require an explicit decision.'));

  if (environment.closure) {
    const canonicalSkills = new Set(environment.catalog.managedSkills);
    const diagnosticAgents = [...new Set([...(environment.state.agentTargets || []), ...(environment.projectConfig?.agentTargets || [])])].sort();
    for (const agent of diagnosticAgents) {
      if (environment.targetRelease && environment.installManifest) {
        checks.push(discoveryCheck(
          agent,
          environment.skillChanges.length ? environment.skillChanges.map((entry) => entry.name) : environment.closure.selected,
          environment.installManifest,
          environment.catalog.skillsCliSpec,
          Boolean(options.offline),
          canonicalSkills,
        ));
      }
    }
  }

  const aliasRoots = [
    path.join(environment.workspace, '.agents', 'skills'),
    path.join(environment.workspace, '.codex', 'skills'),
    path.join(environment.workspace, '.claude', 'skills'),
    path.join(os.homedir(), '.agents', 'skills'),
    path.join(os.homedir(), '.codex', 'skills'),
    path.join(os.homedir(), '.claude', 'skills'),
  ];
  for (const [alias, entry] of Object.entries(environment.catalogAliases)) {
    if (aliasRoots.some((root) => exists(path.join(root, alias)))) checks.push(check(`stale-alias:${alias}`, 'warning', `${entry.diagnostic || `Retired skill name; use $${entry.canonical}.`}`));
  }
  checks.push(check('node-runtime', Number(process.versions.node.split('.')[0]) >= 18 ? 'ok' : 'error', `Node.js ${process.versions.node} is running; Node.js 18 or newer is required.`));
  if (environment.closure?.source.required && !environment.sourceBinding) {
    const resolver = findSourceResolver(options.sourceResolver);
    checks.push(resolver
      ? check('source-resolver', 'ok', 'inspect-dependency-source resolve --json is available.', { path: resolver })
      : check('source-resolver', 'error', 'inspect-dependency-source is unavailable for the required exact source binding.'));
  }
  if (options.offline && environment.projectConfig?.expectedCatalogRelease?.tag) {
    checks.push(environment.releaseIndexSource === 'verified-cache' && environment.releaseArtifactSource === 'verified-cache'
      ? check('offline-release-cache', 'ok', 'The expected immutable index, catalog, and manifest were loaded from verified user cache.')
      : check('offline-release-cache', 'error', 'The expected immutable index/catalog/manifest set is not available from verified user cache.'));
  }
  const severity = checks.some((entry) => entry.status === 'error') ? 'error' : checks.some((entry) => entry.status === 'warning') ? 'warning' : 'ok';
  return {
    schemaVersion: 1,
    status: severity,
    summary: {
      ok: checks.filter((entry) => entry.status === 'ok').length,
      warnings: checks.filter((entry) => entry.status === 'warning').length,
      errors: checks.filter((entry) => entry.status === 'error').length,
    },
    context: {
      workspace: environment.workspace,
      repositoryIdentity: environment.repository.identity,
      profile: environment.profile,
      channel: environment.channel,
      frameworkVersion: environment.frameworkVersion.version,
      targetRelease: environment.targetRelease?.id || null,
      activeGlobalRelease: environment.activeRelease?.id || null,
      offline: Boolean(options.offline),
    },
    checks,
  };
}
