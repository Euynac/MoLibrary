import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {
  GuideError,
  PROJECT_SCHEMA_VERSION,
  atomicWrite,
  claudeImportState,
  compareOrdinalUtf8,
  digest,
  ensureClaudeImport,
  exists,
  fileDigest,
  gitWorkspaceFingerprint,
  instructionState,
  loadProjectConfig,
  loadState,
  normalizePath,
  readText,
  removeClaudeImport,
  removeInstructionBlock,
  run,
  runChecked,
  shellDisplay,
  semverChannel,
  stableJson,
  stateFilePath,
  textDiff,
  upsertInstructionBlock,
  walkFiles,
  withFileLock,
  workspaceKey,
} from './guide-shared.mjs';
import {
  assertCatalogDigest,
  canonicalSkillUrl,
  discoverChannelReleaseTag,
  loadCatalog,
  loadReleaseArtifacts,
  loadReleaseIndex,
  renderManagedInstructions,
  resolveProfileClosure,
  resolveRequiredSkillClosure,
  resolveRelease,
  skillsCliSpec,
} from './guide-catalog.mjs';
import { assertProjectReferenceRelease, profileRepositoryIssues, workspaceDetection } from './guide-detect.mjs';
import { assertSourceContractClean, sourceBindingForProfile, resolveCachedSource, verifyLocalSource } from './guide-source.mjs';
import {
  buildSourceManifest,
  compareManagedSkillRecords,
  verifyDiscoveryPayload,
  verifyLocalSkillSource,
} from './guide-installation.mjs';
import {
  normalizeSkillsCliAgent,
  retainedGlobalSkillTransactions,
  withGlobalSkillCompensation,
} from './guide-install-transaction.mjs';

const MUTATING_INTENTS = new Set(['init', 'update', 'configure', 'source', 'contribute', 'forget']);
const VALID_PROFILES = new Set(['application', 'extension-author', 'framework-contributor', 'docs-contributor']);
const VALID_AGENTS = new Set(['codex', 'claude-code']);

function normalizeAgents(values) {
  const aliases = { claude: 'claude-code', 'claude_code': 'claude-code' };
  const agents = [...new Set((values || []).map((value) => aliases[value] || value))];
  for (const agent of agents) if (!VALID_AGENTS.has(agent)) throw new GuideError('invalid_agent', `Unsupported agent target: ${agent}.`);
  return agents.sort();
}

function normalizeTargetSkills(values = []) {
  const skills = [...new Set(values)];
  for (const skill of skills) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(skill)) throw new GuideError('invalid_skill', `Invalid targeted skill name: ${skill}.`);
  }
  return skills.sort(compareOrdinalUtf8);
}

function sameStringSet(left, right) {
  return JSON.stringify([...new Set(left || [])].sort(compareOrdinalUtf8))
    === JSON.stringify([...new Set(right || [])].sort(compareOrdinalUtf8));
}

function requiresSourceReinstall(context, state, skillChanges) {
  return context.channel === 'source'
    && (state.activeRelease?.id !== context.release.id
      || skillChanges.some((entry) => entry.changeState === 'metadata-changed'));
}

export function resolveNestedInstructionSelections(workspace, detection, values = []) {
  const selected = new Map();
  const discovered = new Set(detection.nestedInstructions.map((file) => path.resolve(file)));
  for (const rawValue of values) {
    if (typeof rawValue !== 'string' || !rawValue || rawValue.includes('\0')) {
      throw new GuideError('nested_instruction_path_invalid', 'Nested instruction selections must be non-empty repository-relative paths.');
    }
    if (path.isAbsolute(rawValue) || path.win32.isAbsolute(rawValue)) {
      throw new GuideError('nested_instruction_path_invalid', `Nested instruction path must be repository-relative: ${rawValue}.`);
    }
    const portable = rawValue.replaceAll('\\', '/');
    const segments = portable.split('/');
    if (segments.includes('..')) throw new GuideError('nested_instruction_path_invalid', `Nested instruction path cannot traverse outside the repository: ${rawValue}.`);
    const relative = path.posix.normalize(portable).replace(/^\.\//, '');
    const name = path.posix.basename(relative);
    if (!relative.includes('/') || !['AGENTS.md', 'CLAUDE.md'].includes(name)) {
      throw new GuideError('nested_instruction_path_invalid', `Select an existing nested AGENTS.md or CLAUDE.md file, not ${rawValue}.`);
    }
    const absolute = path.resolve(workspace, ...relative.split('/'));
    const repositoryRelative = path.relative(path.resolve(workspace), absolute);
    if (!repositoryRelative || repositoryRelative.startsWith('..') || path.isAbsolute(repositoryRelative)) {
      throw new GuideError('nested_instruction_path_invalid', `Nested instruction path escapes the selected workspace: ${rawValue}.`);
    }
    if (!exists(absolute)) {
      throw new GuideError('nested_instruction_unavailable', `Selected nested instruction file does not exist: ${relative}.`);
    }
    const fileState = fs.lstatSync(absolute);
    if (fileState.isSymbolicLink() || path.resolve(fs.realpathSync(absolute)) !== absolute) {
      throw new GuideError('nested_instruction_symlink_forbidden', `Selected nested instruction path cannot contain symlinks: ${relative}.`);
    }
    if (!fileState.isFile()) {
      throw new GuideError('nested_instruction_unavailable', `Selected nested instruction path is not a file: ${relative}.`);
    }
    if (!discovered.has(absolute)) {
      throw new GuideError('nested_instruction_not_discovered', `Selected path is not a diagnosed nested instruction file: ${relative}.`);
    }
    selected.set(relative, { path: absolute, relative, kind: name === 'AGENTS.md' ? 'agents' : 'claude' });
  }
  return [...selected.values()].sort((left, right) => compareOrdinalUtf8(left.relative, right.relative));
}

function validateNestedClaudeSibling(workspace, siblingAgents, detection) {
  const relative = path.relative(path.resolve(workspace), siblingAgents).split(path.sep).join('/');
  if (!relative || relative.startsWith('../') || path.isAbsolute(relative) || !exists(siblingAgents)) return 'missing';
  try {
    const state = fs.lstatSync(siblingAgents);
    if (state.isSymbolicLink() || !state.isFile()) return 'unsafe';
    if (path.resolve(fs.realpathSync(siblingAgents)) !== path.resolve(siblingAgents)) return 'unsafe';
  } catch {
    return 'unsafe';
  }
  const discovered = new Set(detection.nestedInstructions.map((file) => path.resolve(file)));
  return discovered.has(path.resolve(siblingAgents)) ? null : 'undiscovered';
}

function discoverInstalledManagedSkills(agents, catalog, cliSpec, offline) {
  const canonical = new Set(Object.entries(catalog.skills)
    .filter(([, entry]) => entry.ownership === 'monica' && entry.managed !== false)
    .map(([name]) => name));
  const discovered = new Set();
  const payloads = {};
  for (const agent of agents) {
    if (offline) {
      const payload = offlineDiscoveryPayloadForPlanning(agent);
      payloads[agent] = payload;
      for (const { name } of payload) if (canonical.has(name)) discovered.add(name);
      continue;
    }
    const executable = process.env.MONICA_GUIDE_NPX || 'npx';
    const result = run(executable, ['--yes', cliSpec, 'ls', '-g', '-a', agent, '--json']);
    if (result.status !== 0) throw new GuideError('global_skill_discovery_failed', `Cannot enumerate ${agent} global skills before switching or updating the one active Monica release.`);
    let payload;
    try { payload = JSON.parse(result.stdout); } catch { throw new GuideError('global_skill_discovery_contract_invalid', `${agent} returned invalid skills ls --json output.`); }
    if (!Array.isArray(payload)) throw new GuideError('global_skill_discovery_contract_invalid', `${agent} skills ls --json must return a top-level array.`);
    payloads[agent] = payload;
    for (const entry of payload) if (canonical.has(entry?.name)) discovered.add(entry.name);
  }
  return { skills: [...discovered].sort(compareOrdinalUtf8), payloads };
}

function offlineDiscoveryPayloadForPlanning(agent) {
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
      payload.push({ name: entry.name, path: path.join(root, entry.name), scope: 'global', agents: [agent] });
    }
  }
  return payload;
}

