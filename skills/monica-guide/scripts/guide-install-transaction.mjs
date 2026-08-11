import fs from 'node:fs';
import path from 'node:path';

import {
  GuideError,
  atomicWrite,
  compareOrdinalUtf8,
  fileDigest,
  run,
  stableJson,
} from './guide-shared.mjs';

function cliPrefix(cliSpec, offline) {
  return [...(offline ? ['--offline'] : []), '--yes', cliSpec];
}

function invoke(runner, executable, args, code, timeoutMs) {
  const result = runner(executable, args, { timeout: timeoutMs });
  if (result.error?.code === 'ETIMEDOUT') {
    throw new GuideError(
      'skills_cli_timeout',
      `Pinned skills CLI timed out after ${timeoutMs} ms during protected global-skill mutation. Verify the local CLI/cache and retry; offline mode never fetches a substitute.`,
      { command: executable, args, timeoutMs },
    );
  }
  if (result.error || result.status !== 0) {
    const detail = (result.stderr || result.stdout || result.error?.message || 'unknown error').trim();
    throw new GuideError(code, `${executable} failed: ${detail}`, {
      command: executable,
      args,
      exitCode: result.status,
    });
  }
  return result;
}

export function normalizeSkillsCliAgent(value) {
  const normalized = String(value || '').trim().toLowerCase().replace(/[ _]+/g, '-');
  return /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(normalized) ? normalized : null;
}

function transactionParent(statePath) {
  return path.join(path.dirname(statePath), 'skill-install-transactions');
}

export function retainedGlobalSkillTransactions(statePath) {
  const parent = transactionParent(statePath);
  if (!fs.existsSync(parent)) return [];
  const retained = [];
  for (const entry of fs.readdirSync(parent, { withFileTypes: true }).sort((left, right) => compareOrdinalUtf8(left.name, right.name))) {
    if (!entry.isDirectory()) continue;
    const root = path.join(parent, entry.name);
    const metadataPath = path.join(root, 'transaction.json');
    try {
      const metadata = JSON.parse(fs.readFileSync(metadataPath, 'utf8'));
      retained.push({
        path: root,
        status: typeof metadata.status === 'string' ? metadata.status : 'invalid',
        attemptedSkills: Array.isArray(metadata.attemptedSkills) ? metadata.attemptedSkills : [],
        attemptedFiles: Array.isArray(metadata.attemptedFiles) ? metadata.attemptedFiles : [],
      });
    } catch (error) {
      retained.push({ path: root, status: 'invalid', error: error.message });
    }
  }
  return retained;
}

function assertNoRetainedTransactions(statePath) {
  const retained = retainedGlobalSkillTransactions(statePath);
  if (retained.length) {
    throw new GuideError(
      'global_skill_recovery_required',
      'A prior Monica global-skill transaction still has private recovery evidence. Diagnose and resolve it before another mutation.',
      { transactions: retained },
    );
  }
}

function readDiscovery({ runner, executable, cliSpec, offline, agents, timeoutMs }) {
  const merged = new Map();
  for (const agent of [...new Set(agents || [])]) {
    const result = invoke(
      runner,
      executable,
      [...cliPrefix(cliSpec, offline), 'ls', '-g', '-a', agent, '--json'],
      'global_skill_transaction_discovery_failed',
      timeoutMs,
    );
    let payload;
    try {
      payload = JSON.parse(result.stdout);
    } catch (error) {
      throw new GuideError(
        'global_skill_transaction_discovery_invalid',
        `Pinned skills CLI returned invalid discovery JSON for ${agent}: ${error.message}`,
      );
    }
    if (!Array.isArray(payload)) {
      throw new GuideError('global_skill_transaction_discovery_invalid', `Pinned skills CLI discovery for ${agent} must be a top-level array.`);
    }
    for (const entry of payload) {
      if (!entry?.name) continue;
      const previous = merged.get(entry.name);
      if (previous && (previous.path !== entry.path || stableJson(previous.provenance) !== stableJson(normalizeProvenance(entry, entry.name)))) {
        throw new GuideError('global_skill_transaction_discovery_ambiguous', `Agent targets disagree about the installed ${entry.name} location or provenance.`);
      }
      const entryAgents = Array.isArray(entry.agents) ? entry.agents : [agent];
      merged.set(entry.name, {
        ...entry,
        agents: [...new Set([...(previous?.agents || []), ...entryAgents])].sort(compareOrdinalUtf8),
        provenance: normalizeProvenance(entry, entry.name),
      });
    }
  }
  return [...merged.values()].map(({ provenance, ...entry }) => ({ ...entry, ...provenance }));
}

