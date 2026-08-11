import fs from 'node:fs';
import path from 'node:path';
import {
  GuideError,
  PROJECT_SCHEMA_VERSION,
  atomicWrite,
  claudeImportState,
  compareOrdinalUtf8,
  digest,
  ensureClaudeImport,
  externalCommandTimeout,
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
  assertReleaseSelectorCompatibility,
  assertTaggedReleaseConstraints,
  canonicalSkillUrl,
  discoverChannelReleaseTag,
  loadCatalog,
  loadReleaseArtifacts,
  loadReleaseIndex,
  renderManagedInstructions,
  resolveProfileClosure,
  resolveRequiredSkillClosure,
  resolveRelease,
  resolveTaggedRelease,
  skillsCliSpec,
} from './guide-catalog.mjs';
import { assertProjectReferenceRelease, profileRepositoryIssues, workspaceDetection } from './guide-detect.mjs';
import {
  SOURCE_REPOSITORIES,
  assertSourceContractClean,
  normalizeSourceRepository,
  observeSourceBinding,
  resolveCachedSource,
  verifySourceBinding,
  verifyLocalSource,
} from './guide-source.mjs';
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
const APPLICATION_ARCHITECTURES = ['microservice', 'modular-monolith'];
const APPLY_TIME_SENTINEL = '<apply-time>';

function sourceRequirementIsRequired(requirement, capabilities) {
  return requirement?.requirement === 'required'
    || (requirement?.requirement === 'conditional' && capabilities.includes(requirement.condition));
}

export function applicationArchitectureSelection(profile, capabilities = []) {
  if (profile !== 'application') return { selected: null, issue: null };
  const selected = APPLICATION_ARCHITECTURES.filter((architecture) => capabilities.includes(architecture));
  if (selected.length === 1) return { selected: selected[0], issue: null };
  if (selected.length === 0) {
    return {
      selected: null,
      issue: {
        code: 'application_architecture_required',
        message: 'Choose exactly one application architecture with --capability microservice or --capability modular-monolith.',
        details: { choices: APPLICATION_ARCHITECTURES },
      },
    };
  }
  return {
    selected: null,
    issue: {
      code: 'application_architecture_conflict',
      message: 'Application architecture capabilities are mutually exclusive; select only microservice or modular-monolith.',
      details: { selected },
    },
  };
}

function normalizeAgents(values) {
  const agents = [...new Set((values || []).map(normalizeSkillsCliAgent))];
  if (agents.includes(null)) throw new GuideError('invalid_agent', 'Agent targets must be pinned npx skills identifiers such as codex, claude-code, or cursor.');
  return agents.sort(compareOrdinalUtf8);
}

