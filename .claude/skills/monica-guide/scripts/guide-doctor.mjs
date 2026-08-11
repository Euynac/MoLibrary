import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { GuideError, exists, externalCommandTimeout, inspectFileLock, loadProjectConfig, loadState, normalizePath, run, semverChannel, stateFilePath, workspaceKey } from './guide-shared.mjs';
import {
  assertReleaseSelectorCompatibility,
  assertTaggedReleaseConstraints,
  discoverChannelReleaseTag,
  loadCatalog,
  loadReleaseArtifacts,
  loadReleaseIndex,
  resolveProfileClosure,
  resolveRequiredSkillClosure,
  resolveRelease,
  resolveTaggedRelease,
  skillsCliSpec,
} from './guide-catalog.mjs';
import { assertProjectReferenceRelease, profileRepositoryIssues, workspaceDetection } from './guide-detect.mjs';
import { applicationArchitectureSelection, instructionDiagnostics } from './guide-plan.mjs';
import { retainedGlobalSkillTransactions } from './guide-install-transaction.mjs';
import {
  SOURCE_REPOSITORIES,
  assertSourceContractClean,
  findSourceResolver,
  normalizeSourceRepository,
  observeSourceBinding,
  resolveCachedSource,
  verifySourceBinding,
} from './guide-source.mjs';
import { buildSourceManifest, compareManagedSkillRecords, verifyDiscoveryPayload, verifyLocalSkillSource } from './guide-installation.mjs';

const SOURCE_EXPECTATION_DOMAIN_CODES = new Set([
  'dirty_project_reference_source',
  'mixed_framework_versions',
  'package_version_unresolved',
  'project_reference_source_identity_mismatch',
  'project_reference_source_unverified',
  'project_reference_version_unresolved',
  'release_channel_mismatch',
  'release_version_mismatch',
  'source_commit_unresolved',
  'version_property_unresolved',
  'version_range_unsupported',
  'version_unpublished',
  'version_unresolved',
]);

function check(id, status, message, details = undefined, remediation = undefined) {
  return { id, status, message, ...(details === undefined ? {} : { details }), ...(remediation ? { remediation } : {}) };
}

function discoveryCheck(agent, skills, manifest, cliSpec, offline, canonicalSkills, aliases = {}, timeoutMs = undefined) {
  const executable = process.env.MONICA_GUIDE_NPX || 'npx';
  const timeout = externalCommandTimeout(timeoutMs);
  const result = run(executable, [...(offline ? ['--offline'] : []), '--yes', cliSpec, 'ls', '-g', '-a', agent, '--json'], { timeout });
  if (result.error?.code === 'ETIMEDOUT') {
    throw new GuideError(
      'skills_cli_timeout',
      `Pinned skills CLI timed out after ${timeout} ms while querying ${agent}. Verify the local CLI/cache and retry; offline mode never fetches a substitute.`,
      { agent, timeoutMs: timeout },
    );
  }
  if (result.status !== 0) return check(`skill-discovery:${agent}`, 'error', `${agent} global discovery could not be queried with skills ls --json.`);
  let payload;
  try { payload = JSON.parse(result.stdout); } catch { return check(`skill-discovery:${agent}`, 'error', `${agent} returned invalid skills ls --json output.`); }
  if (!Array.isArray(payload)) return check(`skill-discovery:${agent}`, 'error', `${agent} skills ls --json did not return a top-level array.`);
  try {
    verifyDiscoveryPayload(payload, skills, manifest);
    const extras = payload.map((entry) => entry?.name).filter((name) => canonicalSkills.has(name) && !skills.includes(name)).sort();
    if (extras.length) return check(`skill-discovery:${agent}`, 'error', `${agent} has catalog-managed Monica skills outside the recorded global set: ${extras.join(', ')}.`, { extras }, 'Run update to adopt and reinstall them at the active immutable release.');
    const staleAliases = [...new Set(payload.map((entry) => entry?.name).filter((name) => Object.hasOwn(aliases, name)))].sort();
    if (staleAliases.length) {
      return check(
        `skill-discovery:${agent}`,
        'warning',
        `${agent} still discovers retired Monica skill aliases: ${staleAliases.join(', ')}.`,
        { aliases: staleAliases.map((name) => ({ name, canonical: aliases[name].canonical, diagnostic: aliases[name].diagnostic })) },
        'Remove the retired aliases after confirming their canonical replacements are installed.',
      );
    }
    return check(`skill-discovery:${agent}`, 'ok', `${agent} reports every selected Monica skill with immutable manifest-matching content.`);
  } catch (error) {
    return check(`skill-discovery:${agent}`, 'error', error.message, error.details, 'Run a full update to reinstall unhealthy managed skills and verify the complete global set.');
  }
}

