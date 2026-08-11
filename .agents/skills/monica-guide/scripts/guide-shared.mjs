import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

export const STATE_SCHEMA_VERSION = 4;
export const PROJECT_SCHEMA_VERSION = 2;
export const DEFAULT_EXTERNAL_COMMAND_TIMEOUT_MS = 30_000;
export const MIN_EXTERNAL_COMMAND_TIMEOUT_MS = 50;
export const MAX_EXTERNAL_COMMAND_TIMEOUT_MS = 300_000;

const WORKSPACE_KEY_DIGEST_PREFIX_LENGTH = 'sha256:'.length;
const WORKSPACE_KEY_LENGTH = 24;
const SOURCE_REPOSITORY_NAMES = new Map([
  ['tairitsua/monica', 'Tairitsua/Monica'],
  ['tairitsua/monica.docs', 'Tairitsua/Monica.Docs'],
]);

const SEMVER_PATTERN = /^(0|[1-9]\d*)\.(0|[1-9]\d*)\.(0|[1-9]\d*)(?:-((?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*)(?:\.(?:0|[1-9]\d*|\d*[A-Za-z-][0-9A-Za-z-]*))*))?(?:\+([0-9A-Za-z-]+(?:\.[0-9A-Za-z-]+)*))?$/;

export function parseSemVer(value) {
  const match = String(value || '').match(SEMVER_PATTERN);
  return match ? { value: match[0], major: match[1], minor: match[2], patch: match[3], prerelease: match[4] || null, build: match[5] || null } : null;
}

export function semverChannel(value) {
  const parsed = parseSemVer(value);
  return parsed ? (parsed.prerelease ? 'preview' : 'stable') : null;
}

export class GuideError extends Error {
  constructor(code, message, details = undefined) {
    super(message);
    this.name = 'GuideError';
    this.code = code;
    this.details = details;
  }
}

export function canonicalize(value) {
  if (Array.isArray(value)) return value.map(canonicalize);
  if (value && typeof value === 'object') {
    return Object.fromEntries(Object.keys(value).sort().map((key) => [key, canonicalize(value[key])]));
  }
  return value;
}

export function stableJson(value, space = 0) {
  return `${JSON.stringify(canonicalize(value), null, space)}${space ? '\n' : ''}`;
}

export function digest(value) {
  const input = Buffer.isBuffer(value) ? value : Buffer.from(typeof value === 'string' ? value : stableJson(value));
  return `sha256:${crypto.createHash('sha256').update(input).digest('hex')}`;
}

/** Compare UTF-8 text by unsigned bytes, matching Python's ordinal path ordering. */
export function compareOrdinalUtf8(left, right) {
  return Buffer.compare(Buffer.from(String(left), 'utf8'), Buffer.from(String(right), 'utf8'));
}

export function exists(filePath) {
  try {
    fs.accessSync(filePath);
    return true;
  } catch {
    return false;
  }
}

export function readText(filePath, fallback = null) {
  try {
    return fs.readFileSync(filePath, 'utf8');
  } catch (error) {
    if (error.code === 'ENOENT' && fallback !== null) return fallback;
    throw new GuideError('read_failed', `Cannot read ${filePath}: ${error.message}`);
  }
}

export function readJson(filePath, fallback = undefined) {
  let text;
  try {
    text = fs.readFileSync(filePath, 'utf8');
  } catch (error) {
    if (error.code === 'ENOENT' && fallback !== undefined) return fallback;
    throw new GuideError('read_failed', `Cannot read JSON file ${filePath}: ${error.message}`);
  }
  try {
    return JSON.parse(text);
  } catch (error) {
    throw new GuideError('invalid_json', `Invalid JSON in ${filePath}: ${error.message}`);
  }
}

export function fileDigest(filePath) {
  if (!exists(filePath)) return null;
  const hash = crypto.createHash('sha256');
  const descriptor = fs.openSync(filePath, 'r');
  const buffer = Buffer.allocUnsafe(64 * 1024);
  try {
    let length;
    while ((length = fs.readSync(descriptor, buffer, 0, buffer.length, null)) > 0) hash.update(buffer.subarray(0, length));
  } finally {
    fs.closeSync(descriptor);
  }
  return `sha256:${hash.digest('hex')}`;
}

function currentRuntimeIsWsl() {
  if (process.platform !== 'linux') return false;
  if (process.env.WSL_INTEROP || process.env.WSL_DISTRO_NAME) return true;
  try { return /microsoft/i.test(fs.readFileSync('/proc/version', 'utf8')); } catch { return false; }
}

export function normalizePath(inputPath, cwd = process.cwd(), runtime = {}) {
  const platform = runtime.platform || process.platform;
  const isWsl = runtime.isWsl ?? currentRuntimeIsWsl();
  if (platform === 'win32') {
    let value = inputPath || cwd;
    const wsl = value.match(/^\/mnt\/([A-Za-z])(?:\/(.*))?$/);
    if (wsl) value = `${wsl[1].toUpperCase()}:\\${(wsl[2] || '').replaceAll('/', '\\')}`;
    return path.win32.resolve(value);
  }
  if (!inputPath) return path.resolve(cwd);
  let value = inputPath;
  if (isWsl && /^[A-Za-z]:[\\/]/.test(value)) {
    const drive = value[0].toLowerCase();
    value = `/mnt/${drive}/${value.slice(3).replaceAll('\\', '/')}`;
  }
  return path.resolve(cwd, value);
}

