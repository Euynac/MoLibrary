import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { once } from 'node:events';
import test from 'node:test';
import { fileURLToPath } from 'node:url';
import { execFileSync, spawn, spawnSync } from 'node:child_process';
import { buildPlan, applyPlan } from '../scripts/guide-plan.mjs';
import { doctorGlobal, inspectEnvironment, inspectGlobalEnvironment, doctor, listSourceBindings, resolveSourceBinding } from '../scripts/guide-doctor.mjs';
import { loadCatalog, loadReleaseArtifacts, loadReleaseIndex, renderManagedInstructions, resolveProfileClosure, validateIndex } from '../scripts/guide-catalog.mjs';
import { detectFrameworkVersion } from '../scripts/guide-detect.mjs';
import { targetSkillRecord, verifyInstalledSkill } from '../scripts/guide-installation.mjs';
import { resolveCachedSource, verifyLocalSource } from '../scripts/guide-source.mjs';
import { GuideError, digest, emptyState, gitStatusSnapshot, gitWorkspaceFingerprint, loadState, normalizePath, parseSemVer, semverChannel, stableJson, upsertInstructionBlock, withFileLock, workspaceKey } from '../scripts/guide-shared.mjs';
import { globalStatusEnvelope, main, parseArguments, renderStatus, statusEnvelope } from '../scripts/monica-guide.mjs';

const TEST_ROOT = path.dirname(fileURLToPath(import.meta.url));
const SKILL_ROOT = path.dirname(TEST_ROOT);
const SKILLS_ROOT = path.dirname(SKILL_ROOT);
const REPOSITORY_ROOT = path.dirname(SKILLS_ROOT);
const CATALOG_PATH = path.join(SKILL_ROOT, 'assets', 'default-catalog.json');
const INDEX_PATH = path.join(SKILL_ROOT, 'assets', 'default-index.json');
const RELEASE_INDEX_PATH = path.join(REPOSITORY_ROOT, '.monica', 'agent-skill-index.json');
const GUIDE_ENTRYPOINT = path.join(SKILL_ROOT, 'scripts', 'monica-guide.mjs');
const GUIDE_SHARED_URL = new URL('../scripts/guide-shared.mjs', import.meta.url).href;
const CATALOG_DIGEST = digest(fs.readFileSync(CATALOG_PATH));
const COMMIT = 'a'.repeat(40);
const TAG = 'v1.2.3';
const VERSION = '1.2.3';