function bindingCheck(repository, binding, { required = false, expectedCommit = null, sourceSkillError = null, resolverPath = null, timeoutMs = undefined } = {}) {
  const id = `source-binding:${repository === SOURCE_REPOSITORIES.monica ? 'monica' : 'docs'}`;
  if (!binding) {
    return required
      ? check(id, 'error', `This workflow requires a global ${repository} source binding.`, undefined, `Preview source bind --repository ${repository === SOURCE_REPOSITORIES.monica ? 'monica' : 'docs'} with an exact local or cached source.`)
      : check(id, 'ok', `${repository} source is not bound; this workflow does not require it.`);
  }
  let observation;
  try {
    observation = required
      ? verifySourceBinding(binding, { expectedCommit, resolverPath, timeoutMs })
      : observeSourceBinding(binding, { expectedCommit });
  } catch (error) {
    return check(id, required ? 'error' : 'warning', error.message, error.details);
  }
  if (sourceSkillError) return check(id, 'error', sourceSkillError.message, sourceSkillError.details);
  if (!required && observation.pathHealth === 'path-only') {
    return check(
      id,
      'warning',
      `${repository} cached source path exists, but its exact provenance has not been revalidated in this diagnostic.`,
      observation,
      `Run source resolve --repository ${repository === SOURCE_REPOSITORIES.monica ? 'monica' : 'docs'} to revalidate the cache-only resolver contract.`,
    );
  }
  if (observation.pathHealth !== 'available') {
    return check(id, required ? 'error' : 'warning', observation.warnings[0]?.message || `${repository} source is unavailable.`, observation);
  }
  if (observation.compatibility === 'mismatch') {
    return check(id, required ? 'error' : 'warning', `Bound ${repository} commit does not match the exact commit required by this workspace.`, observation);
  }
  if (observation.dirty) return check(id, required ? 'error' : 'warning', `${repository} source is available but contains local changes; binding remains lookup-only.`, observation);
  return check(id, 'ok', `Exact ${repository} source is globally bound at ${binding.commit}.`, observation);
}

function profileSourceRequirements(catalog, profile, closure) {
  const configured = closure?.sourceRequirements || catalog.profiles?.[profile]?.sourceRequirements;
  const capabilities = new Set(closure?.capabilities || []);
  if (Array.isArray(configured)) {
    return configured.map((entry) => ({
      repository: normalizeSourceRepository(typeof entry === 'string' ? entry : entry.repository),
      required: typeof entry === 'string'
        ? true
        : entry.requirement
          ? entry.requirement === 'required' || (entry.requirement === 'conditional' && capabilities.has(entry.condition))
          : entry.required !== false,
      compatibility: typeof entry === 'string' ? 'framework-version' : entry.compatibility || 'framework-version',
    }));
  }
  if (profile === 'docs-contributor') {
    return [
      { repository: SOURCE_REPOSITORIES.monica, required: true, compatibility: 'framework-version' },
      { repository: SOURCE_REPOSITORIES.docs, required: true, compatibility: 'workspace-commit' },
    ];
  }
  return [{ repository: SOURCE_REPOSITORIES.monica, required: Boolean(closure?.source?.required), compatibility: 'framework-version' }];
}

function workspaceSourceExpectation(repository, workspace, state, index) {
  if (!workspace) {
    return repository === SOURCE_REPOSITORIES.monica && state.activeRelease?.commit
      ? { commit: state.activeRelease.commit, basis: 'active-global-release' }
      : { commit: null, basis: null };
  }
  const normalized = normalizePath(workspace);
  if (!exists(normalized) || !fs.statSync(normalized).isDirectory()) throw new GuideError('workspace_unavailable', `Workspace is not a directory: ${normalized}.`);
  const project = loadProjectConfig(normalized).config;
  const detection = workspaceDetection(normalized);
  if (repository === SOURCE_REPOSITORIES.docs && /^Tairitsua\/Monica\.Docs$/i.test(detection.repository.identity || '')) {
    const commit = detection.repository.git?.commit?.toLowerCase() || null;
    if (!commit) throw new GuideError('source_commit_unresolved', 'Cannot identify the exact Monica.Docs workspace HEAD required for source parity.');
    return { commit, basis: 'docs-workspace-head' };
  }
  if (repository === SOURCE_REPOSITORIES.monica) {
    if (/^Tairitsua\/Monica$/i.test(detection.repository.identity || '') && detection.repository.git?.commit) {
      return { commit: detection.repository.git.commit.toLowerCase(), basis: 'monica-workspace-head' };
    }
    if (detection.versionError) {
      const dirtyProjectReferenceCommit = detection.versionError.code === 'dirty_project_reference_source'
        && /^[0-9a-f]{40}$/i.test(detection.versionError.details?.commit || '')
        ? detection.versionError.details.commit.toLowerCase()
        : null;
      if (dirtyProjectReferenceCommit) {
        return {
          commit: dirtyProjectReferenceCommit,
          basis: 'dirty-project-reference-head',
          warnings: [{
            code: detection.versionError.code,
            message: detection.versionError.message,
            details: detection.versionError.details,
          }],
        };
      }
      throw new GuideError(detection.versionError.code, detection.versionError.message, detection.versionError.details);
    }
    const referenceCommits = [...new Set((detection.frameworkVersion.entries || [])
      .filter((entry) => entry.origin === 'ProjectReference' && entry.sourceCommit)
      .map((entry) => entry.sourceCommit.toLowerCase()))];
    if (referenceCommits.length === 1) return { commit: referenceCommits[0], basis: 'project-reference' };
    if (detection.frameworkVersion.version) {
      try {
        const release = resolveRelease(index, {
          channel: semverChannel(detection.frameworkVersion.version) || 'stable',
          frameworkVersion: detection.frameworkVersion.version,
        });
        return { commit: release.commit, basis: 'framework-version-release', release: release.id };
      } catch (error) {
        const configuredRelease = project?.expectedCatalogRelease;
        if (!(error instanceof GuideError)
          || error.code !== 'version_unpublished'
          || configuredRelease?.monicaVersion !== detection.frameworkVersion.version
          || !configuredRelease.commit) {
          throw error;
        }
        return { commit: configuredRelease.commit.toLowerCase(), basis: 'repository-release-fallback' };
      }
    }
    const projectCommit = project?.expectedCatalogRelease?.commit;
    if (projectCommit) return { commit: projectCommit.toLowerCase(), basis: 'repository-release-fallback' };
    if (state.activeRelease?.commit) return { commit: state.activeRelease.commit, basis: 'active-global-release' };
  }
  return { commit: null, basis: null };
}