function installedSkillDrift(skills, agents, payloads, manifest, offline) {
  const drift = [];
  for (const skill of skills) {
    const failures = [];
    for (const agent of agents) {
      try {
        verifyDiscoveryPayload(payloads[agent] || [], [skill], manifest);
        if (!offline) {
          const entry = payloads[agent].find((candidate) => candidate?.name === skill);
          const memberships = new Set((entry?.agents || []).map(normalizeSkillsCliAgent).filter(Boolean));
          if (!memberships.has(agent)) throw new GuideError('skill_agent_membership_mismatch', `${skill} is not installed for ${agent}.`);
        }
      } catch (error) {
        failures.push({ agent, code: error.code || 'skill_installation_drift', message: error.message });
      }
    }
    if (failures.length) drift.push({ name: skill, failures });
  }
  return drift;
}

function addBlocker(plan, code, message, details = undefined) {
  plan.blockers.push({ code, message, ...(details === undefined ? {} : { details }) });
}

function addWarning(plan, code, message, details = undefined) {
  plan.warnings.push({ code, message, ...(details === undefined ? {} : { details }) });
}

function fileAction(filePath, before, after, purpose, mode = 0o600) {
  if (before === after) return null;
  return {
    type: after === null ? 'delete-file' : 'write-file',
    purpose,
    path: filePath,
    beforeDigest: before === null ? null : digest(before),
    afterDigest: after === null ? null : digest(after),
    diff: textDiff(filePath, before, after),
    content: after,
    mode,
  };
}

function comparableRelease(release) {
  if (!release) return null;
  return {
    id: release.id,
    monicaVersion: release.monicaVersion ?? null,
    tag: release.tag ?? null,
    commit: release.commit,
    catalogDigest: release.catalogDigest ?? null,
  };
}

function workspaceFingerprint(workspace, statePath, catalogInfo) {
  const gitFingerprint = gitWorkspaceFingerprint(workspace);
  const files = walkFiles(workspace, {
    maxDepth: 8,
    include: (_file, name) => name === 'AGENTS.md'
      || name === 'CLAUDE.md'
      || name === 'guide.json'
      || name === 'Directory.Build.props'
      || name === 'Directory.Packages.props'
      || name === 'packages.lock.json'
      || name === 'project.assets.json'
      || name.endsWith('.csproj')
      || name.endsWith('.fsproj')
      || (!gitFingerprint && /\.(?:cs|razor|md|json|ya?ml|props|targets|lock|toml|[cm]?js|tsx?)$/i.test(name)),
  });
  const hashes = {};
  for (const file of files) hashes[path.relative(workspace, file).replaceAll(path.sep, '/')] = fileDigest(file);
  for (const projectFile of files.filter((file) => file.endsWith('.csproj') || file.endsWith('.fsproj'))) {
    const assets = path.join(path.dirname(projectFile), 'obj', 'project.assets.json');
    if (exists(assets)) hashes[path.relative(workspace, assets).replaceAll(path.sep, '/')] = fileDigest(assets);
  }
  return digest({
    files: hashes,
    git: gitFingerprint,
    state: fileDigest(statePath),
    catalog: catalogInfo.catalogDigest,
    index: catalogInfo.indexDigest,
  });
}

function refreshStoredSource(binding, release, profile, resolverPath) {
  if (!binding) return null;
  if (binding.commit !== release?.commit) throw new GuideError('stored_source_release_mismatch', `Stored source commit ${binding.commit || 'missing'} does not match release ${release?.commit || 'missing'}.`);
  if (!binding.sourcePath || !exists(binding.sourcePath)) throw new GuideError('stored_source_unavailable', 'Stored source path is unavailable.', { sourcePath: binding.sourcePath });
  if (binding.verificationState !== 'verified') throw new GuideError('stored_source_unverified', 'Stored source binding is not verified.');
  if (binding.provenance === 'local-git' || profile === 'framework-contributor') {
    return verifyLocalSource(binding.sourcePath, {
      access: profile === 'framework-contributor' ? 'read-write' : 'read-only',
      expectedCommit: release.commit,
    });
  }
  if (binding.access !== 'read-only'
    || binding.dirty === true
    || binding.provenance?.resolver !== 'inspect-dependency-source'
    || !['exact_commit', 'exact_tag'].includes(binding.resolutionKind)) {
    throw new GuideError('stored_source_provenance_invalid', 'Stored source binding has unsupported provenance or access.');
  }
  return resolveCachedSource({ exactRef: release.commit, access: 'read-only', resolverPath });
}

function cliArguments(cliSpec, offline) {
  return [...(offline ? ['--offline'] : []), '--yes', cliSpec];
}

function installAction(skill, release, agents, cliSpec, { offline = false, localSourceRoot = null, catalog } = {}) {
  if ((offline || release.channel === 'source') && !localSourceRoot) throw new GuideError('local_skill_source_required', 'Offline and source-channel installs require an exact verified local Monica skill source.');
  const source = localSourceRoot ? path.resolve(localSourceRoot, catalog.skills[skill].path) : canonicalSkillUrl(release.installRef, skill);
  const args = [...cliArguments(cliSpec, offline), 'add', source, '-g'];
  for (const agent of agents) args.push('-a', agent);
  args.push('-s', skill, '-y');
  return {
    type: 'install-skill',
    purpose: localSourceRoot
      ? `Install catalog-selected Monica skill ${skill} globally from verified local source ${source}.`
      : `Install catalog-selected Monica skill ${skill} globally from ${release.installRef}.`,
    skill,
    source,
    offline,
    command: shellDisplay('npx', args),
    executable: 'npx',
    args,
  };
}

function verificationAction(skills, agents, release, cliSpec, manifest, canonicalSkills, rejectUnexpected, offline) {
  const prefixes = skills.map((skill) => `skills/${skill}/`);
  const selectedManifest = {
    schemaVersion: manifest?.schemaVersion || 2,
    skillDigestAlgorithm: manifest?.skillDigestAlgorithm,
    skillDigests: Object.fromEntries(skills.map((skill) => [skill, manifest?.skillDigests?.[skill]])),
    skillRevisions: Object.fromEntries(skills.map((skill) => [skill, manifest?.skillRevisions?.[skill] ?? null])),
    skillLastChangedIn: Object.fromEntries(skills.map((skill) => [skill, manifest?.skillLastChangedIn?.[skill] ?? null])),
    files: Object.fromEntries(Object.entries(manifest?.files || {}).filter(([filePath]) => prefixes.some((prefix) => filePath.startsWith(prefix)))),
  };
  return {
    type: 'verify-skills',
    purpose: 'Verify actual global skill discovery for every selected host.',
    skills,
    agents,
    cliSpec,
    releaseRef: release.installRef,
    canonicalSkills,
    rejectUnexpected,
    offline,
    manifest: selectedManifest,
    commands: [shellDisplay('npx', [...cliArguments(cliSpec, offline), 'ls', '-g', '--json'])],
  };
}