function temporaryDirectory(t) {
  const base = process.platform === 'win32' ? os.tmpdir() : '/tmp';
  const directory = fs.mkdtempSync(path.join(base, 'monica-guide-test-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  return directory;
}

function write(filePath, content) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  fs.writeFileSync(filePath, content, 'utf8');
}

function writeResolver(filePath, sourcePath, {
  actualCommit = COMMIT,
  expectedCommit = COMMIT,
  ref = COMMIT,
} = {}) {
  write(filePath, `import json\nprint(json.dumps({"status":"ok","source_path":${JSON.stringify(sourcePath)},"verification_state":"verified","resolution_kind":"exact_commit","repository":{"id":"repo","canonical_name":"Tairitsua/Monica"},"artifact":{"id":"artifact","kind":"github_archive","ref":${JSON.stringify(ref)},"actual_commit":${JSON.stringify(actualCommit)},"expected_commit":${JSON.stringify(expectedCommit)}}}))\n`);
}

function applicationWorkspace(root, version = VERSION) {
  const workspace = path.join(root, 'application');
  fs.mkdirSync(workspace, { recursive: true });
  write(path.join(workspace, 'App.csproj'), `<Project Sdk="Microsoft.NET.Sdk"><ItemGroup><PackageReference Include="Monica.Core" Version="${version}" /></ItemGroup></Project>\n`);
  return workspace;
}

function emptyGitWorkspace(root, name = 'empty-repository') {
  const workspace = path.join(root, name);
  fs.mkdirSync(workspace, { recursive: true });
  execFileSync('git', ['init', '-q', workspace]);
  return workspace;
}

function initializeGit(repository, remote = 'https://github.com/Tairitsua/Monica.git') {
  execFileSync('git', ['init', '-q', repository]);
  execFileSync('git', ['-C', repository, 'config', 'user.email', 'guide-test@example.invalid']);
  execFileSync('git', ['-C', repository, 'config', 'user.name', 'Guide Test']);
  if (remote) execFileSync('git', ['-C', repository, 'remote', 'add', 'origin', remote]);
  execFileSync('git', ['-C', repository, 'add', '.']);
  execFileSync('git', ['-C', repository, 'commit', '-qm', 'fixture']);
  return execFileSync('git', ['-C', repository, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
}

function releaseDiscoveryFetch(index = releaseIndex()) {
  const assets = releaseFetch(index);
  return async (url) => {
    if (url === 'https://api.github.com/repos/Tairitsua/Monica/releases/latest') {
      const payload = { tag_name: index.channels.stable, draft: false, prerelease: false, assets: [{ name: 'agent-skill-index.json' }] };
      return { ok: true, status: 200, url, text: async () => JSON.stringify(payload) };
    }
    if (url === 'https://api.github.com/repos/Tairitsua/Monica/releases?per_page=30') {
      const payload = index.channels.preview
        ? [{ tag_name: index.channels.preview, draft: false, prerelease: true, assets: [{ name: 'agent-skill-index.json' }] }]
        : [];
      return { ok: true, status: 200, url, text: async () => JSON.stringify(payload) };
    }
    return assets(url);
  };
}

let cachedSkillContracts = null;
function skillFileContracts() {
  if (cachedSkillContracts) return cachedSkillContracts;
  const catalog = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8'));
  const files = {};
  const skillDigests = {};
  for (const [name, entry] of Object.entries(catalog.skills)) {
    if (entry.ownership !== 'monica' || entry.managed !== true) continue;
    const root = path.join(SKILLS_ROOT, name);
    const visit = (directory) => {
      for (const child of fs.readdirSync(directory, { withFileTypes: true })) {
        const absolute = path.join(directory, child.name);
        if (child.isDirectory()) visit(absolute);
        else if (child.isFile()) files[path.relative(path.dirname(SKILLS_ROOT), absolute).split(path.sep).join('/')] = digest(fs.readFileSync(absolute));
      }
    };
    visit(root);
    const prefix = `skills/${name}/`;
    const fileManifest = Object.entries(files).filter(([filePath]) => filePath.startsWith(prefix))
      .map(([filePath, hash]) => [filePath.slice(prefix.length), hash])
      .sort(([left], [right]) => Buffer.compare(Buffer.from(left, 'utf8'), Buffer.from(right, 'utf8')))
      .map(([relative, hash]) => `${hash.slice(7)}  ${relative}\n`).join('');
    skillDigests[name] = digest(fileManifest);
  }
  cachedSkillContracts = { files, skillDigests };
  return cachedSkillContracts;
}

function releaseIndex({
  version = VERSION,
  tag = TAG,
  commit = COMMIT,
  publishedAt = '2026-08-05T00:00:00Z',
  contracts = skillFileContracts(),
  skillRevisions = null,
  skillLastChangedIn = null,
} = {}) {
  const assetBaseUrl = `https://github.com/Tairitsua/Monica/releases/download/${tag}`;
  const release = {
    monicaVersion: version,
    tag,
    commit,
    catalogDigest: CATALOG_DIGEST,
    skillTreeDigest: `sha256:${'b'.repeat(64)}`,
    skillDigestAlgorithm: 'sha256-file-manifest-v1',
    skillDigests: contracts.skillDigests,
    skillRevisions: skillRevisions || Object.fromEntries(Object.keys(contracts.skillDigests).map((skill) => [skill, 1])),
    skillLastChangedIn: skillLastChangedIn || Object.fromEntries(Object.keys(contracts.skillDigests).map((skill) => [skill, tag])),
    manifestDigest: null,
    assetBaseUrl,
    catalogUrl: `${assetBaseUrl}/agent-skill-catalog.json`,
    manifestUrl: `${assetBaseUrl}/agent-skill-manifest.json`,
    publishedAt,
  };
  const manifest = releaseManifestForRelease(release, contracts.files);
  release.manifestDigest = digest(Buffer.from(`${JSON.stringify(manifest, null, 2)}\n`));
  return {
    $schema: './schemas/agent-skill-index.schema.json',
    schemaVersion: 2,
    channels: {
      stable: semverChannel(version) === 'stable' ? tag : null,
      preview: semverChannel(version) === 'preview' ? tag : null,
    },
    versions: { [version]: tag },
    releases: {
      [tag]: release,
    },
  };
}

function changedSkillContracts(skillNames) {
  const contracts = structuredClone(skillFileContracts());
  for (const skill of skillNames) {
    const skillFile = `skills/${skill}/SKILL.md`;
    contracts.files[skillFile] = digest(`changed:${skill}`);
    const prefix = `skills/${skill}/`;
    const fileManifest = Object.entries(contracts.files)
      .filter(([filePath]) => filePath.startsWith(prefix))
      .map(([filePath, hash]) => [filePath.slice(prefix.length), hash])
      .sort(([left], [right]) => Buffer.compare(Buffer.from(left, 'utf8'), Buffer.from(right, 'utf8')))
      .map(([relative, hash]) => `${hash.slice(7)}  ${relative}\n`).join('');
    contracts.skillDigests[skill] = digest(fileManifest);
  }
  return contracts;
}

function releaseHistory(changedSkills = []) {
  const previous = releaseIndex();
  const targetTag = 'v1.2.4';
  const targetContracts = changedSkillContracts(changedSkills);
  const targetRevisions = {};
  const targetLastChangedIn = {};
  for (const [skill, targetDigest] of Object.entries(targetContracts.skillDigests)) {
    const changed = previous.releases[TAG].skillDigests[skill] !== targetDigest;
    targetRevisions[skill] = previous.releases[TAG].skillRevisions[skill] + (changed ? 1 : 0);
    targetLastChangedIn[skill] = changed ? targetTag : previous.releases[TAG].skillLastChangedIn[skill];
  }
  const target = releaseIndex({
    version: '1.2.4',
    tag: targetTag,
    commit: 'd'.repeat(40),
    publishedAt: '2026-08-06T00:00:00Z',
    contracts: targetContracts,
    skillRevisions: targetRevisions,
    skillLastChangedIn: targetLastChangedIn,
  });
  return {
    $schema: './schemas/agent-skill-index.schema.json',
    schemaVersion: 2,
    channels: { stable: targetTag, preview: null },
    versions: { [VERSION]: TAG, '1.2.4': targetTag },
    releases: { [TAG]: previous.releases[TAG], [targetTag]: target.releases[targetTag] },
  };
}

function releaseManifestForRelease(release, files) {
  return {
    schemaVersion: 2,
    monicaVersion: release.monicaVersion,
    tag: release.tag,
    resolvedCommit: release.commit,
    catalogDigest: release.catalogDigest,
    skillTreeDigest: release.skillTreeDigest,
    skillDigestAlgorithm: release.skillDigestAlgorithm,
    skillDigests: release.skillDigests,
    skillRevisions: release.skillRevisions,
    skillLastChangedIn: release.skillLastChangedIn,
    publishedAt: release.publishedAt,
    indexUrl: `${release.assetBaseUrl}/agent-skill-index.json`,
    catalogUrl: `${release.assetBaseUrl}/agent-skill-catalog.json`,
    archiveUrl: `${release.assetBaseUrl}/monica-agent-skills-${release.tag}.zip`,
    fileManifestScope: 'release-payload-except-index-v1',
    files,
  };
}

function releaseManifest(index = releaseIndex()) {
  const release = index.releases[index.channels.stable || index.channels.preview];
  return releaseManifestForRelease(release, fixtureFilesForRelease(release));
}

function fixtureFilesForRelease(release) {
  const base = skillFileContracts();
  const changed = Object.keys(release.skillDigests).filter((skill) => release.skillDigests[skill] !== base.skillDigests[skill]);
  return changed.length ? changedSkillContracts(changed).files : base.files;
}

function releaseFetch(index = releaseIndex()) {
  return async (url) => {
    let text;
    if (url.endsWith('/agent-skill-index.json')) text = `${JSON.stringify(index, null, 2)}\n`;
    else if (url.endsWith('/agent-skill-catalog.json')) text = fs.readFileSync(CATALOG_PATH, 'utf8');
    else if (url.endsWith('/agent-skill-manifest.json')) {
      const release = Object.values(index.releases).find((entry) => url.startsWith(`${entry.assetBaseUrl}/`));
      if (!release) throw new Error(`No release fixture matches test URL: ${url}`);
      text = `${JSON.stringify(releaseManifestForRelease(release, fixtureFilesForRelease(release)), null, 2)}\n`;
    }
    else throw new Error(`Unexpected test URL: ${url}`);
    return { ok: true, status: 200, url, text: async () => text };
  };
}

function baseOptions(root, workspace) {
  return {
    workspace,
    state: path.join(root, 'state', 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'application',
    capabilities: ['modular-monolith'],
    agents: ['codex', 'claude-code'],
    agentValidationRunner: () => ({ status: 0, stdout: '[]', stderr: '', error: null }),
    releaseTag: TAG,
    fetchImplementation: releaseFetch(),
  };
}

function mockNpx(root, skills, { corrupt = false, agents = ['codex', 'claude-code'], extraEntries = [] } = {}) {
  const executable = path.join(root, 'mock-npx.sh');
  const installedRoot = path.join(root, 'installed');
  fs.mkdirSync(installedRoot, { recursive: true });
  const payload = JSON.stringify([...skills.map((name, index) => {
    const installed = path.join(installedRoot, name);
    fs.cpSync(path.join(SKILLS_ROOT, name), installed, { recursive: true });
    if (corrupt && index === 0) fs.appendFileSync(path.join(installed, 'SKILL.md'), '\ncorrupt\n');
    return { name, path: installed, scope: 'global', agents, source: 'Tairitsua/Monica', sourceUrl: 'https://github.com/Tairitsua/Monica.git', sourceType: 'github' };
  }), ...extraEntries]);
  write(executable, `#!/bin/sh\necho "$*" >> "${path.join(root, 'npx.log')}"\ncase "$*" in\n  *" ls "*) cat <<'JSON'\n${payload}\nJSON\n  ;;\nesac\n`);
  fs.chmodSync(executable, 0o755);
  return executable;
}

function configuredState(root, workspace, {
  index = releaseIndex(),
  releaseTag = TAG,
  agents = ['codex'],
  unknownMetadata = false,
} = {}) {
  const { catalog } = loadCatalog({ catalogPath: CATALOG_PATH, indexPath: INDEX_PATH });
  const closure = resolveProfileClosure(catalog, 'application', ['modular-monolith']);
  const release = index.releases[releaseTag];
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.activeRelease = {
    id: releaseTag,
    monicaVersion: release.monicaVersion,
    tag: release.tag,
    commit: release.commit,
    catalogDigest: release.catalogDigest,
  };
  state.managedSkills = Object.fromEntries(closure.selected.map((skill) => [skill, unknownMetadata
    ? { revision: null, digest: null, lastChangedIn: null }
    : {
      revision: release.skillRevisions[skill],
      digest: release.skillDigests[skill],
      lastChangedIn: release.skillLastChangedIn[skill],
    }]));
  state.agentTargets = [...agents].sort();
  write(statePath, stableJson(state, 2));
  write(path.join(workspace, '.monica', 'guide.json'), stableJson({
    schemaVersion: 2,
    profile: 'application',
    channel: 'stable',
    capabilities: ['modular-monolith'],
    expectedCatalogRelease: { ...state.activeRelease, indexTag: releaseTag },
    instructionBlockVersion: 1,
    managedClaudeImport: false,
  }, 2));
  return { statePath, state, skills: closure.selected };
}

test('all four profiles resolve required, recommended, conditional, and external closure', () => {
  const { catalog } = loadCatalog({ catalogPath: CATALOG_PATH, indexPath: INDEX_PATH });
  const application = resolveProfileClosure(catalog, 'application', []);
  assert.deepEqual(application.required, ['monica-application', 'monica-application-project-unit-development', 'monica-guide']);
  assert.deepEqual(application.recommended, ['monica-application-unit-testing', 'monica-contribution']);
  assert.deepEqual(application.conditional, []);
  assert.deepEqual(resolveProfileClosure(catalog, 'application', ['microservice']).conditional, ['monica-application-microservice']);
  assert.deepEqual(resolveProfileClosure(catalog, 'application', ['modular-monolith']).conditional, ['monica-application-modular-monolith']);

  const extension = resolveProfileClosure(catalog, 'extension-author', ['ui']);
  assert.deepEqual(extension.required, ['monica-architecture', 'monica-development', 'monica-framework', 'monica-guide', 'monica-third-party-module-development', 'monica-unit-testing']);
  assert.deepEqual(extension.recommended, ['monica-contribution', 'monica-docs-authoring', 'monica-opentelemetry']);
  assert.deepEqual(extension.conditional, ['monica-ui-audit', 'monica-ui-bridge-debug', 'monica-ui-development', 'monica-ui-localization']);

  const framework = resolveProfileClosure(catalog, 'framework-contributor', []);
  assert.deepEqual(framework.required, ['monica-architecture', 'monica-development', 'monica-framework', 'monica-guide', 'monica-opentelemetry', 'monica-requirement-design', 'monica-unit-testing']);
  assert.deepEqual(framework.recommended, ['monica-contribution', 'monica-docs-authoring']);
  assert.deepEqual(framework.conditional, []);

  const docs = resolveProfileClosure(catalog, 'docs-contributor', []);
  assert.deepEqual(docs.required, ['monica-application', 'monica-application-microservice', 'monica-application-modular-monolith', 'monica-application-project-unit-development', 'monica-architecture', 'monica-docs-authoring', 'monica-guide']);
  assert.deepEqual(docs.recommended, ['monica-application-unit-testing', 'monica-contribution']);
  assert.deepEqual(docs.conditional, []);
  assert.deepEqual(application.sourceRequirements, []);
  assert.deepEqual(extension.sourceRequirements, [{
    repository: 'Tairitsua/Monica',
    requirement: 'conditional',
    compatibility: 'framework-version',
    condition: 'framework-internal-work',
  }]);
  assert.deepEqual(framework.sourceRequirements, [{
    repository: 'Tairitsua/Monica',
    requirement: 'required',
    compatibility: 'workspace-commit',
  }]);
  assert.deepEqual(docs.sourceRequirements, [{
    repository: 'Tairitsua/Monica',
    requirement: 'required',
    compatibility: 'framework-version',
  }, {
    repository: 'Tairitsua/Monica.Docs',
    requirement: 'required',
    compatibility: 'workspace-commit',
  }]);
});

test('init applies atomically while offline update/configure require and use an exact local skill source', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const options = baseOptions(root, workspace);
  const first = await buildPlan('init', options);
  const second = await buildPlan('init', options);
  assert.equal(first.planDigest, second.planDigest);
  assert.equal(first.blockers.length, 0);
  assert.ok(first.actions.some((action) => action.type === 'install-skill'));
  assert.ok(first.actions.filter((action) => action.type === 'install-skill').every((action) => action.command.includes('skills@1.5.21')));
  assert.ok(first.actions.some((action) => action.type === 'verify-skills'));
  assert.ok(first.actions.some((action) => action.path?.endsWith('AGENTS.md') && action.diff.includes('monica-guide:managed:start')));
  assert.equal(first.actions.find((action) => action.path?.endsWith('.monica/guide.json')).mode, 0o644);
  for (const action of first.actions.filter((entry) => entry.path === options.state || entry.purpose?.startsWith('Cache the verified'))) {
    assert.equal(action.mode, 0o600);
  }

  const skills = first.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill);
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, skills);
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const applied = await applyPlan('init', { ...options, planDigest: first.planDigest, precomputedPlan: first });
  assert.equal(applied.applied, true);
  assert.ok(fs.existsSync(path.join(workspace, '.monica', 'guide.json')));
  assert.match(fs.readFileSync(path.join(workspace, 'CLAUDE.md'), 'utf8'), /^@AGENTS\.md/m);
  const state = loadState(options.state);
  assert.equal(state.activeRelease.id, TAG);
  assert.ok(Object.hasOwn(state.managedSkills, 'monica-guide'));
  assert.equal(state.managedSkills['monica-guide'].revision, 1);
  assert.equal(state.managedSkills['monica-guide'].lastChangedIn, TAG);
  const observation = Object.values(state.observations)[0];
  assert.equal(observation.schemaVersion, 1);
  assert.ok(!Number.isNaN(Date.parse(observation.observedAt)));
  assert.equal(observation.observedAt === '<apply-time>', false);
  assert.equal(Object.hasOwn(observation, 'dirty'), false);
  assert.equal(Object.hasOwn(observation, 'sourceBindings'), false);
  const projectConfig = JSON.parse(fs.readFileSync(path.join(workspace, '.monica', 'guide.json'), 'utf8'));
  assert.equal(projectConfig.schemaVersion, 2);
  assert.equal(Object.hasOwn(projectConfig, 'agentTargets'), false);
  assert.ok(state.verifiedReleaseIndexes[TAG]);
  if (process.platform !== 'win32') {
    assert.equal(fs.statSync(path.join(workspace, '.monica', 'guide.json')).mode & 0o777, 0o644);
    assert.equal(fs.statSync(options.state).mode & 0o777, 0o600);
    for (const action of first.actions.filter((entry) => entry.purpose?.startsWith('Cache the verified'))) {
      assert.equal(fs.statSync(action.path).mode & 0o777, 0o600);
    }
  }

  const offline = await inspectEnvironment({ workspace, state: options.state, catalog: CATALOG_PATH, index: INDEX_PATH, offline: true });
  assert.equal(offline.targetRelease.id, TAG);
  assert.equal(offline.releaseIndexSource, 'verified-cache');
  fs.rmSync(path.join(root, 'npx.log'), { force: true });
  const blockedUpdate = await buildPlan('update', { workspace, state: options.state, catalog: CATALOG_PATH, index: INDEX_PATH, offline: true });
  assert.ok(blockedUpdate.blockers.some((entry) => entry.code === 'offline_local_skill_source_required'));
  assert.equal(blockedUpdate.actions.some((action) => action.type === 'install-skill' || action.type === 'verify-skills'), false);
  assert.match(fs.readFileSync(path.join(root, 'npx.log'), 'utf8'), /ls -g -a codex --json/);

  const localSource = path.join(root, 'cached-monica-source');
  fs.cpSync(SKILLS_ROOT, path.join(localSource, 'skills'), { recursive: true });
  const withSource = loadState(options.state);
  withSource.sourceBindings['Tairitsua/Monica'] = {
    repository: 'Tairitsua/Monica',
    ref: COMMIT,
    commit: COMMIT,
    provenance: { resolver: 'inspect-dependency-source', repositoryId: 'repo', artifactId: 'artifact', resolutionKind: 'exact_commit', expectedCommit: COMMIT },
    resolutionKind: 'exact_commit',
    sourcePath: localSource,
  };
  write(options.state, stableJson(withSource, 2));
  const resolver = path.join(root, 'resolver.py');
  writeResolver(resolver, localSource);
  const offlineOptions = { workspace, state: options.state, catalog: CATALOG_PATH, index: INDEX_PATH, offline: true, sourceResolver: resolver };
  const update = await buildPlan('update', offlineOptions);
  assert.equal(update.blockers.length, 0);
  const offlineActions = update.actions.filter((action) => action.type === 'install-skill');
  assert.equal(offlineActions.length, 0);
  assert.ok(offlineActions.every((action) => action.offline
    && action.args.includes('--offline')
    && action.source.startsWith(`${localSource}${path.sep}`)
    && !action.source.includes('github.com')));
  const offlineVerification = update.actions.find((action) => action.type === 'verify-skills');
  assert.ok(offlineVerification.offline);
  assert.ok(offlineVerification.commands.every((command) => command.includes('npx --offline --yes skills@1.5.21')));
  fs.rmSync(path.join(root, 'npx.log'), { force: true });

  const switchedSource = path.join(root, 'switched-cached-source');
  fs.cpSync(SKILLS_ROOT, path.join(switchedSource, 'skills'), { recursive: true });
  writeResolver(resolver, switchedSource);
  await assert.rejects(
    applyPlan('update', { ...offlineOptions, planDigest: update.planDigest, precomputedPlan: update }),
    (error) => error.code === 'source_binding_drift',
  );
  assert.equal(fs.existsSync(path.join(root, 'npx.log')), false);
  writeResolver(resolver, localSource);

  const configure = await buildPlan('configure', { ...offlineOptions, capabilities: ['modular-monolith', 'ui'] });
  assert.equal(configure.blockers.length, 0);
  assert.ok(configure.actions.some((action) => action.type === 'install-skill'
    && action.skill === 'monica-ui-development'
    && action.source === path.join(localSource, 'skills', 'monica-ui-development')
    && action.args.includes('--offline')));

  fs.appendFileSync(path.join(localSource, 'skills', 'monica-guide', 'SKILL.md'), '\nmismatch\n');
  const mismatched = await buildPlan('update', offlineOptions);
  assert.ok(mismatched.blockers.some((entry) => entry.code === 'local_skill_source_mismatch'));
  assert.equal(mismatched.actions.some((action) => action.type === 'install-skill' || action.type === 'verify-skills'), false);

  const contribution = await buildPlan('contribute', { workspace, state: options.state, catalog: CATALOG_PATH, index: INDEX_PATH, preference: 'prepare' });
  assert.equal(contribution.route.skill, 'monica-contribution');
  assert.equal(contribution.route.remoteMutationAuthorized, false);
  assert.ok(contribution.actions.some((action) => action.type === 'write-file'));

  const forget = await buildPlan('forget', { workspace, state: options.state, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.ok(forget.actions.some((action) => action.type === 'delete-file' && action.path.endsWith('.monica/guide.json')));
  assert.ok(forget.actions.some((action) => action.path.endsWith('AGENTS.md') && !action.content.includes('monica-guide:managed:start')));
});

test('default update installs only changed skills, verifies the full set, and records target revisions', async (t) => {
  const root = temporaryDirectory(t);
  const changedSkill = 'monica-application-project-unit-development';
  const history = releaseHistory([changedSkill]);
  const workspace = applicationWorkspace(root, '1.2.4');
  const configured = configuredState(root, workspace, { index: history });
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, configured.skills, { agents: ['codex'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const options = {
    workspace,
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    agents: ['codex'],
    switchGlobal: true,
    fetchImplementation: releaseDiscoveryFetch(history),
  };
  const plan = await buildPlan('update', options);
  assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
  assert.deepEqual(plan.context.installSkills, [changedSkill]);
  assert.deepEqual(plan.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill), [changedSkill]);
  assert.deepEqual(plan.actions.find((action) => action.type === 'verify-skills').skills, configured.skills);
  const nextState = JSON.parse(plan.actions.find((action) => action.path === configured.statePath).content);
  assert.equal(nextState.activeRelease.id, 'v1.2.4');
  assert.equal(nextState.managedSkills[changedSkill].revision, 2);
  assert.equal(nextState.managedSkills[changedSkill].lastChangedIn, 'v1.2.4');
  assert.equal(nextState.managedSkills['monica-guide'].revision, 1);

  const environment = await inspectEnvironment({
    ...options,
    releaseTag: 'v1.2.4',
    fetchImplementation: releaseFetch(history),
  });
  assert.equal(environment.targetRelease.id, 'v1.2.4');
  assert.equal(environment.releaseError.code, 'project_release_conflict');
  const report = await doctor({ ...options, releaseTag: 'v1.2.4', fetchImplementation: releaseFetch(history) });
  assert.equal(report.context.targetRelease, 'v1.2.4');
  assert.equal(report.checks.find((entry) => entry.id === 'immutable-release').status, 'error');
});

test('legacy project target ownership migrates diagnostically and rewrites only on approved workspace mutation', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const configured = configuredState(root, workspace);
  const configPath = path.join(workspace, '.monica', 'guide.json');
  const legacy = JSON.parse(fs.readFileSync(configPath, 'utf8'));
  legacy.schemaVersion = 1;
  legacy.agentTargets = ['cursor'];
  const { catalog } = loadCatalog({ catalogPath: CATALOG_PATH, indexPath: INDEX_PATH });
  legacy.instructionBlockVersion = catalog.managedInstructions.version;
  write(configPath, stableJson(legacy, 2));
  const managedBody = renderManagedInstructions(catalog, {
    profile: legacy.profile,
    channel: legacy.channel,
    release: legacy.expectedCatalogRelease,
    capabilities: legacy.capabilities,
  });
  write(path.join(workspace, 'AGENTS.md'), upsertInstructionBlock('', managedBody, { markers: catalog.managedInstructions.markers }));

  const common = {
    workspace,
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: TAG,
    fetchImplementation: releaseFetch(),
  };
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, configured.skills, { agents: ['codex'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const environment = await inspectEnvironment(common);
  assert.deepEqual(environment.state.agentTargets, ['codex']);
  assert.deepEqual(environment.projectConfigMigration.droppedAgentTargets, ['cursor']);
  const status = statusEnvelope(environment);
  assert.equal(status.status, 'migration-pending');
  assert.equal(status.severity, 'warning');
  const report = await doctor(common);
  assert.equal(report.checks.find((entry) => entry.id === 'project-config-migration').status, 'warning');
  assert.equal(JSON.parse(fs.readFileSync(configPath, 'utf8')).schemaVersion, 1);

  const update = await buildPlan('update', common);
  const projectAction = update.actions.find((action) => action.path === configPath);
  const migrated = JSON.parse(projectAction.content);
  assert.equal(migrated.schemaVersion, 2);
  assert.equal(Object.hasOwn(migrated, 'agentTargets'), false);
  assert.deepEqual(update.context.desiredAgentTargets, ['codex']);

  const forget = await buildPlan('forget', common);
  assert.ok(forget.actions.some((action) => action.type === 'delete-file' && action.path === configPath));
  assert.ok(forget.warnings.some((entry) => entry.code === 'project_config_migration_pending'));
});

test('targeted update expands required dependencies and blocks changed skills outside its closure', async (t) => {
  const root = temporaryDirectory(t);
  const changedSkill = 'monica-application-project-unit-development';
  const history = releaseHistory([changedSkill]);
  const workspace = applicationWorkspace(root, '1.2.4');
  const configured = configuredState(root, workspace, { index: history });
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, configured.skills, { agents: ['codex'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const options = {
    workspace,
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    agents: ['codex'],
    switchGlobal: true,
    fetchImplementation: releaseDiscoveryFetch(history),
  };
  const allowed = await buildPlan('update', { ...options, skills: ['monica-application'] });
  assert.equal(allowed.blockers.length, 0, JSON.stringify(allowed.blockers));
  assert.ok(allowed.context.targetedSkillClosure.includes(changedSkill));
  assert.deepEqual(allowed.context.installSkills, [changedSkill]);
  assert.deepEqual(allowed.actions.find((action) => action.type === 'verify-skills').skills, configured.skills);

  const blocked = await buildPlan('update', { ...options, skills: ['monica-guide'] });
  assert.ok(blocked.blockers.some((entry) => entry.code === 'targeted_update_would_mix_releases'));
  assert.equal(blocked.actions.some((action) => action.type === 'install-skill'), false);
});

test('agent target changes and migrated unknown metadata force a full reinstall', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const configured = configuredState(root, workspace);
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, configured.skills, { agents: ['codex', 'claude-code'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const options = {
    workspace,
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: TAG,
    agents: ['codex', 'claude-code'],
    fetchImplementation: releaseFetch(),
  };
  const agentChange = await buildPlan('update', options);
  assert.equal(agentChange.context.fullReinstallReason, 'agent-targets-changed');
  assert.deepEqual(agentChange.context.installSkills, configured.skills);

  configured.state.managedSkills = Object.fromEntries(configured.skills.map((skill) => [skill, {
    revision: null,
    digest: null,
    lastChangedIn: null,
  }]));
  configured.state.agentTargets = ['claude-code', 'codex'];
  write(configured.statePath, stableJson(configured.state, 2));
  const unknown = await buildPlan('update', options);
  assert.equal(unknown.context.fullReinstallReason, 'skill-metadata-unknown');
  assert.deepEqual(unknown.context.installSkills, configured.skills);
});

test('update heals installed drift and targeted update blocks drift outside its selection', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const configured = configuredState(root, workspace);
  const corruptedSkill = configured.skills[0];
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, configured.skills, { corrupt: true, agents: ['codex'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const options = {
    workspace,
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: TAG,
    agents: ['codex'],
    fetchImplementation: releaseFetch(),
  };
  const full = await buildPlan('update', options);
  assert.deepEqual(full.context.installSkills, [corruptedSkill]);
  assert.deepEqual(full.actions.find((action) => action.type === 'verify-skills').skills, configured.skills);

  const selected = await buildPlan('update', { ...options, skills: [corruptedSkill] });
  assert.equal(selected.blockers.length, 0);
  assert.deepEqual(selected.context.installSkills, [corruptedSkill]);
  const otherSkill = configured.skills.find((skill) => skill !== corruptedSkill);
  const blocked = await buildPlan('update', { ...options, skills: [otherSkill] });
  assert.ok(blocked.blockers.some((entry) => entry.code === 'targeted_update_other_skill_drift'));
});

test('status and update expand required dependencies for skills owned by another workspace', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const index = releaseIndex();
  const configured = configuredState(root, workspace, { index });
  const state = loadState(configured.statePath);
  const extra = 'monica-ui-bridge-debug';
  const release = index.releases[TAG];
  state.managedSkills[extra] = {
    revision: release.skillRevisions[extra],
    digest: release.skillDigests[extra],
    lastChangedIn: release.skillLastChangedIn[extra],
  };
  write(configured.statePath, stableJson(state, 2));
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, [...configured.skills, extra], { agents: ['codex'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const options = {
    workspace,
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: TAG,
    agents: ['codex'],
    fetchImplementation: releaseFetch(index),
  };
  const environment = await inspectEnvironment(options);
  const plan = await buildPlan('update', options);
  const statusNames = environment.skillChanges.map((entry) => entry.name);
  const planNames = plan.context.skillChanges.map((entry) => entry.name);
  assert.deepEqual(statusNames, planNames);
  for (const dependency of ['monica-ui-development', 'monica-ui-localization']) {
    assert.ok(statusNames.includes(dependency));
    assert.ok(plan.context.installSkills.includes(dependency));
  }
  assert.deepEqual(plan.actions.find((action) => action.type === 'verify-skills').skills, planNames);
  const report = await doctor(options);
  const discovery = report.checks.find((entry) => entry.id === 'skill-discovery:codex');
  assert.equal(discovery.status, 'error');
  assert.match(discovery.message, /monica-(architecture|development|ui-development|ui-localization)/);
});

test('status, doctor, and update consistently reject retired managed skill records', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const configured = configuredState(root, workspace);
  configured.state.managedSkills['monica-retired'] = {
    revision: 1,
    digest: `sha256:${'f'.repeat(64)}`,
    lastChangedIn: TAG,
  };
  write(configured.statePath, stableJson(configured.state, 2));

  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, configured.skills, { agents: ['codex'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const options = {
    workspace,
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: TAG,
    agents: ['codex'],
    fetchImplementation: releaseFetch(),
  };

  const environment = await inspectEnvironment(options);
  assert.equal(environment.skillMetadataError.code, 'managed_skill_missing_from_release');
  assert.deepEqual(environment.skillMetadataError.details.skills, ['monica-retired']);

  const status = statusEnvelope(environment);
  assert.equal(status.status, 'error');
  assert.equal(status.error.code, 'managed_skill_missing_from_release');

  const report = await doctor(options);
  const versionCheck = report.checks.find((entry) => entry.id === 'managed-skill-versions');
  assert.equal(versionCheck.status, 'error');
  assert.match(versionCheck.remediation, /obsolete global skill and state record/);

  const plan = await buildPlan('update', options);
  assert.ok(plan.blockers.some((entry) => entry.code === 'managed_skill_missing_from_release'));
});

test('apply refuses installed skills whose discovery source is a stale release', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const options = baseOptions(root, workspace);
  const plan = await buildPlan('init', options);
  assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
  const skills = plan.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill);
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, skills, { corrupt: true });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  await assert.rejects(
    applyPlan('init', { ...options, planDigest: plan.planDigest, precomputedPlan: plan }),
    (error) => error.code === 'skill_content_mismatch',
  );
  assert.equal(fs.existsSync(path.join(workspace, '.monica', 'guide.json')), false);
});

test('apply verifies every selected skill is discovered for every planned agent', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const options = baseOptions(root, workspace);
  const plan = await buildPlan('init', options);
  assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
  const skills = plan.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill);
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, skills, { agents: ['Codex'] });
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  await assert.rejects(
    applyPlan('init', { ...options, planDigest: plan.planDigest, precomputedPlan: plan }),
    (error) => error.code === 'skill_agent_membership_mismatch'
      && error.details?.transaction?.status === 'compensated',
  );
  assert.equal(fs.existsSync(path.join(workspace, '.monica', 'guide.json')), false);
  assert.equal(fs.existsSync(options.state), false);
});

test('apply rejects workspace drift inside the state lock before running installers', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const options = baseOptions(root, workspace);
  const plan = await buildPlan('init', options);
  assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
  const skills = plan.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill);
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, skills);
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  write(path.join(workspace, 'AGENTS.md'), '# Concurrent edit\n');
  await assert.rejects(
    applyPlan('init', { ...options, planDigest: plan.planDigest, precomputedPlan: plan }),
    (error) => error instanceof GuideError && error.code === 'workspace_drift',
  );
  assert.equal(fs.existsSync(path.join(root, 'npx.log')), false);
});

test('Git fingerprint rejects changed content even when porcelain state is unchanged', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  write(path.join(workspace, 'Program.cs'), 'class Program { const string Value = "base"; }\n');
  initializeGit(workspace, 'https://github.com/example/application.git');
  write(path.join(workspace, 'Program.cs'), 'class Program { const string Value = "first"; }\n');
  const options = baseOptions(root, workspace);
  const plan = await buildPlan('init', options);
  assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
  const porcelainBefore = execFileSync('git', ['-C', workspace, 'status', '--porcelain=v1'], { encoding: 'utf8' });
  write(path.join(workspace, 'Program.cs'), 'class Program { const string Value = "second"; }\n');
  const porcelainAfter = execFileSync('git', ['-C', workspace, 'status', '--porcelain=v1'], { encoding: 'utf8' });
  assert.equal(porcelainAfter, porcelainBefore);
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, plan.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill));
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  await assert.rejects(
    applyPlan('init', { ...options, planDigest: plan.planDigest, precomputedPlan: plan }),
    (error) => error.code === 'workspace_drift',
  );
  assert.equal(fs.existsSync(path.join(root, 'npx.log')), false);
});

