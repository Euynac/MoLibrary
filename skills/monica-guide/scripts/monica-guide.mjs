#!/usr/bin/env node

import { GuideError, externalCommandTimeout, formatManagedSkillVersion, stableJson } from './guide-shared.mjs';
import { applyPlan, buildPlan } from './guide-plan.mjs';
import {
  doctor,
  doctorGlobal,
  inspectEnvironment,
  inspectGlobalEnvironment,
  inspectGlobalInstalledSkillHealth,
  listSourceBindings,
  resolveSourceBinding,
} from './guide-doctor.mjs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const VALUE_OPTIONS = new Map([
  ['--workspace', 'workspace'],
  ['--profile', 'profile'],
  ['--channel', 'channel'],
  ['--catalog', 'catalog'],
  ['--index', 'index'],
  ['--state', 'state'],
  ['--release-tag', 'releaseTag'],
  ['--release-index', 'releaseIndex'],
  ['--release-index-url', 'releaseIndexUrl'],
  ['--release-catalog', 'releaseCatalog'],
  ['--release-manifest', 'releaseManifest'],
  ['--source-path', 'sourcePath'],
  ['--source-ref', 'sourceRef'],
  ['--source-resolver', 'sourceResolver'],
  ['--repository', 'repository'],
  ['--preference', 'preference'],
  ['--plan-digest', 'planDigest'],
  ['--timeout-ms', 'timeoutMs'],
]);
const REPEAT_OPTIONS = new Map([
  ['--capability', 'capabilities'],
  ['--agent', 'agents'],
  ['--skill', 'skills'],
  ['--nested-instruction', 'nestedInstructions'],
]);
const BOOLEAN_OPTIONS = new Map([
  ['--apply', 'apply'],
  ['--switch-global', 'switchGlobal'],
  ['--offline', 'offline'],
  ['--json', 'json'],
  ['--help', 'help'],
]);
const INTENTS = new Set(['overview', 'init', 'status', 'doctor', 'update', 'configure', 'source', 'contribute', 'forget']);
const SOURCE_ACTIONS = new Set(['list', 'resolve', 'bind', 'unbind']);
const LOCAL_PATH_PRIVACY_NOTICE = 'Keep this output local: source bindings and workspace diagnostics may contain private filesystem paths.';
const DOMAIN_BLOCKER_ERROR_CODES = new Set([
  'dirty_exact_source',
  'dirty_project_reference_source',
  'dirty_source_contract',
  'file_drift',
  'global_release_conflict',
  'global_skill_recovery_required',
  'global_state_drift',
  'mixed_framework_versions',
  'offline_local_skill_source_required',
  'offline_release_artifacts_unavailable',
  'offline_release_index_unavailable',
  'package_version_unresolved',
  'plan_blocked',
  'plan_digest_mismatch',
  'project_reference_source_identity_mismatch',
  'project_reference_source_unverified',
  'project_reference_version_unresolved',
  'release_channel_mismatch',
  'release_selector_conflict',
  'release_version_mismatch',
  'source_binding_drift',
  'source_binding_unavailable',
  'source_commit_mismatch',
  'source_drift',
  'source_identity_mismatch',
  'source_provenance_unverified',
  'state_locked',
  'stored_source_release_mismatch',
  'version_property_unresolved',
  'version_range_unsupported',
  'version_unpublished',
  'version_unresolved',
  'workspace_drift',
]);

export function exitCodeForResult(result) {
  if (result?.blockers?.length) return 3;
  if (result?.severity === 'error' || ['error', 'unhealthy', 'blocked', 'migration-required'].includes(result?.status)) return 3;
  if (result?.severity === 'warning' || result?.status === 'warning') return 1;
  return 0;
}

export function exitCodeForError(error) {
  return error instanceof GuideError && DOMAIN_BLOCKER_ERROR_CODES.has(error.code) ? 3 : 2;
}

