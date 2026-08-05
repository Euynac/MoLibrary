import crypto from 'node:crypto';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { spawnSync } from 'node:child_process';

export const STATE_SCHEMA_VERSION = 3;
export const PROJECT_SCHEMA_VERSION = 1;

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
  return exists(filePath) ? digest(fs.readFileSync(filePath)) : null;
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
  return digest(`${identity || 'local'}\n${path.resolve(workspace)}`).slice(7, 31);
}

export function emptyState() {
  return {
    schemaVersion: STATE_SCHEMA_VERSION,
    activeRelease: null,
    managedSkills: {},
    agentTargets: [],
    sourceBindings: {},
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
  const state = { ...emptyState(), ...input, schemaVersion: STATE_SCHEMA_VERSION };
  if (!Array.isArray(state.agentTargets)) throw new GuideError('invalid_state', 'agentTargets must be an array.');
  for (const field of ['managedSkills', 'sourceBindings', 'workspacePreferences', 'contributionPreferences', 'verifiedReleaseIndexes', 'verifiedReleaseArtifacts', 'observations']) {
    if (!state[field] || typeof state[field] !== 'object' || Array.isArray(state[field])) throw new GuideError('invalid_state', `${field} must be an object.`);
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
  return state;
}

export function loadState(filePath) {
  return migrateState(readJson(filePath, emptyState()));
}

export function loadProjectConfig(workspace) {
  const filePath = path.join(workspace, '.monica', 'guide.json');
  const config = readJson(filePath, null);
  if (config === null) return { filePath, config: null };
  if (config.schemaVersion !== PROJECT_SCHEMA_VERSION) throw new GuideError('project_schema_mismatch', `Unsupported .monica/guide.json schema ${config.schemaVersion}.`);
  return { filePath, config };
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
  let descriptor;
  try {
    descriptor = fs.openSync(lockPath, 'wx', 0o600);
  } catch (error) {
    if (error.code !== 'EEXIST') throw error;
    let ownerAlive = false;
    let ownerKnownDead = false;
    try {
      const owner = JSON.parse(fs.readFileSync(lockPath, 'utf8'));
      if (Number.isInteger(owner.pid) && owner.pid > 0) {
        try {
          process.kill(owner.pid, 0);
          ownerAlive = true;
        } catch (ownerError) {
          if (ownerError?.code === 'EPERM') ownerAlive = true;
          else if (ownerError?.code === 'ESRCH') ownerKnownDead = true;
        }
      }
    } catch { /* Malformed locks fail closed below. */ }
    if (ownerAlive || !ownerKnownDead) throw new GuideError('state_locked', `Another Monica Guide process owns ${lockPath}.`);
    fs.unlinkSync(lockPath);
    descriptor = fs.openSync(lockPath, 'wx', 0o600);
  }
  try {
    fs.writeFileSync(descriptor, stableJson({ pid: process.pid, createdAt: new Date().toISOString() }, 2));
    fs.closeSync(descriptor);
    descriptor = undefined;
    return callback();
  } finally {
    if (descriptor !== undefined) fs.closeSync(descriptor);
    if (exists(lockPath)) fs.unlinkSync(lockPath);
  }
}

export function run(command, args, options = {}) {
  const result = spawnSync(command, args, {
    cwd: options.cwd,
    encoding: 'utf8',
    windowsHide: true,
    maxBuffer: options.maxBuffer ?? 8 * 1024 * 1024,
    env: options.env ?? process.env,
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
  const dirtyOutput = includeDirty ? (git(root, ['status', '--porcelain=v1', '--untracked-files=normal']) ?? '') : '';
  return { root: path.resolve(root), commit, remote, remoteName, remotes, dirty: dirtyOutput.length > 0, dirtyDigest: digest(dirtyOutput) };
}

export function gitWorkspaceFingerprint(workspace) {
  const root = git(workspace, ['rev-parse', '--show-toplevel']);
  if (!root) return null;
  const head = git(workspace, ['rev-parse', 'HEAD']) || null;
  const staged = run('git', ['-C', workspace, 'diff', '--binary', '--cached', '--', '.']);
  const worktree = run('git', ['-C', workspace, 'diff', '--binary', '--', '.']);
  const untrackedResult = run('git', ['-C', workspace, 'ls-files', '--others', '--exclude-standard', '-z', '--', '.']);
  if (staged.status !== 0 || worktree.status !== 0 || untrackedResult.status !== 0) throw new GuideError('git_fingerprint_failed', `Cannot fingerprint Git workspace ${workspace}.`);
  const prefix = git(workspace, ['rev-parse', '--show-prefix']) || '';
  const untracked = [];
  for (const listed of untrackedResult.stdout.split('\0').filter(Boolean).sort()) {
    const relativeToRoot = prefix && !listed.startsWith(prefix) ? `${prefix}${listed}` : listed;
    const absolute = path.resolve(root, relativeToRoot);
    const relativeToWorkspace = path.relative(path.resolve(workspace), absolute);
    if (relativeToWorkspace.startsWith('..') || path.isAbsolute(relativeToWorkspace)) continue;
    const stat = fs.lstatSync(absolute);
    untracked.push({
      path: relativeToWorkspace.split(path.sep).join('/'),
      kind: stat.isSymbolicLink() ? 'symlink' : stat.isFile() ? 'file' : 'other',
      digest: stat.isSymbolicLink() ? digest(fs.readlinkSync(absolute)) : stat.isFile() ? digest(fs.readFileSync(absolute)) : null,
    });
  }
  return digest({ head, staged: digest(staged.stdout), worktree: digest(worktree.stdout), untracked });
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