function transactionGuardAction(skills, agents, cliSpec, offline, installActions, restoreReference) {
  const installSources = Object.fromEntries(installActions.map((action) => [action.skill, action.source]));
  const restoreSources = Object.fromEntries(installActions.map((action) => [
    action.skill,
    restoreReference ? canonicalSkillUrl(restoreReference, action.skill) : action.source,
  ]));
  return {
    type: 'protect-global-skills',
    purpose: 'Protect planned Monica skill and file mutations with a private snapshot and verified compensation.',
    skills,
    agents,
    cliSpec,
    offline,
    installSources,
    restoreSources,
    guarantee: 'best-effort-compensation',
  };
}

function prepareForget(plan, context) {
  const { workspace, project, state, statePath, key, catalog } = context;
  let instructionsChanged = false;
  if (!project.config) addWarning(plan, 'not_initialized', 'This repository has no .monica/guide.json configuration.');
  const markers = catalog.managedInstructions?.markers || { start: '<!-- monica-guide:managed:start -->', end: '<!-- monica-guide:managed:end -->' };
  const agentsPath = path.join(workspace, 'AGENTS.md');
  if (exists(agentsPath)) {
    const before = readText(agentsPath);
    const after = removeInstructionBlock(before, markers);
    const action = fileAction(agentsPath, before, after, 'Remove only the Monica Guide managed instruction block.');
    if (action) {
      plan.actions.push(action);
      instructionsChanged = true;
    }
  }
  const claudePath = path.join(workspace, 'CLAUDE.md');
  if (project.config?.managedClaudeImport && exists(claudePath)) {
    const before = readText(claudePath);
    const after = removeClaudeImport(before);
    const action = fileAction(claudePath, before, after === '' ? null : after, 'Remove the Guide-owned @AGENTS.md import.');
    if (action) {
      plan.actions.push(action);
      instructionsChanged = true;
    }
  }
  if (project.config) plan.actions.push(fileAction(project.filePath, readText(project.filePath), null, 'Remove repository-shared Monica Guide configuration.'));
  const nextState = structuredClone(state);
  delete nextState.workspacePreferences[key];
  delete nextState.sourceBindings[key];
  delete nextState.contributionPreferences[key];
  delete nextState.observations[key];
  const beforeState = exists(statePath) ? readText(statePath) : null;
  const afterState = stableJson(nextState, 2);
  const stateAction = fileAction(statePath, beforeState, afterState, 'Forget this workspace from Monica Guide user state.');
  if (stateAction) plan.actions.push(stateAction);
  return instructionsChanged;
}

function prepareContribution(plan, context, options) {
  const preference = options.preference || context.state.contributionPreferences[context.key] || 'ask';
  if (!['never', 'prepare', 'ask'].includes(preference)) throw new GuideError('invalid_contribution_preference', `Contribution preference must be never, prepare, or ask; found ${preference}.`);
  const nextState = structuredClone(context.state);
  nextState.contributionPreferences[context.key] = preference;
  const before = exists(context.statePath) ? readText(context.statePath) : null;
  const after = stableJson(nextState, 2);
  const action = fileAction(context.statePath, before, after, `Set local contribution preparation preference to ${preference}.`);
  if (action) plan.actions.push(action);
  plan.route = {
    skill: 'monica-contribution',
    preference,
    remoteMutationAuthorized: false,
    message: preference === 'never' ? 'Do not prepare an upstream contribution.' : 'Use $monica-contribution for local classification and drafting; request fresh approval before every remote mutation.',
  };
}

async function prepareReleaseContext(context, plan, options) {
  if (context.detection.versionError) {
    addBlocker(plan, context.detection.versionError.code, context.detection.versionError.message, context.detection.versionError.details);
    return;
  }
  if (context.channel === 'source') {
    try {
      context.release = resolveRelease(context.index, {
        channel: 'source',
        frameworkVersion: context.detection.frameworkVersion.version,
        sourceRef: options.sourceRef || context.project.config?.expectedCatalogRelease?.commit,
      });
      const policyAccess = context.profile === 'framework-contributor' ? 'read-write' : 'read-only';
      let binding;
      if (options.sourcePath || context.profile === 'framework-contributor') {
        const selectedPath = options.sourcePath || context.workspace;
        const exact = verifyLocalSource(selectedPath, { access: policyAccess, expectedCommit: context.release.commit });
        assertSourceContractClean(exact.sourcePath);
        binding = { ...exact, access: policyAccess };
      } else {
        binding = resolveCachedSource({ exactRef: context.release.commit, access: 'read-only', resolverPath: options.sourceResolver });
      }
      const catalogPath = path.join(binding.sourcePath, '.monica', 'agent-skill-catalog.json');
      const skillsPath = path.join(binding.sourcePath, 'skills');
      if (!exists(catalogPath) || !exists(skillsPath)) throw new GuideError('source_catalog_unavailable', 'Exact source channel requires .monica/agent-skill-catalog.json and skills/ at the selected commit.');
      const sourceCatalogInfo = loadCatalog({ catalogPath, indexPath: context.catalogInfo.indexPath });
      context.catalog = sourceCatalogInfo.catalog;
      context.catalogDigest = sourceCatalogInfo.catalogDigest;
      context.installManifest = buildSourceManifest(binding.sourcePath, context.catalog);
      context.sourceBinding = binding;
      context.release.catalogDigest = context.catalogDigest;
      context.release.skillDigests = context.installManifest.skillDigests;
      context.release.skillRevisions = context.installManifest.skillRevisions;
      context.release.skillLastChangedIn = context.installManifest.skillLastChangedIn;
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      addBlocker(plan, error.code, error.message, error.details);
    }
    return;
  }

  let releaseTag = options.releaseTag
    || context.project.config?.expectedCatalogRelease?.indexTag
    || context.project.config?.expectedCatalogRelease?.tag;
  const shouldDiscover = !options.releaseTag
    && !options.offline
    && ['stable', 'preview'].includes(context.channel)
    && (context.intent === 'update' || !context.project.config?.expectedCatalogRelease?.tag);
  if (shouldDiscover) {
    try {
      releaseTag = await discoverChannelReleaseTag(context.channel, options.fetchImplementation);
      context.discoveredReleaseTag = releaseTag;
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      addBlocker(plan, error.code, error.message, error.details);
      return;
    }
  }
  let releaseIndex;
  try {
    releaseIndex = await loadReleaseIndex({
      releaseTag,
      releaseIndexPath: options.releaseIndex,
      releaseIndexUrl: options.releaseIndexUrl,
      offline: options.offline,
      state: context.state,
      statePath: context.statePath,
      fetchImplementation: options.fetchImplementation,
    });
  } catch (error) {
    if (!(error instanceof GuideError)) throw error;
    addBlocker(plan, error.code, error.message, error.details);
    return;
  }
  if (releaseIndex) {
    context.index = releaseIndex.index;
    context.releaseIndex = releaseIndex;
  }
  try {
    context.release = resolveRelease(context.index, {
      channel: context.channel,
      frameworkVersion: context.detection.frameworkVersion.version,
      sourceRef: options.sourceRef,
    });
    context.releaseArtifacts = await loadReleaseArtifacts({
      release: context.release,
      releaseCatalogPath: options.releaseCatalog,
      releaseManifestPath: options.releaseManifest,
      offline: options.offline,
      state: context.state,
      statePath: context.statePath,
      fetchImplementation: options.fetchImplementation,
    });
    context.catalog = context.releaseArtifacts.catalog;
    context.catalogDigest = context.releaseArtifacts.catalogDigest;
    context.installManifest = context.releaseArtifacts.manifest;
  } catch (error) {
    if (!(error instanceof GuideError)) throw error;
    addBlocker(plan, error.code, error.message, error.details);
  }
}