test('state locks fail closed for competing, malformed, and proven-dead owners', async (t) => {
  const root = temporaryDirectory(t);
  const lockPath = path.join(root, 'state.json.lock');
  write(lockPath, stableJson({ pid: process.pid, createdAt: '2000-01-01T00:00:00Z' }, 2));
  fs.utimesSync(lockPath, new Date(0), new Date(0));
  assert.throws(() => withFileLock(lockPath, () => null), (error) => error.code === 'state_locked');
  fs.unlinkSync(lockPath);
  write(lockPath, stableJson({ pid: 2147483647, token: 'a'.repeat(48), createdAt: '2000-01-01T00:00:00Z' }, 2));
  assert.throws(() => withFileLock(lockPath, () => null), (error) => error.code === 'state_locked' && error.details.status === 'stale');
  fs.unlinkSync(lockPath);
  let called = false;
  withFileLock(lockPath, () => { called = true; });
  assert.equal(called, true);
  assert.equal(fs.existsSync(lockPath), false);

  const childScript = `
    import fs from 'node:fs';
    import { withFileLock } from ${JSON.stringify(GUIDE_SHARED_URL)};
    withFileLock(process.argv[1], () => {
      process.stdout.write('locked\\n');
      fs.readFileSync(0, 'utf8');
    });
  `;
  const child = spawn(process.execPath, ['--input-type=module', '--eval', childScript, lockPath], {
    stdio: ['pipe', 'pipe', 'pipe'],
  });
  let childError = '';
  child.stderr.setEncoding('utf8');
  child.stderr.on('data', (chunk) => { childError += chunk; });
  await new Promise((resolve, reject) => {
    child.once('error', reject);
    child.once('exit', (code) => reject(new Error(`Lock-holder child exited early with ${code}: ${childError}`)));
    child.stdout.once('data', (chunk) => String(chunk).includes('locked') ? resolve() : reject(new Error(`Unexpected child output: ${chunk}`)));
  });
  const childLock = fs.readFileSync(lockPath, 'utf8');
  assert.equal(JSON.parse(childLock).pid, child.pid);
  assert.throws(() => withFileLock(lockPath, () => null), (error) => error.code === 'state_locked' && error.details.status === 'owned');
  assert.equal(fs.readFileSync(lockPath, 'utf8'), childLock);
  const childExitPromise = once(child, 'exit');
  child.stdin.end();
  const [childExit] = await childExitPromise;
  assert.equal(childExit, 0, childError);
  assert.equal(fs.existsSync(lockPath), false);

  withFileLock(lockPath, () => {
    fs.unlinkSync(lockPath);
    write(lockPath, stableJson({ pid: process.pid, token: 'b'.repeat(48), createdAt: new Date().toISOString() }, 2));
  });
  assert.equal(fs.existsSync(lockPath), true);
});

test('detected profile requires explicit confirmation before init', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const plan = await buildPlan('init', { ...baseOptions(root, workspace), profile: undefined });
  assert.ok(plan.blockers.some((entry) => entry.code === 'profile_confirmation_required'));
});

test('empty repositories derive stable and preview channels from an explicit immutable release across plan, status, and doctor', async (t) => {
  const root = temporaryDirectory(t);
  const fixtures = [
    { version: VERSION, tag: TAG, commit: COMMIT, channel: 'stable' },
    { version: '1.3.0-rc.1', tag: 'v1.3.0-rc.1', commit: 'e'.repeat(40), channel: 'preview' },
  ];
  const planDigests = [];
  for (const fixture of fixtures) {
    const workspace = emptyGitWorkspace(root, fixture.channel);
    const statePath = path.join(root, `${fixture.channel}-state.json`);
    const index = releaseIndex(fixture);
    const options = {
      workspace,
      state: statePath,
      catalog: CATALOG_PATH,
      index: INDEX_PATH,
      profile: 'application',
      agents: ['codex'],
      agentValidationRunner: () => ({ status: 0, stdout: '[]', stderr: '', error: null }),
      releaseTag: fixture.tag,
      fetchImplementation: releaseFetch(index),
    };
    const unresolved = await buildPlan('init', options);
    assert.equal(unresolved.context.channel, fixture.channel);
    assert.equal(unresolved.context.targetRelease.id, fixture.tag);
    assert.ok(unresolved.blockers.some((entry) => entry.code === 'application_architecture_required'));
    assert.equal(unresolved.actions.some((action) => action.type === 'install-skill'), false);
    const unresolvedEnvironment = await inspectEnvironment(options);
    const unresolvedStatus = statusEnvelope(unresolvedEnvironment);
    assert.equal(unresolvedStatus.status, 'error');
    assert.equal(unresolvedStatus.error.code, 'application_architecture_required');
    const unresolvedDoctor = await doctor(options);
    assert.equal(unresolvedDoctor.checks.find((entry) => entry.id === 'application-architecture').status, 'error');

    const selectedOptions = { ...options, capabilities: ['modular-monolith'] };
    const plan = await buildPlan('init', selectedOptions);
    assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
    assert.equal(plan.context.channel, fixture.channel);
    assert.equal(plan.context.targetRelease.id, fixture.tag);
    assert.equal(plan.context.targetRelease.tag, fixture.tag);
    assert.equal(plan.context.targetRelease.monicaVersion, fixture.version);
    planDigests.push(plan.planDigest);

    const plannedState = JSON.parse(plan.actions.find((action) => action.path === statePath).content);
    assert.equal(plannedState.activeRelease.id, fixture.tag);
    assert.equal(Object.values(plannedState.workspacePreferences)[0].channel, fixture.channel);
    assert.equal(Object.values(plannedState.observations)[0].observedAt, '<apply-time>');
    const projectAction = plan.actions.find((action) => action.path?.endsWith('.monica/guide.json'));
    const projectConfig = JSON.parse(projectAction.content);
    assert.equal(projectConfig.channel, fixture.channel);
    assert.equal(projectConfig.expectedCatalogRelease.id, fixture.tag);
    assert.equal(projectConfig.expectedCatalogRelease.indexTag, fixture.tag);

    const environment = await inspectEnvironment(selectedOptions);
    assert.equal(environment.releaseError, null);
    assert.equal(environment.channel, fixture.channel);
    assert.equal(environment.targetRelease.id, fixture.tag);
    const status = statusEnvelope(environment);
    assert.equal(status.observation.channel, fixture.channel);
    assert.equal(status.observation.targetRelease, fixture.tag);
    const report = await doctor(selectedOptions);
    assert.equal(report.context.channel, fixture.channel);
    assert.equal(report.context.targetRelease, fixture.tag);
    assert.equal(report.context.applicationArchitecture, 'modular-monolith');
    assert.ok(report.checks.some((entry) => entry.id === 'immutable-release' && entry.status === 'ok'));

    for (const action of plan.actions.filter((entry) => entry.purpose?.startsWith('Cache the verified'))) {
      write(action.path, action.content);
    }
    for (const observation of Object.values(plannedState.observations)) observation.observedAt = '2026-08-11T00:00:00.000Z';
    write(statePath, stableJson(plannedState, 2));
    const offlineEnvironment = await inspectEnvironment({ ...selectedOptions, offline: true, fetchImplementation: undefined });
    assert.equal(offlineEnvironment.releaseError, null);
    assert.equal(offlineEnvironment.channel, fixture.channel);
    assert.equal(offlineEnvironment.targetRelease.id, fixture.tag);
    assert.equal(offlineEnvironment.releaseIndexSource, 'verified-cache');
    assert.equal(offlineEnvironment.releaseArtifactSource, 'verified-cache');
  }
  assert.notEqual(planDigests[0], planDigests[1]);
});

test('explicit release constraints fail consistently without falling back to another release or channel', async (t) => {
  const root = temporaryDirectory(t);
  const previewTag = 'v1.3.0-rc.1';
  const previewIndex = releaseIndex({ version: '1.3.0-rc.1', tag: previewTag, commit: 'e'.repeat(40) });
  const workspace = emptyGitWorkspace(root, 'explicit-channel-conflict');
  const options = {
    workspace,
    state: path.join(root, 'explicit-channel-state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'application',
    capabilities: ['modular-monolith'],
    releaseTag: previewTag,
    channel: 'stable',
    fetchImplementation: releaseFetch(previewIndex),
  };
  const plan = await buildPlan('init', options);
  assert.equal(plan.context.channel, 'preview');
  assert.equal(plan.context.targetRelease.id, previewTag);
  assert.ok(plan.blockers.some((entry) => entry.code === 'release_channel_mismatch'));
  assert.equal(plan.actions.some((action) => action.type === 'install-skill'), false);
  const environment = await inspectEnvironment(options);
  assert.equal(environment.channel, 'preview');
  assert.equal(environment.targetRelease.id, previewTag);
  assert.equal(environment.releaseError.code, 'release_channel_mismatch');
  const status = statusEnvelope(environment);
  assert.equal(status.status, 'error');
  assert.equal(status.error.code, 'release_channel_mismatch');
  assert.equal(status.observation.channel, 'preview');
  const report = await doctor(options);
  assert.equal(report.context.channel, 'preview');
  assert.ok(report.checks.some((entry) => entry.id === 'immutable-release'
    && entry.status === 'error'
    && entry.message.includes('explicitly requested channel stable')));

  const persistedWorkspace = emptyGitWorkspace(root, 'persisted-channel-conflict');
  write(path.join(persistedWorkspace, '.monica', 'guide.json'), stableJson({
    schemaVersion: 2,
    profile: 'application',
    channel: 'stable',
    capabilities: ['modular-monolith'],
    expectedCatalogRelease: {
      id: TAG,
      monicaVersion: VERSION,
      tag: TAG,
      commit: COMMIT,
      catalogDigest: CATALOG_DIGEST,
      indexTag: TAG,
    },
    instructionBlockVersion: 1,
    managedClaudeImport: false,
  }, 2));
  const persistedPlan = await buildPlan('init', {
    workspace: persistedWorkspace,
    state: path.join(root, 'persisted-channel-state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: previewTag,
    fetchImplementation: releaseFetch(previewIndex),
  });
  assert.equal(persistedPlan.context.channel, 'preview');
  assert.ok(persistedPlan.blockers.some((entry) => entry.code === 'project_channel_conflict'));

  const history = releaseHistory([]);
  const persistedReleasePlan = await buildPlan('init', {
    workspace: persistedWorkspace,
    state: path.join(root, 'persisted-release-state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: 'v1.2.4',
    fetchImplementation: releaseFetch(history),
  });
  assert.equal(persistedReleasePlan.context.targetRelease.id, 'v1.2.4');
  assert.ok(persistedReleasePlan.blockers.some((entry) => entry.code === 'project_release_conflict'));
});

test('application architecture capabilities are mutually exclusive', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = emptyGitWorkspace(root);
  const options = {
    ...baseOptions(root, workspace),
    capabilities: ['microservice', 'modular-monolith'],
  };
  const plan = await buildPlan('init', options);
  assert.ok(plan.blockers.some((entry) => entry.code === 'application_architecture_conflict'));
  assert.equal(plan.context.applicationArchitecture, null);
  assert.equal(plan.actions.some((action) => action.type === 'install-skill'), false);
  const environment = await inspectEnvironment(options);
  const status = statusEnvelope(environment);
  assert.equal(status.status, 'error');
  assert.equal(status.error.code, 'application_architecture_conflict');
});

test('existing application structure selects exactly one architecture capability', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  write(
    path.join(workspace, 'src', 'Domains', 'Ordering', 'Domains.Ordering.csproj'),
    '<Project Sdk="Microsoft.NET.Sdk" />\n',
  );
  const plan = await buildPlan('init', {
    ...baseOptions(root, workspace),
    capabilities: [],
  });
  assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
  assert.equal(plan.context.applicationArchitecture, 'modular-monolith');
  assert.ok(plan.context.capabilities.includes('modular-monolith'));
  assert.ok(plan.actions.some((action) => action.type === 'install-skill'
    && action.skill === 'monica-application-modular-monolith'));
  assert.equal(plan.actions.some((action) => action.type === 'install-skill'
    && action.skill === 'monica-application-microservice'), false);
});

test('immutable and source release selectors reject incompatible combinations before artifact access', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = emptyGitWorkspace(root);
  const common = {
    workspace,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'application',
    capabilities: ['modular-monolith'],
  };
  const combinations = [
    { releaseTag: TAG, sourceRef: COMMIT },
    { sourceRef: COMMIT, channel: 'stable' },
    { releaseTag: TAG, channel: 'source' },
  ];
  for (const selectors of combinations) {
    const plan = await buildPlan('init', { ...common, ...selectors });
    assert.ok(plan.blockers.some((entry) => entry.code === 'release_selector_conflict'));
    assert.equal(plan.actions.some((action) => action.type === 'install-skill' || action.type === 'verify-skills'), false);
  }
  const environment = await inspectEnvironment({ ...common, releaseTag: TAG, sourceRef: COMMIT });
  assert.equal(environment.releaseError.code, 'release_selector_conflict');
});

test('an explicit release tag selects that exact release and rejects a detected framework mismatch', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root, '1.2.3');
  const oldIndex = releaseIndex();
  const newIndex = releaseIndex({ version: '2.0.0', tag: 'v2.0.0', commit: 'd'.repeat(40), publishedAt: '2026-08-06T00:00:00Z' });
  newIndex.releases['v2.0.0'].skillRevisions = structuredClone(oldIndex.releases['v1.2.3'].skillRevisions);
  newIndex.releases['v2.0.0'].skillLastChangedIn = structuredClone(oldIndex.releases['v1.2.3'].skillLastChangedIn);
  const newManifest = releaseManifestForRelease(newIndex.releases['v2.0.0'], skillFileContracts().files);
  newIndex.releases['v2.0.0'].manifestDigest = digest(Buffer.from(`${JSON.stringify(newManifest, null, 2)}\n`));
  const combined = {
    $schema: './schemas/agent-skill-index.schema.json',
    schemaVersion: 2,
    channels: { stable: 'v2.0.0', preview: null },
    versions: { '1.2.3': 'v1.2.3', '2.0.0': 'v2.0.0' },
    releases: {
      'v1.2.3': oldIndex.releases['v1.2.3'],
      'v2.0.0': newIndex.releases['v2.0.0'],
    },
  };
  const plan = await buildPlan('init', {
    workspace,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'application',
    capabilities: ['modular-monolith'],
    releaseTag: 'v2.0.0',
    fetchImplementation: releaseFetch(combined),
  });
  assert.equal(plan.context.targetRelease.tag, 'v2.0.0');
  assert.equal(plan.context.channel, 'stable');
  assert.ok(plan.blockers.some((entry) => entry.code === 'release_version_mismatch'));
  assert.equal(plan.actions.some((action) => action.type === 'install-skill'), false);
  assert.equal(plan.actions.some((action) => action.path?.endsWith('.monica/guide.json')), false);
});