function validateAgentTargets(agents, catalog, offline, timeoutMs, runner = run) {
  const executable = process.env.MONICA_GUIDE_NPX || 'npx';
  const cliSpec = skillsCliSpec(catalog);
  for (const agent of agents) {
    const result = runner(executable, [...(offline ? ['--offline'] : []), '--yes', cliSpec, 'ls', '-g', '-a', agent, '--json'], { timeout: timeoutMs });
    if (result.error?.code === 'ETIMEDOUT') {
      throw new GuideError(
        'skills_cli_timeout',
        `Pinned skills CLI timed out after ${timeoutMs} ms while validating global agent target ${agent}. Verify the local CLI/cache and retry; offline mode never fetches a substitute.`,
        { agent, timeoutMs },
      );
    }
    if (result.error || result.status !== 0) {
      throw new GuideError('agent_target_unavailable', `Pinned skills CLI could not query global agent target ${agent}.`, { agent, exitCode: result.status });
    }
    let payload;
    try { payload = JSON.parse(result.stdout); } catch (error) {
      throw new GuideError('agent_target_contract_invalid', `Pinned skills CLI returned invalid JSON for agent target ${agent}: ${error.message}`, { agent });
    }
    if (!Array.isArray(payload)) throw new GuideError('agent_target_contract_invalid', `Pinned skills CLI discovery for ${agent} must return a top-level array.`, { agent });
  }
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

function discoverInstalledManagedSkills(agents, catalog, cliSpec, offline, timeoutMs, runner = run) {
  const canonical = new Set(Object.entries(catalog.skills)
    .filter(([, entry]) => entry.ownership === 'monica' && entry.managed !== false)
    .map(([name]) => name));
  const discovered = new Set();
  const payloads = {};
  for (const agent of agents) {
    const executable = process.env.MONICA_GUIDE_NPX || 'npx';
    const result = runner(executable, [...(offline ? ['--offline'] : []), '--yes', cliSpec, 'ls', '-g', '-a', agent, '--json'], { timeout: timeoutMs });
    if (result.error?.code === 'ETIMEDOUT') {
      throw new GuideError(
        'skills_cli_timeout',
        `Pinned skills CLI timed out after ${timeoutMs} ms while discovering ${agent}. Verify the local CLI/cache and retry; offline mode never fetches a substitute.`,
        { agent, timeoutMs },
      );
    }
    if (result.status !== 0) throw new GuideError('global_skill_discovery_failed', `Cannot enumerate ${agent} global skills before switching or updating the one active Monica release.`);
    let payload;
    try { payload = JSON.parse(result.stdout); } catch { throw new GuideError('global_skill_discovery_contract_invalid', `${agent} returned invalid skills ls --json output.`); }
    if (!Array.isArray(payload)) throw new GuideError('global_skill_discovery_contract_invalid', `${agent} skills ls --json must return a top-level array.`);
    payloads[agent] = payload;
    for (const entry of payload) if (canonical.has(entry?.name)) discovered.add(entry.name);
  }
  return { skills: [...discovered].sort(compareOrdinalUtf8), payloads };
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

function sourceResolverPrerequisite(catalog, agents) {
  const name = catalog.sourcePolicies?.immutableBinding?.resolverSkill;
  const external = name ? catalog.externalSkills?.[name] : null;
  const distribution = external?.distribution;
  if (!name || !distribution) return null;
  const cliSpec = skillsCliSpec(catalog);
  const installArgs = ['--yes', cliSpec, 'add', distribution.immutableSkillUrl, '-g'];
  for (const agent of agents) installArgs.push('-a', agent);
  installArgs.push('-s', name, '-y');
  return {
    name,
    purpose: external.purpose,
    repository: distribution.repository,
    ref: distribution.ref,
    commit: distribution.commit,
    immutableSkillUrl: distribution.immutableSkillUrl,
    digest: distribution.digest,
    digestAlgorithm: distribution.digestAlgorithm,
    installCommand: shellDisplay('npx', installArgs),
    verificationCommands: agents.map((agent) => shellDisplay('npx', ['--yes', cliSpec, 'ls', '-g', '-a', agent, '--json'])),
    managedByGuide: false,
  };
}

function addSourceResolutionBlocker(plan, error, context, { offline = false } = {}) {
  const prerequisite = error.code === 'source_resolver_unavailable'
    ? sourceResolverPrerequisite(context.catalog, context.agents)
    : null;
  if (!prerequisite) {
    addBlocker(plan, error.code, error.message, error.details);
    return;
  }
  addBlocker(
    plan,
    offline ? 'offline_local_skill_source_required' : 'source_resolver_prerequisite_missing',
    offline
      ? 'Offline operation requires an already installed immutable source resolver and an exact verified local/cached Monica source.'
      : `Required external skill ${prerequisite.name} is unavailable; install and verify its pinned immutable distribution separately, then rerun this preview.`,
    {
      cause: { code: error.code, message: error.message, details: error.details },
      prerequisite,
      requiresSeparateApproval: true,
      networkRequired: !offline,
    },
  );
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

function assertActiveSourceBinding(binding, state, catalogInfo) {
  const observation = observeSourceBinding(binding);
  const activeSourceCommit = binding.repository === SOURCE_REPOSITORIES.monica
    && String(state.activeRelease?.id || '').startsWith('source:')
    ? state.activeRelease.commit?.toLowerCase() || null
    : null;
  if (!activeSourceCommit) return observation;
  if (binding.commit !== activeSourceCommit) {
    throw new GuideError(
      'active_source_binding_required',
      `Active global source release ${state.activeRelease.id} requires Monica binding ${activeSourceCommit}; preview an explicit global update/switch before binding another commit.`,
    );
  }
  if (observation.dirty) throw new GuideError('dirty_exact_source', 'The binding backing an active source release must be clean.', observation);
  if (state.activeRelease.catalogDigest && state.activeRelease.catalogDigest !== catalogInfo.catalogDigest) {
    throw new GuideError('source_catalog_mismatch', 'The bundled catalog does not match the active source release catalog; preview an explicit global update/switch first.');
  }
  assertSourceContractClean(binding.sourcePath);
  const boundCatalogPath = path.join(binding.sourcePath, '.monica', 'agent-skill-catalog.json');
  if (!exists(boundCatalogPath) || fileDigest(boundCatalogPath) !== state.activeRelease.catalogDigest) {
    throw new GuideError('source_catalog_mismatch', 'Proposed binding does not contain the catalog bytes recorded for the active source release.');
  }
  const manifest = buildSourceManifest(binding.sourcePath, catalogInfo.catalog);
  const mismatches = Object.entries(state.managedSkills)
    .filter(([skill, record]) => record.digest && manifest.skillDigests?.[skill] !== record.digest)
    .map(([skill]) => skill);
  if (mismatches.length) {
    throw new GuideError('source_manifest_mismatch', `Proposed binding does not contain the active source-release bytes for: ${mismatches.join(', ')}.`, { mismatches });
  }
  return observation;
}

function revalidateProposedSourceBinding(binding, options) {
  if (binding.provenance === 'local-git') {
    try {
      return verifyLocalSource(binding.sourcePath, {
        repository: binding.repository,
        exactRef: binding.ref,
      });
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      throw new GuideError(
        'source_binding_drift',
        `Proposed local ${binding.repository} binding changed after preview. Generate and approve a new plan.`,
        { proposed: binding, cause: { code: error.code, message: error.message, details: error.details } },
      );
    }
  }
  if (binding.provenance?.resolver === 'inspect-dependency-source') {
    return resolveCachedSource({
      exactRef: binding.ref,
      repository: binding.repository,
      resolverPath: options.sourceResolver,
      timeoutMs: externalCommandTimeout(options.timeoutMs),
    });
  }
  throw new GuideError('source_binding_drift', `Proposed ${binding.repository} binding has unsupported provenance. Generate and approve a new plan.`);
}

function buildGlobalSourcePlan(options) {
  const action = options.sourceAction;
  if (!['bind', 'unbind'].includes(action)) throw new GuideError('source_action_invalid', 'Mutating source operations must be bind or unbind.');
  const repository = normalizeSourceRepository(options.repository);
  const statePath = stateFilePath(options.state);
  const state = loadState(statePath);
  const catalogInfo = loadCatalog({ catalogPath: options.catalog, indexPath: options.index });
  const timeoutMs = externalCommandTimeout(options.timeoutMs);
  if (!Object.hasOwn(catalogInfo.catalog.sourceRepositories || {}, repository)) {
    throw new GuideError('source_repository_unavailable', `Catalog does not declare first-party source repository ${repository}.`);
  }
  const plan = {
    schemaVersion: 1,
    intent: 'source',
    sourceAction: action,
    scope: 'global',
    workspace: null,
    dryRun: true,
    context: {
      repository,
      activeGlobalRelease: comparableRelease(state.activeRelease),
      currentBinding: state.sourceBindings[repository] || null,
    },
    actions: [],
    warnings: [],
    blockers: [],
  };
  const unresolvedRepositories = Object.keys(state.sourceBindingCandidates).filter((candidate) => candidate !== repository);
  if (unresolvedRepositories.length) {
    addBlocker(
      plan,
      'source_binding_migration_required',
      `Resolve conflicting legacy bindings for ${unresolvedRepositories.join(', ')} before changing ${repository}.`,
      Object.fromEntries(unresolvedRepositories.map((candidate) => [candidate, state.sourceBindingCandidates[candidate]])),
    );
  }
  const nextState = structuredClone(state);
  if (action === 'bind') {
    let binding;
    try {
      if (options.sourcePath) {
        binding = verifyLocalSource(options.sourcePath, { repository, exactRef: options.sourceRef || null });
      } else {
        if (!options.sourceRef) throw new GuideError('source_ref_required', 'Cached source binding requires --source-ref <exact-ref>.');
        binding = resolveCachedSource({ exactRef: options.sourceRef, repository, resolverPath: options.sourceResolver, timeoutMs });
      }
      const observation = assertActiveSourceBinding(binding, state, catalogInfo);
      plan.context.proposedBinding = binding;
      plan.context.observation = observation;
      for (const warning of observation.warnings) addWarning(plan, warning.code, warning.message, observation);
      nextState.sourceBindings[repository] = binding;
      delete nextState.sourceBindingCandidates[repository];
    } catch (error) {
      if (!(error instanceof GuideError)) throw error;
      addBlocker(plan, error.code, error.message, error.details);
    }
  } else {
    if (repository === SOURCE_REPOSITORIES.monica && String(state.activeRelease?.id || '').startsWith('source:')) {
      addBlocker(
        plan,
        'active_source_binding_required',
        `Cannot unbind Monica while active global skills use ${state.activeRelease.id}; preview an explicit global update/switch first.`,
      );
    }
    if (!state.sourceBindings[repository] && !state.sourceBindingCandidates[repository]) {
      addWarning(plan, 'source_not_bound', `${repository} has no global source binding or migration candidates.`);
    }
    delete nextState.sourceBindings[repository];
    delete nextState.sourceBindingCandidates[repository];
  }
  if (!plan.blockers.length) {
    const before = exists(statePath) ? readText(statePath) : null;
    const after = stableJson(nextState, 2);
    const stateAction = fileAction(
      statePath,
      before,
      after,
      action === 'bind'
        ? `Bind verified ${repository} source globally as a lookup-only locator.`
        : `Remove the global ${repository} source binding and legacy migration candidates.`,
    );
    if (stateAction) plan.actions.push(stateAction);
  }
  plan.preconditions = {
    scope: 'global',
    statePath,
    stateDigest: fileDigest(statePath),
    catalogDigest: catalogInfo.catalogDigest,
    indexDigest: catalogInfo.indexDigest,
    localSkillSource: null,
    proposedSourceBinding: action === 'bind' ? plan.context.proposedBinding || null : null,
  };
  plan.planDigest = digest({ ...plan, planDigest: undefined });
  return plan;
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

function refreshStoredSource(binding, expectedCommit, { resolverPath = null, requireClean = false, timeoutMs = undefined } = {}) {
  if (!binding) return null;
  if (!expectedCommit || binding.commit.toLowerCase() !== expectedCommit.toLowerCase()) {
    throw new GuideError('stored_source_release_mismatch', `Stored source commit ${binding.commit || 'missing'} does not match required commit ${expectedCommit || 'missing'}.`);
  }
  const observation = verifySourceBinding(binding, { expectedCommit, resolverPath, timeoutMs });
  if (observation.compatibility !== 'compatible') throw new GuideError('stored_source_release_mismatch', `Stored source commit does not match required commit ${expectedCommit}.`, observation);
  if (requireClean && observation.dirty) throw new GuideError('dirty_exact_source', `Exact ${binding.repository} source contains local changes.`, observation);
  return binding;
}

function cliArguments(cliSpec, offline) {
  return [...(offline ? ['--offline'] : []), '--yes', cliSpec];
}

function installAction(skill, release, agents, cliSpec, { offline = false, localSourceRoot = null, catalog, timeoutMs } = {}) {
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
    timeoutMs,
    command: shellDisplay('npx', args),
    executable: 'npx',
    args,
  };
}

function removeAgentTargetsAction(skill, agents, cliSpec, offline, timeoutMs) {
  const args = [...cliArguments(cliSpec, offline), 'remove', skill, '-g'];
  for (const agent of agents) args.push('-a', agent);
  args.push('-y');
  return {
    type: 'remove-skill-targets',
    purpose: `Remove catalog-managed Monica skill ${skill} only from replaced global agent targets: ${agents.join(', ')}.`,
    skill,
    agents,
    cliSpec,
    offline,
    timeoutMs,
    executable: 'npx',
    args,
    command: shellDisplay('npx', args),
  };
}

function verificationAction(skills, agents, release, cliSpec, manifest, canonicalSkills, rejectUnexpected, offline, timeoutMs) {
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
    timeoutMs,
    manifest: selectedManifest,
    commands: agents.map((agent) => shellDisplay('npx', [...cliArguments(cliSpec, offline), 'ls', '-g', '-a', agent, '--json'])),
  };
}

function removedTargetsVerificationAction(skills, agents, cliSpec, offline, timeoutMs) {
  return {
    type: 'verify-removed-agent-targets',
    purpose: `Verify replaced global agent targets no longer discover catalog-managed Monica skills: ${agents.join(', ')}.`,
    skills,
    agents,
    cliSpec,
    offline,
    timeoutMs,
    commands: agents.map((agent) => shellDisplay('npx', [...cliArguments(cliSpec, offline), 'ls', '-g', '-a', agent, '--json'])),
  };
}

function transactionGuardAction(skills, agents, cliSpec, offline, installActions, restoreReference, timeoutMs) {
  const actionBySkill = new Map(installActions.map((action) => [action.skill, action]));
  const installSources = Object.fromEntries(skills.map((skill) => [skill, actionBySkill.get(skill)?.source || null]));
  const restoreSources = Object.fromEntries(skills.map((skill) => [
    skill,
    restoreReference ? canonicalSkillUrl(restoreReference, skill) : actionBySkill.get(skill)?.source || null,
  ]));
  return {
    type: 'protect-global-skills',
    purpose: 'Protect planned Monica skill and file mutations with a private snapshot and verified compensation.',
    skills,
    agents,
    cliSpec,
    offline,
    timeoutMs,
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
  recordWorkspaceObservation(nextState, context, 'contribute');
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

function recordWorkspaceObservation(state, context, intent) {
  state.observations[context.key] = {
    schemaVersion: 1,
    // The approved preview contains this sentinel. applyPlan materializes the
    // actual UTC timestamp under the state lock, after every precondition passes.
    observedAt: APPLY_TIME_SENTINEL,
    intent,
    repositoryIdentity: context.detection.repository.identity || null,
    candidateProfile: context.detection.repository.candidateProfile || null,
    selectedProfile: context.profile || context.project.config?.profile || null,
    channel: context.channel || context.project.config?.channel || null,
    capabilities: [...(context.capabilities || context.project.config?.capabilities || [])],
    frameworkVersion: context.detection.frameworkVersion.version || null,
    versionSource: context.detection.frameworkVersion.tier || null,
    targetRelease: context.release?.id || context.project.config?.expectedCatalogRelease?.id || null,
    detectionTruncated: Boolean(context.detection.repository.detectionScan?.truncated),
  };
}

async function prepareReleaseContext(context, plan, options) {
  if (context.detection.versionError) {
    addBlocker(plan, context.detection.versionError.code, context.detection.versionError.message, context.detection.versionError.details);
    return;
  }
  try {
    assertReleaseSelectorCompatibility({
      releaseTag: options.releaseTag,
      sourceRef: options.sourceRef,
      explicitChannel: options.channel,
      persistedChannel: context.project.config?.channel,
    });
  } catch (error) {
    if (!(error instanceof GuideError)) throw error;
    addBlocker(plan, error.code, error.message, error.details);
    return;
  }
  if (context.channel === 'source') {
    try {
      context.release = resolveRelease(context.index, {
        channel: 'source',
        frameworkVersion: context.detection.frameworkVersion.version,
        sourceRef: options.sourceRef || context.project.config?.expectedCatalogRelease?.commit,
      });
      let binding;
      if (options.sourcePath || context.profile === 'framework-contributor') {
        const selectedPath = options.sourcePath || context.workspace;
        const exact = verifyLocalSource(selectedPath, { repository: SOURCE_REPOSITORIES.monica, expectedCommit: context.release.commit });
        assertSourceContractClean(exact.sourcePath);
        binding = exact;
      } else if (context.state.sourceBindings[SOURCE_REPOSITORIES.monica]?.commit === context.release.commit) {
        binding = refreshStoredSource(context.state.sourceBindings[SOURCE_REPOSITORIES.monica], context.release.commit, {
          resolverPath: options.sourceResolver,
          requireClean: true,
          timeoutMs: context.timeoutMs,
        });
        assertSourceContractClean(binding.sourcePath);
      } else {
        binding = resolveCachedSource({ exactRef: context.release.commit, repository: SOURCE_REPOSITORIES.monica, resolverPath: options.sourceResolver, timeoutMs: context.timeoutMs });
      }
      const exactObservation = observeSourceBinding(binding, { expectedCommit: context.release.commit });
      if (exactObservation.dirty) throw new GuideError('dirty_exact_source', 'The source channel requires a clean exact Monica checkout.', exactObservation);
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
      addSourceResolutionBlocker(plan, error, context, { offline: Boolean(options.offline) });
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
    if (options.releaseTag) {
      context.release = resolveTaggedRelease(context.index, options.releaseTag);
      context.channel = context.release.channel;
      assertTaggedReleaseConstraints(context.release, {
        explicitChannel: options.channel,
        persistedChannel: context.project.config?.channel,
        expectedRelease: context.project.config?.expectedCatalogRelease,
        frameworkVersion: context.detection.frameworkVersion.version,
      });
    } else {
      context.release = resolveRelease(context.index, {
        channel: context.channel,
        frameworkVersion: context.detection.frameworkVersion.version,
        sourceRef: options.sourceRef,
      });
      context.channel = context.release.channel;
    }
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
  if (intent === 'source') return buildGlobalSourcePlan(options);
  if (options.skills?.length && intent !== 'update') throw new GuideError('targeted_skill_intent_invalid', '--skill is supported only by update.');
  if (options.nestedInstructions?.length && !['init', 'configure', 'update'].includes(intent)) {
    throw new GuideError('nested_instruction_intent_invalid', '--nested-instruction is supported only by init, configure, and update.');
  }
  const workspace = normalizePath(options.workspace || process.cwd());
  if (!exists(workspace) || !fs.statSync(workspace).isDirectory()) throw new GuideError('workspace_unavailable', `Workspace is not a directory: ${workspace}.`);
  const statePath = stateFilePath(options.state);
  const state = loadState(statePath);
  const project = loadProjectConfig(workspace);
  const catalogInfo = loadCatalog({ catalogPath: options.catalog, indexPath: options.index });
  const timeoutMs = externalCommandTimeout(options.timeoutMs);
  let catalog = catalogInfo.catalog;
  const detection = workspaceDetection(workspace);
  const key = workspaceKey(workspace, detection.repository.identity);
  const context = {
    workspace, statePath, state, project, catalogInfo, catalog, index: catalogInfo.index, detection, key,
    release: null, releaseIndex: null, releaseArtifacts: null, installManifest: null, catalogDigest: catalogInfo.catalogDigest, sourceBinding: null, intent, timeoutMs,
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
  if (project.migration) {
    addWarning(plan, 'project_config_migration_pending', project.migration.message, project.migration);
    plan.context.projectConfigMigration = project.migration;
  }
  if (Object.keys(state.sourceBindingCandidates).length) {
    addBlocker(
      plan,
      'source_binding_migration_required',
      'Conflicting legacy workspace source bindings require explicit global source bind or unbind decisions before other mutations.',
      state.sourceBindingCandidates,
    );
  }
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
    if (profile === 'extension-author' && (detection.repository.characteristics.projectReference || detection.frameworkVersion.entries.some((entry) => entry.origin === 'ProjectReference'))) {
      addBlocker(plan, 'extension_project_reference_forbidden', 'Extension projects must consume Monica through immutable NuGet packages; a Monica ProjectReference was detected. Bind exact Monica source separately as a lookup locator; the binding grants no write permission.');
    }
    for (const issue of profileRepositoryIssues(workspace, profile, detection.repository)) addBlocker(plan, issue.code, issue.message, issue.details);
    if (profile && detection.repository.candidateProfile && profile !== detection.repository.candidateProfile) {
      addWarning(plan, 'profile_differs_from_detection', `Confirmed profile ${profile} differs from detected candidate ${detection.repository.candidateProfile}.`);
    }
    context.profile = profile;
    context.channel = options.channel || project.config?.channel || semverChannel(detection.frameworkVersion.version) || 'stable';
    context.capabilities = [...new Set(options.capabilities?.length ? options.capabilities : (project.config?.capabilities || detection.repository.capabilities || []))].sort();
    context.applicationArchitecture = applicationArchitectureSelection(profile, context.capabilities);
    if (context.applicationArchitecture.issue) {
      const issue = context.applicationArchitecture.issue;
      addBlocker(plan, issue.code, issue.message, issue.details);
    }
    context.agentTargetsExplicit = Boolean(options.agentTargetsExplicit || options.agents?.length);
    context.agents = normalizeAgents(context.agentTargetsExplicit ? options.agents : state.agentTargets);
    if (!context.agents.length) addBlocker(plan, 'agent_target_required', 'Select at least one pinned npx skills agent target with --agent.');
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
    plan.context.capabilities = context.capabilities;
    plan.context.applicationArchitecture = context.applicationArchitecture.selected;
    plan.context.agentTargets = context.agents;
    plan.context.agentTargetsExplicit = context.agentTargetsExplicit;
    plan.context.requestedSkills = context.targetSkills;
    await prepareReleaseContext(context, plan, options);
    if (context.agents.length && context.catalog) {
      try {
        validateAgentTargets(context.agents, context.catalog, Boolean(options.offline), context.timeoutMs, options.agentValidationRunner || run);
      } catch (error) {
        if (!(error instanceof GuideError)) throw error;
        addBlocker(plan, error.code, error.message, error.details);
      }
    }
    plan.context.channel = context.channel;
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
    let sourceBinding = context.sourceBinding;
    if (context.closure && context.release && context.installManifest) {
      const monicaRequirement = context.closure.sourceRequirements.find((entry) => entry.repository === SOURCE_REPOSITORIES.monica) || null;
      const monicaExpectedCommit = monicaRequirement?.compatibility === 'workspace-commit'
        ? detection.repository.git?.commit?.toLowerCase() || null
        : context.release.commit;
      const existingBinding = state.sourceBindings[SOURCE_REPOSITORIES.monica];
      let refreshedStoredSource = null;
      if (existingBinding && !options.sourcePath && !sourceBinding) {
        try {
          refreshedStoredSource = refreshStoredSource(existingBinding, monicaExpectedCommit, {
            resolverPath: options.sourceResolver,
            requireClean: sourceRequirementIsRequired(monicaRequirement, context.capabilities) || Boolean(options.offline) || context.channel === 'source',
            timeoutMs: context.timeoutMs,
          });
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          addWarning(plan, 'stored_source_invalid', `Stored global source binding cannot satisfy this exact workflow and remains available only as a lookup locator: ${error.message}`, error.details);
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
            sourceBinding = verifyLocalSource(projectReferenceSources[0], { repository: SOURCE_REPOSITORIES.monica, expectedCommit: monicaExpectedCommit });
          } catch (error) {
            if (!(error instanceof GuideError)) throw error;
            addBlocker(plan, error.code, error.message, error.details);
          }
        }
      } else if (options.offline && !options.sourcePath && profile !== 'framework-contributor') {
        try {
          sourceBinding = resolveCachedSource({ exactRef: context.release.commit, repository: SOURCE_REPOSITORIES.monica, resolverPath: options.sourceResolver, timeoutMs: context.timeoutMs });
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          if (error.code === 'source_resolver_unavailable') addSourceResolutionBlocker(plan, error, context, { offline: true });
          else addBlocker(
            plan,
            'offline_local_skill_source_required',
            'Offline operation requires an exact verified local/cached Monica source whose managed skills match the selected release.',
            { cause: { code: error.code, message: error.message, details: error.details } },
          );
        }
      } else if (context.closure.sourceRequirements.some((entry) => entry.repository === SOURCE_REPOSITORIES.monica && sourceRequirementIsRequired(entry, context.capabilities)) || options.sourcePath) {
        try {
          sourceBinding = profile === 'framework-contributor' && !options.sourcePath
            ? verifyLocalSource(workspace, { repository: SOURCE_REPOSITORIES.monica, expectedCommit: monicaExpectedCommit })
            : options.sourcePath
              ? verifyLocalSource(options.sourcePath, { repository: SOURCE_REPOSITORIES.monica, expectedCommit: monicaExpectedCommit })
              : resolveCachedSource({ exactRef: monicaExpectedCommit, repository: SOURCE_REPOSITORIES.monica, resolverPath: options.sourceResolver, timeoutMs: context.timeoutMs });
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          addSourceResolutionBlocker(plan, error, context);
        }
      } else if (context.closure.sourceRequirements.some((entry) => entry.repository === SOURCE_REPOSITORIES.monica && entry.requirement === 'conditional' && !sourceRequirementIsRequired(entry, context.capabilities))) {
        addWarning(plan, 'source_available_on_demand', 'A global Monica source binding can be added later when work depends on framework internals.');
      }
      const cleanMonicaSourceRequired = sourceRequirementIsRequired(monicaRequirement, context.capabilities) || Boolean(options.offline) || context.channel === 'source';
      if (sourceBinding && cleanMonicaSourceRequired) {
        const observation = observeSourceBinding(sourceBinding, { expectedCommit: monicaExpectedCommit });
        if (observation.dirty) addBlocker(plan, 'dirty_exact_source', 'This exact operation requires a clean Monica source checkout.', observation);
      }
      for (const requirement of context.closure.sourceRequirements.filter((entry) => sourceRequirementIsRequired(entry, context.capabilities) && entry.repository !== SOURCE_REPOSITORIES.monica)) {
        const binding = state.sourceBindings[requirement.repository];
        const expectedCommit = requirement.compatibility === 'workspace-commit' ? detection.repository.git?.commit?.toLowerCase() || null : context.release.commit;
        if (requirement.compatibility === 'workspace-commit' && !expectedCommit) {
          addBlocker(
            plan,
            'source_commit_unresolved',
            `Cannot identify the exact ${requirement.repository} workspace HEAD required by ${profile}.`,
            { repository: requirement.repository },
          );
          continue;
        }
        if (!binding) {
          addBlocker(plan, 'source_binding_required', `${profile} requires a global ${requirement.repository} source binding.`, { repository: requirement.repository, expectedCommit });
          continue;
        }
        try {
          const observation = verifySourceBinding(binding, { expectedCommit, resolverPath: options.sourceResolver, timeoutMs: context.timeoutMs });
          if (observation.compatibility === 'mismatch') {
            addBlocker(plan, 'source_binding_incompatible', `Global ${requirement.repository} source binding cannot satisfy this exact workflow.`, observation);
          } else if (observation.dirty) {
            addBlocker(plan, 'dirty_exact_source', `${profile} requires a clean exact ${requirement.repository} source checkout.`, observation);
          }
        } catch (error) {
          if (!(error instanceof GuideError)) throw error;
          addBlocker(plan, error.code, error.message, error.details);
        }
      }
    }
    const requiresLocalSkillSource = intent !== 'source'
      && Boolean(context.installManifest)
      && (Boolean(options.offline) || context.channel === 'source');
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
    } else if (intent !== 'source' && context.release && context.installManifest && context.channel !== 'source') {
      plan.context.installationSource = { type: 'immutable-tag', ref: context.release?.installRef || null, offline: false };
    }
    if (context.closure?.external.length) addWarning(plan, 'external_skills_unmanaged', `External skills are diagnosed but never installed or updated by Monica Guide: ${context.closure.external.join(', ')}.`);

    if (context.release && context.closure && plan.blockers.length === 0) {
      const cliSpec = skillsCliSpec(catalog);
      const previousAgents = [...state.agentTargets];
      const desiredAgents = [...context.agents];
      const affectedAgents = [...new Set([...previousAgents, ...desiredAgents])].sort(compareOrdinalUtf8);
      const addedAgents = desiredAgents.filter((agent) => !previousAgents.includes(agent));
      const removedAgents = previousAgents.filter((agent) => !desiredAgents.includes(agent));
      const agentTargetsChanged = !sameStringSet(previousAgents, desiredAgents);
      plan.context.previousAgentTargets = previousAgents;
      plan.context.desiredAgentTargets = desiredAgents;
      plan.context.addedAgentTargets = addedAgents;
      plan.context.removedAgentTargets = removedAgents;
      if (context.agentTargetsExplicit && agentTargetsChanged) {
        addWarning(plan, 'global_agent_targets_replaced', `The explicit --agent selection replaces the global target set; added: ${addedAgents.join(', ') || 'none'}, removed: ${removedAgents.join(', ') || 'none'}.`);
      }
      let installationDiscovery = { skills: [], payloads: {} };
      const enforceGlobalInventory = intent === 'update' || agentTargetsChanged || Boolean(state.activeRelease && state.activeRelease.id !== context.release.id);
      if (enforceGlobalInventory) {
        try {
          installationDiscovery = discoverInstalledManagedSkills(
            affectedAgents,
            catalog,
            cliSpec,
            Boolean(options.offline),
            context.timeoutMs,
            options.agentValidationRunner || run,
          );
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
          ? installedSkillDrift(managedSkills, desiredAgents, installationDiscovery.payloads, context.installManifest, Boolean(options.offline))
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
        const installActions = installSkills.map((skill) => installAction(skill, context.release, desiredAgents, cliSpec, {
          offline: Boolean(options.offline),
          localSourceRoot: context.localSkillSource?.path || null,
          catalog,
          timeoutMs: context.timeoutMs,
        }));
        const removalSkills = removedAgents.length ? managedSkills : [];
        const removalActions = removalSkills.map((skill) => removeAgentTargetsAction(
          skill,
          removedAgents,
          cliSpec,
          Boolean(options.offline),
          context.timeoutMs,
        ));
        const protectedSkills = [...new Set([...installSkills, ...removalSkills])].sort(compareOrdinalUtf8);
        const restoreReference = state.activeRelease?.tag || state.activeRelease?.commit || context.release.installRef;
        plan.actions.push(transactionGuardAction(
          protectedSkills,
          affectedAgents,
          cliSpec,
          Boolean(options.offline),
          installActions,
          restoreReference,
          context.timeoutMs,
        ));
        plan.actions.push(...installActions);
        plan.actions.push(...removalActions);
        const canonicalSkills = Object.entries(catalog.skills)
          .filter(([, entry]) => entry.ownership === 'monica' && entry.managed !== false)
          .map(([name]) => name)
          .sort();
        plan.actions.push(verificationAction(managedSkills, desiredAgents, context.release, cliSpec, context.installManifest, canonicalSkills, enforceGlobalInventory, Boolean(options.offline), context.timeoutMs));
        if (removedAgents.length) {
          plan.actions.push(removedTargetsVerificationAction(managedSkills, removedAgents, cliSpec, Boolean(options.offline), context.timeoutMs));
        }
      }
      const nextState = structuredClone(state);
      if (intent !== 'source') {
        nextState.activeRelease = comparableRelease(context.release);
        nextState.managedSkills = Object.fromEntries(skillChanges.map((entry) => [entry.name, entry.target]));
        nextState.agentTargets = desiredAgents;
      }
      nextState.workspacePreferences[key] = {
        workspace,
        repository: detection.repository.identity,
        profile,
        channel: context.channel,
        capabilities: context.capabilities,
      };
      recordWorkspaceObservation(nextState, context, intent);
      if (sourceBinding) nextState.sourceBindings[SOURCE_REPOSITORIES.monica] = sourceBinding;
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
  runChecked(executable, action.args, {
    code: 'skill_install_failed',
    timeout: action.timeoutMs,
    timeoutCode: 'skills_cli_timeout',
    timeoutMessage: `Pinned skills CLI timed out after ${action.timeoutMs} ms while installing ${action.skill}. Verify the local CLI/cache and retry; offline mode never fetches a substitute.`,
  });
}

function applyTargetRemoval(action) {
  const executable = process.env.MONICA_GUIDE_NPX || action.executable;
  runChecked(executable, action.args, {
    code: 'skill_target_remove_failed',
    timeout: action.timeoutMs,
    timeoutCode: 'skills_cli_timeout',
    timeoutMessage: `Pinned skills CLI timed out after ${action.timeoutMs} ms while removing ${action.skill} from ${action.agents.join(', ')}. Verify the local CLI/cache and retry.`,
  });
}

function applyFileAction(action) {
  if (action.type === 'write-file') atomicWrite(action.path, action.content, action.mode);
  else if (action.type === 'delete-file' && exists(action.path)) fs.unlinkSync(action.path);
  else throw new GuideError('unknown_action', `Unknown plan action type: ${action.type}.`);
}

function applyVerification(action) {
  const executable = process.env.MONICA_GUIDE_NPX || 'npx';
  for (const agent of action.agents) {
    const result = runChecked(executable, [...cliArguments(action.cliSpec, action.offline), 'ls', '-g', '-a', agent, '--json'], {
      code: 'skill_discovery_failed',
      timeout: action.timeoutMs,
      timeoutCode: 'skills_cli_timeout',
      timeoutMessage: `Pinned skills CLI timed out after ${action.timeoutMs} ms while verifying ${agent}. Verify the local CLI/cache and retry; offline mode never fetches a substitute.`,
    });
    let payload;
    try { payload = JSON.parse(result.stdout); } catch (error) { throw new GuideError('skill_discovery_contract_invalid', `${agent} global skills discovery returned invalid JSON: ${error.message}`); }
    if (!Array.isArray(payload)) throw new GuideError('skill_discovery_contract_invalid', `${agent} global skills discovery must return a top-level array.`);
    if (action.rejectUnexpected) {
      const canonical = new Set(action.canonicalSkills);
      const expected = new Set(action.skills);
      const extras = payload.map((entry) => entry?.name).filter((name) => canonical.has(name) && !expected.has(name)).sort();
      if (extras.length) throw new GuideError('global_skill_set_drift', `${agent} discovery gained catalog-managed Monica skills after preview: ${extras.join(', ')}. Generate a new update preview so they can be adopted at the active release.`, { agent, extras });
    }
    verifyDiscoveryPayload(payload, action.skills, action.manifest);
    for (const skill of action.skills) {
      const entry = payload.find((candidate) => candidate?.name === skill);
      if (!Array.isArray(entry?.agents)) throw new GuideError('skill_discovery_contract_invalid', `${skill} discovery has no agent membership list.`);
      const actualAgents = new Set(entry.agents.map(normalizeSkillsCliAgent).filter(Boolean));
      if (!actualAgents.has(agent)) {
        throw new GuideError('skill_agent_membership_mismatch', `${skill} is not discovered for planned agent target ${agent}.`, {
          skill,
          expectedAgent: agent,
          actualAgents: [...actualAgents].sort(),
        });
      }
    }
  }
}

function applyRemovedTargetsVerification(action) {
  const executable = process.env.MONICA_GUIDE_NPX || 'npx';
  for (const agent of action.agents) {
    const result = runChecked(executable, [...cliArguments(action.cliSpec, action.offline), 'ls', '-g', '-a', agent, '--json'], {
      code: 'skill_discovery_failed',
      timeout: action.timeoutMs,
      timeoutCode: 'skills_cli_timeout',
      timeoutMessage: `Pinned skills CLI timed out after ${action.timeoutMs} ms while verifying removed target ${agent}. Verify the local CLI/cache and retry.`,
    });
    let payload;
    try { payload = JSON.parse(result.stdout); } catch (error) {
      throw new GuideError('skill_discovery_contract_invalid', `${agent} global skills discovery returned invalid JSON: ${error.message}`);
    }
    if (!Array.isArray(payload)) throw new GuideError('skill_discovery_contract_invalid', `${agent} global skills discovery must return a top-level array.`);
    const remaining = action.skills.filter((skill) => payload.some((entry) => entry?.name === skill
      && (!Array.isArray(entry.agents) || entry.agents.map(normalizeSkillsCliAgent).includes(agent))));
    if (remaining.length) {
      throw new GuideError(
        'skill_target_remove_incomplete',
        `Removed global agent target ${agent} still discovers managed Monica skills: ${remaining.join(', ')}.`,
        { agent, remaining },
      );
    }
  }
}

function materializeApplyTime(plan, statePath, observedAt = new Date().toISOString()) {
  const materialized = structuredClone(plan);
  for (const action of materialized.actions.filter((entry) => entry.type === 'write-file' && path.resolve(entry.path) === path.resolve(statePath))) {
    let payload;
    try { payload = JSON.parse(action.content); } catch { continue; }
    let changed = false;
    for (const observation of Object.values(payload.observations || {})) {
      if (observation?.observedAt === APPLY_TIME_SENTINEL) {
        observation.observedAt = observedAt;
        changed = true;
      }
    }
    if (!changed) continue;
    action.content = stableJson(payload, 2);
    action.afterDigest = digest(action.content);
    action.diff = textDiff(action.path, exists(action.path) ? readText(action.path) : null, action.content);
  }
  return materialized;
}

export async function applyPlan(intent, options) {
  if (!options.planDigest) throw new GuideError('plan_digest_required', '--apply requires --plan-digest from the approved dry run.');
  const statePath = stateFilePath(options.state);
  return withFileLock(`${statePath}.lock`, () => {
    // Returning a promise from the lock callback would release too early, so the
    // caller supplies a precomputed plan and all recomputation is synchronous
    // except immutable-index acquisition, which occurred before taking the lock.
    const approvedPlan = options.precomputedPlan;
    if (!approvedPlan) throw new GuideError('plan_missing', 'Internal error: apply requires a precomputed plan.');
    if (approvedPlan.planDigest !== options.planDigest) throw new GuideError('plan_digest_mismatch', `Plan changed: expected ${options.planDigest}, current ${approvedPlan.planDigest}.`);
    if (approvedPlan.blockers.length) throw new GuideError('plan_blocked', 'The approved plan contains blockers and cannot be applied.', approvedPlan.blockers);
    const retainedTransactions = retainedGlobalSkillTransactions(statePath);
    if (retainedTransactions.length) {
      throw new GuideError(
        'global_skill_recovery_required',
        'A retained global-skill transaction must be diagnosed and resolved before another Monica Guide mutation.',
        { transactions: retainedTransactions },
      );
    }
    const currentCatalog = loadCatalog({ catalogPath: options.catalog, indexPath: options.index });
    if (approvedPlan.preconditions.scope === 'global') {
      if (fileDigest(statePath) !== approvedPlan.preconditions.stateDigest
        || currentCatalog.catalogDigest !== approvedPlan.preconditions.catalogDigest
        || currentCatalog.indexDigest !== approvedPlan.preconditions.indexDigest) {
        throw new GuideError('global_state_drift', 'User state, catalog, or index changed after plan computation. Generate and approve a new preview.');
      }
      const proposed = approvedPlan.preconditions.proposedSourceBinding;
      if (proposed) {
        const refreshed = revalidateProposedSourceBinding(proposed, options);
        if (stableJson(refreshed) !== stableJson(proposed)) {
          throw new GuideError(
            'source_binding_drift',
            `Proposed ${proposed.repository} binding changed after preview. Generate and approve a new plan.`,
            { proposed, observed: refreshed },
          );
        }
        assertActiveSourceBinding(refreshed, loadState(statePath), currentCatalog);
      }
    } else {
      const currentFingerprint = workspaceFingerprint(approvedPlan.workspace, statePath, currentCatalog);
      if (currentFingerprint !== approvedPlan.preconditions.fingerprint) {
        throw new GuideError('workspace_drift', 'Workspace, catalog, index, or user state changed after plan computation. Generate and approve a new preview.');
      }
    }
    const plan = materializeApplyTime(approvedPlan, statePath);
    for (const action of plan.actions.filter((entry) => entry.type === 'write-file' || entry.type === 'delete-file')) {
      const currentDigest = fileDigest(action.path);
      if (currentDigest !== action.beforeDigest) throw new GuideError('file_drift', `Planned file changed before apply: ${action.path}.`);
    }
    if (plan.preconditions.localSkillSource) {
      const source = plan.preconditions.localSkillSource;
      const observation = verifySourceBinding(source.binding, { expectedCommit: source.binding.commit, resolverPath: options.sourceResolver, timeoutMs: externalCommandTimeout(options.timeoutMs) });
      if (observation.dirty) throw new GuideError('source_binding_drift', 'Exact local Monica source became dirty after preview. Generate a new plan.', observation);
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
    const removalActions = plan.actions.filter((action) => action.type === 'remove-skill-targets');
    const verificationActions = plan.actions.filter((action) => action.type === 'verify-skills');
    const removedTargetVerificationActions = plan.actions.filter((action) => action.type === 'verify-removed-agent-targets');
    const fileActions = plan.actions.filter((action) => action.type === 'write-file' || action.type === 'delete-file');
    const knownActionTypes = new Set(['protect-global-skills', 'install-skill', 'remove-skill-targets', 'verify-skills', 'verify-removed-agent-targets', 'write-file', 'delete-file']);
    const unknownAction = plan.actions.find((action) => !knownActionTypes.has(action.type));
    if (unknownAction) throw new GuideError('unknown_action', `Unknown plan action type: ${unknownAction.type}.`);
    if ((installActions.length || removalActions.length || verificationActions.length || removedTargetVerificationActions.length) && !guard) {
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
        timeoutMs: guard.timeoutMs,
        mutate: ({ runSkill, runFile }) => {
          for (const action of installActions) runSkill(action.skill, () => applyInstall(action));
          for (const action of removalActions) runSkill(action.skill, () => applyTargetRemoval(action));
          for (const action of verificationActions) applyVerification(action);
          for (const action of removedTargetVerificationActions) applyRemovedTargetsVerification(action);
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

export function instructionDiagnostics(workspace, catalog, projectConfig = null, agentTargets = []) {
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
  const claudeRequired = agentTargets.includes('claude-code');
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