export function parseArguments(argv) {
  const options = { capabilities: [], agents: [], agentTargetsExplicit: false, skills: [], nestedInstructions: [] };
  let intent = null;
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (!argument.startsWith('-') && !intent) {
      intent = argument;
      continue;
    }
    if (!argument.startsWith('-') && intent === 'source' && !options.sourceAction) {
      options.sourceAction = argument;
      continue;
    }
    if (VALUE_OPTIONS.has(argument)) {
      const value = argv[++index];
      if (!value || value.startsWith('--')) throw new GuideError('missing_option_value', `${argument} requires a value.`);
      options[VALUE_OPTIONS.get(argument)] = value;
    } else if (REPEAT_OPTIONS.has(argument)) {
      const value = argv[++index];
      if (!value || value.startsWith('--')) throw new GuideError('missing_option_value', `${argument} requires a value.`);
      options[REPEAT_OPTIONS.get(argument)].push(value);
      if (argument === '--agent') options.agentTargetsExplicit = true;
    } else if (BOOLEAN_OPTIONS.has(argument)) {
      options[BOOLEAN_OPTIONS.get(argument)] = true;
    } else {
      throw new GuideError('unknown_option', `Unknown option: ${argument}.`);
    }
  }
  if (options.help && !intent) return { intent: 'help', options };
  if (!intent) intent = 'overview';
  if (!INTENTS.has(intent)) throw new GuideError('invalid_intent', `Intent must be one of: ${[...INTENTS].join(', ')}.`);
  if (options.timeoutMs !== undefined) options.timeoutMs = externalCommandTimeout(options.timeoutMs);
  const sourceReadOnly = intent === 'source' && ['list', 'resolve'].includes(options.sourceAction);
  const readOnly = ['overview', 'status', 'doctor'].includes(intent) || sourceReadOnly;
  if (readOnly && (options.apply || options.planDigest)) {
    throw new GuideError('readonly_apply_invalid', `${intent}${intent === 'source' ? ` ${options.sourceAction}` : ''} is read-only and does not accept --apply or --plan-digest.`);
  }
  const workspaceIgnored = intent === 'overview'
    || (intent === 'source' && ['list', 'bind', 'unbind'].includes(options.sourceAction));
  if (workspaceIgnored && options.workspace) {
    throw new GuideError('workspace_option_invalid', `${intent}${intent === 'source' ? ` ${options.sourceAction}` : ''} does not accept --workspace.`);
  }
  if (options.apply && !options.planDigest) throw new GuideError('plan_digest_required', '--apply requires --plan-digest <digest>.');
  if (options.planDigest && !options.apply) throw new GuideError('apply_required', '--plan-digest is accepted only with --apply.');
  if (options.skills.length && intent !== 'update') throw new GuideError('targeted_skill_intent_invalid', '--skill is accepted only by update.');
  if (intent === 'source') {
    if (!SOURCE_ACTIONS.has(options.sourceAction)) throw new GuideError('source_action_invalid', 'Source action must be list, resolve, bind, or unbind.');
    if (options.sourceAction !== 'list' && !options.repository) throw new GuideError('source_repository_required', 'Source operations other than list require --repository monica|docs.');
  }
  return { intent, options };
}

function renderPlan(plan) {
  const lines = [
    `${plan.dryRun ? 'PREVIEW' : 'APPLIED'} ${plan.intent}${plan.sourceAction ? ` ${plan.sourceAction}` : ''} for ${plan.workspace || 'global user state'}`,
    `planDigest: ${plan.planDigest}`,
    `Actions: ${plan.actions.length}; warnings: ${plan.warnings.length}; blockers: ${plan.blockers.length}`,
  ];
  if (plan.blockers.length) {
    lines.push('', 'Blockers:');
    for (const blocker of plan.blockers) lines.push(`  ERROR ${blocker.code}: ${blocker.message}`);
  }
  if (plan.warnings.length) {
    lines.push('', 'Warnings:');
    for (const warning of plan.warnings) lines.push(`  WARN ${warning.code}: ${warning.message}`);
  }
  if (plan.actions.length) {
    lines.push('', 'Actions:');
    plan.actions.forEach((action, index) => {
      lines.push(`  ${index + 1}. [${action.type}] ${action.purpose}`);
      if (action.command) lines.push(`     ${action.command}`);
      if (action.commands) for (const command of action.commands) lines.push(`     ${command}`);
      if (action.diff) lines.push('', action.diff);
    });
  }
  if (plan.context?.skillChanges?.length) {
    lines.push('', 'Managed skills:');
    for (const entry of plan.context.skillChanges) {
      const installed = formatManagedSkillVersion(entry.installed, plan.context.activeGlobalRelease);
      const target = formatManagedSkillVersion(entry.target, plan.context.targetRelease);
      const selected = plan.context.installSkills?.includes(entry.name) ? ' (install)' : '';
      lines.push(`  ${entry.name.padEnd(42)} ${installed.padEnd(12)} -> ${target.padEnd(18)} ${entry.changeState}${selected}`);
    }
  }
  if (plan.route) lines.push('', `Route: $${plan.route.skill} (${plan.route.preference}); remote mutation authorized: no.`);
  if (plan.dryRun && !plan.blockers.length) lines.push('', `Apply unchanged plan with --apply --plan-digest ${plan.planDigest}`);
  return `${lines.join('\n')}\n`;
}