export async function buildPlan(intent, options = {}) {
  if (!MUTATING_INTENTS.has(intent)) throw new GuideError('invalid_intent', `Cannot build a mutating plan for ${intent}.`);
  if (options.skills?.length && intent !== 'update') throw new GuideError('targeted_skill_intent_invalid', '--skill is supported only by update.');
  if (options.nestedInstructions?.length && !['init', 'configure', 'update'].includes(intent)) {
    throw new GuideError('nested_instruction_intent_invalid', '--nested-instruction is supported only by init, configure, and update.');
  }
  const workspace = normalizePath(options.workspace);
  if (!exists(workspace) || !fs.statSync(workspace).isDirectory()) throw new GuideError('workspace_unavailable', `Workspace is not a directory: ${workspace}.`);
  const statePath = stateFilePath(options.state);
  const state = loadState(statePath);
  const project = loadProjectConfig(workspace);
  const catalogInfo = loadCatalog({ catalogPath: options.catalog, indexPath: options.index });
  let catalog = catalogInfo.catalog;
  const detection = workspaceDetection(workspace);
  const key = workspaceKey(workspace, detection.repository.identity);
  const context = {
    workspace, statePath, state, project, catalogInfo, catalog, index: catalogInfo.index, detection, key,
    release: null, releaseIndex: null, releaseArtifacts: null, installManifest: null, catalogDigest: catalogInfo.catalogDigest, sourceBinding: null, intent,
  };
  const plan = {
    schemaVersion: 1,
    intent,
    workspace,
    dryRun: true,
    context: {
      repositoryIdentity: detection.repository.identity,
      candidateProfile: detection.repository.candidateProfile,
      candidateConfidence: detection.repository.confidence,
      detectedFrameworkVersion: detection.frameworkVersion.version,
      versionSource: detection.frameworkVersion.tier,
      activeGlobalRelease: comparableRelease(state.activeRelease),
    },
    actions: [],
    warnings: [],
    blockers: [],
  };
  if (intent === 'forget') {
    if (prepareForget(plan, context)) addWarning(plan, 'instruction_reload_required', 'AGENTS.md or CLAUDE.md will change; start a new agent run or session after applying.');
  } else if (intent === 'contribute') {
    prepareContribution(plan, context, options);
  } else {
    const profile = options.profile || project.config?.profile || detection.repository.candidateProfile;
    const profileConfirmed = Boolean(options.profile || project.config?.profile);
    if (!profile || !VALID_PROFILES.has(profile)) addBlocker(plan, 'profile_required', 'Select one of application, extension-author, framework-contributor, or docs-contributor.');
    if (!profileConfirmed && ['init', 'configure'].includes(intent)) {
      addBlocker(plan, 'profile_confirmation_required', `Detected ${profile || 'no unambiguous profile'}; rerun with --profile after user confirmation.`);
    }
    if (intent === 'update' && !project.config) addBlocker(plan, 'initialization_required', 'Run and approve init before update.');
    if (intent === 'source' && !profileConfirmed) addBlocker(plan, 'profile_confirmation_required', 'Confirm --profile before creating the first source binding.');
    if (profile === 'extension-author' && (detection.repository.characteristics.projectReference || detection.frameworkVersion.entries.some((entry) => entry.origin === 'ProjectReference'))) {
      addBlocker(plan, 'extension_project_reference_forbidden', 'Extension projects must consume Monica through immutable NuGet packages; a Monica ProjectReference was detected. Bind read-only source separately.');
    }
    for (const issue of profileRepositoryIssues(workspace, profile, detection.repository)) addBlocker(plan, issue.code, issue.message, issue.details);
    if (profile && detection.repository.candidateProfile && profile !== detection.repository.candidateProfile) {
      addWarning(plan, 'profile_differs_from_detection', `Confirmed profile ${profile} differs from detected candidate ${detection.repository.candidateProfile}.`);
    }
    context.profile = profile;
    context.channel = options.channel || project.config?.channel || semverChannel(detection.frameworkVersion.version) || 'stable';
    context.capabilities = [...new Set(options.capabilities?.length ? options.capabilities : (project.config?.capabilities || detection.repository.capabilities || []))].sort();
    context.agents = normalizeAgents(options.agents?.length ? options.agents : (project.config?.agentTargets || ['codex', 'claude-code']));
    context.targetSkills = normalizeTargetSkills(options.skills || []);
    context.nestedInstructionSelections = resolveNestedInstructionSelections(workspace, detection, options.nestedInstructions || []);
    for (const selection of context.nestedInstructionSelections.filter((entry) => entry.kind === 'claude')) {
      if (!context.agents.includes('claude-code')) {
        addBlocker(plan, 'nested_claude_requires_agent', `Selected nested CLAUDE.md requires the claude-code agent target: ${selection.relative}.`);
      }
      const siblingAgents = path.join(path.dirname(selection.path), 'AGENTS.md');
      const siblingIssue = validateNestedClaudeSibling(workspace, siblingAgents, detection);
      if (siblingIssue === 'missing') {
        addBlocker(plan, 'nested_claude_agents_missing', `Selected nested CLAUDE.md requires an existing sibling AGENTS.md: ${selection.relative}.`);
      } else if (siblingIssue) {
        addBlocker(
          plan,
          'nested_claude_agents_unsafe',
          `Selected nested CLAUDE.md requires a diagnosed, regular, non-symlinked sibling AGENTS.md: ${selection.relative}.`,
          { sibling: path.relative(workspace, siblingAgents).split(path.sep).join('/'), reason: siblingIssue },
        );
      }
    }
    plan.context.profile = profile;
    plan.context.profileConfirmed = profileConfirmed;
    plan.context.channel = context.channel;
    plan.context.capabilities = context.capabilities;
    plan.context.agentTargets = context.agents;
    plan.context.requestedSkills = context.targetSkills;
    await prepareReleaseContext(context, plan, options);
    if (context.release) {
      try { assertProjectReferenceRelease(detection.frameworkVersion, context.release); }
      catch (error) { if (error instanceof GuideError) addBlocker(plan, error.code, error.message, error.details); else throw error; }
    }
    catalog = context.catalog;
    if (profile && context.release) {
      try { context.closure = resolveProfileClosure(catalog, profile, context.capabilities); }
      catch (error) { if (error instanceof GuideError) addBlocker(plan, error.code, error.message, error.details); else throw error; }
    }
    if (context.release) {
      plan.context.targetRelease = comparableRelease(context.release);
      const active = state.activeRelease;
      if (active && active.id !== context.release.id && !options.switchGlobal) {
        addBlocker(plan, 'global_release_conflict', `Global Monica release ${active.id} is active, while this repository requires ${context.release.id}. Rerun with --switch-global only after explicitly choosing the switch.`);
      }
    }
    if (intent === 'source' && context.release && state.activeRelease?.id !== context.release.id) {
      addBlocker(plan, 'global_release_not_verified', `Source binding requires already-installed global release ${context.release.id}; active release is ${state.activeRelease?.id || 'none'}.`);
    }
    let sourceBinding = context.sourceBinding;
    let discardStoredSource = false;
    if (context.closure && context.release) {
      const existingBinding = state.sourceBindings[key];
      let refreshedStoredSource = null;
      if (existingBinding && !options.sourcePath && !sourceBinding) {
        try {
          refreshedStoredSource = refreshStoredSource(existingBinding, context.release, profile, options.sourceResolver);
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          discardStoredSource = true;
          addWarning(plan, 'stored_source_invalid', `Stored exact source binding is no longer valid and will not be retained: ${error.message}`, error.details);
        }
      }
      const projectReferenceSources = [...new Set(detection.frameworkVersion.entries
        .filter((entry) => entry.origin === 'ProjectReference')
        .map((entry) => entry.sourcePath))];
      if (sourceBinding) {
        // Source-channel preparation already established exact catalog/source parity.
      } else if (refreshedStoredSource) {
        sourceBinding = refreshedStoredSource;
      } else if (profile === 'application' && projectReferenceSources.length) {
        if (projectReferenceSources.length > 1) {
          addBlocker(plan, 'multiple_project_reference_sources', 'Monica ProjectReferences resolve to more than one local source checkout; select one exact source explicitly.', projectReferenceSources);
        } else {
          try {
            sourceBinding = verifyLocalSource(projectReferenceSources[0], { access: 'read-only', expectedCommit: context.release.commit });
          } catch (error) {
            if (!(error instanceof GuideError)) throw error;
            addBlocker(plan, error.code, error.message, error.details);
          }
        }
      } else if (options.offline && !options.sourcePath && profile !== 'framework-contributor') {
        try {
          sourceBinding = sourceBindingForProfile({
            workspace,
            profile,
            profileClosure: { ...context.closure, source: { ...context.closure.source, required: true } },
            release: context.release,
            sourceAccess: options.sourceAccess,
            resolverPath: options.sourceResolver,
          });
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          if (error.code === 'source_access_forbidden') addBlocker(plan, error.code, error.message, error.details);
          else addBlocker(
            plan,
            'offline_local_skill_source_required',
            'Offline operation requires an exact verified local/cached Monica source whose managed skills match the selected release.',
            { cause: { code: error.code, message: error.message, details: error.details } },
          );
        }
      } else if (context.closure.source.required || options.sourcePath || intent === 'source') {
        try {
          sourceBinding = sourceBindingForProfile({
            workspace,
            profile,
            profileClosure: intent === 'source'
              ? { ...context.closure, source: { ...context.closure.source, required: true } }
              : context.closure,
            release: context.release,
            sourcePath: options.sourcePath,
            sourceAccess: options.sourceAccess,
            resolverPath: options.sourceResolver,
          });
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          addBlocker(plan, error.code, error.message, error.details);
        }
      } else if (context.closure.source.recommended) {
        addWarning(plan, 'source_recommended', 'Exact read-only Monica source is recommended for behavior that depends on framework internals.');
      }
    }
    const requiresLocalSkillSource = intent !== 'source' && (Boolean(options.offline) || context.channel === 'source');
    context.localSkillSource = null;
    if (requiresLocalSkillSource) {
      if (!sourceBinding) {
        if (!plan.blockers.some((entry) => entry.code === 'offline_local_skill_source_required')) {
          addBlocker(plan, 'local_skill_source_required', `${options.offline ? 'Offline operation' : 'The source channel'} requires an exact verified local Monica source whose managed skills match the selected release.`);
        }
      } else {
        try {
          const localManifest = verifyLocalSkillSource(sourceBinding.sourcePath, catalog, context.installManifest);
          context.localSkillSource = {
            path: sourceBinding.sourcePath,
            binding: sourceBinding,
            manifest: localManifest,
            skillPaths: Object.fromEntries(Object.entries(catalog.skills)
              .filter(([, entry]) => entry.ownership === 'monica' && entry.managed === true)
              .map(([name, entry]) => [name, entry.path])),
          };
          plan.context.installationSource = { type: 'local', path: sourceBinding.sourcePath, offline: Boolean(options.offline) };
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          addBlocker(plan, error.code, error.message, error.details);
        }
      }
    } else if (intent !== 'source') {
      plan.context.installationSource = { type: 'immutable-tag', ref: context.release?.installRef || null, offline: false };
    }
    if (context.closure?.external.length) addWarning(plan, 'external_skills_unmanaged', `External skills are diagnosed but never installed or updated by Monica Guide: ${context.closure.external.join(', ')}.`);

    if (context.release && context.closure && plan.blockers.length === 0) {
      const cliSpec = skillsCliSpec(catalog);
      const managedAgents = [...new Set([...state.agentTargets, ...context.agents])].sort();
      let installationDiscovery = { skills: [], payloads: {} };
      const enforceGlobalInventory = intent === 'update' || Boolean(state.activeRelease && state.activeRelease.id !== context.release.id);
      if (enforceGlobalInventory) {
        try {
          installationDiscovery = discoverInstalledManagedSkills(managedAgents, catalog, cliSpec, Boolean(options.offline));
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          addBlocker(plan, error.code, error.message, error.details);
        }
      }
      const recordedManagedSkills = Object.keys(state.managedSkills);
      const retiredManagedSkills = recordedManagedSkills.filter((skill) => !catalog.skills[skill] || catalog.skills[skill].ownership !== 'monica' || catalog.skills[skill].managed === false);
      if (retiredManagedSkills.length) addBlocker(plan, 'managed_skill_missing_from_release', `The target catalog no longer manages recorded Monica skills: ${retiredManagedSkills.join(', ')}. Resolve their diagnostic aliases or remove them explicitly before switching releases.`, retiredManagedSkills);
      let managedSkills = [];
      let skillChanges = [];
      let installSkills = [];
      try {
        const managedRoots = [...new Set([...recordedManagedSkills, ...context.closure.selected, ...installationDiscovery.skills])]
          .filter((skill) => catalog.skills[skill]?.ownership === 'monica' && catalog.skills[skill]?.managed !== false);
        managedSkills = resolveRequiredSkillClosure(catalog, managedRoots);
        skillChanges = compareManagedSkillRecords(state.managedSkills, managedSkills, context.release, context.installManifest);
        const drift = enforceGlobalInventory
          ? installedSkillDrift(managedSkills, managedAgents, installationDiscovery.payloads, context.installManifest, Boolean(options.offline))
          : [];
        const driftNames = new Set(drift.map((entry) => entry.name));
        plan.context.installedSkillDrift = drift;
        if (context.targetSkills.length) {
          const unmanagedTargets = context.targetSkills.filter((skill) => !managedSkills.includes(skill));
          if (unmanagedTargets.length) {
            addBlocker(
              plan,
              'targeted_skill_not_managed',
              `Targeted update skills are not part of the active global Monica set: ${unmanagedTargets.join(', ')}.`,
              { skills: unmanagedTargets },
            );
          } else {
            const targetClosure = resolveRequiredSkillClosure(catalog, context.targetSkills);
            const targetSet = new Set(targetClosure);
            const agentTargetsChanged = !sameStringSet(state.agentTargets, managedAgents);
            const sourceInstallRequired = requiresSourceReinstall(context, state, skillChanges);
            if (agentTargetsChanged) {
              addBlocker(plan, 'targeted_update_agent_change', 'A targeted update cannot change global agent targets. Run a full update so every managed skill is installed for the new target set.');
            }
            if (sourceInstallRequired) {
              addBlocker(plan, 'targeted_update_source_switch', 'A targeted update cannot establish or change source-channel provenance. Run a full update from the exact bound checkout.');
            }
            const outsideChanges = skillChanges.filter((entry) => !targetSet.has(entry.name)
              && ['new', 'unknown', 'content-changed'].includes(entry.changeState));
            if (outsideChanges.length) {
              addBlocker(
                plan,
                'targeted_update_would_mix_releases',
                'The target release changes other managed skills outside the requested required-dependency closure. Run a full update instead.',
                { requested: context.targetSkills, closure: targetClosure, outsideChanges },
              );
            }
            const outsideDrift = drift.filter((entry) => !targetSet.has(entry.name));
            if (outsideDrift.length) {
              addBlocker(
                plan,
                'targeted_update_other_skill_drift',
                'Other managed skills are missing, tampered, or not installed for every target agent. Run a full update so final verification can succeed.',
                { requested: context.targetSkills, closure: targetClosure, outsideDrift },
              );
            }
            installSkills = skillChanges
              .filter((entry) => targetSet.has(entry.name)
                && (driftNames.has(entry.name) || ['new', 'unknown', 'content-changed'].includes(entry.changeState)))
              .map((entry) => entry.name);
            plan.context.targetedSkillClosure = targetClosure;
          }
        } else if (intent === 'update') {
          const agentTargetsChanged = !sameStringSet(state.agentTargets, managedAgents);
          const metadataUnknown = skillChanges.some((entry) => entry.changeState === 'unknown');
          const sourceInstallRequired = requiresSourceReinstall(context, state, skillChanges);
          installSkills = agentTargetsChanged || metadataUnknown || sourceInstallRequired
            ? managedSkills
            : skillChanges
              .filter((entry) => driftNames.has(entry.name) || ['new', 'content-changed'].includes(entry.changeState))
              .map((entry) => entry.name);
          plan.context.fullReinstallReason = agentTargetsChanged
            ? 'agent-targets-changed'
            : metadataUnknown
              ? 'skill-metadata-unknown'
              : sourceInstallRequired ? 'source-provenance-change' : null;
        } else {
          installSkills = managedSkills;
        }
      } catch (error) {
        if (!(error instanceof GuideError)) throw error;
        addBlocker(plan, error.code, error.message, error.details);
      }
      plan.context.skillChanges = skillChanges;
      plan.context.installSkills = installSkills;
      if (intent !== 'source' && plan.blockers.length === 0) {
        const installActions = installSkills.map((skill) => installAction(skill, context.release, managedAgents, cliSpec, {
          offline: Boolean(options.offline),
          localSourceRoot: context.localSkillSource?.path || null,
          catalog,
        }));
        const restoreReference = state.activeRelease?.tag || state.activeRelease?.commit || context.release.installRef;
        plan.actions.push(transactionGuardAction(
          installSkills,
          managedAgents,
          cliSpec,
          Boolean(options.offline),
          installActions,
          restoreReference,
        ));
        plan.actions.push(...installActions);
        const canonicalSkills = Object.entries(catalog.skills)
          .filter(([, entry]) => entry.ownership === 'monica' && entry.managed !== false)
          .map(([name]) => name)
          .sort();
        plan.actions.push(verificationAction(managedSkills, managedAgents, context.release, cliSpec, context.installManifest, canonicalSkills, enforceGlobalInventory, Boolean(options.offline)));
      }
      const nextState = structuredClone(state);
      if (intent !== 'source') {
        nextState.activeRelease = comparableRelease(context.release);
        nextState.managedSkills = Object.fromEntries(skillChanges.map((entry) => [entry.name, entry.target]));
        nextState.agentTargets = managedAgents;
      }
      nextState.workspacePreferences[key] = {
        workspace,
        repository: detection.repository.identity,
        profile,
        channel: context.channel,
        capabilities: context.capabilities,
      };
      if (sourceBinding) nextState.sourceBindings[key] = sourceBinding;
      else if (discardStoredSource) delete nextState.sourceBindings[key];
      if (!(key in nextState.contributionPreferences)) nextState.contributionPreferences[key] = 'ask';
      if (context.releaseIndex?.needsCache) {
        nextState.verifiedReleaseIndexes[context.releaseIndex.tag] = {
          digest: context.releaseIndex.indexDigest,
          commit: context.releaseIndex.release.commit,
        };
        const cacheBefore = exists(context.releaseIndex.cachePath) ? readText(context.releaseIndex.cachePath) : null;
        const cacheAction = fileAction(context.releaseIndex.cachePath, cacheBefore, context.releaseIndex.cacheContent, `Cache the verified immutable release index for ${context.releaseIndex.tag}.`);
        if (cacheAction) plan.actions.push(cacheAction);
      }
      if (context.releaseArtifacts?.needsCache) {
        nextState.verifiedReleaseArtifacts[context.release.tag] = {
          catalogDigest: context.releaseArtifacts.catalogDigest,
          manifestDigest: context.releaseArtifacts.manifestDigest,
          commit: context.release.commit,
        };
        const catalogBefore = exists(context.releaseArtifacts.cachePaths.catalog) ? readText(context.releaseArtifacts.cachePaths.catalog) : null;
        const manifestBefore = exists(context.releaseArtifacts.cachePaths.manifest) ? readText(context.releaseArtifacts.cachePaths.manifest) : null;
        const catalogAction = fileAction(context.releaseArtifacts.cachePaths.catalog, catalogBefore, context.releaseArtifacts.catalogContent, `Cache the verified release catalog for ${context.release.tag}.`);
        const manifestAction = fileAction(context.releaseArtifacts.cachePaths.manifest, manifestBefore, context.releaseArtifacts.manifestContent, `Cache the verified release manifest for ${context.release.tag}.`);
        if (catalogAction) plan.actions.push(catalogAction);
        if (manifestAction) plan.actions.push(manifestAction);
      }
      const beforeState = exists(statePath) ? readText(statePath) : null;
      const afterState = stableJson(nextState, 2);
      const stateAction = fileAction(statePath, beforeState, afterState, 'Update Monica Guide user state with the active global release and workspace preferences.');
      if (stateAction) plan.actions.push(stateAction);

      if (intent !== 'source') {
        const expectedRelease = { ...comparableRelease(context.release), indexTag: context.releaseIndex?.tag || null };
        const claudePath = path.join(workspace, 'CLAUDE.md');
        const claudeBefore = exists(claudePath) ? readText(claudePath) : '';
        const rootClaudeState = claudeImportState(claudeBefore);
        if (rootClaudeState.status === 'duplicate') throw new GuideError('duplicate_claude_import', 'Refusing to modify CLAUDE.md because it contains duplicate @AGENTS.md imports.');
        const previousClaudeOwnership = project.config?.managedClaudeImport === true;
        const wantsClaude = context.agents.includes('claude-code');
        const managedClaudeImport = wantsClaude && (previousClaudeOwnership || rootClaudeState.status === 'absent');
        const projectConfig = {
          schemaVersion: PROJECT_SCHEMA_VERSION,
          profile,
          channel: context.channel,
          capabilities: context.capabilities,
          agentTargets: context.agents,
          expectedCatalogRelease: expectedRelease,
          instructionBlockVersion: catalog.managedInstructions?.version || 1,
          managedClaudeImport,
        };
        const projectBefore = project.config ? readText(project.filePath) : null;
        const projectAfter = stableJson(projectConfig, 2);
        const projectAction = fileAction(project.filePath, projectBefore, projectAfter, 'Write repository-shared Monica Guide configuration.', 0o644);
        if (projectAction) plan.actions.push(projectAction);

        const markers = catalog.managedInstructions?.markers || { start: '<!-- monica-guide:managed:start -->', end: '<!-- monica-guide:managed:end -->' };
        const managedBody = renderManagedInstructions(catalog, context);
        let instructionsChanged = false;
        const agentsPath = path.join(workspace, 'AGENTS.md');
        const agentsBefore = exists(agentsPath) ? readText(agentsPath) : '';
        const agentsAfter = upsertInstructionBlock(agentsBefore, managedBody, { markers });
        const agentsAction = fileAction(agentsPath, exists(agentsPath) ? agentsBefore : null, agentsAfter, 'Create or update only the root Monica Guide managed instruction block.', 0o644);
        if (agentsAction) {
          plan.actions.push(agentsAction);
          instructionsChanged = true;
        }
        if (wantsClaude) {
          const claudeAfter = ensureClaudeImport(claudeBefore);
          const claudeAction = fileAction(claudePath, exists(claudePath) ? claudeBefore : null, claudeAfter, 'Ensure the minimal root Claude Code import @AGENTS.md.', 0o644);
          if (claudeAction) {
            plan.actions.push(claudeAction);
            instructionsChanged = true;
          }
        } else if (previousClaudeOwnership && exists(claudePath)) {
          const claudeAfter = removeClaudeImport(claudeBefore);
          const claudeAction = fileAction(claudePath, claudeBefore, claudeAfter === '' ? null : claudeAfter, 'Remove the Guide-owned @AGENTS.md import because Claude Code is no longer selected.', 0o644);
          if (claudeAction) {
            plan.actions.push(claudeAction);
            instructionsChanged = true;
          }
        }
        for (const selection of context.nestedInstructionSelections) {
          const before = readText(selection.path);
          const after = selection.kind === 'agents'
            ? upsertInstructionBlock(before, managedBody, { markers })
            : ensureClaudeImport(before);
          const action = fileAction(
            selection.path,
            before,
            after,
            selection.kind === 'agents'
              ? `Update the explicitly selected nested managed instruction block at ${selection.relative}.`
              : `Ensure @AGENTS.md only in the explicitly selected nested Claude file ${selection.relative}.`,
            0o644,
          );
          if (action) {
            plan.actions.push(action);
            instructionsChanged = true;
          }
        }
        const selectedNested = new Set(context.nestedInstructionSelections.map((entry) => entry.path));
        const unselectedNested = detection.nestedInstructions.filter((file) => !selectedNested.has(path.resolve(file)));
        if (unselectedNested.length) addWarning(plan, 'nested_instructions_detected', 'Nested AGENTS.md or CLAUDE.md files were diagnosed but not rewritten.', unselectedNested);
        if (instructionsChanged) addWarning(plan, 'instruction_reload_required', 'AGENTS.md or CLAUDE.md will change; start a new agent run or session after applying.');
      }
    }
  }
  plan.preconditions = {
    fingerprint: workspaceFingerprint(workspace, statePath, catalogInfo),
    statePath,
    catalogDigest: catalogInfo.catalogDigest,
    indexDigest: context.releaseIndex?.indexDigest || catalogInfo.indexDigest,
    releaseCatalogDigest: context.catalogDigest,
    releaseManifestDigest: context.releaseArtifacts?.manifestDigest || (context.installManifest ? digest(context.installManifest) : null),
    localSkillSource: context.localSkillSource ? {
      path: context.localSkillSource.path,
      binding: context.localSkillSource.binding,
      manifestDigest: digest(context.localSkillSource.manifest),
      targetManifest: context.installManifest,
      skillPaths: context.localSkillSource.skillPaths,
    } : null,
  };
  plan.planDigest = digest({ ...plan, planDigest: undefined });
  return plan;
}