export function stateFilePath(override = undefined) {
  if (override) return normalizePath(override);
  if (process.env.MONICA_GUIDE_STATE) return normalizePath(process.env.MONICA_GUIDE_STATE);
  if (process.platform === 'win32') {
    const root = process.env.LOCALAPPDATA;
    if (!root) throw new GuideError('state_home_unavailable', 'LOCALAPPDATA is required for Monica Guide user state.');
    return path.join(root, 'Monica Guide', 'state.json');
  }
  if (process.platform === 'darwin') return path.join(os.homedir(), 'Library', 'Application Support', 'Monica Guide', 'state.json');
  return path.join(process.env.XDG_DATA_HOME || path.join(os.homedir(), '.local', 'share'), 'monica-guide', 'state.json');
}

export function workspaceKey(workspace, identity = null) {
  return digest(`${identity || 'local'}\n${path.resolve(workspace)}`)
    .slice(WORKSPACE_KEY_DIGEST_PREFIX_LENGTH, WORKSPACE_KEY_DIGEST_PREFIX_LENGTH + WORKSPACE_KEY_LENGTH);
}

export function emptyState() {
  return {
    schemaVersion: STATE_SCHEMA_VERSION,
    activeRelease: null,
    managedSkills: {},
    agentTargets: [],
    sourceBindings: {},
    sourceBindingCandidates: {},
    workspacePreferences: {},
    contributionPreferences: {},
    verifiedReleaseIndexes: {},
    verifiedReleaseArtifacts: {},
    observations: {},
  };
}