export function statusEnvelope(environment) {
  const selectedPreference = environment.state.workspacePreferences[environment.key] || null;
  const instructionError = environment.instructionState.issues.find((issue) => issue.severity === 'error') || null;
  const dirtyUnconfirmedReference = !environment.profileConfirmed
    && environment.versionError?.code === 'dirty_project_reference_source';
  const versionError = dirtyUnconfirmedReference ? null : environment.versionError;
  const releaseError = dirtyUnconfirmedReference
    && environment.releaseError?.code === environment.versionError?.code
    ? null
    : environment.releaseError;
  const warnings = dirtyUnconfirmedReference ? [environment.versionError] : [];
  const bindingWarnings = Object.values(environment.sourceBindingDiagnostics || {})
    .flatMap((entry) => entry.observation?.warnings || []);
  const projectReleaseError = environment.projectConfig?.expectedCatalogRelease && environment.targetRelease
    && environment.projectConfig.expectedCatalogRelease.id !== environment.targetRelease.id
    ? {
      code: 'project_release_conflict',
      message: `Repository expects ${environment.projectConfig.expectedCatalogRelease.id}, but resolution selected ${environment.targetRelease.id}.`,
    }
    : null;
  const globalReleaseError = environment.activeRelease && environment.targetRelease
    && environment.activeRelease.id !== environment.targetRelease.id
    ? {
      code: 'global_release_conflict',
      message: `Global release ${environment.activeRelease.id} conflicts with repository release ${environment.targetRelease.id}.`,
    }
    : null;
  const architectureError = environment.applicationArchitecture?.issue || null;
  const error = environment.repositoryIssues[0]
    || releaseError
    || versionError
    || projectReleaseError
    || globalReleaseError
    || architectureError
    || environment.closureError
    || environment.skillMetadataError
    || environment.sourceRequirementIssues?.[0]
    || (environment.recoveryTransactions.length ? {
      code: 'global_skill_recovery_required',
      message: 'A retained global-skill transaction must be resolved before another mutation.',
      details: { transactions: environment.recoveryTransactions },
    } : null)
    || instructionError;
  return {
    schemaVersion: 1,
    privacy: LOCAL_PATH_PRIVACY_NOTICE,
    severity: error ? 'error' : environment.projectConfigMigration || warnings.length || bindingWarnings.length ? 'warning' : 'ok',
    status: error
      ? 'error'
      : environment.projectConfigMigration ? 'migration-pending'
      : !environment.projectConfig ? 'unconfigured'
        : environment.skillChanges.some((entry) => entry.changeState !== 'unchanged') ? 'update-required' : 'configured',
    preferences: {
      repository: environment.projectConfig,
      user: {
        workspace: selectedPreference,
        contribution: environment.contributionPreference,
        agentTargets: environment.state.agentTargets,
      },
    },
    observation: {
      observedAt: new Date().toISOString(),
      workspace: environment.workspace,
      repositoryIdentity: environment.repository.identity,
      repositoryRoot: environment.repository.git?.root || null,
      repositoryRemote: environment.repository.git?.remoteName || null,
      candidateProfile: environment.candidateProfile,
      profile: environment.profile,
      profileConfirmed: environment.profileConfirmed,
      applicationArchitecture: environment.applicationArchitecture?.selected || null,
      detectedFrameworkVersion: environment.frameworkVersion.version,
      versionSource: environment.frameworkVersion.tier,
      channel: environment.channel,
      targetRelease: environment.targetRelease?.id || null,
      activeGlobalRelease: environment.activeRelease?.id || null,
      sourceBindings: environment.sourceBindingDiagnostics,
      sourceRequirementIssues: environment.sourceRequirementIssues,
      warnings,
      detectionScan: environment.repository.detectionScan || null,
      recoveryTransactions: environment.recoveryTransactions,
      managedInstructionState: environment.instructionState.status,
      projectConfigMigration: environment.projectConfigMigration,
      nestedInstructionFiles: environment.nestedInstructions,
      releaseIndexSource: environment.releaseIndexSource,
      managedSkills: environment.skillChanges,
    },
    error,
  };
}