function diagnosticExpectedCommit(repository, detection, release) {
  if (repository === SOURCE_REPOSITORIES.docs) {
    return /^Tairitsua\/Monica\.Docs$/i.test(detection.repository.identity || '')
      ? detection.repository.git?.commit?.toLowerCase() || null
      : null;
  }
  if (release?.commit) return release.commit;
  if (detection.versionError?.code === 'dirty_project_reference_source'
    && /^[0-9a-f]{40}$/i.test(detection.versionError.details?.commit || '')) {
    return detection.versionError.details.commit.toLowerCase();
  }
  const referenceCommits = [...new Set((detection.frameworkVersion.entries || [])
    .filter((entry) => entry.origin === 'ProjectReference' && /^[0-9a-f]{40}$/i.test(entry.sourceCommit || ''))
    .map((entry) => entry.sourceCommit.toLowerCase()))];
  return referenceCommits.length === 1 ? referenceCommits[0] : null;
}

function sourceBindingDiagnostics(sourceBindings, detection, release) {
  return Object.fromEntries(Object.values(SOURCE_REPOSITORIES).map((repository) => {
    const binding = sourceBindings[repository] || null;
    const expectedCommit = diagnosticExpectedCommit(repository, detection, release);
    return [repository, {
      binding,
      observation: binding ? observeSourceBinding(binding, { expectedCommit }) : null,
      expectedCommit,
    }];
  }));
}

export function inspectGlobalEnvironment(options = {}) {
  const statePath = stateFilePath(options.state);
  const state = loadState(statePath);
  const catalogInfo = loadCatalog({ catalogPath: options.catalog, indexPath: options.index });
  const sourceBindings = {};
  for (const repository of Object.values(SOURCE_REPOSITORIES)) {
    const binding = state.sourceBindings[repository] || null;
    sourceBindings[repository] = binding ? { binding, observation: observeSourceBinding(binding) } : { binding: null, observation: null };
  }
  return {
    schemaVersion: 1,
    statePath,
    state,
    catalog: {
      raw: catalogInfo.catalog,
      index: catalogInfo.index,
      version: catalogInfo.catalog.catalogVersion,
      digest: catalogInfo.catalogDigest,
      skillsCliSpec: skillsCliSpec(catalogInfo.catalog),
    },
    activeRelease: state.activeRelease,
    agentTargets: state.agentTargets,
    managedSkills: state.managedSkills,
    sourceBindings,
    sourceBindingCandidates: state.sourceBindingCandidates,
    recoveryTransactions: retainedGlobalSkillTransactions(statePath),
    stateLock: inspectFileLock(`${statePath}.lock`),
  };
}

export function listSourceBindings(options = {}) {
  const environment = inspectGlobalEnvironment(options);
  const warnings = Object.values(environment.sourceBindings).flatMap((entry) => entry.observation?.warnings || []);
  const migrationRequired = Object.keys(environment.sourceBindingCandidates).length > 0;
  return {
    schemaVersion: 1,
    status: migrationRequired ? 'migration-required' : Object.values(environment.sourceBindings).some((entry) => entry.binding) ? 'configured' : 'unconfigured',
    severity: migrationRequired ? 'error' : warnings.length ? 'warning' : 'ok',
    bindings: environment.sourceBindings,
    migrationCandidates: environment.sourceBindingCandidates,
    warnings,
  };
}