function applyInstall(action) {
  const executable = process.env.MONICA_GUIDE_NPX || action.executable;
  runChecked(executable, action.args, { code: 'skill_install_failed' });
}

function applyFileAction(action) {
  if (action.type === 'write-file') atomicWrite(action.path, action.content, action.mode);
  else if (action.type === 'delete-file' && exists(action.path)) fs.unlinkSync(action.path);
  else throw new GuideError('unknown_action', `Unknown plan action type: ${action.type}.`);
}

function applyVerification(action) {
  const executable = process.env.MONICA_GUIDE_NPX || 'npx';
  const result = runChecked(executable, [...cliArguments(action.cliSpec, action.offline), 'ls', '-g', '--json'], { code: 'skill_discovery_failed' });
  let payload;
  try { payload = JSON.parse(result.stdout); } catch (error) { throw new GuideError('skill_discovery_contract_invalid', `Global skills discovery returned invalid JSON: ${error.message}`); }
  if (!Array.isArray(payload)) throw new GuideError('skill_discovery_contract_invalid', 'Global skills discovery must return a top-level array.');
  if (action.rejectUnexpected) {
    const canonical = new Set(action.canonicalSkills);
    const expected = new Set(action.skills);
    const extras = payload.map((entry) => entry?.name).filter((name) => canonical.has(name) && !expected.has(name)).sort();
    if (extras.length) throw new GuideError('global_skill_set_drift', `Global discovery gained catalog-managed Monica skills after preview: ${extras.join(', ')}. Generate a new update preview so they can be adopted at the active release.`, { extras });
  }
  verifyDiscoveryPayload(payload, action.skills, action.manifest);
  for (const skill of action.skills) {
    const entry = payload.find((candidate) => candidate?.name === skill);
    if (!Array.isArray(entry?.agents)) throw new GuideError('skill_discovery_contract_invalid', `${skill} discovery has no agent membership list.`);
    const actualAgents = new Set(entry.agents.map(normalizeSkillsCliAgent).filter(Boolean));
    const missingAgents = action.agents.filter((agent) => !actualAgents.has(agent));
    if (missingAgents.length) {
      throw new GuideError('skill_agent_membership_mismatch', `${skill} is not discovered for every planned agent target.`, {
        skill,
        expectedAgents: action.agents,
        actualAgents: [...actualAgents].sort(),
        missingAgents,
      });
    }
  }
}