export function renderStatus(status) {
  const observation = status.observation;
  const lines = [
    `Monica Guide status: ${status.status}`,
    `Workspace                 ${observation.workspace}`,
    `Repository identity       ${observation.repositoryIdentity || 'unresolved'}`,
    `Candidate profile         ${observation.candidateProfile || 'ambiguous'}`,
    `Selected profile          ${observation.profile || 'unresolved'}`,
    `Profile confirmed         ${observation.profileConfirmed ? 'yes' : 'no'}`,
    observation.profile === 'application'
      ? `Application architecture  ${observation.applicationArchitecture || 'unresolved'}`
      : '',
    `Framework version         ${observation.detectedFrameworkVersion || 'unresolved'}`,
    `Release channel           ${observation.channel || 'unresolved'}`,
    `Target release            ${observation.targetRelease || 'unresolved'}`,
    `Active global release     ${observation.activeGlobalRelease || 'none'}`,
    `Recovery transactions     ${observation.recoveryTransactions.length}`,
    `Managed instructions      ${observation.managedInstructionState}`,
    observation.projectConfigMigration ? `Project config migration  schema ${observation.projectConfigMigration.fromSchemaVersion} -> ${observation.projectConfigMigration.toSchemaVersion} pending approved mutation` : '',
    `Contribution preference   ${status.preferences.user.contribution}`,
    `Privacy                   ${status.privacy}`,
    status.error ? `Error                     ${status.error.code}: ${status.error.message}` : '',
  ].filter(Boolean);
  for (const warning of observation.warnings || []) lines.push(`WARN                      ${warning.code}: ${warning.message}`);
  lines.push('', 'Source bindings:');
  for (const [repository, entry] of Object.entries(observation.sourceBindings || {})) {
    lines.push(`  ${repository.padEnd(24)} ${entry.binding ? `${entry.observation.pathHealth} @ ${entry.binding.commit.slice(0, 12)}` : 'unbound'}`);
    if (entry.binding) {
      lines.push(`    ref: ${entry.binding.ref}; path: ${entry.binding.sourcePath}`);
      lines.push(`    provenance: ${renderProvenance(entry.binding.provenance)}; compatibility: ${entry.observation.compatibility}`);
    }
    for (const warning of entry.observation?.warnings || []) lines.push(`    WARN ${warning.code}: ${warning.message}`);
  }
  if (observation.managedSkills.length) {
    lines.push('', 'Managed skills:');
    for (const entry of observation.managedSkills) {
      const installed = formatManagedSkillVersion(entry.installed, { id: observation.activeGlobalRelease });
      const target = formatManagedSkillVersion(entry.target, { id: observation.targetRelease });
      lines.push(`  ${entry.name.padEnd(42)} ${installed.padEnd(20)} ${target.padEnd(20)} ${entry.changeState}`);
    }
  }
  return `${lines.join('\n')}\n`;
}