function inventoryTree(root) {
  if (!fs.existsSync(root) || !fs.lstatSync(root).isDirectory()) {
    throw new GuideError('global_skill_snapshot_unavailable', `Installed skill directory is unavailable: ${root}.`);
  }
  const files = {};
  const visit = (directory) => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true }).sort((left, right) => compareOrdinalUtf8(left.name, right.name))) {
      const absolute = path.join(directory, entry.name);
      if (entry.isSymbolicLink()) {
        throw new GuideError('global_skill_snapshot_symlink', `Installed Monica skill contains an internal symlink: ${absolute}.`);
      }
      if (entry.isDirectory()) {
        visit(absolute);
      } else if (entry.isFile()) {
        const stat = fs.statSync(absolute);
        const relative = path.relative(root, absolute).split(path.sep).join('/');
        files[relative] = {
          digest: fileDigest(absolute),
          mode: process.platform === 'win32' ? null : stat.mode & 0o777,
          size: stat.size,
        };
      }
    }
  };
  visit(root);
  if (!files['SKILL.md']) throw new GuideError('global_skill_snapshot_invalid', `Installed Monica skill has no SKILL.md: ${root}.`);
  return files;
}

function normalizeProvenance(entry, skill) {
  const provenance = {
    source: entry.source ?? null,
    sourceUrl: entry.sourceUrl ?? null,
    sourceType: entry.sourceType ?? null,
  };
  for (const [field, value] of Object.entries(provenance)) {
    if (value !== null && typeof value !== 'string') {
      throw new GuideError('global_skill_transaction_discovery_invalid', `Global discovery ${field} for ${skill} must be a string or null.`);
    }
  }
  return provenance;
}

function plannedEntry(payload, skill, targetAgents) {
  const matches = payload.filter((entry) => entry?.name === skill);
  if (matches.length > 1) {
    throw new GuideError('global_skill_transaction_discovery_ambiguous', `Global discovery contains ${matches.length} entries for ${skill}.`);
  }
  if (!matches.length) return null;
  const entry = matches[0];
  if (entry.scope !== 'global' || typeof entry.path !== 'string' || !Array.isArray(entry.agents)) {
    throw new GuideError('global_skill_transaction_discovery_invalid', `Global discovery entry for ${skill} is incomplete.`, { entry });
  }
  const agents = [...new Set(entry.agents.map((agent) => String(agent)))].sort(compareOrdinalUtf8);
  const cliAgents = [...new Set(agents.map(normalizeSkillsCliAgent).filter(Boolean))].sort(compareOrdinalUtf8);
  if (!cliAgents.length) {
    throw new GuideError(
      'global_skill_membership_not_restorable',
      `Pre-existing ${skill} has no valid agent-target binding that the Guide can restore through the pinned CLI.`,
      { agents },
    );
  }
  return {
    path: path.resolve(entry.path),
    agents,
    cliAgents,
    extraTargetAgents: targetAgents.filter((agent) => !cliAgents.includes(agent)),
    provenance: normalizeProvenance(entry, skill),
  };
}

function isLocalSource(source) {
  return typeof source === 'string' && (path.isAbsolute(source) || path.win32.isAbsolute(source));
}