export async function applyPlan(intent, options) {
  if (!options.planDigest) throw new GuideError('plan_digest_required', '--apply requires --plan-digest from the approved dry run.');
  const statePath = stateFilePath(options.state);
  return withFileLock(`${statePath}.lock`, () => {
    // Returning a promise from the lock callback would release too early, so the
    // caller supplies a precomputed plan and all recomputation is synchronous
    // except immutable-index acquisition, which occurred before taking the lock.
    const plan = options.precomputedPlan;
    if (!plan) throw new GuideError('plan_missing', 'Internal error: apply requires a precomputed plan.');
    if (plan.planDigest !== options.planDigest) throw new GuideError('plan_digest_mismatch', `Plan changed: expected ${options.planDigest}, current ${plan.planDigest}.`);
    if (plan.blockers.length) throw new GuideError('plan_blocked', 'The approved plan contains blockers and cannot be applied.', plan.blockers);
    const retainedTransactions = retainedGlobalSkillTransactions(statePath);
    if (retainedTransactions.length) {
      throw new GuideError(
        'global_skill_recovery_required',
        'A retained global-skill transaction must be diagnosed and resolved before another Monica Guide mutation.',
        { transactions: retainedTransactions },
      );
    }
    const currentCatalog = loadCatalog({ catalogPath: options.catalog, indexPath: options.index });
    const currentFingerprint = workspaceFingerprint(plan.workspace, statePath, currentCatalog);
    if (currentFingerprint !== plan.preconditions.fingerprint) {
      throw new GuideError('workspace_drift', 'Workspace, catalog, index, or user state changed after plan computation. Generate and approve a new preview.');
    }
    for (const action of plan.actions.filter((entry) => entry.type === 'write-file' || entry.type === 'delete-file')) {
      const currentDigest = exists(action.path) ? digest(fs.readFileSync(action.path)) : null;
      if (currentDigest !== action.beforeDigest) throw new GuideError('file_drift', `Planned file changed before apply: ${action.path}.`);
    }
    if (plan.preconditions.localSkillSource) {
      const source = plan.preconditions.localSkillSource;
      if (source.binding.provenance === 'local-git') {
        verifyLocalSource(source.path, { access: source.binding.access, expectedCommit: source.binding.commit });
      } else if (source.binding.provenance?.resolver === 'inspect-dependency-source') {
        const refreshed = resolveCachedSource({ exactRef: source.binding.commit, access: 'read-only', resolverPath: options.sourceResolver });
        if (path.resolve(refreshed.sourcePath) !== path.resolve(source.path) || refreshed.commit !== source.binding.commit) {
          throw new GuideError('source_binding_drift', 'Cached Monica source provenance changed after preview. Generate a new plan.', {
            expectedPath: source.path,
            actualPath: refreshed.sourcePath,
            expectedCommit: source.binding.commit,
            actualCommit: refreshed.commit,
          });
        }
      }
      const sourceCatalog = {
        skills: Object.fromEntries(Object.entries(source.skillPaths).map(([name, skillPath]) => [name, {
          path: skillPath,
          ownership: 'monica',
          managed: true,
        }])),
      };
      const currentSourceManifest = verifyLocalSkillSource(source.path, sourceCatalog, source.targetManifest);
      if (digest(currentSourceManifest) !== source.manifestDigest) {
        throw new GuideError('source_drift', 'Exact local Monica skill source changed after preview. Generate a new plan.');
      }
    }
    const guard = plan.actions.find((action) => action.type === 'protect-global-skills');
    const installActions = plan.actions.filter((action) => action.type === 'install-skill');
    const verificationActions = plan.actions.filter((action) => action.type === 'verify-skills');
    const fileActions = plan.actions.filter((action) => action.type === 'write-file' || action.type === 'delete-file');
    const knownActionTypes = new Set(['protect-global-skills', 'install-skill', 'verify-skills', 'write-file', 'delete-file']);
    const unknownAction = plan.actions.find((action) => !knownActionTypes.has(action.type));
    if (unknownAction) throw new GuideError('unknown_action', `Unknown plan action type: ${unknownAction.type}.`);
    if ((installActions.length || verificationActions.length) && !guard) {
      throw new GuideError('global_skill_transaction_guard_missing', 'The approved plan has global skill mutations without a compensation guard. Generate a new preview.');
    }
    let transaction = null;
    if (guard) {
      const protectedResult = withGlobalSkillCompensation({
        statePath,
        skills: guard.skills,
        agents: guard.agents,
        cliSpec: guard.cliSpec,
        installSources: guard.installSources,
        restoreSources: guard.restoreSources,
        fileActions,
        offline: guard.offline,
        mutate: ({ runSkill, runFile }) => {
          for (const action of installActions) runSkill(action.skill, () => applyInstall(action));
          for (const action of verificationActions) applyVerification(action);
          for (const action of fileActions) runFile(action, () => applyFileAction(action));
        },
      });
      transaction = protectedResult.transaction;
    } else {
      for (const action of fileActions) applyFileAction(action);
    }
    const runtimeWarnings = transaction?.status === 'committed-cleanup-pending'
      ? [{
        code: 'global_skill_transaction_cleanup_pending',
        message: `The plan applied successfully, but private transaction cleanup is pending at ${transaction.snapshotPath}. Resolve it before another mutation.`,
        details: transaction,
      }]
      : [];
    return { ...plan, dryRun: false, applied: true, runtimeWarnings };
  });
}