export function migrateState(input) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) throw new GuideError('invalid_state', 'User state must be a JSON object.');
  const version = input.schemaVersion ?? 0;
  if (version > STATE_SCHEMA_VERSION) throw new GuideError('future_state_schema', `User state schema ${version} is newer than supported schema ${STATE_SCHEMA_VERSION}.`);
  if (version === 0) {
    input = {
      schemaVersion: 1,
      activeRelease: input.activeRelease ?? input.release ?? null,
      agentTargets: input.agentTargets ?? input.agents ?? [],
      sourceBindings: input.sourceBindings ?? {},
      workspaces: input.workspaces ?? {},
      contributionPreference: input.contributionPreference ?? 'ask',
    };
  }
  if (input.schemaVersion === 1) {
    const preferences = input.workspacePreferences ?? input.workspaces ?? {};
    const contributionPreferences = input.contributionPreferences ?? {};
    for (const key of Object.keys(preferences)) {
      if (!(key in contributionPreferences) && input.contributionPreference) contributionPreferences[key] = input.contributionPreference;
    }
    input = {
      schemaVersion: 2,
      activeRelease: input.activeRelease ?? null,
      managedSkills: input.managedSkills ?? [],
      agentTargets: input.agentTargets ?? [],
      sourceBindings: input.sourceBindings ?? {},
      workspacePreferences: preferences,
      contributionPreferences,
      verifiedReleaseIndexes: input.verifiedReleaseIndexes ?? {},
      verifiedReleaseArtifacts: input.verifiedReleaseArtifacts ?? {},
      observations: input.observations ?? {},
    };
  }
  if (input.schemaVersion === 2) {
    if (!Array.isArray(input.managedSkills)) throw new GuideError('invalid_state', 'managedSkills must be an array in user state schema 2.');
    const managedSkillNames = new Set();
    for (const skill of input.managedSkills) {
      if (typeof skill !== 'string' || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(skill)) {
        throw new GuideError('invalid_state', 'managedSkills must contain only canonical skill-name strings in user state schema 2.');
      }
      if (managedSkillNames.has(skill)) throw new GuideError('invalid_state', `managedSkills contains duplicate skill name ${skill}.`);
      managedSkillNames.add(skill);
    }
    input = {
      ...input,
      schemaVersion: 3,
      managedSkills: Object.fromEntries([...managedSkillNames].map((skill) => [skill, {
        revision: null,
        digest: null,
        lastChangedIn: null,
      }])),
    };
  }
  if (input.schemaVersion === 3) {
    if (!input.sourceBindings || typeof input.sourceBindings !== 'object' || Array.isArray(input.sourceBindings)) {
      throw new GuideError('invalid_state', 'sourceBindings must be an object in user state schema 3.');
    }
    const promoted = {};
    const candidates = {};
    for (const [legacyKey, legacy] of Object.entries(input.sourceBindings ?? {})) {
      const binding = persistedSourceBinding(legacy);
      if (!binding) throw new GuideError('invalid_state', `Legacy sourceBindings.${legacyKey} cannot be migrated without repository, sourcePath, and exact commit.`);
      const records = candidates[binding.repository] ?? [];
      if (!records.some((record) => stableJson(record) === stableJson(binding))) records.push(binding);
      candidates[binding.repository] = records;
    }
    for (const [repository, records] of Object.entries(candidates)) {
      if (records.length === 1) promoted[repository] = records[0];
    }
    input = {
      ...input,
      schemaVersion: 4,
      sourceBindings: promoted,
      sourceBindingCandidates: Object.fromEntries(Object.entries(candidates).filter(([, records]) => records.length > 1)),
    };
  }
  const state = { ...emptyState(), ...input, schemaVersion: STATE_SCHEMA_VERSION };
  if (!Array.isArray(state.agentTargets)) throw new GuideError('invalid_state', 'agentTargets must be an array.');
  const normalizedAgentTargets = state.agentTargets.map((agent) => String(agent || '').trim().toLowerCase().replace(/[ _]+/g, '-'));
  if (normalizedAgentTargets.some((agent) => !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(agent))) {
    throw new GuideError('invalid_state', 'agentTargets must contain only pinned npx skills target identifiers.');
  }
  if (new Set(normalizedAgentTargets).size !== normalizedAgentTargets.length) {
    throw new GuideError('invalid_state', 'agentTargets must not contain duplicates.');
  }
  const sortedAgentTargets = [...normalizedAgentTargets].sort(compareOrdinalUtf8);
  if (stableJson(normalizedAgentTargets) !== stableJson(sortedAgentTargets)) {
    throw new GuideError('invalid_state', 'agentTargets must use canonical sorted order.');
  }
  state.agentTargets = sortedAgentTargets;
  for (const field of ['managedSkills', 'sourceBindings', 'sourceBindingCandidates', 'workspacePreferences', 'contributionPreferences', 'verifiedReleaseIndexes', 'verifiedReleaseArtifacts', 'observations']) {
    if (!state[field] || typeof state[field] !== 'object' || Array.isArray(state[field])) throw new GuideError('invalid_state', `${field} must be an object.`);
  }
  for (const [repository, binding] of Object.entries(state.sourceBindings)) validateSourceBinding(repository, binding);
  for (const [repository, records] of Object.entries(state.sourceBindingCandidates)) {
    if (!SOURCE_REPOSITORY_NAMES.has(repository.toLowerCase()) || !Array.isArray(records) || records.length < 2) {
      throw new GuideError('invalid_state', `sourceBindingCandidates.${repository} must contain at least two supported source bindings.`);
    }
    for (const binding of records) validateSourceBinding(repository, binding);
  }
  for (const [skill, record] of Object.entries(state.managedSkills)) {
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(skill)) throw new GuideError('invalid_state', `managedSkills contains invalid skill name ${skill}.`);
    if (!record || typeof record !== 'object' || Array.isArray(record)) throw new GuideError('invalid_state', `managedSkills.${skill} must be an object.`);
    const fields = Object.keys(record).sort();
    if (fields.join(',') !== 'digest,lastChangedIn,revision') throw new GuideError('invalid_state', `managedSkills.${skill} must contain only revision, digest, and lastChangedIn.`);
    if (record.revision !== null && (!Number.isInteger(record.revision) || record.revision < 1)) throw new GuideError('invalid_state', `managedSkills.${skill}.revision must be a positive integer or null.`);
    if (record.digest !== null && !/^sha256:[0-9a-f]{64}$/.test(record.digest)) throw new GuideError('invalid_state', `managedSkills.${skill}.digest must be a SHA-256 digest or null.`);
    if (record.lastChangedIn !== null && (!String(record.lastChangedIn).startsWith('v') || !parseSemVer(String(record.lastChangedIn).slice(1)))) {
      throw new GuideError('invalid_state', `managedSkills.${skill}.lastChangedIn must be an immutable vSemVer tag or null.`);
    }
    const unknown = record.revision === null && record.digest === null && record.lastChangedIn === null;
    const source = record.revision === null && record.digest !== null && record.lastChangedIn === null;
    const tagged = record.revision !== null && record.digest !== null && record.lastChangedIn !== null;
    if (!unknown && !source && !tagged) {
      throw new GuideError(
        'invalid_state',
        `managedSkills.${skill} must be a complete tagged record, a digest-only source record, or an all-null migrated record.`,
      );
    }
  }
  for (const [key, observation] of Object.entries(state.observations)) {
    if (!observation || typeof observation !== 'object' || Array.isArray(observation)) {
      throw new GuideError('invalid_state', `observations.${key} must be an object.`);
    }
    if (observation.schemaVersion !== 1 || typeof observation.observedAt !== 'string'
      || Number.isNaN(Date.parse(observation.observedAt))) {
      throw new GuideError('invalid_state', `observations.${key} must contain schemaVersion 1 and an ISO observedAt timestamp.`);
    }
    for (const forbidden of ['dirty', 'sourceBindings', 'sourceVerification', 'verificationStatus']) {
      if (Object.hasOwn(observation, forbidden)) {
        throw new GuideError('invalid_state', `observations.${key} must not persist volatile ${forbidden} data.`);
      }
    }
  }
  return state;
}