export function resolveSourceBinding(options = {}) {
  const repository = normalizeSourceRepository(options.repository);
  const environment = inspectGlobalEnvironment(options);
  if (!Object.hasOwn(environment.catalog.raw.sourceRepositories || {}, repository)) {
    throw new GuideError('source_repository_unavailable', `Catalog does not declare first-party source repository ${repository}.`);
  }
  let expectation;
  try {
    expectation = workspaceSourceExpectation(repository, options.workspace, environment.state, environment.catalog.index);
  } catch (error) {
    if (!(error instanceof GuideError) || !SOURCE_EXPECTATION_DOMAIN_CODES.has(error.code)) throw error;
    const binding = environment.state.sourceBindings[repository] || null;
    return {
      schemaVersion: 1,
      status: 'blocked',
      severity: 'error',
      repository,
      binding,
      observation: binding ? observeSourceBinding(binding) : null,
      expectation: { commit: null, basis: 'unresolved' },
      offeredBinding: null,
      warning: { code: error.code, message: error.message, details: error.details },
    };
  }
  const migrationCandidates = environment.state.sourceBindingCandidates[repository] || null;
  if (migrationCandidates) {
    return {
      schemaVersion: 1,
      status: 'migration-required',
      severity: 'error',
      repository,
      binding: null,
      observation: null,
      expectation,
      offeredBinding: null,
      migrationCandidates,
      warning: { code: 'source_binding_migration_required', message: `Conflicting legacy ${repository} bindings require an explicit bind or unbind decision.` },
    };
  }
  const binding = environment.state.sourceBindings[repository] || null;
  if (binding) {
    let observation;
    try {
      observation = verifySourceBinding(binding, { expectedCommit: expectation.commit, resolverPath: options.sourceResolver, timeoutMs: options.timeoutMs });
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      return {
        schemaVersion: 1,
        status: 'unhealthy',
        severity: 'error',
        repository,
        binding,
        observation: observeSourceBinding(binding, { expectedCommit: expectation.commit }),
        expectation,
        offeredBinding: null,
        warning: { code: error.code, message: error.message, details: error.details },
      };
    }
    const expectationWarnings = expectation.warnings || [];
    if (expectationWarnings.length) {
      const knownWarnings = new Set(observation.warnings.map((entry) => `${entry.code}:${entry.message}`));
      observation = {
        ...observation,
        warnings: [...observation.warnings, ...expectationWarnings.filter((entry) => !knownWarnings.has(`${entry.code}:${entry.message}`))],
      };
    }
    return {
      schemaVersion: 1,
      status: observation.pathHealth === 'available' ? 'resolved' : 'unhealthy',
      severity: observation.pathHealth !== 'available' ? 'error' : observation.warnings.length ? 'warning' : 'ok',
      repository,
      binding,
      observation,
      expectation,
      offeredBinding: null,
    };
  }
  const exactRef = options.sourceRef || expectation.commit;
  let offeredBinding = null;
  let warning = expectation.warnings?.[0] || null;
  if (exactRef) {
    try {
      offeredBinding = resolveCachedSource({ exactRef, repository, resolverPath: options.sourceResolver, timeoutMs: options.timeoutMs });
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      warning = { code: error.code, message: error.message, details: error.details };
    }
  }
  return {
    schemaVersion: 1,
    status: offeredBinding ? 'offered' : 'unbound',
    severity: warning ? 'warning' : 'ok',
    repository,
    binding: null,
    observation: null,
    expectation,
    offeredBinding,
    warning,
    warnings: expectation.warnings || [],
  };
}