export function instructionDiagnostics(workspace, catalog, projectConfig = null) {
  const agentsPath = path.join(workspace, 'AGENTS.md');
  const markers = catalog.managedInstructions?.markers || { start: '<!-- monica-guide:managed:start -->', end: '<!-- monica-guide:managed:end -->' };
  const block = instructionState(exists(agentsPath) ? readText(agentsPath) : '', markers);
  const expectedVersion = catalog.managedInstructions?.version || 1;
  const issues = [];
  let bodyMatches = null;
  if (block.status === 'malformed') {
    issues.push({ code: 'malformed_instruction_block', severity: 'error', message: 'Root AGENTS.md contains malformed or duplicate Monica Guide markers.' });
  } else if (projectConfig && block.status === 'absent') {
    issues.push({ code: 'instruction_block_missing', severity: 'error', message: 'Configured repository is missing the root Monica Guide instruction block.' });
  } else if (projectConfig && block.status === 'valid') {
    const expectedBody = `\n${renderManagedInstructions(catalog, {
      profile: projectConfig.profile,
      channel: projectConfig.channel,
      release: projectConfig.expectedCatalogRelease,
      capabilities: projectConfig.capabilities || [],
    }).trim()}\n`;
    const actualBody = block.body.replace(/\r\n/g, '\n');
    bodyMatches = actualBody === expectedBody;
    if (!bodyMatches) {
      issues.push({
        code: 'instruction_body_mismatch',
        severity: 'error',
        message: 'Root AGENTS.md managed body does not match the configured profile template.',
        details: { actualDigest: digest(actualBody), expectedDigest: digest(expectedBody) },
      });
    }
  }
  const actualVersion = projectConfig?.instructionBlockVersion ?? null;
  if (projectConfig && actualVersion !== expectedVersion) {
    issues.push({
      code: 'instruction_block_version_mismatch',
      severity: 'error',
      message: `Repository instruction block version ${actualVersion ?? 'missing'} does not match catalog version ${expectedVersion}.`,
      details: { actualVersion, expectedVersion },
    });
  }
  const claudePath = path.join(workspace, 'CLAUDE.md');
  const claude = claudeImportState(exists(claudePath) ? readText(claudePath) : '');
  const claudeRequired = Boolean(projectConfig?.agentTargets?.includes('claude-code'));
  if (claude.status === 'duplicate') {
    issues.push({ code: 'duplicate_claude_import', severity: 'error', message: 'Root CLAUDE.md contains duplicate @AGENTS.md imports.' });
  } else if (projectConfig && claudeRequired && claude.status === 'absent') {
    issues.push({ code: 'claude_import_missing', severity: 'error', message: 'Claude Code is selected but root CLAUDE.md does not import @AGENTS.md.' });
  } else if (projectConfig && !claudeRequired && projectConfig.managedClaudeImport && claude.status === 'valid') {
    issues.push({ code: 'claude_import_ownership_drift', severity: 'warning', message: 'Guide-owned root @AGENTS.md import remains although Claude Code is not selected.' });
  }
  const status = issues.some((issue) => issue.severity === 'error')
    ? 'invalid'
    : block.status;
  return {
    ...block,
    status,
    blockStatus: block.status,
    bodyMatches,
    expectedVersion,
    actualVersion,
    claude: { ...claude, required: claudeRequired, path: claudePath },
    issues,
  };
}