function persistedSourceBinding(input) {
  if (!input || typeof input !== 'object' || Array.isArray(input)) return null;
  const repository = SOURCE_REPOSITORY_NAMES.get(String(input.repository || 'Tairitsua/Monica').toLowerCase());
  if (!repository || !input.sourcePath || !input.commit) return null;
  return {
    repository,
    ref: String(input.ref || input.commit),
    commit: String(input.commit).toLowerCase(),
    sourcePath: normalizePath(String(input.sourcePath)),
    resolutionKind: input.resolutionKind || 'exact_commit',
    provenance: input.provenance ?? 'local-git',
  };
}

function validateSourceBinding(repositoryKey, input) {
  const repository = SOURCE_REPOSITORY_NAMES.get(repositoryKey.toLowerCase());
  if (!repository || repository !== repositoryKey) throw new GuideError('invalid_state', `Unsupported source binding repository ${repositoryKey}.`);
  if (!input || typeof input !== 'object' || Array.isArray(input)) throw new GuideError('invalid_state', `sourceBindings.${repositoryKey} must be an object.`);
  const fields = Object.keys(input).sort();
  const expected = ['commit', 'provenance', 'ref', 'repository', 'resolutionKind', 'sourcePath'];
  if (fields.join(',') !== expected.sort().join(',')) {
    throw new GuideError('invalid_state', `sourceBindings.${repositoryKey} must contain only repository, ref, commit, sourcePath, resolutionKind, and provenance.`);
  }
  if (input.repository !== repositoryKey) throw new GuideError('invalid_state', `sourceBindings.${repositoryKey}.repository must match its registry key.`);
  if (typeof input.ref !== 'string' || !input.ref) throw new GuideError('invalid_state', `sourceBindings.${repositoryKey}.ref must be non-empty.`);
  if (!/^[0-9a-f]{40}$/i.test(input.commit || '')) throw new GuideError('invalid_state', `sourceBindings.${repositoryKey}.commit must be an exact Git commit.`);
  if (typeof input.sourcePath !== 'string' || !path.isAbsolute(input.sourcePath)) throw new GuideError('invalid_state', `sourceBindings.${repositoryKey}.sourcePath must be absolute.`);
  if (!['exact_commit', 'exact_tag'].includes(input.resolutionKind)) throw new GuideError('invalid_state', `sourceBindings.${repositoryKey}.resolutionKind must be exact_commit or exact_tag.`);
  if (!(typeof input.provenance === 'string' || (input.provenance && typeof input.provenance === 'object' && !Array.isArray(input.provenance)))) {
    throw new GuideError('invalid_state', `sourceBindings.${repositoryKey}.provenance must describe local Git or resolver provenance.`);
  }
}

export function loadState(filePath) {
  return migrateState(readJson(filePath, emptyState()));
}

export function loadProjectConfig(workspace) {
  const filePath = path.join(workspace, '.monica', 'guide.json');
  const config = readJson(filePath, null);
  if (config === null) return { filePath, config: null, migration: null };
  const expectedFields = [
    'capabilities',
    'channel',
    'expectedCatalogRelease',
    'instructionBlockVersion',
    'managedClaudeImport',
    'profile',
    'schemaVersion',
  ];
  if (config.schemaVersion === 1) {
    const legacyFields = [...expectedFields, 'agentTargets'].sort(compareOrdinalUtf8);
    const actualFields = Object.keys(config).sort(compareOrdinalUtf8);
    if (stableJson(actualFields) !== stableJson(legacyFields)) {
      throw new GuideError('project_config_invalid', 'Legacy .monica/guide.json schema 1 contains unsupported fields.', { actualFields, expectedFields: legacyFields });
    }
    if (!Array.isArray(config.agentTargets) || config.agentTargets.some((agent) => typeof agent !== 'string'
      || !/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(agent))) {
      throw new GuideError('project_config_invalid', 'Legacy .monica/guide.json agentTargets must contain pinned npx skills target identifiers.');
    }
    const { agentTargets, ...shared } = config;
    return {
      filePath,
      config: { ...shared, schemaVersion: PROJECT_SCHEMA_VERSION },
      migration: {
        fromSchemaVersion: 1,
        toSchemaVersion: PROJECT_SCHEMA_VERSION,
        droppedAgentTargets: [...agentTargets],
        message: 'Legacy repository agent targets are ignored; user state is authoritative. The next approved workspace configuration mutation writes schema 2.',
      },
    };
  }
  if (config.schemaVersion !== PROJECT_SCHEMA_VERSION) throw new GuideError('project_schema_mismatch', `Unsupported .monica/guide.json schema ${config.schemaVersion}.`);
  const actualFields = Object.keys(config).sort(compareOrdinalUtf8);
  if (stableJson(actualFields) !== stableJson(expectedFields.sort(compareOrdinalUtf8))) {
    throw new GuideError(
      'project_config_invalid',
      '.monica/guide.json may contain only repository-shared profile, channel, capabilities, release, instruction, and Claude-import ownership fields.',
      { actualFields, expectedFields },
    );
  }
  return { filePath, config, migration: null };
}