function supportedMonicaProvenance(provenance) {
  if (Object.values(provenance).every((value) => value === null)) return 'none';
  if (provenance.sourceType !== 'github' || !/^Tairitsua\/Monica$/i.test(provenance.source || '')) return null;
  try {
    const sourceUrl = new URL(provenance.sourceUrl);
    const repository = sourceUrl.pathname.replace(/^\//, '').replace(/\.git$/, '').replace(/\/$/, '');
    return sourceUrl.hostname.toLowerCase() === 'github.com' && /^Tairitsua\/Monica$/i.test(repository)
      ? 'monica-github'
      : null;
  } catch {
    return null;
  }
}

function immutableMonicaSkillSource(source, skill) {
  if (typeof source !== 'string') return false;
  try {
    const parsed = new URL(source);
    const segments = parsed.pathname.split('/').filter(Boolean);
    return parsed.hostname.toLowerCase() === 'github.com'
      && /^Tairitsua$/i.test(segments[0] || '')
      && /^Monica$/i.test(segments[1] || '')
      && segments[2] === 'tree'
      && Boolean(segments[3])
      && segments.slice(4).join('/') === `skills/${skill}`;
  } catch {
    return false;
  }
}

function ensurePrivateDirectory(directory) {
  fs.mkdirSync(directory, { recursive: true, mode: 0o700 });
  if (process.platform !== 'win32') fs.chmodSync(directory, 0o700);
}

function tryCleanupTransaction(directory) {
  try {
    fs.rmSync(directory, { recursive: true, force: true });
  } catch (error) {
    return { code: error.code || 'cleanup_failed', message: error.message };
  }
  const parent = path.dirname(directory);
  try {
    if (fs.readdirSync(parent).length === 0) fs.rmdirSync(parent);
  } catch {
    // Another retained transaction or a platform directory lock is harmless here.
  }
  return null;
}

function cleanupSnapshot(context, directory) {
  try {
    return context.cleanup(directory);
  } catch (error) {
    return { code: error.code || 'cleanup_failed', message: error.message };
  }
}

function assertNoSymlinkComponents(filePath) {
  const absolute = path.resolve(filePath);
  const parsed = path.parse(absolute);
  let current = parsed.root;
  for (const segment of absolute.slice(parsed.root.length).split(path.sep).filter(Boolean)) {
    current = path.join(current, segment);
    if (!fs.existsSync(current)) continue;
    if (fs.lstatSync(current).isSymbolicLink()) {
      throw new GuideError('file_transaction_path_unsafe', `Planned file mutation crosses a symlink: ${current}.`);
    }
  }
}

function captureLocalFiles(transactionRoot, fileActions) {
  const root = path.join(transactionRoot, 'files');
  ensurePrivateDirectory(root);
  const records = {};
  for (const [index, action] of fileActions.entries()) {
    const absolute = path.resolve(action.path);
    if (records[absolute]) throw new GuideError('duplicate_file_action', `The plan mutates ${absolute} more than once.`);
    assertNoSymlinkComponents(absolute);
    if (!fs.existsSync(absolute)) {
      records[absolute] = { existed: false, mode: null, digest: null, snapshot: null };
      continue;
    }
    const stat = fs.lstatSync(absolute);
    if (stat.isSymbolicLink() || !stat.isFile()) {
      throw new GuideError('file_transaction_path_unsafe', `Planned file mutation requires a regular non-symlinked file: ${absolute}.`);
    }
    const snapshot = path.join(root, `${index}.bin`);
    fs.copyFileSync(absolute, snapshot);
    if (process.platform !== 'win32') fs.chmodSync(snapshot, 0o600);
    records[absolute] = {
      existed: true,
      mode: process.platform === 'win32' ? null : stat.mode & 0o777,
      digest: fileDigest(absolute),
      snapshot,
    };
  }
  return records;
}

function captureSnapshot(context) {
  assertNoRetainedTransactions(context.statePath);
  const payload = readDiscovery(context);
  const parent = transactionParent(context.statePath);
  ensurePrivateDirectory(parent);
  let transactionRoot = null;
  try {
    transactionRoot = fs.mkdtempSync(path.join(parent, 'transaction-'));
    if (process.platform !== 'win32') fs.chmodSync(transactionRoot, 0o700);
    const snapshotRoot = path.join(transactionRoot, 'skills');
    ensurePrivateDirectory(snapshotRoot);
    const records = {};
    for (const skill of context.skills) {
      const entry = plannedEntry(payload, skill, context.agents);
      if (!entry) {
        records[skill] = { existed: false, agents: [], cliAgents: [], path: null, files: null, provenance: null };
        continue;
      }
      const provenanceKind = supportedMonicaProvenance(entry.provenance);
      if (!provenanceKind) {
        throw new GuideError(
          'global_skill_provenance_not_restorable',
          `Pre-existing ${skill} has provenance that cannot be restored through the pinned CLI.`,
          { provenance: entry.provenance },
        );
      }
      const installSource = context.installSources[skill];
      const restoreSource = context.restoreSources[skill];
      const installIsLocal = isLocalSource(installSource);
      const needsRemoteRestore = !installIsLocal || entry.extraTargetAgents.length > 0;
      if (provenanceKind === 'monica-github' && needsRemoteRestore && !immutableMonicaSkillSource(restoreSource, skill)) {
        throw new GuideError('global_skill_provenance_not_restorable', `Pre-existing ${skill} has no immutable Monica restore source.`);
      }
      if (provenanceKind === 'monica-github' && installIsLocal && entry.extraTargetAgents.length && context.offline) {
        throw new GuideError(
          'offline_global_skill_membership_change_unsupported',
          `Offline mutation cannot add agent bindings to pre-existing remote-provenance skill ${skill} and still restore that provenance without network access.`,
          { extraTargetAgents: entry.extraTargetAgents },
        );
      }
      const files = inventoryTree(entry.path);
      const destination = path.join(snapshotRoot, skill);
      fs.cpSync(entry.path, destination, { recursive: true, errorOnExist: true, force: false });
      if (stableJson(inventoryTree(destination)) !== stableJson(files)) {
        throw new GuideError('global_skill_snapshot_copy_mismatch', `Private snapshot copy for ${skill} does not match installed bytes.`);
      }
      records[skill] = {
        ...entry,
        existed: true,
        files,
        provenanceKind,
        installSource,
        installIsLocal,
        restoreSource,
      };
    }
    const fileRecords = captureLocalFiles(transactionRoot, context.fileActions);
    const snapshot = {
      root: transactionRoot,
      snapshotRoot,
      records,
      fileRecords,
      attemptedSkills: [],
      attemptedFiles: [],
      status: 'prepared',
    };
    persistMetadata(context, snapshot, 'prepared');
    return snapshot;
  } catch (error) {
    if (transactionRoot) tryCleanupTransaction(transactionRoot);
    throw error;
  }
}

function persistMetadata(context, snapshot, status, extra = {}) {
  snapshot.status = status;
  atomicWrite(path.join(snapshot.root, 'transaction.json'), stableJson({
    schemaVersion: 2,
    status,
    skills: context.skills,
    targetAgents: context.agents,
    cliSpec: context.cliSpec,
    offline: context.offline,
    attemptedSkills: snapshot.attemptedSkills,
    attemptedFiles: snapshot.attemptedFiles,
    records: snapshot.records,
    files: snapshot.fileRecords,
    ...extra,
  }, 2), 0o600);
}

function removeBindings(context, skill, agents, failures) {
  if (!agents.length) return;
  try {
    const args = [...cliPrefix(context.cliSpec, context.offline), 'remove', skill, '-g'];
    for (const agent of agents) args.push('-a', agent);
    args.push('-y');
    invoke(
      context.runner,
      context.executable,
      args,
      'global_skill_rollback_remove_failed',
      context.timeoutMs,
    );
  } catch (error) {
    failures.push({ phase: 'remove', skill, code: error.code || 'remove_failed', message: error.message });
  }
}

function restoreSkills(context, snapshot, failures) {
  for (const skill of [...snapshot.attemptedSkills].reverse()) {
    const record = snapshot.records[skill];
    if (!record?.existed) {
      removeBindings(context, skill, context.agents, failures);
      continue;
    }
    const clearNewRemoteLock = record.provenanceKind === 'none' && !record.installIsLocal;
    const agentsToRemove = clearNewRemoteLock
      ? context.agents
      : record.extraTargetAgents;
    removeBindings(context, skill, [...new Set(agentsToRemove)].sort(compareOrdinalUtf8), failures);
    const source = record.provenanceKind === 'none' || (record.installIsLocal && !record.extraTargetAgents.length)
      ? path.join(snapshot.snapshotRoot, skill)
      : record.restoreSource;
    try {
      invoke(
        context.runner,
        context.executable,
        [...cliPrefix(context.cliSpec, context.offline), 'add', source, '-g', '-a', ...record.cliAgents, '-s', skill, '-y'],
        'global_skill_rollback_restore_failed',
        context.timeoutMs,
      );
    } catch (error) {
      failures.push({ phase: 'restore', skill, code: error.code || 'restore_failed', message: error.message });
    }
  }
}

function restoreFiles(snapshot, failures) {
  for (const filePath of [...snapshot.attemptedFiles].reverse()) {
    const record = snapshot.fileRecords[filePath];
    try {
      assertNoSymlinkComponents(filePath);
      if (record.existed) {
        atomicWrite(filePath, fs.readFileSync(record.snapshot), record.mode ?? 0o600);
      } else if (fs.existsSync(filePath)) {
        const state = fs.lstatSync(filePath);
        if (state.isSymbolicLink() || !state.isFile()) throw new Error('rollback target is not a regular file');
        fs.unlinkSync(filePath);
      }
    } catch (error) {
      failures.push({ phase: 'restore-file', path: filePath, code: error.code || 'restore_failed', message: error.message });
    }
  }
}

function verifyRestoredSkills(context, snapshot, failures) {
  let payload;
  try {
    payload = readDiscovery(context);
  } catch (error) {
    failures.push({ phase: 'verify-discovery', code: error.code || 'verification_failed', message: error.message });
    return [{ kind: 'unverified', message: 'Restored global skill discovery could not be verified.' }];
  }
  const drift = [];
  for (const skill of snapshot.attemptedSkills) {
    const expected = snapshot.records[skill];
    let actual;
    try {
      actual = plannedEntry(payload, skill, context.agents);
    } catch (error) {
      drift.push({ skill, kind: 'discovery', code: error.code, message: error.message });
      continue;
    }
    if (!expected.existed) {
      if (actual) drift.push({ skill, kind: 'unexpected', message: 'Skill was absent before apply but remains globally discovered.' });
      continue;
    }
    if (!actual) {
      drift.push({ skill, kind: 'missing', message: 'Pre-existing skill is no longer globally discovered.' });
      continue;
    }
    if (actual.path !== expected.path) drift.push({ skill, kind: 'path', expected: expected.path, actual: actual.path });
    if (stableJson(actual.agents) !== stableJson(expected.agents)) {
      drift.push({ skill, kind: 'agents', expected: expected.agents, actual: actual.agents });
    }
    if (stableJson(actual.provenance) !== stableJson(expected.provenance)) {
      drift.push({ skill, kind: 'provenance', expected: expected.provenance, actual: actual.provenance });
    }
    try {
      const files = inventoryTree(actual.path);
      if (stableJson(files) !== stableJson(expected.files)) {
        drift.push({ skill, kind: 'content', message: 'Restored canonical bytes or portable file modes differ.' });
      }
    } catch (error) {
      drift.push({ skill, kind: 'content', code: error.code, message: error.message });
    }
  }
  return drift;
}

function verifyRestoredFiles(snapshot) {
  const drift = [];
  for (const filePath of snapshot.attemptedFiles) {
    const expected = snapshot.fileRecords[filePath];
    if (!expected.existed) {
      if (fs.existsSync(filePath)) drift.push({ path: filePath, kind: 'unexpected-file' });
      continue;
    }
    if (!fs.existsSync(filePath)) {
      drift.push({ path: filePath, kind: 'missing-file' });
      continue;
    }
    const state = fs.lstatSync(filePath);
    if (state.isSymbolicLink() || !state.isFile()) {
      drift.push({ path: filePath, kind: 'unsafe-file' });
      continue;
    }
    const actualDigest = fileDigest(filePath);
    const actualMode = process.platform === 'win32' ? null : state.mode & 0o777;
    if (actualDigest !== expected.digest || actualMode !== expected.mode) {
      drift.push({ path: filePath, kind: 'file-content', expectedDigest: expected.digest, actualDigest, expectedMode: expected.mode, actualMode });
    }
  }
  return drift;
}

function primaryEnvelope(error) {
  return {
    code: error?.code || 'global_skill_install_failed',
    message: error?.message || String(error),
    ...(error?.details === undefined ? {} : { details: error.details }),
  };
}

function compensatedError(error, status, snapshotPath = null) {
  const transaction = { status, snapshotPath };
  if (error instanceof GuideError) {
    const priorDetails = error.details && typeof error.details === 'object' && !Array.isArray(error.details)
      ? error.details
      : error.details === undefined ? {} : { causeDetails: error.details };
    error.details = { ...priorDetails, transaction };
    return error;
  }
  return new GuideError('global_skill_install_failed', error?.message || String(error), { transaction });
}

function rollback(context, snapshot, primaryError) {
  const failures = [];
  restoreFiles(snapshot, failures);
  restoreSkills(context, snapshot, failures);
  const drift = [
    ...verifyRestoredFiles(snapshot),
    ...verifyRestoredSkills(context, snapshot, failures),
  ];
  if (!failures.length && !drift.length) {
    try {
      persistMetadata(context, snapshot, 'compensated', { primary: primaryEnvelope(primaryError) });
    } catch {
      // A successful cleanup below still removes the no-longer-needed evidence.
    }
    const cleanupError = cleanupSnapshot(context, snapshot.root);
    if (!cleanupError) throw compensatedError(primaryError, 'compensated');
    throw compensatedError(primaryError, 'compensated-cleanup-pending', snapshot.root);
  }
  try {
    persistMetadata(context, snapshot, 'rollback-incomplete', {
      primary: primaryEnvelope(primaryError),
      failures,
      drift,
    });
  } catch (metadataError) {
    failures.push({ phase: 'retain-evidence', code: metadataError.code || 'write_failed', message: metadataError.message });
  }
  throw new GuideError(
    'global_skill_rollback_incomplete',
    `Monica Guide mutation failed and compensation could not restore its observable recovery contract. Private evidence is retained at ${snapshot.root}.`,
    {
      primary: primaryEnvelope(primaryError),
      transaction: { status: 'incomplete', snapshotPath: snapshot.root, failures, drift },
    },
  );
}

/**
 * Runs global skill and local file mutations under one compensating boundary.
 * The observable recovery contract covers canonical skill path/content/modes,
 * CLI discovery memberships and provenance, plus planned file bytes/modes. The
 * pinned CLI does not expose per-agent copy/symlink topology, so it is not part
 * of the contract. Process or machine termination can still interrupt recovery.
 */
export function withGlobalSkillCompensation({
  statePath,
  skills,
  agents,
  cliSpec,
  installSources = {},
  restoreSources = {},
  fileActions = [],
  offline = false,
  timeoutMs = 30_000,
  executable = process.env.MONICA_GUIDE_NPX || 'npx',
  runner = run,
  cleanup = tryCleanupTransaction,
  mutate,
}) {
  const context = {
    statePath,
    skills: [...new Set(skills)].sort(compareOrdinalUtf8),
    agents: [...new Set(agents)].sort(compareOrdinalUtf8),
    cliSpec,
    installSources,
    restoreSources,
    fileActions,
    offline: Boolean(offline),
    timeoutMs,
    executable,
    runner,
    cleanup,
  };
  if (!context.skills.length && !context.fileActions.length) {
    return {
      result: mutate({ runSkill: (_skill, callback) => callback(), runFile: (_action, callback) => callback() }),
      transaction: { status: 'not-required' },
    };
  }
  const snapshot = captureSnapshot(context);
  const controls = {
    runSkill(skill, callback) {
      if (!context.skills.includes(skill)) throw new GuideError('global_skill_transaction_scope_invalid', `Skill ${skill} is outside the protected transaction scope.`);
      if (!snapshot.attemptedSkills.includes(skill)) snapshot.attemptedSkills.push(skill);
      persistMetadata(context, snapshot, 'mutating');
      return callback();
    },
    runFile(action, callback) {
      const filePath = path.resolve(action.path);
      if (!snapshot.fileRecords[filePath]) throw new GuideError('file_transaction_scope_invalid', `File ${filePath} is outside the protected transaction scope.`);
      if (!snapshot.attemptedFiles.includes(filePath)) snapshot.attemptedFiles.push(filePath);
      persistMetadata(context, snapshot, 'mutating');
      return callback();
    },
  };
  let result;
  try {
    result = mutate(controls);
    persistMetadata(context, snapshot, 'committed');
  } catch (primaryError) {
    return rollback(context, snapshot, primaryError);
  }
  const cleanupError = cleanupSnapshot(context, snapshot.root);
  return cleanupError
    ? { result, transaction: { status: 'committed-cleanup-pending', snapshotPath: snapshot.root, cleanupError } }
    : { result, transaction: { status: 'committed', snapshotPath: null } };
}