function renderDoctor(report) {
  const lines = [
    `Monica Guide doctor: ${report.status} (${report.summary.ok} ok, ${report.summary.warnings} warnings, ${report.summary.errors} errors)`,
    `Privacy: ${report.privacy}`,
  ];
  for (const entry of report.checks) {
    const label = entry.status === 'ok' ? 'OK' : entry.status === 'warning' ? 'WARN' : 'ERROR';
    lines.push(`${label.padEnd(5)} ${entry.id.padEnd(28)} ${entry.message}`);
    if (entry.remediation) lines.push(`      remediation: ${entry.remediation}`);
  }
  return `${lines.join('\n')}\n`;
}

export function overviewEnvelope(environment) {
  return {
    schemaVersion: 1,
    privacy: LOCAL_PATH_PRIVACY_NOTICE,
    status: 'ready',
    title: 'Monica Guide toolbox',
    capabilities: [
      { id: 'learn', description: 'Explain Monica concepts and help choose a workflow without changing files.' },
      { id: 'source', description: 'Locate or bind verified Monica and Monica.Docs source globally.' },
      { id: 'initialize', description: 'Detect and preview an application, extension, framework, or docs workspace profile.' },
      { id: 'diagnose', description: 'Inspect global or workspace health with readable or JSON output.' },
      { id: 'update', description: 'Preview catalog-selected Monica skill updates for approved agent targets.' },
      { id: 'contribute', description: 'Route safe local contribution classification and drafting.' },
    ],
    global: {
      bundledCatalog: { version: environment.catalog.version, digest: environment.catalog.digest },
      activeRelease: environment.activeRelease?.id || null,
      agentTargets: environment.agentTargets,
      managedSkillCount: Object.keys(environment.managedSkills).length,
      sourceBindings: environment.sourceBindings,
    },
    nextActions: [
      'Run status or doctor for global health.',
      'Run source list to inspect reusable source bindings.',
      'Describe what you want to build before previewing init in a workspace.',
    ],
  };
}

function renderOverview(overview) {
  const lines = [
    overview.title,
    '',
    'Guide is installed and ready. No repository, profile, or source binding is required.',
    `Active downstream release: ${overview.global.activeRelease || 'none selected'}`,
    `Bundled catalog: ${overview.global.bundledCatalog.version} (${overview.global.bundledCatalog.digest})`,
    `Agent targets: ${overview.global.agentTargets.join(', ') || 'none selected'}`,
    `Privacy: ${overview.privacy}`,
    '',
    'Global source bindings:',
    ...Object.entries(overview.global.sourceBindings).map(([repository, entry]) => `  ${repository.padEnd(24)} ${entry.binding ? `${entry.observation.pathHealth} ${entry.binding.sourcePath}` : 'unbound'}`),
    '',
    'Workflow areas (descriptions, not CLI command names):',
    ...overview.capabilities.map((entry) => `  ${entry.id.padEnd(12)} ${entry.description}`),
    '',
    'Next:',
    ...overview.nextActions.map((entry) => `  - ${entry}`),
  ];
  return `${lines.join('\n')}\n`;
}

export async function globalStatusEnvelope(environment, options) {
  const sourceWarnings = Object.values(environment.sourceBindings)
    .flatMap((entry) => entry.observation?.warnings || []);
  const installedSkillHealth = await inspectGlobalInstalledSkillHealth(environment, options);
  const installedSkillError = installedSkillHealth.checks.find((entry) => entry.status === 'error') || null;
  const error = Object.keys(environment.sourceBindingCandidates).length
    ? { code: 'source_binding_migration_required', message: 'Conflicting legacy source bindings require an explicit decision.' }
    : environment.recoveryTransactions.length
      ? { code: 'global_skill_recovery_required', message: 'A retained global-skill transaction requires review.' }
      : environment.stateLock.status !== 'absent'
        ? { code: 'state_locked', message: `State lock is ${environment.stateLock.status}.` }
        : installedSkillError
          ? { code: installedSkillError.id, message: installedSkillError.message, details: installedSkillError.details }
          : null;
  return {
    schemaVersion: 1,
    privacy: LOCAL_PATH_PRIVACY_NOTICE,
    status: error ? 'error' : sourceWarnings.length ? 'warning' : 'ready',
    observation: {
      observedAt: new Date().toISOString(),
      workspace: null,
      activeGlobalRelease: environment.activeRelease?.id || null,
      agentTargets: environment.agentTargets,
      managedSkills: environment.managedSkills,
      installedSkillHealth,
      sourceBindings: environment.sourceBindings,
      sourceBindingCandidates: environment.sourceBindingCandidates,
      recoveryTransactions: environment.recoveryTransactions,
      stateLock: environment.stateLock,
    },
    error,
  };
}

