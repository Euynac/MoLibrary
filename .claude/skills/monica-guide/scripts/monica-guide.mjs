#!/usr/bin/env node

import { GuideError, stableJson } from './guide-shared.mjs';
import { applyPlan, buildPlan } from './guide-plan.mjs';
import { doctor, inspectEnvironment } from './guide-doctor.mjs';
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
  ['--source-access', 'sourceAccess'],
  ['--source-resolver', 'sourceResolver'],
  ['--preference', 'preference'],
  ['--plan-digest', 'planDigest'],
]);
const REPEAT_OPTIONS = new Map([
  ['--capability', 'capabilities'],
  ['--agent', 'agents'],
  ['--nested-instruction', 'nestedInstructions'],
]);
const BOOLEAN_OPTIONS = new Map([
  ['--apply', 'apply'],
  ['--switch-global', 'switchGlobal'],
  ['--offline', 'offline'],
  ['--json', 'json'],
  ['--help', 'help'],
]);
const INTENTS = new Set(['init', 'status', 'doctor', 'update', 'configure', 'source', 'contribute', 'forget']);

export function parseArguments(argv) {
  const options = { workspace: process.cwd(), capabilities: [], agents: [], nestedInstructions: [] };
  let intent = null;
  for (let index = 0; index < argv.length; index += 1) {
    const argument = argv[index];
    if (!argument.startsWith('-') && !intent) {
      intent = argument;
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
    } else if (BOOLEAN_OPTIONS.has(argument)) {
      options[BOOLEAN_OPTIONS.get(argument)] = true;
    } else {
      throw new GuideError('unknown_option', `Unknown option: ${argument}.`);
    }
  }
  if (options.help && !intent) return { intent: 'help', options };
  if (!INTENTS.has(intent)) throw new GuideError('invalid_intent', `Intent must be one of: ${[...INTENTS].join(', ')}.`);
  if (options.apply && !options.planDigest) throw new GuideError('plan_digest_required', '--apply requires --plan-digest <digest>.');
  if (options.planDigest && !options.apply) throw new GuideError('apply_required', '--plan-digest is accepted only with --apply.');
  return { intent, options };
}

function renderPlan(plan) {
  const lines = [
    `${plan.dryRun ? 'PREVIEW' : 'APPLIED'} ${plan.intent} for ${plan.workspace}`,
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
  if (plan.route) lines.push('', `Route: $${plan.route.skill} (${plan.route.preference}); remote mutation authorized: no.`);
  if (plan.dryRun && !plan.blockers.length) lines.push('', `Apply unchanged plan with --apply --plan-digest ${plan.planDigest}`);
  return `${lines.join('\n')}\n`;
}

function statusEnvelope(environment) {
  const selectedPreference = environment.state.workspacePreferences[environment.key] || null;
  const instructionError = environment.instructionState.issues.find((issue) => issue.severity === 'error') || null;
  const error = environment.repositoryIssues[0]
    || environment.releaseError
    || environment.versionError
    || environment.closureError
    || (environment.recoveryTransactions.length ? {
      code: 'global_skill_recovery_required',
      message: 'A retained global-skill transaction must be resolved before another mutation.',
      details: { transactions: environment.recoveryTransactions },
    } : null)
    || instructionError;
  return {
    schemaVersion: 1,
    status: error ? 'error' : environment.projectConfig ? 'configured' : 'unconfigured',
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
      profileConfirmed: environment.profileConfirmed,
      detectedFrameworkVersion: environment.frameworkVersion.version,
      versionSource: environment.frameworkVersion.tier,
      targetRelease: environment.targetRelease?.id || null,
      activeGlobalRelease: environment.activeRelease?.id || null,
      sourceBinding: environment.sourceBinding,
      recoveryTransactions: environment.recoveryTransactions,
      managedInstructionState: environment.instructionState.status,
      nestedInstructionFiles: environment.nestedInstructions,
      releaseIndexSource: environment.releaseIndexSource,
    },
    error,
  };
}

function renderStatus(status) {
  const observation = status.observation;
  return [
    `Monica Guide status: ${status.status}`,
    `Workspace                 ${observation.workspace}`,
    `Repository identity       ${observation.repositoryIdentity || 'unresolved'}`,
    `Candidate profile         ${observation.candidateProfile || 'ambiguous'}`,
    `Profile confirmed         ${observation.profileConfirmed ? 'yes' : 'no'}`,
    `Framework version         ${observation.detectedFrameworkVersion || 'unresolved'}`,
    `Target release            ${observation.targetRelease || 'unresolved'}`,
    `Active global release     ${observation.activeGlobalRelease || 'none'}`,
    `Recovery transactions     ${observation.recoveryTransactions.length}`,
    `Managed instructions      ${observation.managedInstructionState}`,
    `Contribution preference   ${status.preferences.user.contribution}`,
    status.error ? `Error                     ${status.error.code}: ${status.error.message}` : '',
  ].filter(Boolean).join('\n').concat('\n');
}

function renderDoctor(report) {
  const lines = [
    `Monica Guide doctor: ${report.status} (${report.summary.ok} ok, ${report.summary.warnings} warnings, ${report.summary.errors} errors)`,
  ];
  for (const entry of report.checks) {
    const label = entry.status === 'ok' ? 'OK' : entry.status === 'warning' ? 'WARN' : 'ERROR';
    lines.push(`${label.padEnd(5)} ${entry.id.padEnd(28)} ${entry.message}`);
    if (entry.remediation) lines.push(`      remediation: ${entry.remediation}`);
  }
  return `${lines.join('\n')}\n`;
}

function help() {
  return `Monica Guide\n\nUsage:\n  node monica-guide.mjs <intent> --workspace <path> [options]\n\nIntents:\n  init status doctor update configure source contribute forget\n\nSafety:\n  Mutating intents are previews by default. Apply only with both\n  --apply and --plan-digest <approved digest>. Initial setup should pass\n  --release-tag <immutable-vSemVer-tag> from the advertised Monica release.\n  init, configure, and update may repeat --nested-instruction with an\n  existing repository-relative nested AGENTS.md or CLAUDE.md path.\n`;
}

export async function main(argv = process.argv.slice(2)) {
  const { intent, options } = parseArguments(argv);
  if (intent === 'help') {
    process.stdout.write(help());
    return 0;
  }
  if (intent === 'status') {
    const status = statusEnvelope(await inspectEnvironment(options));
    process.stdout.write(options.json ? stableJson(status, 2) : renderStatus(status));
    return status.status === 'error' ? 2 : 0;
  }
  if (intent === 'doctor') {
    const report = await doctor(options);
    process.stdout.write(options.json ? stableJson(report, 2) : renderDoctor(report));
    return report.status === 'error' ? 2 : report.status === 'warning' ? 1 : 0;
  }
  const plan = await buildPlan(intent, options);
  const result = options.apply ? await applyPlan(intent, { ...options, precomputedPlan: plan }) : plan;
  process.stdout.write(options.json ? stableJson(result, 2) : renderPlan(result));
  return result.blockers.length ? 2 : 0;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  try {
    process.exitCode = await main();
  } catch (error) {
    const guideError = error instanceof GuideError ? error : new GuideError('internal_error', error.message || String(error));
    const jsonRequested = process.argv.includes('--json');
    const envelope = { status: 'error', error: { code: guideError.code, message: guideError.message, ...(guideError.details === undefined ? {} : { details: guideError.details }) } };
    process.stderr.write(jsonRequested ? stableJson(envelope, 2) : `ERROR ${guideError.code}: ${guideError.message}\n`);
    process.exitCode = 2;
  }
}