export async function inspectGlobalInstalledSkillHealth(environment, options = {}) {
  if (!environment.activeRelease) {
    const managedSkillNames = Object.keys(environment.managedSkills);
    if (managedSkillNames.length) {
      return {
        status: 'error',
        release: null,
        changes: [],
        checks: [check(
          'managed-skill-health',
          'error',
          `User state records ${managedSkillNames.length} managed Monica skill(s) without an active immutable release, so their content cannot be verified.`,
          { skills: managedSkillNames.sort() },
          'Preview update with an explicit immutable release to adopt and verify the recorded Monica skills.',
        )],
      };
    }
    return {
      status: 'not-selected',
      release: null,
      changes: [],
      checks: [check('managed-skill-health', 'ok', 'No downstream Monica skill release is active; there is no installed release set to verify.')],
    };
  }

  const active = environment.activeRelease;
  let release;
  let artifacts;
  try {
    if (active.tag) {
      const releaseIndex = await loadReleaseIndex({
        releaseTag: active.tag,
        releaseIndexPath: options.releaseIndex,
        releaseIndexUrl: options.releaseIndexUrl,
        offline: options.offline,
        state: environment.state,
        statePath: environment.statePath,
        fetchImplementation: options.fetchImplementation,
      });
      release = resolveTaggedRelease(releaseIndex.index, active.tag);
      assertTaggedReleaseConstraints(release, { expectedRelease: active });
      artifacts = await loadReleaseArtifacts({
        release,
        releaseCatalogPath: options.releaseCatalog,
        releaseManifestPath: options.releaseManifest,
        offline: options.offline,
        state: environment.state,
        statePath: environment.statePath,
        fetchImplementation: options.fetchImplementation,
      });
    } else if (active.id?.startsWith('source:') && /^[0-9a-f]{40}$/i.test(active.commit || '')) {
      const binding = environment.state.sourceBindings[SOURCE_REPOSITORIES.monica];
      if (!binding || binding.commit.toLowerCase() !== active.commit.toLowerCase()) {
        throw new GuideError('source_binding_unavailable', 'The active source release has no matching global Monica source binding.');
      }
      const observation = verifySourceBinding(binding, { expectedCommit: active.commit, resolverPath: options.sourceResolver, timeoutMs: options.timeoutMs });
      if (observation.dirty) throw new GuideError('dirty_exact_source', 'The active source release binding contains local changes.', observation);
      assertSourceContractClean(binding.sourcePath);
      const catalogInfo = loadCatalog({
        catalogPath: path.join(binding.sourcePath, '.monica', 'agent-skill-catalog.json'),
        indexPath: options.index,
      });
      if (active.catalogDigest && catalogInfo.catalogDigest !== active.catalogDigest) {
        throw new GuideError('catalog_digest_mismatch', 'The active source release catalog no longer matches recorded global state.');
      }
      const manifest = buildSourceManifest(binding.sourcePath, catalogInfo.catalog);
      release = {
        ...active,
        channel: 'source',
        skillDigests: manifest.skillDigests,
        skillRevisions: manifest.skillRevisions,
        skillLastChangedIn: manifest.skillLastChangedIn,
      };
      artifacts = { catalog: catalogInfo.catalog, manifest, source: binding.sourcePath };
    } else {
      throw new GuideError('release_not_immutable', 'The active global Monica release is neither an immutable tag nor an exact source commit.');
    }
  } catch (error) {
    if (!(error instanceof GuideError)) throw error;
    return {
      status: 'error',
      release: active,
      changes: [],
      checks: [check('global-release-contract', 'error', error.message, error.details, 'Restore the verified release cache or preview an explicit global update before trusting installed Monica skills.')],
    };
  }

  const canonicalSkills = new Set(Object.entries(artifacts.catalog.skills)
    .filter(([, entry]) => entry.ownership === 'monica' && entry.managed === true)
    .map(([name]) => name));
  const managedSkills = Object.keys(environment.managedSkills).sort();
  const checks = [check('global-release-contract', 'ok', `Active global release ${release.id} has a verified immutable catalog and skill manifest.`)];
  let changes = [];
  if (!managedSkills.length) {
    checks.push(check('managed-skill-health', 'error', `Active global release ${release.id} records no managed Monica skills.`));
  } else {
    try {
      changes = compareManagedSkillRecords(environment.managedSkills, managedSkills, release, artifacts.manifest);
      const changed = changes.filter((entry) => entry.changeState !== 'unchanged');
      checks.push(changed.length
        ? check('managed-skill-health', 'error', 'Recorded managed skill metadata does not match the active immutable release.', { changed })
        : check('managed-skill-health', 'ok', `All ${managedSkills.length} recorded managed skill versions match ${release.id}.`));
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      checks.push(check('managed-skill-health', 'error', error.message, error.details));
    }
  }
  if (!environment.agentTargets.length) {
    checks.push(check('agent-targets', 'error', 'Managed Monica skills have no recorded global agent target.'));
  } else {
    for (const agent of environment.agentTargets) {
      checks.push(discoveryCheck(
        agent,
        managedSkills,
        artifacts.manifest,
        environment.catalog.skillsCliSpec,
        Boolean(options.offline),
        canonicalSkills,
        artifacts.catalog.aliases || {},
        options.timeoutMs,
      ));
    }
  }
  const status = checks.some((entry) => entry.status === 'error') ? 'error' : checks.some((entry) => entry.status === 'warning') ? 'warning' : 'ok';
  return { status, release, changes, checks, artifactSource: artifacts.source };
}

function agentTargetCheck(agent, cliSpec, offline, timeoutMs) {
  const executable = process.env.MONICA_GUIDE_NPX || 'npx';
  const args = [...(offline ? ['--offline'] : []), '--yes', cliSpec, 'ls', '-g', '-a', agent, '--json'];
  const timeout = externalCommandTimeout(timeoutMs);
  const result = run(executable, args, { timeout });
  if (result.error?.code === 'ETIMEDOUT') {
    throw new GuideError(
      'skills_cli_timeout',
      `Pinned skills CLI timed out after ${timeout} ms while querying ${agent}. Verify the local CLI/cache and retry; offline mode never fetches a substitute.`,
      { agent, timeoutMs: timeout },
    );
  }
  if (result.status !== 0) return check(`agent-target:${agent}`, 'error', `Pinned skills CLI does not recognize or cannot query global target ${agent}.`);
  try {
    if (!Array.isArray(JSON.parse(result.stdout))) throw new Error('top-level value is not an array');
  } catch (error) {
    return check(`agent-target:${agent}`, 'error', `Pinned skills CLI returned invalid discovery JSON for ${agent}: ${error.message}`);
  }
  return check(`agent-target:${agent}`, 'ok', `Pinned skills CLI can query global target ${agent}.`);
}