function renderGlobalStatus(status) {
  const lines = [
    `Monica Guide global status: ${status.status}`,
    `Active global release     ${status.observation.activeGlobalRelease || 'none selected'}`,
    `Agent targets             ${status.observation.agentTargets.join(', ') || 'none selected'}`,
    `Managed skills            ${Object.keys(status.observation.managedSkills).length}`,
    `Installed skill health    ${status.observation.installedSkillHealth.status}`,
    `Recovery transactions     ${status.observation.recoveryTransactions.length}`,
    `State lock                ${status.observation.stateLock.status}`,
    `Privacy                   ${status.privacy}`,
  ];
  lines.push('', 'Source bindings:');
  for (const [repository, entry] of Object.entries(status.observation.sourceBindings)) {
    lines.push(`  ${repository.padEnd(24)} ${entry.binding ? `${entry.observation.pathHealth} @ ${entry.binding.commit.slice(0, 12)}` : 'unbound'}`);
    if (entry.binding) {
      lines.push(`    ref: ${entry.binding.ref}; path: ${entry.binding.sourcePath}`);
      lines.push(`    provenance: ${renderProvenance(entry.binding.provenance)}; compatibility: ${entry.observation.compatibility}`);
    }
    for (const warning of entry.observation?.warnings || []) lines.push(`    WARN ${warning.code}: ${warning.message}`);
  }
  if (status.error) lines.push('', `ERROR ${status.error.code}: ${status.error.message}`);
  for (const entry of status.observation.installedSkillHealth.checks.filter((check) => check.status !== 'ok')) {
    lines.push(`${entry.status === 'error' ? 'ERROR' : 'WARN'} ${entry.id}: ${entry.message}`);
  }
  return `${lines.join('\n')}\n`;
}

function renderSource(result) {
  if (result.bindings) {
    const lines = [`Monica Guide sources: ${result.status} (${result.severity})`, `Privacy: ${result.privacy}`];
    for (const [repository, entry] of Object.entries(result.bindings)) {
      lines.push(`${repository.padEnd(24)} ${entry.binding ? `${entry.observation.pathHealth} ${entry.binding.sourcePath}` : 'unbound'}`);
      if (entry.binding) {
        lines.push(`  ref: ${entry.binding.ref}; commit: ${entry.binding.commit}`);
        lines.push(`  provenance: ${renderProvenance(entry.binding.provenance)}; compatibility: ${entry.observation.compatibility}`);
      }
      for (const warning of entry.observation?.warnings || []) lines.push(`  WARN ${warning.code}: ${warning.message}`);
    }
    for (const warning of result.warnings || []) if (!lines.some((line) => line.includes(`${warning.code}: ${warning.message}`))) lines.push(`WARN ${warning.code}: ${warning.message}`);
    return `${lines.join('\n')}\n`;
  }
  const lines = [
    `Monica Guide source ${result.repository}: ${result.status} (${result.severity})`,
    `Privacy: ${result.privacy}`,
    result.binding ? `Path: ${result.binding.sourcePath}` : 'Path: unbound',
    result.binding ? `Stored ref: ${result.binding.ref}` : '',
    result.binding ? `Stored commit: ${result.binding.commit}` : '',
    result.binding ? `Provenance: ${renderProvenance(result.binding.provenance)}` : '',
    result.observation?.observedCommit ? `Observed commit: ${result.observation.observedCommit}` : '',
    `Compatibility: ${result.observation?.compatibility || 'not-evaluated'}`,
    result.offeredBinding ? `Verified cached source available for binding: ${result.offeredBinding.sourcePath}` : '',
    result.warning ? `WARN ${result.warning.code}: ${result.warning.message}` : '',
  ].filter(Boolean);
  for (const warning of result.warnings || []) {
    if (!lines.some((line) => line.includes(`${warning.code}: ${warning.message}`))) lines.push(`WARN ${warning.code}: ${warning.message}`);
  }
  for (const warning of result.observation?.warnings || []) lines.push(`WARN ${warning.code}: ${warning.message}`);
  return `${lines.join('\n')}\n`;
}

