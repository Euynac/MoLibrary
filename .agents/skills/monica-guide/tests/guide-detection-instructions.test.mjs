import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';

import { loadCatalog, renderManagedInstructions } from '../scripts/guide-catalog.mjs';
import { detectRepository, profileRepositoryIssues, workspaceDetection } from '../scripts/guide-detect.mjs';
import { instructionDiagnostics, resolveNestedInstructionSelections } from '../scripts/guide-plan.mjs';
import { parseArguments } from '../scripts/monica-guide.mjs';
import {
  claudeImportState,
  ensureClaudeImport,
  gitInfo,
  instructionState,
  removeClaudeImport,
  removeInstructionBlock,
  upsertInstructionBlock,
} from '../scripts/guide-shared.mjs';

const TEST_ROOT = path.dirname(fileURLToPath(import.meta.url));
const SKILL_ROOT = path.dirname(TEST_ROOT);
const CATALOG_PATH = path.join(SKILL_ROOT, 'assets', 'default-catalog.json');
const INDEX_PATH = path.join(SKILL_ROOT, 'assets', 'default-index.json');

function temporaryDirectory(t) {
  const directory = fs.mkdtempSync(path.join(os.tmpdir(), 'monica-guide-detection-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  return directory;
}

function write(filePath, content = '') {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, content, 'utf8');
}

function initializeGit(repository, { origin, upstream } = {}) {
  write(path.join(repository, 'README.md'), 'fixture\n');
  execFileSync('git', ['init', '-q', repository]);
  execFileSync('git', ['-C', repository, 'config', 'user.email', 'guide-test@example.invalid']);
  execFileSync('git', ['-C', repository, 'config', 'user.name', 'Guide Test']);
  if (origin) execFileSync('git', ['-C', repository, 'remote', 'add', 'origin', origin]);
  if (upstream) execFileSync('git', ['-C', repository, 'remote', 'add', 'upstream', upstream]);
  execFileSync('git', ['-C', repository, 'add', '.']);
  execFileSync('git', ['-C', repository, 'commit', '-qm', 'fixture']);
}

test('canonical upstream identity is accepted only at the real Git root', (t) => {
  const repository = temporaryDirectory(t);
  initializeGit(repository, {
    origin: 'https://github.com/example/Monica-fork.git',
    upstream: 'git@github.com:Tairitsua/Monica.git',
  });
  const info = gitInfo(repository, { includeDirty: false });
  assert.equal(info.remoteName, 'upstream');
  assert.equal(detectRepository(repository).identity, 'Tairitsua/Monica');
  assert.deepEqual(profileRepositoryIssues(repository, 'framework-contributor', detectRepository(repository)), []);

  const child = path.join(repository, 'src');
  fs.mkdirSync(child);
  assert.ok(profileRepositoryIssues(child, 'framework-contributor', detectRepository(child))
    .some((issue) => issue.code === 'framework_repository_root_required'));

  if (process.platform !== 'win32') {
    const alias = `${repository}-alias`;
    fs.symlinkSync(repository, alias, 'dir');
    t.after(() => fs.rmSync(alias, { recursive: true, force: true }));
    assert.deepEqual(profileRepositoryIssues(alias, 'framework-contributor', detectRepository(alias)), []);
  }
});

test('canonical identity requires github.com as the actual origin or upstream host', (t) => {
  for (const remote of [
    'https://evil.example/github.com/Tairitsua/Monica.git',
    'git@gitlab.example:github.com/Tairitsua/Monica.git',
  ]) {
    const repository = temporaryDirectory(t);
    initializeGit(repository, { origin: remote });
    assert.equal(detectRepository(repository).identity, null);
    assert.ok(profileRepositoryIssues(repository, 'framework-contributor', detectRepository(repository))
      .some((issue) => issue.code === 'framework_repository_required'));
  }

  const mirrorOnly = temporaryDirectory(t);
  initializeGit(mirrorOnly);
  execFileSync('git', ['-C', mirrorOnly, 'remote', 'add', 'mirror', 'https://github.com/Tairitsua/Monica.git']);
  const info = gitInfo(mirrorOnly, { includeDirty: false });
  assert.equal(info.remoteName, null);
  assert.equal(info.remote, null);
  assert.equal(detectRepository(mirrorOnly).identity, null);
});

test('framework root Version is the final exact-version fallback and skill projections are ignored', (t) => {
  const repository = temporaryDirectory(t);
  write(path.join(repository, 'Monica.slnx'));
  fs.mkdirSync(path.join(repository, 'Monica.Core'));
  write(path.join(repository, 'Directory.Build.props'), '<Project><PropertyGroup><Version>9.8.7-preview.2</Version></PropertyGroup></Project>\n');
  write(path.join(repository, 'skills', 'fixture', 'Fake.razor'), '<MudButton />\n');
  write(path.join(repository, '.agents', 'skills', 'fixture', 'Fake.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.UI" Version="1.0.0" /></ItemGroup></Project>\n');
  initializeGit(repository, { origin: 'https://github.com/Tairitsua/Monica.git' });

  const detection = workspaceDetection(repository);
  assert.equal(detection.frameworkVersion.version, '9.8.7-preview.2');
  assert.equal(detection.frameworkVersion.tier, 'framework-root');
  assert.deepEqual(detection.repository.capabilities, []);
});

test('characteristic-only docs inference requires both locales and the exact frontend path', (t) => {
  const repository = temporaryDirectory(t);
  fs.mkdirSync(path.join(repository, 'docs', 'en-US'), { recursive: true });
  fs.mkdirSync(path.join(repository, 'docs', 'zh-CN'), { recursive: true });
  assert.notEqual(detectRepository(repository).candidateProfile, 'docs-contributor');
  fs.mkdirSync(path.join(repository, 'frontend', 'monica-docs-web'), { recursive: true });
  const detection = detectRepository(repository);
  assert.equal(detection.candidateProfile, 'docs-contributor');
  assert.equal(detection.confidence, 'characteristic');
});

test('managed instruction helpers preserve every surrounding byte', () => {
  const markers = { start: '<!-- monica-guide:managed:start -->', end: '<!-- monica-guide:managed:end -->' };
  const prefix = 'title  \r\n\r\n';
  const suffix = '\r\n\r\ntail  \r\n';
  const original = `${prefix}${markers.start}\r\nold\r\n${markers.end}${suffix}`;
  const updated = upsertInstructionBlock(original, 'new\nbody', { markers });
  assert.ok(updated.startsWith(prefix));
  assert.ok(updated.endsWith(suffix));
  assert.equal(removeInstructionBlock(updated, markers), `${prefix}${suffix}`);
  assert.equal(upsertInstructionBlock('head\n\n', 'body', { markers }), `head\n\n${markers.start}\nbody\n${markers.end}\n`);
  assert.throws(() => upsertInstructionBlock(`x ${markers.start}\nbody\n${markers.end}`, 'new', { markers }), /malformed/i);

  const claude = 'before  \r\n\r\n@AGENTS.md\r\nafter  \r\n';
  assert.equal(ensureClaudeImport(claude), claude);
  assert.equal(removeClaudeImport(claude), 'before  \r\n\r\nafter  \r\n');
  assert.equal(ensureClaudeImport('before\n\n'), 'before\n\n@AGENTS.md\n');
});

test('instruction diagnostics validate body, block version, and Claude import', (t) => {
  const workspace = temporaryDirectory(t);
  const { catalog } = loadCatalog({ catalogPath: CATALOG_PATH, indexPath: INDEX_PATH });
  const config = {
    profile: 'application',
    channel: 'stable',
    capabilities: [],
    agentTargets: ['codex', 'claude-code'],
    instructionBlockVersion: catalog.managedInstructions.version,
    expectedCatalogRelease: { id: 'v1.2.3' },
  };
  const body = renderManagedInstructions(catalog, { profile: config.profile, channel: config.channel, release: config.expectedCatalogRelease, capabilities: [] });
  write(path.join(workspace, 'AGENTS.md'), upsertInstructionBlock('user\r\n\r\n', body, { markers: catalog.managedInstructions.markers }));
  write(path.join(workspace, 'CLAUDE.md'), '@AGENTS.md\r\n');
  assert.deepEqual(instructionDiagnostics(workspace, catalog, config).issues, []);

  write(path.join(workspace, 'AGENTS.md'), upsertInstructionBlock('', 'stale', { markers: catalog.managedInstructions.markers }));
  const stale = instructionDiagnostics(workspace, catalog, { ...config, instructionBlockVersion: 0 });
  assert.ok(stale.issues.some((issue) => issue.code === 'instruction_body_mismatch'));
  assert.ok(stale.issues.some((issue) => issue.code === 'instruction_block_version_mismatch'));
  write(path.join(workspace, 'CLAUDE.md'), 'no import\n');
  assert.ok(instructionDiagnostics(workspace, catalog, config).issues.some((issue) => issue.code === 'claude_import_missing'));
  write(path.join(workspace, 'CLAUDE.md'), '@AGENTS.md\n@AGENTS.md\n');
  assert.equal(claudeImportState(fs.readFileSync(path.join(workspace, 'CLAUDE.md'), 'utf8')).status, 'duplicate');
  assert.ok(instructionDiagnostics(workspace, catalog, config).issues.some((issue) => issue.code === 'duplicate_claude_import'));
});

test('duplicate root Claude imports are diagnosed before initialization', (t) => {
  const workspace = temporaryDirectory(t);
  const { catalog } = loadCatalog({ catalogPath: CATALOG_PATH, indexPath: INDEX_PATH });
  write(path.join(workspace, 'CLAUDE.md'), '@AGENTS.md\n@AGENTS.md\n');
  const diagnostics = instructionDiagnostics(workspace, catalog);
  assert.equal(diagnostics.status, 'invalid');
  assert.ok(diagnostics.issues.some((issue) => issue.code === 'duplicate_claude_import'));
});

test('nested instruction selection is explicit, bounded, deterministic, and rejects symlinks', (t) => {
  const workspace = temporaryDirectory(t);
  const agents = path.join(workspace, 'a', 'AGENTS.md');
  const claude = path.join(workspace, 'b', 'CLAUDE.md');
  write(agents, 'a\n');
  write(claude, 'b\n');
  const detection = { nestedInstructions: [claude, agents] };
  assert.deepEqual(resolveNestedInstructionSelections(workspace, detection, []), []);
  assert.deepEqual(
    resolveNestedInstructionSelections(workspace, detection, ['b/CLAUDE.md', 'a/AGENTS.md']).map((entry) => entry.relative),
    ['a/AGENTS.md', 'b/CLAUDE.md'],
  );
  assert.throws(() => resolveNestedInstructionSelections(workspace, detection, ['../AGENTS.md']), /traverse/i);
  assert.throws(() => resolveNestedInstructionSelections(workspace, detection, ['AGENTS.md']), /nested/i);
  if (process.platform !== 'win32') {
    const link = path.join(workspace, 'c', 'AGENTS.md');
    fs.mkdirSync(path.dirname(link));
    fs.symlinkSync(agents, link);
    assert.throws(
      () => resolveNestedInstructionSelections(workspace, { nestedInstructions: [link] }, ['c/AGENTS.md']),
      (error) => error.code === 'nested_instruction_symlink_forbidden',
    );
  }
});

test('nested instruction discovery has no undocumented depth cutoff', (t) => {
  const workspace = temporaryDirectory(t);
  const nested = path.join(workspace, ...Array.from({ length: 10 }, (_, index) => `level-${index}`), 'AGENTS.md');
  write(nested, 'deep\n');
  assert.ok(workspaceDetection(workspace).nestedInstructions.includes(nested));
});

test('CLI collects repeated nested instruction selections', () => {
  const parsed = parseArguments(['configure', '--nested-instruction', 'one/AGENTS.md', '--nested-instruction', 'two/CLAUDE.md']);
  assert.deepEqual(parsed.options.nestedInstructions, ['one/AGENTS.md', 'two/CLAUDE.md']);
  assert.equal(instructionState('plain text').status, 'absent');
});

test('CLI accepts repeatable targeted skills only for update', () => {
  const parsed = parseArguments(['update', '--skill', 'monica-guide', '--skill', 'monica-application']);
  assert.deepEqual(parsed.options.skills, ['monica-guide', 'monica-application']);
  assert.throws(
    () => parseArguments(['configure', '--skill', 'monica-guide']),
    (error) => error.code === 'targeted_skill_intent_invalid',
  );
});