test('global release conflicts require an explicit switch and update the managed union', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.activeRelease = { id: 'old', tag: 'v1.0.0', commit: 'c'.repeat(40) };
  state.managedSkills = {
    'monica-ui-design': { revision: 1, digest: skillFileContracts().skillDigests['monica-ui-design'], lastChangedIn: TAG },
  };
  state.agentTargets = ['codex'];
  write(statePath, stableJson(state, 2));
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, ['monica-ui-design', 'monica-ui-development']);
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const blocked = await buildPlan('init', { ...baseOptions(root, workspace), state: statePath });
  assert.ok(blocked.blockers.some((entry) => entry.code === 'global_release_conflict'));
  const environment = await inspectEnvironment({ ...baseOptions(root, workspace), state: statePath });
  const status = statusEnvelope(environment);
  assert.equal(status.status, 'error');
  assert.equal(status.error.code, 'global_release_conflict');
  const report = await doctor({ ...baseOptions(root, workspace), state: statePath });
  assert.equal(report.checks.find((entry) => entry.id === 'global-release').status, 'error');
  const switched = await buildPlan('init', { ...baseOptions(root, workspace), state: statePath, switchGlobal: true, agents: ['claude-code'] });
  assert.equal(switched.blockers.length, 0);
  assert.ok(switched.actions.some((action) => action.type === 'install-skill' && action.skill === 'monica-ui-design'));
  assert.equal(switched.actions.some((action) => action.type === 'install-skill' && action.skill === 'monica-ui-development'), false);
  const verification = switched.actions.find((action) => action.type === 'verify-skills');
  assert.deepEqual(verification.agents, ['claude-code']);
  assert.ok(switched.actions.filter((action) => action.type === 'install-skill').every((action) => !action.command.includes('-a codex') && action.command.includes('-a claude-code')));
  assert.ok(switched.actions.some((action) => action.type === 'remove-skill-targets' && action.command.includes('-a codex')));
  assert.ok(switched.actions.some((action) => action.type === 'verify-removed-agent-targets'));
});