export function atomicWrite(filePath, content, mode = 0o600) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true, mode: 0o700 });
  const temporary = path.join(path.dirname(filePath), `.${path.basename(filePath)}.${process.pid}.${crypto.randomBytes(6).toString('hex')}.tmp`);
  let descriptor;
  try {
    descriptor = fs.openSync(temporary, 'wx', mode);
    fs.writeFileSync(descriptor, content, 'utf8');
    fs.fsyncSync(descriptor);
    fs.closeSync(descriptor);
    descriptor = undefined;
    fs.renameSync(temporary, filePath);
    try {
      const directory = fs.openSync(path.dirname(filePath), 'r');
      fs.fsyncSync(directory);
      fs.closeSync(directory);
    } catch {
      // Some platforms do not allow fsync on directories.
    }
  } finally {
    if (descriptor !== undefined) fs.closeSync(descriptor);
    if (exists(temporary)) fs.unlinkSync(temporary);
  }
}

export function withFileLock(lockPath, callback) {
  fs.mkdirSync(path.dirname(lockPath), { recursive: true, mode: 0o700 });
  const token = crypto.randomBytes(24).toString('hex');
  let descriptor;
  try {
    descriptor = fs.openSync(lockPath, 'wx', 0o600);
  } catch (error) {
    if (error.code !== 'EEXIST') throw error;
    throw new GuideError('state_locked', `Another Monica Guide process owns ${lockPath}.`, inspectFileLock(lockPath));
  }
  try {
    fs.writeFileSync(descriptor, stableJson({ pid: process.pid, token, createdAt: new Date().toISOString() }, 2));
    fs.fsyncSync(descriptor);
    fs.closeSync(descriptor);
    descriptor = undefined;
    return callback();
  } finally {
    if (descriptor !== undefined) fs.closeSync(descriptor);
    try {
      const current = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
      if (current.token === token) fs.unlinkSync(lockPath);
    } catch {
      // Missing, replaced, or malformed lock files are never removed by a non-owner.
    }
  }
}

export function inspectFileLock(lockPath) {
  if (!exists(lockPath)) return { status: 'absent', path: lockPath };
  let owner;
  try {
    owner = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
  } catch (error) {
    return { status: 'malformed', path: lockPath, message: error.message };
  }
  if (!Number.isInteger(owner.pid) || owner.pid <= 0 || typeof owner.token !== 'string' || !/^[0-9a-f]{48}$/.test(owner.token)) {
    return { status: 'malformed', path: lockPath, owner };
  }
  let alive = null;
  try {
    process.kill(owner.pid, 0);
    alive = true;
  } catch (error) {
    if (error?.code === 'EPERM') alive = true;
    else if (error?.code === 'ESRCH') alive = false;
  }
  return { status: alive === false ? 'stale' : alive === true ? 'owned' : 'unknown', path: lockPath, owner };
}

export function externalCommandTimeout(value = undefined) {
  const selected = value ?? process.env.MONICA_GUIDE_EXTERNAL_TIMEOUT_MS ?? DEFAULT_EXTERNAL_COMMAND_TIMEOUT_MS;
  const timeout = typeof selected === 'number' ? selected : Number(selected);
  if (!Number.isInteger(timeout) || timeout < MIN_EXTERNAL_COMMAND_TIMEOUT_MS || timeout > MAX_EXTERNAL_COMMAND_TIMEOUT_MS) {
    throw new GuideError(
      'external_timeout_invalid',
      `External command timeout must be an integer from ${MIN_EXTERNAL_COMMAND_TIMEOUT_MS} to ${MAX_EXTERNAL_COMMAND_TIMEOUT_MS} milliseconds.`,
    );
  }
  return timeout;
}

export function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: options.cwd,
    encoding: 'utf8',
    windowsHide: true,
    maxBuffer: options.maxBuffer ?? 8 * 1024 * 1024,
    env: options.env ?? process.env,
    ...(options.timeout === undefined ? {} : { timeout: options.timeout, killSignal: 'SIGTERM' }),
  });
  return {
    status: result.status,
    stdout: result.stdout || '',
    stderr: result.stderr || '',
    error: result.error,
  };
}

export function runChecked(command, args, options = {}) {
  const result = run(command, args, options);
  if (result.error?.code === 'ETIMEDOUT') {
    throw new GuideError(
      options.timeoutCode || 'external_command_timeout',
      options.timeoutMessage || `${command} timed out after ${options.timeout} ms. Verify the local tool/cache and retry; offline operation never fetches a substitute.`,
      { command, args, timeoutMs: options.timeout },
    );
  }
  if (result.error || result.status !== 0) {
    const detail = (result.stderr || result.stdout || result.error?.message || 'unknown error').trim();
    throw new GuideError(options.code || 'command_failed', `${command} failed: ${detail}`, { command, args, exitCode: result.status });
  }
  return result;
}

export function git(workspace, args, required = false) {
  const result = run('git', ['-C', workspace, ...args]);
  if (result.status !== 0) {
    if (required) throw new GuideError('git_failed', `Git failed in ${workspace}: ${(result.stderr || result.stdout).trim()}`);
    return null;
  }
  return result.stdout.trim();
}