function renderProvenance(provenance) {
  if (typeof provenance === 'string') return provenance;
  if (!provenance) return 'unknown';
  return provenance.resolver
    ? `${provenance.resolver}${provenance.resolutionKind ? `/${provenance.resolutionKind}` : ''}`
    : stableJson(provenance).trim();
}

function help() {
  return `Monica Guide toolbox\n\nUsage:\n  node monica-guide.mjs [overview]\n  node monica-guide.mjs status|doctor [--workspace <path>] [--json]\n  node monica-guide.mjs source list [--json]\n  node monica-guide.mjs source resolve --repository monica|docs [--workspace <path>] [--json]\n  node monica-guide.mjs source bind --repository monica|docs (--source-path <path> [--source-ref <ref>] | --source-ref <ref>)\n  node monica-guide.mjs source unbind --repository monica|docs\n  node monica-guide.mjs init|configure|update|contribute|forget --workspace <path> [options]\n\nSafety:\n  Mutating intents are previews by default. Apply only with both\n  --apply and --plan-digest <approved digest>. Source bindings are global,\n  lookup-only locators and never grant write permission.\n`;
}

export async function main(argv = process.argv.slice(2)) {
  const { intent, options } = parseArguments(argv);
  if (intent === 'help') {
    process.stdout.write(help());
    return 0;
  }
  if (intent === 'overview') {
    const overview = overviewEnvelope(inspectGlobalEnvironment(options));
    process.stdout.write(options.json ? stableJson(overview, 2) : renderOverview(overview));
    return 0;
  }
  if (intent === 'status') {
    const status = options.workspace
      ? statusEnvelope(await inspectEnvironment(options))
      : await globalStatusEnvelope(inspectGlobalEnvironment(options), options);
    process.stdout.write(options.json ? stableJson(status, 2) : options.workspace ? renderStatus(status) : renderGlobalStatus(status));
    return exitCodeForResult(status);
  }
  if (intent === 'doctor') {
    const report = { ...(options.workspace ? await doctor(options) : await doctorGlobal(options)), privacy: LOCAL_PATH_PRIVACY_NOTICE };
    process.stdout.write(options.json ? stableJson(report, 2) : renderDoctor(report));
    return exitCodeForResult(report);
  }
  if (intent === 'source' && options.sourceAction === 'list') {
    const result = { ...listSourceBindings(options), privacy: LOCAL_PATH_PRIVACY_NOTICE };
    process.stdout.write(options.json ? stableJson(result, 2) : renderSource(result));
    return exitCodeForResult(result);
  }
  if (intent === 'source' && options.sourceAction === 'resolve') {
    const result = { ...resolveSourceBinding(options), privacy: LOCAL_PATH_PRIVACY_NOTICE };
    process.stdout.write(options.json ? stableJson(result, 2) : renderSource(result));
    return exitCodeForResult(result);
  }
  const plan = await buildPlan(intent, options);
  const result = options.apply ? await applyPlan(intent, { ...options, precomputedPlan: plan }) : plan;
  process.stdout.write(options.json ? stableJson(result, 2) : renderPlan(result));
  return exitCodeForResult(result);
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  try {
    process.exitCode = await main();
  } catch (error) {
    const guideError = error instanceof GuideError ? error : new GuideError('internal_error', error.message || String(error));
    const jsonRequested = process.argv.includes('--json');
    const envelope = { status: 'error', error: { code: guideError.code, message: guideError.message, ...(guideError.details === undefined ? {} : { details: guideError.details }) } };
    process.stderr.write(jsonRequested ? stableJson(envelope, 2) : `ERROR ${guideError.code}: ${guideError.message}\n`);
    process.exitCode = exitCodeForError(guideError);
  }
}