test('mixed versions and version ranges fail closed', (t) => {
  const root = temporaryDirectory(t);
  write(path.join(root, 'One.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" Version="1.2.3" /></ItemGroup></Project>');
  write(path.join(root, 'Two.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Hosting" Version="1.2.4" /></ItemGroup></Project>');
  assert.throws(() => detectFrameworkVersion(root), (error) => error.code === 'mixed_framework_versions');
  fs.rmSync(path.join(root, 'Two.csproj'));
  write(path.join(root, 'One.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" Version="[1.2.0,2.0.0)" /></ItemGroup></Project>');
  assert.throws(() => detectFrameworkVersion(root), (error) => error.code === 'version_range_unsupported');
});

test('SemVer accepts build metadata, derives channel from prerelease only, and rejects leading zeroes', () => {
  assert.ok(parseSemVer('1.2.3+build-7'));
  assert.equal(semverChannel('1.2.3+build-7'), 'stable');
  assert.equal(semverChannel('1.2.3-rc.1+build-7'), 'preview');
  assert.equal(parseSemVer('01.2.3'), null);
  assert.equal(parseSemVer('1.2.3-01'), null);
});

test('release validation enforces one chronological skill revision lineage across channels', () => {
  const history = releaseHistory(['monica-application-microservice']);
  assert.doesNotThrow(() => validateIndex(history));

  const duplicateTime = structuredClone(history);
  duplicateTime.releases['v1.2.4'].publishedAt = duplicateTime.releases[TAG].publishedAt;
  assert.throws(() => validateIndex(duplicateTime), (error) => error.code === 'release_timestamp_not_sequential');

  const metadataJump = structuredClone(history);
  metadataJump.releases['v1.2.4'].skillRevisions['monica-guide'] = 2;
  metadataJump.releases['v1.2.4'].skillLastChangedIn['monica-guide'] = 'v1.2.4';
  assert.throws(() => validateIndex(metadataJump), (error) => error.code === 'skill_revision_sequence_invalid');

  const fork = structuredClone(history);
  fork.channels.preview = 'v1.2.5-rc.1';
  fork.versions['1.2.5-rc.1'] = 'v1.2.5-rc.1';
  const forkRelease = structuredClone(fork.releases[TAG]);
  forkRelease.monicaVersion = '1.2.5-rc.1';
  forkRelease.tag = 'v1.2.5-rc.1';
  forkRelease.commit = 'e'.repeat(40);
  forkRelease.publishedAt = '2026-08-07T00:00:00Z';
  forkRelease.assetBaseUrl = 'https://github.com/Tairitsua/Monica/releases/download/v1.2.5-rc.1';
  forkRelease.catalogUrl = `${forkRelease.assetBaseUrl}/agent-skill-catalog.json`;
  forkRelease.manifestUrl = `${forkRelease.assetBaseUrl}/agent-skill-manifest.json`;
  fork.releases['v1.2.5-rc.1'] = forkRelease;
  assert.throws(() => validateIndex(fork), (error) => error.code === 'skill_revision_sequence_invalid');
});

test('tagged release identity cannot be downgraded by an extraneous source manifest field', () => {
  const index = releaseIndex();
  const release = { ...index.releases[TAG], channel: 'stable' };
  const manifest = { ...releaseManifest(index), source: true };
  assert.deepEqual(targetSkillRecord(release, manifest, 'monica-guide'), {
    revision: 1,
    digest: release.skillDigests['monica-guide'],
    lastChangedIn: TAG,
  });
});

test('release validation rejects a skill lineage reintroduced after an absent release', () => {
  const sha = (character) => `sha256:${character.repeat(64)}`;
  const makeRelease = (tag, version, publishedAt, skills) => {
    const assetBaseUrl = `https://github.com/Tairitsua/Monica/releases/download/${tag}`;
    return {
      monicaVersion: version,
      tag,
      commit: version.replace(/\D/g, '').padEnd(40, 'a').slice(0, 40),
      catalogDigest: sha('a'),
      skillTreeDigest: sha('b'),
      skillDigestAlgorithm: 'sha256-file-manifest-v1',
      skillDigests: Object.fromEntries(Object.entries(skills).map(([name, record]) => [name, record.digest])),
      skillRevisions: Object.fromEntries(Object.entries(skills).map(([name, record]) => [name, record.revision])),
      skillLastChangedIn: Object.fromEntries(Object.entries(skills).map(([name, record]) => [name, record.origin])),
      manifestDigest: sha('c'),
      publishedAt,
      assetBaseUrl,
      catalogUrl: `${assetBaseUrl}/agent-skill-catalog.json`,
      manifestUrl: `${assetBaseUrl}/agent-skill-manifest.json`,
    };
  };
  const firstTag = 'v1.0.0';
  const secondTag = 'v1.1.0';
  const thirdTag = 'v1.2.0';
  const shared = { digest: sha('d'), revision: 1, origin: firstTag };
  const reintroduced = { digest: sha('e'), revision: 1, origin: firstTag };
  const index = {
    $schema: './schemas/agent-skill-index.schema.json',
    schemaVersion: 2,
    channels: { stable: thirdTag, preview: null },
    versions: { '1.0.0': firstTag, '1.1.0': secondTag, '1.2.0': thirdTag },
    releases: {
      [firstTag]: makeRelease(firstTag, '1.0.0', '2026-08-01T00:00:00Z', { shared, reintroduced }),
      [secondTag]: makeRelease(secondTag, '1.1.0', '2026-08-02T00:00:00Z', { shared }),
      [thirdTag]: makeRelease(thirdTag, '1.2.0', '2026-08-03T00:00:00Z', {
        shared,
        reintroduced: { ...reintroduced, origin: thirdTag },
      }),
    },
  };
  assert.throws(() => validateIndex(index), (error) => error.code === 'skill_revision_lineage_reintroduced');
});

test('skill digests use Python-compatible ordinal UTF-8 path ordering', (t) => {
  const root = temporaryDirectory(t);
  const skillRoot = path.join(root, 'mixed-case');
  write(path.join(skillRoot, 'SKILL.md'), 'skill\n');
  write(path.join(skillRoot, 'Z.txt'), 'upper\n');
  write(path.join(skillRoot, 'a.txt'), 'lower\n');
  const hashes = {
    'SKILL.md': digest(fs.readFileSync(path.join(skillRoot, 'SKILL.md'))),
    'Z.txt': digest(fs.readFileSync(path.join(skillRoot, 'Z.txt'))),
    'a.txt': digest(fs.readFileSync(path.join(skillRoot, 'a.txt'))),
  };
  const expectedDigest = digest(`${hashes['SKILL.md'].slice(7)}  SKILL.md\n${hashes['Z.txt'].slice(7)}  Z.txt\n${hashes['a.txt'].slice(7)}  a.txt\n`);
  const manifest = {
    skillDigestAlgorithm: 'sha256-file-manifest-v1',
    skillDigests: { 'mixed-case': expectedDigest },
    files: Object.fromEntries(Object.entries(hashes).map(([name, hash]) => [`skills/mixed-case/${name}`, hash])),
  };
  const verified = verifyInstalledSkill({ name: 'mixed-case', path: skillRoot, scope: 'global' }, 'mixed-case', manifest);
  assert.equal(verified.digest, expectedDigest);
});

test('Guide verification consumes the Python release builder manifest without digest drift', (t) => {
  const outputParent = path.join(REPOSITORY_ROOT, '.tmp');
  fs.mkdirSync(outputParent, { recursive: true });
  const output = fs.mkdtempSync(path.join(outputParent, 'guide-release-test-'));
  fs.rmSync(output, { recursive: true });
  t.after(() => fs.rmSync(output, { recursive: true, force: true }));
  const releaseIndex = JSON.parse(fs.readFileSync(RELEASE_INDEX_PATH, 'utf8'));
  validateIndex(releaseIndex);
  let sequence = 1;
  let version;
  do {
    version = `9.8.7-guide.${sequence}`;
    sequence += 1;
  } while (Object.hasOwn(releaseIndex.versions, version));
  const latestRelease = Object.entries(releaseIndex.releases)
    .map(([tag, release]) => ({ tag, publishedAt: Date.parse(release.publishedAt) }))
    .sort((left, right) => left.publishedAt - right.publishedAt)
    .at(-1);
  const previousReleaseArguments = latestRelease
    ? ['--previous-index', RELEASE_INDEX_PATH, '--previous-tag', latestRelease.tag]
    : [];
  const publishedAt = latestRelease
    ? new Date(latestRelease.publishedAt + 1_000).toISOString()
    : '2026-08-05T00:00:00Z';
  execFileSync(process.env.MONICA_GUIDE_PYTHON || 'python3', [
    path.join(REPOSITORY_ROOT, 'scripts', 'build_agent_skill_release.py'),
    '--version', version,
    '--tag', `v${version}`,
    '--commit', COMMIT,
    '--channel', 'preview',
    '--published-at', publishedAt,
    ...previousReleaseArguments,
    '--output', output,
  ], { cwd: REPOSITORY_ROOT, stdio: 'pipe' });
  const catalog = JSON.parse(fs.readFileSync(path.join(output, 'agent-skill-catalog.json'), 'utf8'));
  const manifest = JSON.parse(fs.readFileSync(path.join(output, 'agent-skill-manifest.json'), 'utf8'));
  const managed = Object.entries(catalog.skills).filter(([, entry]) => entry.ownership === 'monica' && entry.managed === true);
  for (const [name, entry] of managed) {
    const verified = verifyInstalledSkill({ name, path: path.join(REPOSITORY_ROOT, entry.path), scope: 'global' }, name, manifest);
    assert.equal(verified.digest, manifest.skillDigests[name]);
  }
});

test('central package property versions and WSL Windows paths are resolved', (t) => {
  const root = temporaryDirectory(t);
  write(path.join(root, 'Directory.Packages.props'), '<Project><PropertyGroup><MonicaVersion>1.2.3</MonicaVersion></PropertyGroup><ItemGroup><PackageVersion Include="Monica.Core" Version="$(MonicaVersion)" /></ItemGroup></Project>');
  assert.equal(detectFrameworkVersion(root).version, '1.2.3');
  assert.equal(normalizePath('D:\\Code\\Project Space\\Example', '/', { platform: 'linux', isWsl: true }), '/mnt/d/Code/Project Space/Example');
  assert.equal(normalizePath('/mnt/d/Code/Project Space/Example', 'C:\\', { platform: 'win32', isWsl: false }), 'D:\\Code\\Project Space\\Example');
  assert.equal(normalizePath('D:\\Code\\Example', '/tmp', { platform: 'linux', isWsl: false }), '/tmp/D:\\Code\\Example');
});

test('version detection handles VersionOverride and fails closed on unresolved central versions', (t) => {
  const root = temporaryDirectory(t);
  write(path.join(root, 'Directory.Packages.props'), '<Project><PropertyGroup><MonicaVersion>1.2.3</MonicaVersion></PropertyGroup><ItemGroup><PackageVersion Include="Monica.Core" Version="$(MonicaVersion)" /></ItemGroup></Project>');
  write(path.join(root, 'App.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" VersionOverride="1.2.4" /></ItemGroup></Project>');
  assert.equal(detectFrameworkVersion(root).version, '1.2.4');
  write(path.join(root, 'Directory.Packages.props'), '<Project><ItemGroup><PackageVersion Include="Monica.Core" Version="$(MissingVersion)" /></ItemGroup></Project>');
  assert.throws(() => detectFrameworkVersion(root), (error) => error.code === 'version_property_unresolved');
  write(path.join(root, 'Directory.Packages.props'), '<Project><ItemGroup><PackageVersion Include="Monica.Core" /></ItemGroup></Project>');
  assert.throws(() => detectFrameworkVersion(root), (error) => error.code === 'version_unresolved');
  fs.rmSync(path.join(root, 'Directory.Packages.props'));
  write(path.join(root, 'App.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" /></ItemGroup></Project>');
  assert.throws(() => detectFrameworkVersion(root), (error) => error.code === 'package_version_unresolved');
});

test('ProjectReference provenance is exact and cannot hide a mismatched NuGet version', async (t) => {
  const root = temporaryDirectory(t);
  const monica = path.join(root, 'Monica');
  fs.mkdirSync(monica);
  write(path.join(monica, 'Directory.Build.props'), '<Project><PropertyGroup><PackageVersion>1.2.3</PackageVersion></PropertyGroup></Project>');
  write(path.join(monica, 'Monica.Core.csproj'), '<Project />');
  const sourceCommit = initializeGit(monica);
  const consumer = path.join(root, 'consumer');
  fs.mkdirSync(consumer);
  write(path.join(consumer, 'App.csproj'), `<Project><ItemGroup><ProjectReference Include="${path.join(monica, 'Monica.Core.csproj')}" /><PackageReference Include="Monica.Hosting" Version="1.2.4" /></ItemGroup></Project>`);
  assert.throws(() => detectFrameworkVersion(consumer), (error) => error.code === 'mixed_framework_versions');
  write(path.join(consumer, 'App.csproj'), `<Project><ItemGroup><ProjectReference Include="${path.join(monica, 'Monica.Core.csproj')}" /></ItemGroup></Project>`);
  const detected = detectFrameworkVersion(consumer);
  assert.equal(detected.entries[0].sourceCommit, sourceCommit);
  const plan = await buildPlan('init', baseOptions(root, consumer));
  assert.ok(plan.blockers.some((entry) => entry.code === 'project_reference_release_mismatch'));
  write(path.join(monica, 'Monica.Core.csproj'), '<Project><PropertyGroup><Dirty>true</Dirty></PropertyGroup></Project>');
  assert.throws(() => detectFrameworkVersion(consumer), (error) => error.code === 'dirty_project_reference_source');
});

test('repository traversal ignores generated and tool-cache trees', (t) => {
  const root = temporaryDirectory(t);
  applicationWorkspace(root);
  write(path.join(root, '.tmp', 'nested', 'Hidden.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" Version="9.9.9" /></ItemGroup></Project>');
  write(path.join(root, 'application', 'obj', 'Generated.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" Version="8.8.8" /></ItemGroup></Project>');
  const started = performance.now();
  const detected = detectFrameworkVersion(root);
  assert.equal(detected.version, VERSION);
  assert.ok(performance.now() - started < 2000);
});

test('global source bind resolves exact cached source and never changes active release', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const options = baseOptions(root, workspace);
  const init = await buildPlan('init', options);
  const skills = init.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill);
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, skills);
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  await applyPlan('init', { ...options, planDigest: init.planDigest, precomputedPlan: init });

  const source = path.join(root, 'managed-source');
  fs.cpSync(SKILLS_ROOT, path.join(source, 'skills'), { recursive: true });
  const resolver = path.join(root, 'resolver.py');
  writeResolver(resolver, source);
  const sourceOptions = {
    sourceAction: 'bind',
    repository: 'monica',
    sourceRef: COMMIT,
    state: options.state,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    offline: true,
    sourceResolver: resolver,
  };
  const sourcePlan = await buildPlan('source', sourceOptions);
  assert.equal(sourcePlan.blockers.length, 0);
  const stateAction = sourcePlan.actions.find((action) => action.path === options.state);
  assert.ok(stateAction);
  assert.equal(JSON.parse(stateAction.content).activeRelease.id, TAG);
  assert.equal(JSON.parse(stateAction.content).sourceBindings['Tairitsua/Monica'].commit, COMMIT);

  await applyPlan('source', {
    ...sourceOptions,
    planDigest: sourcePlan.planDigest,
    precomputedPlan: sourcePlan,
  });
  const listed = listSourceBindings(sourceOptions);
  assert.equal(listed.bindings['Tairitsua/Monica'].binding.sourcePath, source);
  const resolved = resolveSourceBinding({ ...sourceOptions, workspace });
  assert.equal(resolved.status, 'resolved');
  const wrongSource = path.join(root, 'wrong-managed-source');
  fs.mkdirSync(wrongSource);
  writeResolver(resolver, wrongSource);
  const drifted = resolveSourceBinding({ ...sourceOptions, workspace });
  assert.equal(drifted.status, 'unhealthy');
  assert.equal(drifted.severity, 'error');
  assert.equal(drifted.warning.code, 'source_binding_drift');
  writeResolver(resolver, source);
  fs.rmSync(source, { recursive: true, force: true });
  const report = await doctor({ workspace, state: options.state, catalog: CATALOG_PATH, index: INDEX_PATH, offline: true, sourceResolver: resolver });
  assert.ok(report.checks.some((entry) => entry.id === 'source-binding:monica'
    && entry.status === 'error'
    && /source is unavailable/.test(entry.message)));
  const refreshed = await buildPlan('update', {
    workspace,
    state: options.state,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    releaseTag: TAG,
    fetchImplementation: releaseFetch(),
    sourceResolver: resolver,
  });
  assert.ok(refreshed.warnings.some((entry) => entry.code === 'stored_source_invalid'));
  assert.equal(loadState(options.state).sourceBindings['Tairitsua/Monica'].commit, COMMIT);
});

test('active source releases keep a clean manifest-compatible Monica binding', async (t) => {
  const root = temporaryDirectory(t);
  const source = path.join(root, 'Monica-source');
  fs.mkdirSync(path.join(source, '.monica'), { recursive: true });
  fs.cpSync(SKILLS_ROOT, path.join(source, 'skills'), { recursive: true });
  fs.copyFileSync(CATALOG_PATH, path.join(source, '.monica', 'agent-skill-catalog.json'));
  const commit = initializeGit(source);
  const binding = verifyLocalSource(source, { exactRef: commit });
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.activeRelease = { id: `source:${commit}`, monicaVersion: null, tag: null, commit, catalogDigest: CATALOG_DIGEST };
  state.sourceBindings['Tairitsua/Monica'] = binding;
  write(statePath, stableJson(state, 2));
  const common = { state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH, repository: 'monica' };

  const same = await buildPlan('source', { ...common, sourceAction: 'bind', sourcePath: source, sourceRef: commit });
  assert.equal(same.blockers.length, 0, JSON.stringify(same.blockers));
  const unbind = await buildPlan('source', { ...common, sourceAction: 'unbind' });
  assert.ok(unbind.blockers.some((entry) => entry.code === 'active_source_binding_required'));

  write(path.join(source, 'README.md'), 'dirty\n');
  await assert.rejects(
    applyPlan('source', { ...common, sourceAction: 'bind', sourcePath: source, sourceRef: commit, planDigest: same.planDigest, precomputedPlan: same }),
    (error) => error.code === 'dirty_exact_source',
  );
  const dirty = await buildPlan('source', { ...common, sourceAction: 'bind', sourcePath: source, sourceRef: commit });
  assert.ok(dirty.blockers.some((entry) => entry.code === 'dirty_exact_source'));
});

test('source channel binds catalog and skill digests to an exact clean checkout', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const source = path.join(root, 'Monica-source');
  fs.mkdirSync(path.join(source, '.monica'), { recursive: true });
  fs.cpSync(SKILLS_ROOT, path.join(source, 'skills'), { recursive: true });
  fs.copyFileSync(CATALOG_PATH, path.join(source, '.monica', 'agent-skill-catalog.json'));
  execFileSync('git', ['init', '-q', source]);
  execFileSync('git', ['-C', source, 'config', 'user.email', 'guide-test@example.invalid']);
  execFileSync('git', ['-C', source, 'config', 'user.name', 'Guide Test']);
  execFileSync('git', ['-C', source, 'remote', 'add', 'origin', 'https://github.com/Tairitsua/Monica.git']);
  execFileSync('git', ['-C', source, 'add', '.']);
  execFileSync('git', ['-C', source, 'commit', '-qm', 'source fixture']);
  const commit = execFileSync('git', ['-C', source, 'rev-parse', 'HEAD'], { encoding: 'utf8' }).trim();
  const plan = await buildPlan('init', {
    workspace,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'application',
    capabilities: ['modular-monolith'],
    agents: ['codex'],
    agentValidationRunner: () => ({ status: 0, stdout: '[]', stderr: '', error: null }),
    channel: 'source',
    sourceRef: commit,
    sourcePath: source,
    offline: true,
  });
  assert.equal(plan.blockers.length, 0);
  assert.equal(plan.context.targetRelease.commit, commit);
  assert.ok(plan.actions.filter((action) => action.type === 'install-skill').every((action) => action.offline
    && action.args.includes('--offline')
    && action.source === path.join(source, 'skills', action.skill)
    && !action.source.includes('github.com')));
  const verification = plan.actions.find((action) => action.type === 'verify-skills');
  assert.ok(verification.commands.every((command) => command.includes('npx --offline --yes skills@1.5.21')));
  assert.equal(verification.manifest.skillDigestAlgorithm, 'sha256-file-manifest-v1');
  assert.ok(Object.keys(verification.manifest.skillDigests).length > 0);
  assert.ok(Object.values(verification.manifest.skillRevisions).every((revision) => revision === null));
  assert.ok(Object.values(verification.manifest.skillLastChangedIn).every((release) => release === null));
  const plannedState = JSON.parse(plan.actions.find((action) => action.path === path.join(root, 'state.json')).content);
  assert.ok(Object.values(plannedState.managedSkills).every((record) => record.revision === null
    && /^sha256:/.test(record.digest)
    && record.lastChangedIn === null));
  assert.equal(plan.warnings.some((entry) => entry.code === 'source_catalog_parity_unproven'), false);

  const previous = process.env.MONICA_GUIDE_NPX;
  const installedSkills = plan.actions.filter((action) => action.type === 'install-skill').map((action) => action.skill);
  process.env.MONICA_GUIDE_NPX = mockNpx(root, installedSkills);
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  await applyPlan('init', { ...{
    workspace,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'application',
    capabilities: ['modular-monolith'],
    channel: 'source',
    sourceRef: commit,
    sourcePath: source,
    offline: true,
  }, planDigest: plan.planDigest, precomputedPlan: plan });
  const environment = await inspectEnvironment({
    workspace,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    channel: 'source',
    sourceRef: commit,
    offline: true,
  });
  const status = statusEnvelope(environment);
  assert.ok(status.observation.managedSkills.every((entry) => entry.changeState === 'unchanged'
    && entry.installed.revision === null
    && entry.installed.digest !== null));
  assert.match(renderStatus(status), new RegExp(`source@${commit.slice(0, 12)}`));
  const report = await doctor({
    workspace,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    channel: 'source',
    sourceRef: commit,
    offline: true,
  });
  assert.ok(report.checks.some((entry) => entry.id.startsWith('skill-version:') && entry.message.includes(`source@${commit}`)));
});

test('framework exact-source workflows reject ordinary dirt and catalog or skill drift', async (t) => {
  const root = temporaryDirectory(t);
  const source = path.join(root, 'Monica');
  fs.mkdirSync(path.join(source, '.monica'), { recursive: true });
  fs.mkdirSync(path.join(source, 'Monica.Core'));
  write(path.join(source, 'Monica.slnx'), '<Solution />\n');
  fs.cpSync(SKILLS_ROOT, path.join(source, 'skills'), { recursive: true });
  fs.copyFileSync(CATALOG_PATH, path.join(source, '.monica', 'agent-skill-catalog.json'));
  const commit = initializeGit(source);
  const options = {
    workspace: source,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'framework-contributor',
    agents: ['codex'],
    agentValidationRunner: () => ({ status: 0, stdout: '[]', stderr: '', error: null }),
    channel: 'source',
    sourceRef: commit,
    sourcePath: source,
  };
  const clean = await buildPlan('init', options);
  assert.equal(clean.blockers.length, 0);

  write(path.join(source, 'notes.txt'), 'ordinary framework worktree change\n');
  const dirty = await buildPlan('init', options);
  assert.ok(dirty.blockers.some((entry) => entry.code === 'dirty_exact_source'));

  const state = emptyState();
  state.sourceBindings['Tairitsua/Monica'] = verifyLocalSource(source, { exactRef: commit });
  write(options.state, stableJson(state, 2));
  const report = await doctor({
    workspace: source,
    state: options.state,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'framework-contributor',
    channel: 'source',
    sourceRef: commit,
  });
  assert.ok(report.checks.some((entry) => entry.id === 'source-binding:monica' && entry.status === 'error' && /local changes/.test(entry.message)));

  fs.unlinkSync(path.join(source, 'notes.txt'));
  fs.appendFileSync(path.join(source, 'skills', 'monica-guide', 'SKILL.md'), '\ncontract drift\n');
  const blocked = await buildPlan('init', options);
  assert.ok(blocked.blockers.some((entry) => entry.code === 'dirty_source_contract'));
});

test('extension ProjectReference and docs/framework repository prerequisites are blocked', async (t) => {
  const root = temporaryDirectory(t);
  const monica = path.join(root, 'Monica');
  fs.mkdirSync(monica);
  write(path.join(monica, 'Directory.Build.props'), '<Project><PropertyGroup><PackageVersion>1.2.3</PackageVersion></PropertyGroup></Project>');
  write(path.join(monica, 'Monica.Core.csproj'), '<Project />');
  const extension = path.join(root, 'extension');
  fs.mkdirSync(extension);
  write(path.join(extension, 'MyExtension.csproj'), `<Project><ItemGroup><ProjectReference Include="${path.join(monica, 'Monica.Core.csproj')}" /></ItemGroup></Project>`);
  const extensionPlan = await buildPlan('init', { ...baseOptions(root, extension), profile: 'extension-author' });
  assert.ok(extensionPlan.blockers.some((entry) => entry.code === 'extension_project_reference_forbidden'));

  const docsPlan = await buildPlan('init', { ...baseOptions(root, applicationWorkspace(path.join(root, 'docs-case'))), profile: 'docs-contributor' });
  assert.ok(docsPlan.blockers.some((entry) => entry.code === 'docs_repository_required'));
  const frameworkPlan = await buildPlan('init', { ...baseOptions(root, applicationWorkspace(path.join(root, 'framework-case'))), profile: 'framework-contributor' });
  assert.ok(frameworkPlan.blockers.some((entry) => entry.code === 'framework_repository_required'));
});

test('conditional extension source remains an on-demand global binding', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const isolatedHome = path.join(root, 'isolated-home');
  fs.mkdirSync(isolatedHome);
  const previousHome = process.env.HOME;
  const previousResolver = process.env.MONICA_INSPECT_SOURCE_CLI;
  process.env.HOME = isolatedHome;
  delete process.env.MONICA_INSPECT_SOURCE_CLI;
  t.after(() => {
    if (previousHome === undefined) delete process.env.HOME;
    else process.env.HOME = previousHome;
    if (previousResolver === undefined) delete process.env.MONICA_INSPECT_SOURCE_CLI;
    else process.env.MONICA_INSPECT_SOURCE_CLI = previousResolver;
  });

  const plan = await buildPlan('init', {
    ...baseOptions(root, workspace),
    profile: 'extension-author',
    agents: ['codex'],
  });
  assert.equal(plan.blockers.some((entry) => entry.code === 'source_resolver_prerequisite_missing'), false);
  assert.ok(plan.warnings.some((entry) => entry.code === 'source_available_on_demand'));
  assert.equal(plan.actions.some((action) => action.skill === 'inspect-dependency-source'), false);

  const exactPlan = await buildPlan('init', {
    ...baseOptions(root, workspace),
    profile: 'extension-author',
    agents: ['codex'],
    capabilities: ['framework-internal-work'],
  });
  assert.ok(exactPlan.blockers.some((entry) => entry.code === 'source_resolver_prerequisite_missing'));
  assert.equal(exactPlan.warnings.some((entry) => entry.code === 'source_available_on_demand'), false);
});

test('malformed managed markers are refused and doctor emits stable JSON checks', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  write(path.join(workspace, 'AGENTS.md'), '<!-- monica-guide:managed:start -->\nunterminated\n');
  fs.mkdirSync(path.join(workspace, '.agents', 'skills', 'mo-development'), { recursive: true });
  await assert.rejects(buildPlan('init', baseOptions(root, workspace)), (error) => error.code === 'malformed_instruction_block');
  const report = await doctor(baseOptions(root, workspace));
  assert.equal(report.schemaVersion, 1);
  assert.ok(report.checks.some((entry) => entry.id === 'managed-instructions' && entry.status === 'error'));
  assert.ok(report.checks.some((entry) => entry.id === 'stale-alias:mo-development' && entry.status === 'warning'));
});

test('state v1 migration separates durable preferences and reaches schema v4', (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  write(statePath, stableJson({ schemaVersion: 1, activeRelease: null, agentTargets: [], sourceBindings: {}, workspaces: { abc: { profile: 'application' } }, contributionPreference: 'prepare' }, 2));
  const state = loadState(statePath);
  assert.equal(state.schemaVersion, 4);
  assert.equal(state.workspacePreferences.abc.profile, 'application');
  assert.equal(state.contributionPreferences.abc, 'prepare');
  assert.deepEqual(state.observations, {});
});

test('state v2 migration preserves managed names as unknown version records', (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  write(statePath, stableJson({
    ...emptyState(),
    schemaVersion: 2,
    managedSkills: ['monica-guide', 'monica-application'],
  }, 2));
  const state = loadState(statePath);
  assert.equal(state.schemaVersion, 4);
  assert.deepEqual(state.managedSkills['monica-guide'], { revision: null, digest: null, lastChangedIn: null });
  assert.deepEqual(state.managedSkills['monica-application'], { revision: null, digest: null, lastChangedIn: null });
});

test('state v2 migration rejects malformed or duplicate managed skill names', (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  for (const managedSkills of [[null], [123], ['Monica-Guide'], ['monica-guide', 'monica-guide']]) {
    write(statePath, stableJson({
      ...emptyState(),
      schemaVersion: 2,
      managedSkills,
    }, 2));
    assert.throws(() => loadState(statePath), (error) => error.code === 'invalid_state');
  }
});

test('state v3 accepts only coherent tagged, source, or migrated skill metadata tuples', (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  const valid = emptyState();
  valid.managedSkills = {
    tagged: { revision: 2, digest: `sha256:${'a'.repeat(64)}`, lastChangedIn: 'v1.2.3' },
    source: { revision: null, digest: `sha256:${'b'.repeat(64)}`, lastChangedIn: null },
    migrated: { revision: null, digest: null, lastChangedIn: null },
  };
  write(statePath, stableJson(valid, 2));
  assert.deepEqual(Object.keys(loadState(statePath).managedSkills), ['migrated', 'source', 'tagged']);
  for (const record of [
    { revision: 1, digest: null, lastChangedIn: 'v1.2.3' },
    { revision: null, digest: `sha256:${'c'.repeat(64)}`, lastChangedIn: 'v1.2.3' },
    { revision: 1, digest: `sha256:${'d'.repeat(64)}`, lastChangedIn: null },
  ]) {
    valid.managedSkills = { invalid: record };
    write(statePath, stableJson(valid, 2));
    assert.throws(() => loadState(statePath), (error) => error.code === 'invalid_state' && /complete tagged/.test(error.message));
  }
});

test('state v3 promotes identical global source bindings and preserves conflicts for an explicit decision', async (t) => {
  const root = temporaryDirectory(t);
  const binding = (sourcePath, commit = COMMIT) => ({
    repository: 'Tairitsua/Monica',
    ref: commit,
    commit,
    sourcePath,
    resolutionKind: 'exact_commit',
    provenance: 'local-git',
    access: 'read-only',
    dirty: false,
    managed: false,
    verificationState: 'verified',
  });
  const identicalPath = path.join(root, 'same');
  const identicalStatePath = path.join(root, 'identical.json');
  write(identicalStatePath, stableJson({ ...emptyState(), schemaVersion: 3, sourceBindings: { one: binding(identicalPath), two: binding(identicalPath) } }, 2));
  const identical = loadState(identicalStatePath);
  assert.equal(identical.sourceBindings['Tairitsua/Monica'].sourcePath, identicalPath);
  assert.deepEqual(identical.sourceBindingCandidates, {});

  const conflictStatePath = path.join(root, 'conflict.json');
  write(conflictStatePath, stableJson({
    ...emptyState(),
    schemaVersion: 3,
    sourceBindings: { one: binding(path.join(root, 'one')), two: binding(path.join(root, 'two'), 'b'.repeat(40)) },
  }, 2));
  const conflict = loadState(conflictStatePath);
  assert.equal(conflict.sourceBindings['Tairitsua/Monica'], undefined);
  assert.equal(conflict.sourceBindingCandidates['Tairitsua/Monica'].length, 2);
  const unrelated = await buildPlan('source', {
    sourceAction: 'unbind',
    repository: 'docs',
    state: conflictStatePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
  });
  assert.ok(unrelated.blockers.some((entry) => entry.code === 'source_binding_migration_required'));
  const resolution = await buildPlan('source', {
    sourceAction: 'unbind',
    repository: 'monica',
    state: conflictStatePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
  });
  assert.equal(resolution.blockers.length, 0);
  const resolvedState = JSON.parse(resolution.actions.find((entry) => entry.path === conflictStatePath).content);
  assert.deepEqual(resolvedState.sourceBindingCandidates, {});
  const malformedStatePath = path.join(root, 'malformed.json');
  write(malformedStatePath, stableJson({ ...emptyState(), schemaVersion: 3, sourceBindings: { broken: { commit: COMMIT } } }, 2));
  assert.throws(() => loadState(malformedStatePath), (error) => error.code === 'invalid_state');
});

test('Monica and Monica.Docs bind globally as lookup-only sources while dirty and moved observations remain transient', async (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  const repositories = [
    ['monica', 'Tairitsua/Monica', path.join(root, 'Monica')],
    ['docs', 'Tairitsua/Monica.Docs', path.join(root, 'Monica.Docs')],
  ];
  for (const [alias, canonical, sourcePath] of repositories) {
    fs.mkdirSync(sourcePath);
    write(path.join(sourcePath, 'README.md'), `${canonical}\n`);
    initializeGit(sourcePath, `https://github.com/${canonical}.git`);
    const options = { sourceAction: 'bind', repository: alias, sourcePath, state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH };
    const plan = await buildPlan('source', options);
    assert.equal(plan.blockers.length, 0, JSON.stringify(plan.blockers));
    await applyPlan('source', { ...options, planDigest: plan.planDigest, precomputedPlan: plan });
  }
  const listed = listSourceBindings({ state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.ok(listed.bindings['Tairitsua/Monica'].binding);
  assert.ok(listed.bindings['Tairitsua/Monica.Docs'].binding);
  write(path.join(root, 'Monica.Docs', 'notes.md'), 'dirty docs worktree\n');
  const dirtyDocs = resolveSourceBinding({ repository: 'docs', state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.equal(dirtyDocs.status, 'resolved');
  assert.equal(dirtyDocs.severity, 'warning');
  assert.equal(dirtyDocs.observation.dirty, true);
  assert.ok(dirtyDocs.observation.warnings.some((entry) => entry.code === 'source_checkout_dirty'));

  let sourceOutput = '';
  const originalWrite = process.stdout.write;
  process.stdout.write = (chunk) => { sourceOutput += String(chunk); return true; };
  try {
    const listExitCode = await main(['source', 'list', '--state', statePath, '--catalog', CATALOG_PATH, '--index', INDEX_PATH]);
    assert.equal(listExitCode, 1);
    const resolveExitCode = await main(['source', 'resolve', '--repository', 'docs', '--state', statePath, '--catalog', CATALOG_PATH, '--index', INDEX_PATH]);
    assert.equal(resolveExitCode, 1);
  } finally {
    process.stdout.write = originalWrite;
  }
  assert.match(sourceOutput, /ref:/);
  assert.match(sourceOutput, /provenance:/);
  assert.match(sourceOutput, /compatibility:/);
  assert.match(sourceOutput, /source_checkout_dirty/);

  write(path.join(root, 'Monica', 'next.md'), 'next commit\n');
  execFileSync('git', ['-C', path.join(root, 'Monica'), 'add', '.']);
  execFileSync('git', ['-C', path.join(root, 'Monica'), 'commit', '-qm', 'move source']);
  const moved = resolveSourceBinding({ repository: 'monica', state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.equal(moved.status, 'unhealthy');
  assert.equal(moved.observation.pathHealth, 'moved');

  const workspace = applicationWorkspace(path.join(root, 'forget'));
  const state = loadState(statePath);
  state.workspacePreferences[workspaceKey(workspace)] = { workspace, profile: 'application' };
  write(statePath, stableJson(state, 2));
  const forget = await buildPlan('forget', { workspace, state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH });
  const forgottenState = JSON.parse(forget.actions.find((entry) => entry.path === statePath).content);
  assert.equal(Object.keys(forgottenState.sourceBindings).length, 2);
});

test('source resolve derives a NuGet-only workspace expectation from the immutable version index', (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const indexPath = path.join(root, 'index.json');
  write(indexPath, stableJson(releaseIndex(), 2));

  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.activeRelease = {
    id: 'v9.9.9',
    monicaVersion: '9.9.9',
    tag: 'v9.9.9',
    commit: 'b'.repeat(40),
    catalogDigest: CATALOG_DIGEST,
  };
  write(statePath, stableJson(state, 2));

  const source = path.join(root, 'cached-source');
  fs.mkdirSync(source);
  const resolver = path.join(root, 'resolver.py');
  writeResolver(resolver, source);
  const resolved = resolveSourceBinding({
    repository: 'monica',
    workspace,
    state: statePath,
    catalog: CATALOG_PATH,
    index: indexPath,
    sourceResolver: resolver,
  });
  assert.equal(resolved.status, 'offered');
  assert.equal(resolved.expectation.basis, 'framework-version-release');
  assert.equal(resolved.expectation.commit, COMMIT);
  assert.equal(resolved.offeredBinding.commit, COMMIT);
  assert.notEqual(resolved.expectation.commit, state.activeRelease.commit);

  const unavailable = resolveSourceBinding({
    repository: 'monica',
    workspace,
    state: statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    sourceResolver: resolver,
  });
  assert.equal(unavailable.status, 'blocked');
  assert.equal(unavailable.severity, 'error');
  assert.equal(unavailable.offeredBinding, null);
  assert.equal(unavailable.expectation.basis, 'unresolved');
});

test('source expectations prefer current dependency evidence and canonical Monica HEAD over stale persisted releases', (t) => {
  const root = temporaryDirectory(t);
  const indexPath = path.join(root, 'index.json');
  write(indexPath, stableJson(releaseIndex(), 2));
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.activeRelease = {
    id: 'v9.9.9',
    monicaVersion: '9.9.9',
    tag: 'v9.9.9',
    commit: 'c'.repeat(40),
    catalogDigest: CATALOG_DIGEST,
  };
  write(statePath, stableJson(state, 2));

  const application = applicationWorkspace(path.join(root, 'stale-application'));
  write(path.join(application, '.monica', 'guide.json'), stableJson({
    schemaVersion: 2,
    profile: 'application',
    channel: 'stable',
    capabilities: ['modular-monolith'],
    expectedCatalogRelease: {
      id: 'v0.9.0',
      monicaVersion: '0.9.0',
      tag: 'v0.9.0',
      commit: 'b'.repeat(40),
      catalogDigest: CATALOG_DIGEST,
      indexTag: 'v0.9.0',
    },
    instructionBlockVersion: 1,
    managedClaudeImport: false,
  }, 2));
  const cachedApplicationSource = path.join(root, 'cached-application-source');
  fs.mkdirSync(cachedApplicationSource);
  const applicationResolver = path.join(root, 'application-resolver.py');
  writeResolver(applicationResolver, cachedApplicationSource);
  const applicationResult = resolveSourceBinding({
    repository: 'monica',
    workspace: application,
    state: statePath,
    catalog: CATALOG_PATH,
    index: indexPath,
    sourceResolver: applicationResolver,
  });
  assert.equal(applicationResult.expectation.basis, 'framework-version-release');
  assert.equal(applicationResult.expectation.commit, COMMIT);

  const monica = path.join(root, 'Monica');
  fs.mkdirSync(monica);
  write(path.join(monica, 'Monica.slnx'), '<Solution />\n');
  const head = initializeGit(monica);
  const cachedFrameworkSource = path.join(root, 'cached-framework-source');
  fs.mkdirSync(cachedFrameworkSource);
  const frameworkResolver = path.join(root, 'framework-resolver.py');
  writeResolver(frameworkResolver, cachedFrameworkSource, { actualCommit: head, expectedCommit: head, ref: head });
  const frameworkResult = resolveSourceBinding({
    repository: 'monica',
    workspace: monica,
    state: statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    sourceResolver: frameworkResolver,
  });
  assert.equal(frameworkResult.expectation.basis, 'monica-workspace-head');
  assert.equal(frameworkResult.expectation.commit, head);
  assert.equal(frameworkResult.offeredBinding.commit, head);
});

test('source lookup and the CLI distinguish operational workspace failures from domain version blockers', async (t) => {
  const root = temporaryDirectory(t);
  const state = path.join(root, 'state.json');
  const common = ['--state', state, '--catalog', CATALOG_PATH, '--index', INDEX_PATH, '--json'];

  const missing = path.join(root, 'missing');
  assert.throws(
    () => resolveSourceBinding({ repository: 'monica', workspace: missing, state, catalog: CATALOG_PATH, index: INDEX_PATH }),
    (error) => error.code === 'workspace_unavailable',
  );
  const missingProcess = spawnSync(process.execPath, [GUIDE_ENTRYPOINT, 'source', 'resolve', '--repository', 'monica', '--workspace', missing, ...common], { encoding: 'utf8' });
  assert.equal(missingProcess.status, 2, missingProcess.stderr);
  assert.equal(JSON.parse(missingProcess.stderr).error.code, 'workspace_unavailable');

  const mixed = path.join(root, 'mixed');
  write(path.join(mixed, 'One.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" Version="1.0.0" /></ItemGroup></Project>');
  write(path.join(mixed, 'Two.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" Version="2.0.0" /></ItemGroup></Project>');
  const mixedLookup = resolveSourceBinding({ repository: 'monica', workspace: mixed, state, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.equal(mixedLookup.status, 'blocked');
  assert.equal(mixedLookup.warning.code, 'mixed_framework_versions');
  const mixedProcess = spawnSync(process.execPath, [GUIDE_ENTRYPOINT, 'source', 'resolve', '--repository', 'monica', '--workspace', mixed, ...common], { encoding: 'utf8' });
  assert.equal(mixedProcess.status, 3, mixedProcess.stderr);
  assert.equal(JSON.parse(mixedProcess.stdout).warning.code, 'mixed_framework_versions');

  const ranged = path.join(root, 'range');
  write(path.join(ranged, 'Range.csproj'), '<Project><ItemGroup><PackageReference Include="Monica.Core" Version="[1.0.0,2.0.0)" /></ItemGroup></Project>');
  const rangeLookup = resolveSourceBinding({ repository: 'monica', workspace: ranged, state, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.equal(rangeLookup.status, 'blocked');
  assert.equal(rangeLookup.warning.code, 'version_range_unsupported');
  const rangeProcess = spawnSync(process.execPath, [GUIDE_ENTRYPOINT, 'source', 'resolve', '--repository', 'monica', '--workspace', ranged, ...common], { encoding: 'utf8' });
  assert.equal(rangeProcess.status, 3, rangeProcess.stderr);
  assert.equal(JSON.parse(rangeProcess.stdout).warning.code, 'version_range_unsupported');

  const blockedOptions = {
    sourceAction: 'bind',
    repository: 'monica',
    sourcePath: missing,
    state,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
  };
  const blockedPlan = await buildPlan('source', blockedOptions);
  assert.ok(blockedPlan.blockers.length);
  const applyProcess = spawnSync(process.execPath, [
    GUIDE_ENTRYPOINT,
    'source',
    'bind',
    '--repository',
    'monica',
    '--source-path',
    missing,
    '--apply',
    '--plan-digest',
    blockedPlan.planDigest,
    ...common,
  ], { encoding: 'utf8' });
  assert.equal(applyProcess.status, 3, applyProcess.stderr);
  assert.equal(JSON.parse(applyProcess.stderr).error.code, 'plan_blocked');
});

test('dirty Docs ProjectReferences remain warning-only lookups while workspace diagnostics retain both bindings', async (t) => {
  const root = temporaryDirectory(t);
  const monica = path.join(root, 'Monica');
  fs.mkdirSync(monica);
  write(path.join(monica, 'Directory.Build.props'), '<Project><PropertyGroup><PackageVersion>1.2.3</PackageVersion></PropertyGroup></Project>');
  write(path.join(monica, 'Monica.Core.csproj'), '<Project />');
  const monicaCommit = initializeGit(monica);
  const monicaBinding = verifyLocalSource(monica, { repository: 'Tairitsua/Monica', exactRef: monicaCommit });

  const docs = path.join(root, 'Monica.Docs');
  write(path.join(docs, 'docs', 'en-US', 'index.md'), '# Docs\n');
  write(path.join(docs, 'docs', 'zh-CN', 'index.md'), '# 文档\n');
  write(path.join(docs, 'frontend', 'monica-docs-web', 'package.json'), '{"private":true}\n');
  write(path.join(docs, 'Docs.csproj'), `<Project><ItemGroup><ProjectReference Include="${path.join(monica, 'Monica.Core.csproj')}" /></ItemGroup></Project>`);
  const docsCommit = initializeGit(docs, 'https://github.com/Tairitsua/Monica.Docs.git');
  const docsBinding = verifyLocalSource(docs, { repository: 'Tairitsua/Monica.Docs', exactRef: docsCommit });

  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.sourceBindings['Tairitsua/Monica'] = monicaBinding;
  state.sourceBindings['Tairitsua/Monica.Docs'] = docsBinding;
  write(statePath, stableJson(state, 2));
  write(path.join(monica, 'Monica.Core.csproj'), '<Project><PropertyGroup><Dirty>true</Dirty></PropertyGroup></Project>');

  const options = { workspace: docs, state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH };
  const lookup = resolveSourceBinding({ ...options, repository: 'monica' });
  assert.equal(lookup.status, 'resolved');
  assert.equal(lookup.severity, 'warning');
  assert.equal(lookup.expectation.basis, 'dirty-project-reference-head');
  assert.equal(lookup.expectation.commit, monicaCommit);
  assert.equal(lookup.observation.pathHealth, 'available');
  assert.ok(lookup.observation.warnings.some((entry) => entry.code === 'dirty_project_reference_source'));

  const environment = await inspectEnvironment(options);
  const status = statusEnvelope(environment);
  assert.equal(status.severity, 'warning');
  assert.equal(status.error, null);
  assert.ok(status.observation.sourceBindings['Tairitsua/Monica'].binding);
  assert.ok(status.observation.sourceBindings['Tairitsua/Monica.Docs'].binding);
  const renderedStatus = renderStatus(status);
  assert.match(renderedStatus, /Tairitsua\/Monica\s+/);
  assert.match(renderedStatus, /Tairitsua\/Monica\.Docs\s+/);
  assert.match(renderedStatus, /dirty_project_reference_source/);

  const report = await doctor(options);
  assert.equal(report.status, 'warning');
  assert.equal(report.summary.errors, 0);
  assert.equal(report.checks.find((entry) => entry.id === 'profile').status, 'warning');
  assert.match(report.checks.find((entry) => entry.id === 'profile').message, /docs-contributor/);
  assert.equal(report.checks.find((entry) => entry.id === 'source-binding:monica').status, 'warning');
  assert.equal(report.checks.find((entry) => entry.id === 'source-binding:docs').status, 'ok');

  let output = '';
  const originalWrite = process.stdout.write;
  process.stdout.write = (chunk) => { output += String(chunk); return true; };
  try {
    assert.equal(await main(['source', 'resolve', '--repository', 'monica', '--workspace', docs, '--state', statePath, '--catalog', CATALOG_PATH, '--index', INDEX_PATH]), 1);
  } finally {
    process.stdout.write = originalWrite;
  }
  assert.match(output, /resolved \(warning\)/);
  assert.match(output, /dirty_project_reference_source/);
});

test('unsupported immutable catalog schemas fail closed with actionable Guide guidance', (t) => {
  const root = temporaryDirectory(t);
  const legacyCatalog = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8'));
  legacyCatalog.schemaVersion = 1;
  const legacyCatalogPath = path.join(root, 'legacy-catalog.json');
  write(legacyCatalogPath, stableJson(legacyCatalog, 2));
  assert.throws(
    () => loadCatalog({ catalogPath: legacyCatalogPath, indexPath: INDEX_PATH }),
    (error) => error.code === 'catalog_schema_mismatch'
      && /immutable release/.test(error.message)
      && /explicitly upgrade\/switch/.test(error.message)
      && /will not reinterpret or substitute/.test(error.details?.remediation || ''),
  );
});

test('read-only intents reject apply controls and global-only source commands reject workspace', () => {
  for (const argv of [
    ['overview', '--apply'],
    ['status', '--plan-digest', `sha256:${'a'.repeat(64)}`],
    ['doctor', '--apply'],
    ['source', 'list', '--apply'],
    ['source', 'resolve', '--repository', 'monica', '--plan-digest', `sha256:${'a'.repeat(64)}`],
  ]) {
    assert.throws(() => parseArguments(argv), (error) => error.code === 'readonly_apply_invalid');
  }
  for (const argv of [
    ['overview', '--workspace', '/tmp'],
    ['source', 'list', '--workspace', '/tmp'],
    ['source', 'bind', '--repository', 'monica', '--source-path', '/tmp', '--workspace', '/tmp'],
    ['source', 'unbind', '--repository', 'docs', '--workspace', '/tmp'],
  ]) {
    assert.throws(() => parseArguments(argv), (error) => error.code === 'workspace_option_invalid');
  }
  assert.doesNotThrow(() => parseArguments(['source', 'resolve', '--repository', 'monica', '--workspace', '/tmp']));
  const processResult = spawnSync(process.execPath, [GUIDE_ENTRYPOINT, 'overview', '--workspace', '/tmp', '--json'], { encoding: 'utf8' });
  assert.equal(processResult.status, 2, processResult.stderr);
  assert.equal(JSON.parse(processResult.stderr).error.code, 'workspace_option_invalid');
});

test('local source binding is revalidated under the state lock before persistence', async (t) => {
  const root = temporaryDirectory(t);
  const source = path.join(root, 'Monica');
  fs.mkdirSync(source);
  write(path.join(source, 'README.md'), 'first\n');
  const firstCommit = initializeGit(source);
  const options = {
    sourceAction: 'bind',
    repository: 'monica',
    sourcePath: source,
    sourceRef: firstCommit,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
  };
  const plan = await buildPlan('source', options);
  assert.equal(plan.blockers.length, 0);
  write(path.join(source, 'README.md'), 'second\n');
  execFileSync('git', ['-C', source, 'add', 'README.md']);
  execFileSync('git', ['-C', source, 'commit', '-qm', 'move checkout']);
  await assert.rejects(
    applyPlan('source', { ...options, planDigest: plan.planDigest, precomputedPlan: plan }),
    (error) => error.code === 'source_binding_drift',
  );
  assert.equal(fs.existsSync(options.state), false);
});

test('cached source binding is revalidated through the cache-only resolver under the state lock', async (t) => {
  const root = temporaryDirectory(t);
  const source = path.join(root, 'cached-source');
  const movedSource = path.join(root, 'moved-cached-source');
  fs.mkdirSync(source);
  fs.mkdirSync(movedSource);
  const resolver = path.join(root, 'resolver.py');
  writeResolver(resolver, source);
  const options = {
    sourceAction: 'bind',
    repository: 'monica',
    sourceRef: COMMIT,
    sourceResolver: resolver,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    offline: true,
  };
  const plan = await buildPlan('source', options);
  assert.equal(plan.blockers.length, 0);
  writeResolver(resolver, movedSource);
  await assert.rejects(
    applyPlan('source', { ...options, planDigest: plan.planDigest, precomputedPlan: plan }),
    (error) => error.code === 'source_binding_drift',
  );
  assert.equal(fs.existsSync(options.state), false);
});

test('external skills CLI and source resolver timeouts are bounded operational failures', (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.agentTargets = ['cursor'];
  write(statePath, stableJson(state, 2));
  const slowNpx = path.join(root, 'slow-npx.mjs');
  write(slowNpx, '#!/usr/bin/env node\nsetTimeout(() => process.stdout.write("[]"), 1000);\n');
  fs.chmodSync(slowNpx, 0o755);
  const doctorProcess = spawnSync(process.execPath, [
    GUIDE_ENTRYPOINT, 'doctor', '--state', statePath, '--catalog', CATALOG_PATH, '--index', INDEX_PATH,
    '--timeout-ms', '50', '--json',
  ], { encoding: 'utf8', env: { ...process.env, MONICA_GUIDE_NPX: slowNpx } });
  assert.equal(doctorProcess.status, 2, doctorProcess.stderr);
  assert.equal(JSON.parse(doctorProcess.stderr).error.code, 'skills_cli_timeout');

  const slowResolver = path.join(root, 'slow-resolver.py');
  write(slowResolver, 'import time\ntime.sleep(1)\n');
  assert.throws(
    () => resolveCachedSource({ exactRef: COMMIT, resolverPath: slowResolver, timeoutMs: 50 }),
    (error) => error.code === 'source_resolver_timeout' && /offline/i.test(error.message),
  );
});

test('resolver paths and canonical Docs workspace commits fail closed when exact evidence is absent', async (t) => {
  const root = temporaryDirectory(t);
  const relativeResolver = path.join(root, 'relative-resolver.py');
  write(relativeResolver, `import json\nprint(json.dumps({"status":"ok","source_path":"relative","verification_state":"verified","resolution_kind":"exact_commit","repository":{"id":"repo","canonical_name":"Tairitsua/Monica"},"artifact":{"id":"artifact","ref":"${COMMIT}","actual_commit":"${COMMIT}","expected_commit":"${COMMIT}"}}))\n`);
  assert.throws(
    () => resolveCachedSource({ exactRef: COMMIT, resolverPath: relativeResolver }),
    (error) => error.code === 'source_contract_invalid',
  );

  const docs = path.join(root, 'Monica.Docs');
  fs.mkdirSync(docs);
  execFileSync('git', ['init', '-q', docs]);
  execFileSync('git', ['-C', docs, 'remote', 'add', 'origin', 'https://github.com/Tairitsua/Monica.Docs.git']);
  const result = resolveSourceBinding({ repository: 'docs', workspace: docs, state: path.join(root, 'state.json'), catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.equal(result.status, 'blocked');
  assert.equal(result.warning.code, 'source_commit_unresolved');
  const plan = await buildPlan('init', {
    ...baseOptions(root, docs),
    state: path.join(root, 'docs-state.json'),
    profile: 'docs-contributor',
    capabilities: [],
    agents: ['codex'],
  });
  assert.ok(plan.blockers.some((entry) => entry.code === 'source_commit_unresolved'
    && entry.details.repository === 'Tairitsua/Monica.Docs'));
});

test('local source refs accept exact commits and tags but reject moving branches', (t) => {
  const root = temporaryDirectory(t);
  const source = path.join(root, 'Monica');
  fs.mkdirSync(source);
  write(path.join(source, 'README.md'), 'source\n');
  const commit = initializeGit(source);
  execFileSync('git', ['-C', source, 'tag', 'v1.2.3']);
  assert.equal(verifyLocalSource(source, { exactRef: commit }).commit, commit);
  assert.equal(verifyLocalSource(source, { exactRef: 'v1.2.3' }).resolutionKind, 'exact_tag');
  const branch = execFileSync('git', ['-C', source, 'branch', '--show-current'], { encoding: 'utf8' }).trim();
  assert.throws(() => verifyLocalSource(source, { exactRef: branch }), (error) => error.code === 'source_ref_not_immutable');
});

test('agent targets are dynamically validated and large Git diffs are fingerprinted without capture limits', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const calls = [];
  const plan = await buildPlan('init', {
    ...baseOptions(root, workspace),
    agents: ['cursor'],
    agentValidationRunner: (_command, args) => {
      calls.push(args);
      return { status: 0, stdout: '[]', stderr: '', error: null };
    },
  });
  assert.equal(plan.blockers.some((entry) => entry.code.startsWith('agent_target_')), false);
  assert.ok(calls.some((args) => args.join(' ').includes('ls -g -a cursor --json')));
  const rejected = await buildPlan('init', {
    ...baseOptions(root, workspace),
    agents: ['future-agent'],
    agentValidationRunner: () => ({ status: 1, stdout: '', stderr: 'unsupported', error: null }),
  });
  assert.ok(rejected.blockers.some((entry) => entry.code === 'agent_target_unavailable'));
  assert.equal(rejected.actions.some((entry) => entry.type === 'install-skill'), false);

  const gitWorkspace = path.join(root, 'large-diff');
  fs.mkdirSync(gitWorkspace);
  write(path.join(gitWorkspace, 'large.txt'), 'base\n');
  initializeGit(gitWorkspace, 'https://github.com/example/large.git');
  const before = gitWorkspaceFingerprint(gitWorkspace);
  fs.writeFileSync(path.join(gitWorkspace, 'large.txt'), 'x'.repeat(9 * 1024 * 1024));
  const after = gitWorkspaceFingerprint(gitWorkspace);
  assert.notEqual(after, before);
});

test('Git status streams inventories beyond the process buffer and fails closed on command errors', (t) => {
  const root = temporaryDirectory(t);
  const largeStatus = path.join(root, 'large-status');
  write(largeStatus, `#!/usr/bin/env node
const chunk = ('?? ' + 'x'.repeat(120) + '\\n').repeat(1000);
for (let index = 0; index < 80; index += 1) process.stdout.write(chunk);
`);
  fs.chmodSync(largeStatus, 0o755);
  const snapshot = gitStatusSnapshot(root, { command: largeStatus, sampleLimit: 3 });
  assert.equal(snapshot.dirty, true);
  assert.ok(snapshot.bytes > 8 * 1024 * 1024);
  assert.equal(snapshot.changes.length, 3);
  assert.equal(snapshot.truncated, true);
  assert.match(snapshot.digest, /^sha256:[0-9a-f]{64}$/);

  const failedStatus = path.join(root, 'failed-status');
  write(failedStatus, `#!/usr/bin/env node
process.stderr.write('forced status failure');
process.exit(7);
`);
  fs.chmodSync(failedStatus, 0o755);
  assert.throws(
    () => gitStatusSnapshot(root, { command: failedStatus }),
    (error) => error.code === 'git_status_failed'
      && error.details.exitCode === 7
      && error.details.stderr === 'forced status failure',
  );
});

test('toolbox defaults to overview and global doctor treats an unconfigured state as healthy', async (t) => {
  const root = temporaryDirectory(t);
  const state = path.join(root, 'state.json');
  assert.equal(parseArguments([]).intent, 'overview');
  const report = await doctorGlobal({ state, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.equal(report.status, 'ok');
  let overviewOutput = '';
  const overviewWrite = process.stdout.write;
  process.stdout.write = (chunk) => { overviewOutput += String(chunk); return true; };
  try {
    assert.equal(await main(['overview', '--state', state, '--catalog', CATALOG_PATH, '--index', INDEX_PATH]), 0);
  } finally {
    process.stdout.write = overviewWrite;
  }
  assert.match(overviewOutput, /Bundled catalog:/);
  assert.match(overviewOutput, /Tairitsua\/Monica\s+unbound/);
  assert.match(overviewOutput, /Tairitsua\/Monica\.Docs\s+unbound/);
  write(`${state}.lock`, stableJson({ pid: 2147483647, token: 'c'.repeat(48), createdAt: '2000-01-01T00:00:00Z' }, 2));
  const lockedReport = await doctorGlobal({ state, catalog: CATALOG_PATH, index: INDEX_PATH });
  assert.equal(lockedReport.status, 'error');
  assert.ok(lockedReport.checks.some((entry) => entry.id === 'state-lock' && entry.remediation?.includes('remove only the exact reported lock file')));
  fs.unlinkSync(`${state}.lock`);
  let output = '';
  const originalWrite = process.stdout.write;
  process.stdout.write = (chunk) => { output += String(chunk); return true; };
  try {
    const exitCode = await main(['source', 'bind', '--repository', 'monica', '--source-path', path.join(root, 'missing'), '--state', state, '--catalog', CATALOG_PATH, '--index', INDEX_PATH, '--json']);
    assert.equal(exitCode, 3);
  } finally {
    process.stdout.write = originalWrite;
  }
  assert.match(output, /source_unavailable/);
});

test('global status and doctor verify missing or tampered managed skills against the active immutable release', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const configured = configuredState(root, workspace);
  const options = {
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    fetchImplementation: releaseFetch(),
  };
  const previousNpx = process.env.MONICA_GUIDE_NPX;
  t.after(() => previousNpx === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previousNpx);

  process.env.MONICA_GUIDE_NPX = mockNpx(path.join(root, 'missing-install'), configured.skills.slice(1));
  const missingDoctor = await doctorGlobal(options);
  assert.equal(missingDoctor.status, 'error');
  assert.ok(missingDoctor.checks.some((entry) => entry.id === 'skill-discovery:codex' && entry.status === 'error' && /exactly one/.test(entry.message)));
  const missingStatus = await globalStatusEnvelope(inspectGlobalEnvironment(options), options);
  assert.equal(missingStatus.status, 'error');
  assert.equal(missingStatus.observation.installedSkillHealth.status, 'error');

  process.env.MONICA_GUIDE_NPX = mockNpx(path.join(root, 'tampered-install'), configured.skills, { corrupt: true });
  const tamperedDoctor = await doctorGlobal({ ...options, fetchImplementation: releaseFetch() });
  assert.equal(tamperedDoctor.status, 'error');
  assert.ok(tamperedDoctor.checks.some((entry) => entry.id === 'skill-discovery:codex' && entry.status === 'error' && /does not match/.test(entry.message)));
  const tamperedStatus = await globalStatusEnvelope(inspectGlobalEnvironment(options), { ...options, fetchImplementation: releaseFetch() });
  assert.equal(tamperedStatus.status, 'error');
  assert.equal(tamperedStatus.observation.installedSkillHealth.status, 'error');
});

test('global diagnostics reject managed skill records without an active release', async (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.managedSkills['monica-guide'] = { revision: null, digest: null, lastChangedIn: null };
  write(statePath, stableJson(state, 2));
  const options = { state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH };
  const report = await doctorGlobal(options);
  assert.equal(report.status, 'error');
  assert.ok(report.checks.some((entry) => entry.id === 'managed-skill-health' && entry.status === 'error' && /without an active immutable release/.test(entry.message)));
  const status = await globalStatusEnvelope(inspectGlobalEnvironment(options), options);
  assert.equal(status.status, 'error');
  assert.equal(status.observation.installedSkillHealth.status, 'error');
});

test('dynamic agent discovery diagnoses stale aliases without hardcoded host directories', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const configured = configuredState(root, workspace, { agents: ['cursor'] });
  const previousNpx = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(path.join(root, 'cursor-install'), configured.skills, {
    agents: ['cursor'],
    extraEntries: [{ name: 'mo-development', path: path.join(root, 'legacy-alias'), scope: 'global', agents: ['cursor'] }],
  });
  t.after(() => previousNpx === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previousNpx);
  const report = await doctorGlobal({
    state: configured.statePath,
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    fetchImplementation: releaseFetch(),
  });
  assert.equal(report.status, 'warning');
  const discovery = report.checks.find((entry) => entry.id === 'skill-discovery:cursor');
  assert.equal(discovery.status, 'warning');
  assert.deepEqual(discovery.details.aliases.map((entry) => entry.name), ['mo-development']);
  assert.equal(discovery.details.aliases[0].canonical, 'monica-development');
});

test('offline init fails closed without a verified release index', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const plan = await buildPlan('init', { workspace, state: path.join(root, 'state.json'), catalog: CATALOG_PATH, index: INDEX_PATH, profile: 'application', releaseTag: TAG, offline: true });
  assert.ok(plan.blockers.some((entry) => entry.code === 'offline_release_index_unavailable'));
});

test('explicit release-index files are accepted only when their bytes match verified cache state', async (t) => {
  const root = temporaryDirectory(t);
  const indexPath = path.join(root, 'release-index.json');
  const content = `${JSON.stringify(releaseIndex(), null, 2)}\n`;
  write(indexPath, content);
  const statePath = path.join(root, 'state.json');
  await assert.rejects(loadReleaseIndex({ releaseTag: TAG, releaseIndexPath: indexPath, state: emptyState(), statePath, catalogDigest: CATALOG_DIGEST }), (error) => error.code === 'unverified_release_index_file');
  const state = emptyState();
  state.verifiedReleaseIndexes[TAG] = { digest: digest(Buffer.from(content)), catalogDigest: CATALOG_DIGEST, commit: COMMIT };
  const loaded = await loadReleaseIndex({ releaseTag: TAG, releaseIndexPath: indexPath, state, statePath, catalogDigest: CATALOG_DIGEST });
  assert.equal(loaded.release.commit, COMMIT);
});

test('uninitialized status discovers the immutable channel contract online and fails explicitly offline', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const online = await inspectEnvironment({
    workspace,
    state: path.join(root, 'state.json'),
    catalog: CATALOG_PATH,
    index: INDEX_PATH,
    profile: 'application',
    fetchImplementation: releaseDiscoveryFetch(),
  });
  assert.equal(online.releaseError, null);
  assert.equal(online.targetRelease.id, TAG);
  const offline = await inspectEnvironment({ workspace, state: path.join(root, 'offline-state.json'), catalog: CATALOG_PATH, index: INDEX_PATH, profile: 'application', offline: true });
  assert.equal(offline.releaseError.code, 'offline_release_index_unavailable');
});

test('immutable tags reject changed index and artifact bytes after first verification', async (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  const first = await loadReleaseIndex({ releaseTag: TAG, state, statePath, fetchImplementation: releaseFetch() });
  state.verifiedReleaseIndexes[TAG] = { digest: first.indexDigest, commit: COMMIT };
  const noNewlineIndexFetch = async (url) => {
    const response = await releaseFetch()(url);
    if (!url.endsWith('/agent-skill-index.json')) return response;
    return { ...response, text: async () => JSON.stringify(first.index, null, 2) };
  };
  await assert.rejects(
    loadReleaseIndex({ releaseTag: TAG, state, statePath, fetchImplementation: noNewlineIndexFetch }),
    (error) => error.code === 'immutable_release_index_changed',
  );
  const replacedIndex = structuredClone(first.index);
  replacedIndex.releases[TAG].publishedAt = '2026-08-06T00:00:00Z';
  await assert.rejects(
    loadReleaseIndex({ releaseTag: TAG, state, statePath, fetchImplementation: releaseFetch(replacedIndex) }),
    (error) => error.code === 'immutable_release_index_changed',
  );

  const artifacts = await loadReleaseArtifacts({ release: first.release, state, statePath, fetchImplementation: releaseFetch() });
  state.verifiedReleaseArtifacts[TAG] = { catalogDigest: artifacts.catalogDigest, manifestDigest: artifacts.manifestDigest, commit: COMMIT };
  const originalFetch = releaseFetch();
  const replacedAssets = async (url) => {
    const response = await originalFetch(url);
    if (!url.endsWith('/agent-skill-catalog.json')) return response;
    const text = await response.text();
    return { ...response, text: async () => `${text} ` };
  };
  await assert.rejects(
    loadReleaseArtifacts({ release: first.release, state, statePath, fetchImplementation: replacedAssets }),
    (error) => error.code === 'immutable_release_artifacts_changed',
  );
  const freshState = emptyState();
  const noNewlineCatalog = async (url) => {
    const response = await originalFetch(url);
    if (!url.endsWith('/agent-skill-catalog.json')) return response;
    const text = await response.text();
    return { ...response, text: async () => text.replace(/\n$/, '') };
  };
  await assert.rejects(
    loadReleaseArtifacts({ release: first.release, state: freshState, statePath: path.join(root, 'fresh-state.json'), fetchImplementation: noNewlineCatalog }),
    (error) => error.code === 'catalog_digest_mismatch',
  );
});

test('an N-to-N+1 release schema instructs an old Guide to reinstall from the selected tag', async (t) => {
  const root = temporaryDirectory(t);
  const assertUpgrade = (error, asset, supportedSchemaVersion, selectedSchemaVersion) => {
    assert.equal(error.code, 'guide_upgrade_required');
    assert.equal(error.details.asset, asset);
    assert.equal(error.details.releaseTag, TAG);
    assert.equal(error.details.supportedSchemaVersion, supportedSchemaVersion);
    assert.equal(error.details.selectedSchemaVersion, selectedSchemaVersion);
    assert.equal(
      error.details.reinstallUrl,
      `https://github.com/Tairitsua/Monica/tree/${TAG}/skills/monica-guide`,
    );
    assert.match(error.message, /Reinstall monica-guide from the requested immutable tag/);
    assert.match(error.message, /verify skill discovery, then retry/);
    return true;
  };

  const nextIndex = releaseIndex();
  nextIndex.schemaVersion = 3;
  await assert.rejects(
    loadReleaseIndex({
      releaseTag: TAG,
      state: emptyState(),
      statePath: path.join(root, 'index-state.json'),
      fetchImplementation: async (url) => ({
        ok: true,
        status: 200,
        url,
        text: async () => stableJson(nextIndex, 2),
      }),
    }),
    (error) => assertUpgrade(error, 'agent-skill-index.json', 2, 3),
  );

  const nextCatalog = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8'));
  nextCatalog.schemaVersion = 3;
  const nextCatalogText = stableJson(nextCatalog, 2);
  const catalogRelease = structuredClone(releaseIndex().releases[TAG]);
  catalogRelease.catalogDigest = digest(Buffer.from(nextCatalogText));
  const catalogManifestText = stableJson(
    releaseManifestForRelease(catalogRelease, fixtureFilesForRelease(catalogRelease)),
    2,
  );
  catalogRelease.manifestDigest = digest(Buffer.from(catalogManifestText));
  await assert.rejects(
    loadReleaseArtifacts({
      release: catalogRelease,
      state: emptyState(),
      statePath: path.join(root, 'catalog-state.json'),
      fetchImplementation: async (url) => {
        const text = url.endsWith('/agent-skill-catalog.json')
          ? nextCatalogText
          : catalogManifestText;
        return { ok: true, status: 200, url, text: async () => text };
      },
    }),
    (error) => assertUpgrade(error, 'agent-skill-catalog.json', 2, 3),
  );

  const manifestRelease = structuredClone(releaseIndex().releases[TAG]);
  const nextManifest = releaseManifestForRelease(
    manifestRelease,
    fixtureFilesForRelease(manifestRelease),
  );
  nextManifest.schemaVersion = 3;
  const nextManifestText = stableJson(nextManifest, 2);
  manifestRelease.manifestDigest = digest(Buffer.from(nextManifestText));
  await assert.rejects(
    loadReleaseArtifacts({
      release: manifestRelease,
      state: emptyState(),
      statePath: path.join(root, 'manifest-state.json'),
      fetchImplementation: async (url) => {
        const text = url.endsWith('/agent-skill-catalog.json')
          ? fs.readFileSync(CATALOG_PATH, 'utf8')
          : nextManifestText;
        return { ok: true, status: 200, url, text: async () => text };
      },
    }),
    (error) => assertUpgrade(error, 'agent-skill-manifest.json', 2, 3),
  );
});

test('index and catalog structural validation reject malicious contract shapes', (t) => {
  const root = temporaryDirectory(t);
  const invalidIndex = releaseIndex();
  invalidIndex.unexpected = true;
  invalidIndex.channels.stable = 'v9.9.9';
  const indexPath = path.join(root, 'index.json');
  write(indexPath, stableJson(invalidIndex, 2));
  assert.throws(() => loadCatalog({ catalogPath: CATALOG_PATH, indexPath }), (error) => error.code === 'invalid_index');
  delete invalidIndex.unexpected;
  invalidIndex.channels.stable = TAG;
  invalidIndex.releases[TAG].unexpected = true;
  write(indexPath, stableJson(invalidIndex, 2));
  assert.throws(() => loadCatalog({ catalogPath: CATALOG_PATH, indexPath }), (error) => error.code === 'invalid_release');

  const invalidCatalog = JSON.parse(fs.readFileSync(CATALOG_PATH, 'utf8'));
  invalidCatalog.skills['monica-guide'].path = '../escape';
  const catalogPath = path.join(root, 'catalog.json');
  write(catalogPath, stableJson(invalidCatalog, 2));
  assert.throws(() => loadCatalog({ catalogPath, indexPath: INDEX_PATH }), (error) => error.code === 'invalid_catalog');
});

test('cached source resolver must return the requested exact ref and commit', (t) => {
  const root = temporaryDirectory(t);
  const source = path.join(root, 'source');
  fs.mkdirSync(source);
  const resolver = path.join(root, 'resolver.py');
  const wrongCommit = 'b'.repeat(40);
  write(resolver, `import json\nprint(json.dumps({"status":"ok","source_path":${JSON.stringify(source)},"verification_state":"verified","resolution_kind":"exact_commit","repository":{"canonical_name":"Tairitsua/Monica"},"artifact":{"ref":${JSON.stringify(COMMIT)},"actual_commit":${JSON.stringify(wrongCommit)},"expected_commit":${JSON.stringify(COMMIT)}}}))\n`);
  assert.throws(
    () => resolveCachedSource({ exactRef: COMMIT, resolverPath: resolver }),
    (error) => error.code === 'source_commit_mismatch',
  );
});

test('pre-existing Claude import is preserved but never claimed as Guide-owned', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  write(path.join(workspace, 'CLAUDE.md'), '# Existing instructions\n\n@AGENTS.md\n');
  const plan = await buildPlan('init', baseOptions(root, workspace));
  const project = JSON.parse(plan.actions.find((action) => action.path?.endsWith('.monica/guide.json')).content);
  assert.equal(project.managedClaudeImport, false);
  assert.equal(plan.actions.some((action) => action.path?.endsWith('CLAUDE.md')), false);
  write(path.join(workspace, 'CLAUDE.md'), '@AGENTS.md\n@AGENTS.md\n');
  await assert.rejects(buildPlan('init', baseOptions(root, workspace)), (error) => error.code === 'duplicate_claude_import');
});

test('managed Claude removal preserves a whitespace-only surrounding file', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const options = baseOptions(root, workspace);
  const init = await buildPlan('init', options);
  for (const action of init.actions.filter((entry) => entry.type === 'write-file'
    && (entry.path.endsWith('.monica/guide.json') || entry.path.endsWith('AGENTS.md')))) {
    write(action.path, action.content);
  }
  const surroundingBytes = '  \r\n@AGENTS.md\r\n\t';
  write(path.join(workspace, 'CLAUDE.md'), surroundingBytes);

  const configure = await buildPlan('configure', { ...options, agents: ['codex'] });
  const configureClaude = configure.actions.find((action) => action.path?.endsWith('CLAUDE.md'));
  assert.equal(configureClaude.type, 'write-file');
  assert.equal(configureClaude.content, '  \r\n\t');

  const forget = await buildPlan('forget', { workspace, state: options.state, catalog: CATALOG_PATH, index: INDEX_PATH });
  const forgetClaude = forget.actions.find((action) => action.path?.endsWith('CLAUDE.md'));
  assert.equal(forgetClaude.type, 'write-file');
  assert.equal(forgetClaude.content, '  \r\n\t');
});

test('selected nested Claude instructions reject an unsafe sibling AGENTS file', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const nested = path.join(workspace, 'nested');
  write(path.join(nested, 'CLAUDE.md'), 'nested\n');
  fs.mkdirSync(path.join(nested, 'AGENTS.md'));
  const directoryPlan = await buildPlan('init', { ...baseOptions(root, workspace), nestedInstructions: ['nested/CLAUDE.md'] });
  assert.ok(directoryPlan.blockers.some((entry) => entry.code === 'nested_claude_agents_unsafe'));

  if (process.platform !== 'win32') {
    fs.rmSync(path.join(nested, 'AGENTS.md'), { recursive: true });
    const external = path.join(root, 'external-AGENTS.md');
    write(external, 'outside\n');
    fs.symlinkSync(external, path.join(nested, 'AGENTS.md'));
    const symlinkPlan = await buildPlan('init', { ...baseOptions(root, workspace), nestedInstructions: ['nested/CLAUDE.md'] });
    assert.ok(symlinkPlan.blockers.some((entry) => entry.code === 'nested_claude_agents_unsafe'));
  }
});

test('doctor exposes duplicate root Claude imports before initialization', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  write(path.join(workspace, 'CLAUDE.md'), '@AGENTS.md\n@AGENTS.md\n');
  const report = await doctor({ ...baseOptions(root, workspace), profile: undefined });
  const claude = report.checks.find((entry) => entry.id === 'claude-import');
  assert.equal(claude.status, 'error');
  assert.match(claude.message, /duplicate/i);
});

test('doctor reports retained global-skill recovery evidence', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const statePath = path.join(root, 'state', 'state.json');
  const recovery = path.join(root, 'state', 'skill-install-transactions', 'transaction-crash');
  fs.mkdirSync(recovery, { recursive: true });
  write(path.join(recovery, 'transaction.json'), stableJson({
    schemaVersion: 2,
    status: 'mutating',
    attemptedSkills: ['monica-guide'],
    attemptedFiles: [],
  }, 2));
  const report = await doctor({ ...baseOptions(root, workspace), state: statePath });
  const check = report.checks.find((entry) => entry.id === 'global-skill-recovery');
  assert.equal(check.status, 'error');
  assert.equal(check.details.transactions[0].status, 'mutating');
  assert.match(check.remediation, /recover|reconcile/i);
});

test('doctor remediation is executable because update adopts discovered catalog-managed skills', async (t) => {
  const root = temporaryDirectory(t);
  const workspace = applicationWorkspace(root);
  const { catalog } = loadCatalog({ catalogPath: CATALOG_PATH, indexPath: INDEX_PATH });
  const closure = resolveProfileClosure(catalog, 'application', ['modular-monolith']);
  const extra = 'monica-ui-design';
  const statePath = path.join(root, 'state.json');
  const state = emptyState();
  state.activeRelease = { id: TAG, monicaVersion: VERSION, tag: TAG, commit: COMMIT, catalogDigest: CATALOG_DIGEST };
  state.managedSkills = Object.fromEntries(closure.selected.map((skill) => [skill, {
    revision: 1,
    digest: skillFileContracts().skillDigests[skill],
    lastChangedIn: TAG,
  }]));
  state.agentTargets = ['codex'];
  write(statePath, stableJson(state, 2));
  write(path.join(workspace, '.monica', 'guide.json'), stableJson({
    schemaVersion: 2,
    profile: 'application',
    channel: 'stable',
    capabilities: ['modular-monolith'],
    expectedCatalogRelease: { ...state.activeRelease, indexTag: TAG },
    instructionBlockVersion: 1,
    managedClaudeImport: false,
  }, 2));
  const previous = process.env.MONICA_GUIDE_NPX;
  process.env.MONICA_GUIDE_NPX = mockNpx(root, [...closure.selected, extra]);
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_NPX : process.env.MONICA_GUIDE_NPX = previous);
  const options = { workspace, state: statePath, catalog: CATALOG_PATH, index: INDEX_PATH, releaseTag: TAG, fetchImplementation: releaseFetch() };
  const report = await doctor(options);
  const discovery = report.checks.find((entry) => entry.id === 'skill-discovery:codex');
  assert.equal(discovery.status, 'error');
  assert.match(discovery.remediation, /update/i);
  const update = await buildPlan('update', options);
  assert.equal(update.blockers.length, 0);
  assert.ok(update.actions.some((action) => action.type === 'install-skill' && action.skill === extra));
  const nextState = JSON.parse(update.actions.find((action) => action.path === statePath).content);
  assert.ok(Object.hasOwn(nextState.managedSkills, extra));
});

test('release fetches time out with actionable fail-closed diagnostics', async (t) => {
  const root = temporaryDirectory(t);
  const previous = process.env.MONICA_GUIDE_FETCH_TIMEOUT_MS;
  process.env.MONICA_GUIDE_FETCH_TIMEOUT_MS = '20';
  t.after(() => previous === undefined ? delete process.env.MONICA_GUIDE_FETCH_TIMEOUT_MS : process.env.MONICA_GUIDE_FETCH_TIMEOUT_MS = previous);
  const stalledFetch = (_url, { signal }) => new Promise((_resolve, reject) => {
    signal.addEventListener('abort', () => reject(Object.assign(new Error('aborted'), { name: 'AbortError' })), { once: true });
  });
  await assert.rejects(
    loadReleaseIndex({ releaseTag: TAG, state: emptyState(), statePath: path.join(root, 'state.json'), fetchImplementation: stalledFetch }),
    (error) => error.code === 'release_fetch_timeout' && /offline cache/.test(error.message),
  );
});

test('bootstrap prompt contract stays bilingual, pinned, immutable, universal, and preview-gated', () => {
  const prompts = JSON.parse(fs.readFileSync(path.join(SKILL_ROOT, 'assets', 'bootstrap-prompts.json'), 'utf8'));
  assert.equal(prompts.schemaVersion, 3);
  assert.equal(prompts.$schema, './bootstrap-prompts.schema.json');
  assert.equal(prompts.repository, 'Tairitsua/Monica');
  assert.equal(prompts.skill, 'monica-guide');
  assert.equal(prompts.immutableRef, '{{MONICA_IMMUTABLE_REF}}');
  assert.equal(prompts.catalogDigest, '{{MONICA_CATALOG_DIGEST}}');
  assert.equal(prompts.distribution.skillsCli.package, 'skills');
  assert.match(prompts.distribution.skillsCli.version, /^\d+\.\d+\.\d+$/);
  assert.equal(prompts.distribution.immutableSkillUrlTemplate, 'https://github.com/Tairitsua/Monica/tree/{tag}/skills/{skill}');
  assert.deepEqual(prompts.verifiedAgentTargets, ['codex', 'claude-code']);
  assert.equal(Object.hasOwn(prompts, 'hosts'), false);
  assert.equal(Object.hasOwn(prompts, 'goals'), false);

  const cliReference = `${prompts.distribution.skillsCli.package}@${prompts.distribution.skillsCli.version}`;
  const immutableUrl = 'https://github.com/Tairitsua/Monica/tree/{{MONICA_IMMUTABLE_REF}}/skills/monica-guide';
  assert.deepEqual(prompts.fallback, {
    installCommand: `npx --yes ${cliReference} add ${immutableUrl} -g -s monica-guide`,
    verifyCommand: `npx --yes ${cliReference} ls -g --json`,
  });
  assert.deepEqual(Object.keys(prompts.locales).sort(), ['en-US', 'zh-CN']);

  const localeFragments = {
    'en-US': [
      'Install only the monica-guide Agent Skill globally',
      "Use this host's supported Agent Skills installation and discovery mechanism",
      'This installation changes my user-level skill directory.',
      'restart the host only if discovery still fails',
      'Invoke $monica-guide',
      'Monica/Monica.Docs source bindings',
      'ask what I want to do next',
      'After that installation, these restrictions apply until I choose an operation and approve its preview',
      'Do not initialize a repository',
      'choose a profile',
      'install downstream Monica skills',
      'bind or unbind source',
      'make any other file changes',
      'perform remote mutations',
    ],
    'zh-CN': [
      '只从这个不可变来源全局安装 monica-guide Agent Skill',
      '使用当前宿主支持的 Agent Skills 安装与发现方式',
      '这次安装会更改我的用户级 Skill 目录。',
      '只有发现仍失败时才重启宿主',
      '调用 $monica-guide',
      'Monica 和 Monica.Docs 源码绑定',
      '询问我下一步想做什么',
      '完成这次安装后，在我选择操作并批准其预览之前，以下限制适用',
      '不要初始化仓库',
      '选择 profile',
      '安装后续 Monica Skill',
      '绑定或解绑源码',
      '不要再修改其他文件',
      '执行远程变更',
    ],
  };

  for (const locale of ['en-US', 'zh-CN']) {
    const prompt = prompts.locales[locale].prompt;
    assert.ok(prompt.includes(cliReference));
    assert.ok(prompt.includes(immutableUrl));
    for (const fragment of localeFragments[locale]) assert.ok(prompt.includes(fragment), `${locale}: missing ${fragment}`);
    for (const forbidden of ['skills@latest', '--apply', '--agent', '--profile', 'init --']) {
      assert.equal(prompt.includes(forbidden), false, `${locale}: unexpected ${forbidden}`);
    }
    assert.equal(/\b(?:codex|claude-code)\b/i.test(prompt), false);
    assert.deepEqual(
      [...new Set(prompt.match(/\{\{[A-Z0-9_]+\}\}/g) || [])].sort(),
      ['{{MONICA_IMMUTABLE_REF}}'],
    );
  }
});