export function gitInfo(workspace, { includeDirty = true } = {}) {
  const root = git(workspace, ['rev-parse', '--show-toplevel']);
  if (!root) return null;
  const commit = git(root, ['rev-parse', 'HEAD']);
  const remoteNames = (git(root, ['remote']) || '').split(/\r?\n/).filter(Boolean);
  const remotes = Object.fromEntries(remoteNames.map((name) => [name, sanitizeRemote(git(root, ['remote', 'get-url', name]))]));
  const canonicalRemote = [
    ['origin', remotes.origin],
    ['upstream', remotes.upstream],
  ].find(([, remoteUrl]) => /^Tairitsua\/Monica(?:\.Docs)?$/i.test(canonicalRepository(remoteUrl) || ''));
  const remoteName = canonicalRemote?.[0] || (remotes.origin ? 'origin' : remotes.upstream ? 'upstream' : null);
  const remote = canonicalRemote?.[1] || (remoteName ? remotes[remoteName] : null);
  const status = includeDirty
    ? gitStatusSnapshot(root)
    : { dirty: false, digest: digest('') };
  return { root: path.resolve(root), commit, remote, remoteName, remotes, dirty: status.dirty, dirtyDigest: status.digest };
}

export function gitStatusSnapshot(workspace, { pathspec = [], sampleLimit = 0, command = 'git' } = {}) {
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'monica-guide-status-'));
  if (process.platform !== 'win32') fs.chmodSync(temporaryRoot, 0o700);
  try {
    const args = ['-C', workspace, 'status', '--porcelain=v1', '--untracked-files=normal'];
    if (pathspec.length) args.push('--', ...pathspec);
    const result = runToFile(command, args, path.join(temporaryRoot, 'status'));
    if (result.error || result.status !== 0) {
      throw new GuideError('git_status_failed', `Cannot inspect Git status in ${workspace}.`, {
        command,
        exitCode: result.status,
        error: result.error?.message || null,
        stderr: filePrefix(result.stderrPath),
      });
    }
    const size = fs.statSync(result.stdoutPath).size;
    const sample = sampleLimit > 0 ? statusLineSample(result.stdoutPath, sampleLimit) : { lines: [], truncated: false };
    return {
      dirty: size > 0,
      digest: fileDigest(result.stdoutPath),
      bytes: size,
      changes: sample.lines,
      truncated: sample.truncated,
    };
  } finally {
    fs.rmSync(temporaryRoot, { recursive: true, force: true });
  }
}

export function gitWorkspaceFingerprint(workspace) {
  const root = git(workspace, ['rev-parse', '--show-toplevel']);
  if (!root) return null;
  const head = git(workspace, ['rev-parse', 'HEAD']) || null;
  const temporaryRoot = fs.mkdtempSync(path.join(os.tmpdir(), 'monica-guide-git-'));
  if (process.platform !== 'win32') fs.chmodSync(temporaryRoot, 0o700);
  try {
    const staged = runToFile('git', ['-C', workspace, 'diff', '--binary', '--cached', '--', '.'], path.join(temporaryRoot, 'staged'));
    const worktree = runToFile('git', ['-C', workspace, 'diff', '--binary', '--', '.'], path.join(temporaryRoot, 'worktree'));
    const untrackedResult = runToFile('git', ['-C', workspace, 'ls-files', '--others', '--exclude-standard', '-z', '--', '.'], path.join(temporaryRoot, 'untracked'));
    if (staged.status !== 0 || worktree.status !== 0 || untrackedResult.status !== 0) throw new GuideError('git_fingerprint_failed', `Cannot fingerprint Git workspace ${workspace}.`);
    const prefix = git(workspace, ['rev-parse', '--show-prefix']) || '';
    const untracked = [];
    for (const listed of nulSeparatedFileEntries(untrackedResult.stdoutPath).sort(compareOrdinalUtf8)) {
      const relativeToRoot = prefix && !listed.startsWith(prefix) ? `${prefix}${listed}` : listed;
      const absolute = path.resolve(root, relativeToRoot);
      const relativeToWorkspace = path.relative(path.resolve(workspace), absolute);
      if (relativeToWorkspace.startsWith('..') || path.isAbsolute(relativeToWorkspace)) continue;
      const stat = fs.lstatSync(absolute);
      untracked.push({
        path: relativeToWorkspace.split(path.sep).join('/'),
        kind: stat.isSymbolicLink() ? 'symlink' : stat.isFile() ? 'file' : 'other',
        digest: stat.isSymbolicLink() ? digest(fs.readlinkSync(absolute)) : stat.isFile() ? fileDigest(absolute) : null,
      });
    }
    return digest({ head, staged: fileDigest(staged.stdoutPath), worktree: fileDigest(worktree.stdoutPath), untracked });
  } finally {
    fs.rmSync(temporaryRoot, { recursive: true, force: true });
  }
}

function runToFile(command, args, stdoutPath) {
  const stderrPath = `${stdoutPath}.err`;
  const stdout = fs.openSync(stdoutPath, 'wx', 0o600);
  const stderr = fs.openSync(stderrPath, 'wx', 0o600);
  let result;
  try {
    result = spawnSync(command, args, { stdio: ['ignore', stdout, stderr], windowsHide: true, env: process.env });
  } finally {
    fs.closeSync(stdout);
    fs.closeSync(stderr);
  }
  return { status: result.status, error: result.error, stdoutPath, stderrPath };
}