export async function doctorGlobal(options = {}) {
  const environment = inspectGlobalEnvironment(options);
  const checks = [];
  checks.push(check('guide-state', 'ok', `User state schema ${environment.state.schemaVersion} is readable.`));
  checks.push(environment.activeRelease
    ? check('global-release', 'ok', `Global Monica skills are recorded at ${environment.activeRelease.id}.`)
    : check('global-release', 'ok', 'Guide is ready; no downstream Monica skill release has been selected yet.'));
  if (Object.keys(environment.sourceBindingCandidates).length) {
    checks.push(check('source-binding-migration', 'error', 'Legacy workspace source bindings conflict and require an explicit global bind or unbind decision.', environment.sourceBindingCandidates));
  } else {
    checks.push(check('source-binding-migration', 'ok', 'No conflicting legacy source bindings require migration.'));
  }
  for (const repository of Object.values(SOURCE_REPOSITORIES)) {
    checks.push(bindingCheck(repository, environment.sourceBindings[repository].binding, { timeoutMs: options.timeoutMs }));
  }
  if (environment.recoveryTransactions.length) {
    checks.push(check('global-skill-recovery', 'error', 'Private evidence from a prior global-skill transaction requires review.', { transactions: environment.recoveryTransactions }));
  } else {
    checks.push(check('global-skill-recovery', 'ok', 'No retained global-skill recovery transaction was found.'));
  }
  if (environment.stateLock.status === 'absent') {
    checks.push(check('state-lock', 'ok', 'No state mutation lock is present.'));
  } else {
    checks.push(check('state-lock', 'error', `State lock is ${environment.stateLock.status}; Guide will not reclaim it automatically.`, environment.stateLock, 'Confirm no Guide process is active, then remove only the exact reported lock file manually.'));
  }
  const skillHealth = await inspectGlobalInstalledSkillHealth(environment, options);
  checks.push(...skillHealth.checks);
  if (!environment.activeRelease) {
    for (const agent of environment.agentTargets) checks.push(agentTargetCheck(agent, environment.catalog.skillsCliSpec, Boolean(options.offline), options.timeoutMs));
  }
  checks.push(check('node-runtime', Number(process.versions.node.split('.')[0]) >= 18 ? 'ok' : 'error', `Node.js ${process.versions.node} is running; Node.js 18 or newer is required.`));
  const status = checks.some((entry) => entry.status === 'error') ? 'error' : checks.some((entry) => entry.status === 'warning') ? 'warning' : 'ok';
  return {
    schemaVersion: 1,
    status,
    summary: {
      ok: checks.filter((entry) => entry.status === 'ok').length,
      warnings: checks.filter((entry) => entry.status === 'warning').length,
      errors: checks.filter((entry) => entry.status === 'error').length,
    },
    context: {
      workspace: null,
      activeGlobalRelease: environment.activeRelease?.id || null,
      installedSkillHealth: skillHealth.status,
      offline: Boolean(options.offline),
    },
    checks,
  };
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
  const profile = options.profile || project.config?.profile || null;
  const profileConfirmed = Boolean(options.profile || project.config?.profile);
  const repositoryIssues = profileRepositoryIssues(workspace, profile, detection.repository);
  let channel = options.channel || project.config?.channel || semverChannel(detection.frameworkVersion.version) || 'stable';
  const capabilities = [...new Set(options.capabilities?.length ? options.capabilities : (project.config?.capabilities || detection.repository.capabilities || []))].sort();
  const applicationArchitecture = applicationArchitectureSelection(profile, capabilities);
  const key = workspaceKey(workspace, detection.repository.identity);
  let targetCatalog = bootstrapInfo.catalog;
  let targetCatalogDigest = bootstrapInfo.catalogDigest;
  let index = bootstrapInfo.index;
  let releaseIndex = null;
  let releaseArtifacts = null;
  let installManifest = null;
  let release = null;
  let releaseError = detection.versionError ? new GuideError(detection.versionError.code, detection.versionError.message, detection.versionError.details) : null;
  const shouldResolveRelease = Boolean(project.config || options.profile || detection.frameworkVersion.version
    || options.releaseTag || options.channel || options.sourceRef);
  if (!releaseError && shouldResolveRelease) {
    try {
      assertReleaseSelectorCompatibility({
        releaseTag: options.releaseTag,
        sourceRef: options.sourceRef,
        explicitChannel: options.channel,
        persistedChannel: project.config?.channel,
      });
      if (channel === 'source') {
        release = resolveRelease(index, {
          channel,
          frameworkVersion: detection.frameworkVersion.version,
          sourceRef: options.sourceRef || project.config?.expectedCatalogRelease?.commit,
        });
        const binding = state.sourceBindings[SOURCE_REPOSITORIES.monica];
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
        if (options.releaseTag) {
          release = resolveTaggedRelease(index, options.releaseTag);
          channel = release.channel;
          assertTaggedReleaseConstraints(release, {
            explicitChannel: options.channel,
            persistedChannel: project.config?.channel,
            expectedRelease: project.config?.expectedCatalogRelease,
            frameworkVersion: detection.frameworkVersion.version,
          });
        } else {
          release = resolveRelease(index, { channel, frameworkVersion: detection.frameworkVersion.version });
          channel = release.channel;
        }
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
  const sourceBindings = Object.fromEntries(Object.values(SOURCE_REPOSITORIES).map((repository) => [repository, state.sourceBindings[repository] || null]));
  const sourceBinding = sourceBindings[SOURCE_REPOSITORIES.monica];
  const bindingDiagnostics = sourceBindingDiagnostics(sourceBindings, detection, release);
  if (sourceBinding && installManifest && (channel === 'source' || options.offline)) {
    try { verifyLocalSkillSource(sourceBinding.sourcePath, targetCatalog, installManifest); }
    catch (error) {
      if (!(error instanceof GuideError)) throw error;
      sourceSkillError = { code: error.code, message: error.message, details: error.details };
    }
  }
  const sourceRequirementIssues = [];
  if (closure) {
    for (const requirement of profileSourceRequirements(targetCatalog, profile, closure).filter((entry) => entry.required)) {
      const binding = sourceBindings[requirement.repository];
      const expectedCommit = requirement.compatibility === 'workspace-commit'
        ? detection.repository.git?.commit?.toLowerCase() || null
        : release?.commit || null;
      if (requirement.compatibility === 'workspace-commit' && !expectedCommit) {
        sourceRequirementIssues.push({
          code: 'source_commit_unresolved',
          message: `Cannot identify the exact ${requirement.repository} workspace HEAD required by ${profile}.`,
          details: { repository: requirement.repository },
        });
        continue;
      }
      if (!binding) {
        sourceRequirementIssues.push({ code: 'source_binding_required', message: `${profile} requires a global ${requirement.repository} source binding.`, details: { repository: requirement.repository, expectedCommit } });
        continue;
      }
      let observation;
      try {
        observation = verifySourceBinding(binding, { expectedCommit, resolverPath: options.sourceResolver, timeoutMs: options.timeoutMs });
      } catch (error) {
        sourceRequirementIssues.push({ code: error.code || 'source_binding_incompatible', message: error.message, details: error.details });
        continue;
      }
      if (observation.pathHealth !== 'available' || observation.compatibility === 'mismatch') {
        sourceRequirementIssues.push({ code: 'source_binding_incompatible', message: `Global ${requirement.repository} source binding cannot satisfy this workspace.`, details: observation });
      } else if (observation.dirty) {
        sourceRequirementIssues.push({ code: 'dirty_exact_source', message: `Exact ${requirement.repository} source contains local changes.`, details: observation });
      }
    }
  }
  if (sourceSkillError) sourceRequirementIssues.push(sourceSkillError);
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
    applicationArchitecture,
    frameworkVersion: detection.frameworkVersion,
    versionError: detection.versionError,
    projectConfig: project.config,
    projectConfigMigration: project.migration,
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
    sourceBindings,
    sourceBindingDiagnostics: bindingDiagnostics,
    sourceSkillError,
    sourceRequirementIssues,
    contributionPreference: state.contributionPreferences[key] || 'ask',
    instructionState: instructionDiagnostics(workspace, targetCatalog, project.config, state.agentTargets),
    nestedInstructions: detection.nestedInstructions,
    catalog: {
      raw: targetCatalog,
      version: targetCatalog.catalogVersion,
      digest: targetCatalogDigest,
      path: releaseArtifacts?.cachePaths?.catalog || (channel === 'source' ? state.sourceBindings[SOURCE_REPOSITORIES.monica]?.sourcePath : bootstrapInfo.catalogPath),
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
  if (repository.detectionScan?.truncated) {
    checks.push(check(
      'repository-detection-scan',
      'warning',
      'Repository source scanning reached its deterministic limit; profile detection remains advisory.',
      repository.detectionScan,
      'Select a profile explicitly if you initialize this workspace.',
    ));
  } else if (repository.detectionScan) {
    checks.push(check('repository-detection-scan', 'ok', 'Repository profile signals were scanned within deterministic limits.', repository.detectionScan));
  }
  checks.push(environment.projectConfigMigration
    ? check(
      'project-config-migration',
      'warning',
      environment.projectConfigMigration.message,
      environment.projectConfigMigration,
      'Preview and approve update or configure to rewrite schema 2, or forget to remove the workspace configuration.',
    )
    : check('project-config-migration', 'ok', 'Repository configuration uses the current schema or is not initialized.'));
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

  if (!environment.profile) checks.push(check(
    'profile',
    'warning',
    environment.candidateProfile
      ? `No Monica profile is selected; detected candidate ${environment.candidateProfile} remains advisory until initialization.`
      : 'No Monica profile is selected; choose one only when initializing a concrete workflow.',
  ));
  else if (!environment.profileConfirmed) checks.push(check('profile', 'warning', `Candidate profile ${environment.profile} is not confirmed.`, undefined, `Rerun init with --profile ${environment.profile} after confirmation.`));
  else checks.push(check('profile', 'ok', `Profile ${environment.profile} is confirmed.`));

  if (environment.applicationArchitecture.issue) {
    const issue = environment.applicationArchitecture.issue;
    checks.push(check('application-architecture', 'error', issue.message, issue.details, issue.message));
  } else if (environment.profile === 'application') {
    checks.push(check('application-architecture', 'ok', `Application architecture ${environment.applicationArchitecture.selected} is selected.`));
  }

  const dirtyUnconfirmedReference = !environment.profileConfirmed
    && environment.versionError?.code === 'dirty_project_reference_source';
  if (environment.versionError) checks.push(check(
    'framework-version',
    dirtyUnconfirmedReference ? 'warning' : 'error',
    environment.versionError.message,
    environment.versionError.details,
    dirtyUnconfirmedReference
      ? 'Ordinary source lookup remains available. Clean or commit the ProjectReference checkout before approving a workflow that requires exact source parity.'
      : undefined,
  ));
  else if (environment.frameworkVersion.version) checks.push(check('framework-version', 'ok', `Resolved Monica ${environment.frameworkVersion.version} from ${environment.frameworkVersion.tier}.`));
  else checks.push(check('framework-version', 'warning', 'No exact Monica framework version was detected.'));

  if (environment.releaseError) checks.push(check(
    'immutable-release',
    dirtyUnconfirmedReference && environment.releaseError.code === environment.versionError?.code ? 'warning' : 'error',
    dirtyUnconfirmedReference && environment.releaseError.code === environment.versionError?.code
      ? 'Immutable release resolution is deferred while the advisory ProjectReference checkout contains local changes.'
      : environment.releaseError.message,
    environment.releaseError.details,
    environment.releaseError.details?.remediation,
  ));
  else if (environment.targetRelease) checks.push(check('immutable-release', 'ok', `Resolved immutable release ${environment.targetRelease.id} at ${environment.targetRelease.commit}.`));

  if (environment.projectConfig?.expectedCatalogRelease && environment.targetRelease
    && environment.projectConfig.expectedCatalogRelease.id !== environment.targetRelease.id) {
    checks.push(check('project-release', 'error', `Repository expects ${environment.projectConfig.expectedCatalogRelease.id}, but resolution selected ${environment.targetRelease.id}.`));
  } else if (environment.projectConfig) checks.push(check('project-release', 'ok', `Repository configuration expects ${environment.projectConfig.expectedCatalogRelease?.id || 'an unresolved release'}.`));
  else checks.push(check('project-release', 'warning', 'Repository has not been initialized by Monica Guide; this is valid until a workflow is chosen.'));

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
  }
  const requirements = environment.closure
    ? profileSourceRequirements(environment.catalog.raw || {}, environment.profile, environment.closure)
    : [];
  const requirementsByRepository = new Map(requirements.map((requirement) => [requirement.repository, requirement]));
  for (const repositoryName of Object.values(SOURCE_REPOSITORIES)) {
    const requirement = requirementsByRepository.get(repositoryName);
    const expectedCommit = requirement?.compatibility === 'workspace-commit'
      ? environment.repository.git?.commit?.toLowerCase() || null
      : requirement
        ? environment.targetRelease?.commit || null
        : environment.sourceBindingDiagnostics[repositoryName]?.expectedCommit || null;
    checks.push(bindingCheck(repositoryName, environment.sourceBindings[repositoryName], {
      required: requirement?.required || false,
      expectedCommit,
      sourceSkillError: repositoryName === SOURCE_REPOSITORIES.monica ? environment.sourceSkillError : null,
      resolverPath: options.sourceResolver,
      timeoutMs: options.timeoutMs,
    }));
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
    const diagnosticAgents = [...environment.state.agentTargets];
    for (const agent of diagnosticAgents) {
      if (environment.targetRelease && environment.installManifest) {
        checks.push(discoveryCheck(
          agent,
          environment.skillChanges.length ? environment.skillChanges.map((entry) => entry.name) : environment.closure.selected,
          environment.installManifest,
          environment.catalog.skillsCliSpec,
          Boolean(options.offline),
          canonicalSkills,
          environment.catalogAliases,
          options.timeoutMs,
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
  const requiredMonicaSource = environment.closure
    && profileSourceRequirements(environment.catalog.raw || {}, environment.profile, environment.closure)
      .some((entry) => entry.required && entry.repository === SOURCE_REPOSITORIES.monica);
  if (requiredMonicaSource && !environment.sourceBinding) {
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
      applicationArchitecture: environment.applicationArchitecture.selected,
      channel: environment.channel,
      frameworkVersion: environment.frameworkVersion.version,
      targetRelease: environment.targetRelease?.id || null,
      activeGlobalRelease: environment.activeRelease?.id || null,
      offline: Boolean(options.offline),
    },
    checks,
  };
}