function filePrefix(filePath, byteLimit = 4096) {
  const descriptor = fs.openSync(filePath, 'r');
  try {
    const size = Math.min(fs.fstatSync(descriptor).size, byteLimit);
    const buffer = Buffer.alloc(size);
    fs.readSync(descriptor, buffer, 0, size, 0);
    return buffer.toString('utf8').trim();
  } finally {
    fs.closeSync(descriptor);
  }
}

function statusLineSample(filePath, lineLimit) {
  const byteLimit = 256 * 1024;
  const descriptor = fs.openSync(filePath, 'r');
  try {
    const fileSize = fs.fstatSync(descriptor).size;
    const size = Math.min(fileSize, byteLimit);
    const buffer = Buffer.alloc(size);
    fs.readSync(descriptor, buffer, 0, size, 0);
    const lines = buffer.toString('utf8').split(/\r?\n/).filter(Boolean);
    return {
      lines: lines.slice(0, lineLimit),
      truncated: fileSize > size || lines.length > lineLimit,
    };
  } finally {
    fs.closeSync(descriptor);
  }
}

function nulSeparatedFileEntries(filePath) {
  const descriptor = fs.openSync(filePath, 'r');
  const buffer = Buffer.allocUnsafe(64 * 1024);
  const entries = [];
  let remainder = Buffer.alloc(0);
  try {
    let length;
    while ((length = fs.readSync(descriptor, buffer, 0, buffer.length, null)) > 0) {
      const data = Buffer.concat([remainder, buffer.subarray(0, length)]);
      let start = 0;
      for (let index = data.indexOf(0, start); index >= 0; index = data.indexOf(0, start)) {
        if (index > start) entries.push(data.subarray(start, index).toString('utf8'));
        start = index + 1;
      }
      remainder = data.subarray(start);
    }
    if (remainder.length) entries.push(remainder.toString('utf8'));
  } finally {
    fs.closeSync(descriptor);
  }
  return entries;
}

export function formatManagedSkillVersion(record, release, { fullSource = false } = {}) {
  if (!record || record.digest === null) return 'unknown';
  if (record.revision !== null) return `r${record.revision}`;
  const commit = release?.commit || String(release?.id || '').replace(/^source:/, '') || 'unknown';
  return `source@${fullSource ? commit : commit.slice(0, 12)}`;
}

export function sanitizeRemote(remote) {
  if (!remote) return null;
  return remote.replace(/https?:\/\/[^/@]+@/i, 'https://').replace(/[?#].*$/, '').replace(/\.git$/, '');
}

export function canonicalRepository(remote) {
  if (!remote) return null;
  const normalized = sanitizeRemote(remote).trim().replaceAll('\\', '/');
  const scp = normalized.match(/^(?:[^@\s/:]+@)?([^\s/:]+):([^/\s]+\/[^/\s]+)$/);
  if (scp) return scp[1].toLowerCase() === 'github.com' ? scp[2] : null;
  let parsed;
  try {
    parsed = new URL(/^[a-z][a-z\d+.-]*:\/\//i.test(normalized) ? normalized : `https://${normalized}`);
  } catch {
    return null;
  }
  if (parsed.hostname.toLowerCase() !== 'github.com') return null;
  const segments = parsed.pathname.split('/').filter(Boolean);
  return segments.length === 2 ? `${segments[0]}/${segments[1]}` : null;
}

export function walkFiles(root, { maxDepth = 7, include = () => true, ignoredDirectories = [] } = {}) {
  const output = [];
  const ignored = new Set([
    '.git', '.hg', '.svn', '.tmp', '.gitnexus', '.vs', '.next', '.nuget',
    'node_modules', 'bin', 'obj', 'artifacts', 'dist', 'coverage', 'TestResults', 'packages',
    ...ignoredDirectories,
  ]);
  function visit(directory, depth) {
    if (depth > maxDepth) return;
    let entries;
    try {
      entries = fs.readdirSync(directory, { withFileTypes: true });
    } catch {
      return;
    }
    for (const entry of entries) {
      if (entry.isSymbolicLink()) continue;
      const current = path.join(directory, entry.name);
      if (entry.isDirectory()) {
        if (!ignored.has(entry.name)) visit(current, depth + 1);
      } else if (entry.isFile() && include(current, entry.name)) {
        output.push(current);
      }
    }
  }
  visit(root, 0);
  return output.sort();
}

export function textDiff(filePath, before, after) {
  if (before === after) return '';
  const oldLines = before === null ? [] : before.replace(/\n$/, '').split('\n');
  const newLines = after === null ? [] : after.replace(/\n$/, '').split('\n');
  return [
    `--- ${before === null ? '/dev/null' : filePath}`,
    `+++ ${after === null ? '/dev/null' : filePath}`,
    '@@ complete file @@',
    ...oldLines.map((line) => `-${line}`),
    ...newLines.map((line) => `+${line}`),
  ].join('\n');
}

function instructionMarkers(markerOrMarkers = 'monica-guide:managed', version = null) {
  if (markerOrMarkers && typeof markerOrMarkers === 'object') {
    return { start: markerOrMarkers.start, end: markerOrMarkers.end };
  }
  return {
    start: `<!-- ${markerOrMarkers}:start${version === null ? '' : ` v${version}`} -->`,
    end: `<!-- ${markerOrMarkers}:end -->`,
  };
}

export function instructionState(text, markerOrMarkers = 'monica-guide:managed') {
  const markers = instructionMarkers(markerOrMarkers);
  const startPattern = new RegExp(escapeRegex(markers.start), 'g');
  const endPattern = new RegExp(escapeRegex(markers.end), 'g');
  const starts = [...text.matchAll(startPattern)];
  const ends = [...text.matchAll(endPattern)];
  if (starts.length === 0 && ends.length === 0) return { status: 'absent', count: 0, version: null };
  const occupiesLine = (match) => {
    const before = match.index === 0 || text[match.index - 1] === '\n';
    const afterIndex = match.index + match[0].length;
    const after = afterIndex === text.length
      || text[afterIndex] === '\n'
      || (text[afterIndex] === '\r' && text[afterIndex + 1] === '\n');
    return before && after;
  };
  if (starts.length !== 1 || ends.length !== 1 || starts[0].index >= ends[0].index || !occupiesLine(starts[0]) || !occupiesLine(ends[0])) {
    return { status: 'malformed', count: Math.max(starts.length, ends.length), version: null };
  }
  return {
    status: 'valid',
    count: 1,
    version: null,
    start: starts[0].index,
    end: ends[0].index + ends[0][0].length,
    body: text.slice(starts[0].index + starts[0][0].length, ends[0].index),
    markers,
  };
}

export function upsertInstructionBlock(text, body, { marker = 'monica-guide:managed', markers = null, version = 1 } = {}) {
  const selectedMarkers = markers || instructionMarkers(marker, version);
  const state = instructionState(text, selectedMarkers);
  if (state.status === 'malformed') throw new GuideError('malformed_instruction_block', 'Refusing to modify malformed or duplicate Monica Guide instruction markers.');
  const newline = text.includes('\r\n') ? '\r\n' : '\n';
  const renderedBody = body.trim().replace(/\r?\n/g, newline);
  const block = `${selectedMarkers.start}${newline}${renderedBody}${newline}${selectedMarkers.end}`;
  if (state.status === 'absent') {
    if (!text) return `${block}${newline}`;
    const separator = text.endsWith(`${newline}${newline}`) ? '' : text.endsWith(newline) ? newline : `${newline}${newline}`;
    return `${text}${separator}${block}${newline}`;
  }
  return `${text.slice(0, state.start)}${block}${text.slice(state.end)}`;
}

export function removeInstructionBlock(text, markerOrMarkers = 'monica-guide:managed') {
  const state = instructionState(text, markerOrMarkers);
  if (state.status === 'malformed') throw new GuideError('malformed_instruction_block', 'Refusing to modify malformed or duplicate Monica Guide instruction markers.');
  if (state.status === 'absent') return text;
  return `${text.slice(0, state.start)}${text.slice(state.end)}`;
}

export function claudeImportState(text) {
  const ranges = [];
  let start = 0;
  while (start < text.length) {
    const lineFeed = text.indexOf('\n', start);
    const end = lineFeed === -1 ? text.length : lineFeed + 1;
    const contentEnd = lineFeed === -1 ? end : text[lineFeed - 1] === '\r' ? lineFeed - 1 : lineFeed;
    if (text.slice(start, contentEnd).trim() === '@AGENTS.md') ranges.push({ start, end });
    start = end;
  }
  const count = ranges.length;
  return {
    status: count === 0 ? 'absent' : count === 1 ? 'valid' : 'duplicate',
    count,
    ...(count === 1 ? ranges[0] : {}),
  };
}

export function ensureClaudeImport(text) {
  const state = claudeImportState(text);
  if (state.status === 'duplicate') throw new GuideError('duplicate_claude_import', 'Refusing to modify CLAUDE.md because it contains duplicate @AGENTS.md imports.');
  if (state.status === 'valid') return text;
  const newline = text.includes('\r\n') ? '\r\n' : '\n';
  if (!text) return `@AGENTS.md${newline}`;
  const separator = text.endsWith(`${newline}${newline}`) ? '' : text.endsWith(newline) ? newline : `${newline}${newline}`;
  return `${text}${separator}@AGENTS.md${newline}`;
}

export function removeClaudeImport(text) {
  const state = claudeImportState(text);
  if (state.status === 'duplicate') throw new GuideError('duplicate_claude_import', 'Refusing to remove duplicated @AGENTS.md imports from CLAUDE.md.');
  if (state.status === 'absent') return text;
  return `${text.slice(0, state.start)}${text.slice(state.end)}`;
}

export function shellDisplay(command, args) {
  const quote = (value) => /^[A-Za-z0-9_./:@=-]+$/.test(value) ? value : JSON.stringify(value);
  return [command, ...args].map(quote).join(' ');
}

function escapeRegex(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}
