import fs from 'node:fs';
import path from 'node:path';
import { GuideError, compareOrdinalUtf8, digest } from './guide-shared.mjs';

function expectedSkillFiles(manifest, skillName) {
  const prefix = `skills/${skillName}/`;
  const files = new Map();
  for (const [filePath, fileDigest] of Object.entries(manifest?.files || {})) {
    if (filePath.startsWith(prefix)) files.set(filePath.slice(prefix.length), fileDigest);
  }
  if (!files.has('SKILL.md')) throw new GuideError('manifest_skill_missing', `Release manifest does not contain skills/${skillName}/SKILL.md.`);
  return files;
}

function installedFiles(root) {
  const files = new Map();
  const visit = (directory) => {
    for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
      const absolute = path.join(directory, entry.name);
      const relative = path.relative(root, absolute).split(path.sep).join('/');
      if (entry.isSymbolicLink()) throw new GuideError('installed_skill_symlink', `Installed skill contains an unexpected internal symlink: ${absolute}.`);
      if (entry.isDirectory()) visit(absolute);
      else if (entry.isFile()) files.set(relative, digest(fs.readFileSync(absolute)));
    }
  };
  visit(root);
  return files;
}

export function verifyInstalledSkill(entry, skillName, manifest) {
  if (!entry || entry.name !== skillName) throw new GuideError('skill_discovery_missing', `Installed discovery does not contain ${skillName}.`);
  if (entry.scope !== 'global') throw new GuideError('skill_scope_mismatch', `${skillName} is not reported in global scope.`);
  if (!entry.path || typeof entry.path !== 'string') throw new GuideError('skill_path_missing', `${skillName} discovery has no installed path.`);
  let root = path.resolve(entry.path);
  if (fs.existsSync(root) && fs.statSync(root).isFile()) root = path.dirname(root);
  if (!fs.existsSync(root) || !fs.statSync(root).isDirectory()) throw new GuideError('skill_path_unavailable', `Installed path for ${skillName} is unavailable: ${root}.`);
  root = fs.realpathSync(root);
  const expected = expectedSkillFiles(manifest, skillName);
  const actual = installedFiles(root);
  const missing = [...expected.keys()].filter((relative) => !actual.has(relative));
  const extra = [...actual.keys()].filter((relative) => !expected.has(relative));
  const changed = [...expected.keys()].filter((relative) => actual.has(relative) && actual.get(relative) !== expected.get(relative));
  if (missing.length || extra.length || changed.length) {
    throw new GuideError('skill_content_mismatch', `Installed ${skillName} does not match the immutable release manifest.`, { missing, extra, changed });
  }
  const fileManifest = [...actual.entries()].sort(([left], [right]) => compareOrdinalUtf8(left, right))
    .map(([relative, hash]) => `${hash.slice('sha256:'.length)}  ${relative}\n`).join('');
  const skillDigest = digest(fileManifest);
  if (manifest.skillDigestAlgorithm !== 'sha256-file-manifest-v1' || manifest.skillDigests?.[skillName] !== skillDigest) {
    throw new GuideError('skill_digest_mismatch', `Installed ${skillName} digest ${skillDigest} does not match the release contract.`);
  }
  return { name: skillName, path: root, files: expected.size, digest: skillDigest };
}

export function verifyDiscoveryPayload(payload, skills, manifest) {
  if (!Array.isArray(payload)) throw new GuideError('skill_discovery_contract_invalid', 'skills ls --json must return a top-level array.');
  const verified = [];
  for (const skill of skills) {
    const entries = payload.filter((entry) => entry?.name === skill);
    if (entries.length !== 1) throw new GuideError('skill_discovery_ambiguous', `Expected exactly one global discovery entry for ${skill}, found ${entries.length}.`);
    verified.push(verifyInstalledSkill(entries[0], skill, manifest));
  }
  return verified;
}

export function buildSourceManifest(sourceRoot, catalog) {
  const files = {};
  const skillDigests = {};
  for (const [skillName, entry] of Object.entries(catalog.skills || {})) {
    if (entry.ownership !== 'monica' || entry.managed !== true) continue;
    const skillRoot = path.resolve(sourceRoot, entry.path);
    const relativeRoot = path.relative(path.resolve(sourceRoot), skillRoot);
    if (relativeRoot.startsWith('..') || path.isAbsolute(relativeRoot)) throw new GuideError('source_skill_path_invalid', `Catalog path escapes source root for ${skillName}.`);
    if (!fs.existsSync(path.join(skillRoot, 'SKILL.md'))) throw new GuideError('source_skill_missing', `Source catalog skill ${skillName} is missing SKILL.md.`);
    const visit = (directory) => {
      for (const child of fs.readdirSync(directory, { withFileTypes: true })) {
        const absolute = path.join(directory, child.name);
        if (child.isSymbolicLink()) throw new GuideError('source_skill_symlink', `Source skill contains a symlink: ${absolute}.`);
        if (child.isDirectory()) visit(absolute);
        else if (child.isFile()) files[path.relative(sourceRoot, absolute).split(path.sep).join('/')] = digest(fs.readFileSync(absolute));
      }
    };
    visit(skillRoot);
    const prefix = `${entry.path.replace(/\/$/, '')}/`;
    const fileManifest = Object.entries(files).filter(([filePath]) => filePath.startsWith(prefix))
      .map(([filePath, hash]) => [filePath.slice(prefix.length), hash])
      .sort(([left], [right]) => compareOrdinalUtf8(left, right))
      .map(([relative, hash]) => `${hash.slice('sha256:'.length)}  ${relative}\n`).join('');
    skillDigests[skillName] = digest(fileManifest);
  }
  return { schemaVersion: 1, source: true, skillDigestAlgorithm: 'sha256-file-manifest-v1', skillDigests, files };
}

export function verifyLocalSkillSource(sourceRoot, catalog, releaseManifest) {
  if (!sourceRoot || !fs.existsSync(sourceRoot) || !fs.statSync(sourceRoot).isDirectory()) {
    throw new GuideError('local_skill_source_unavailable', `Local Monica skill source is unavailable: ${sourceRoot || 'missing'}.`);
  }
  if (releaseManifest?.skillDigestAlgorithm !== 'sha256-file-manifest-v1') {
    throw new GuideError('local_skill_source_manifest_invalid', 'Target release has no supported per-skill digest contract.');
  }
  const actual = buildSourceManifest(sourceRoot, catalog);
  const mismatches = [];
  for (const [skillName, entry] of Object.entries(catalog.skills || {})) {
    if (entry.ownership !== 'monica' || entry.managed !== true) continue;
    const expectedDigest = releaseManifest.skillDigests?.[skillName];
    const actualDigest = actual.skillDigests[skillName];
    if (!expectedDigest || actualDigest !== expectedDigest) {
      mismatches.push({ skill: skillName, expectedDigest: expectedDigest || null, actualDigest: actualDigest || null });
    }
  }
  if (mismatches.length) {
    throw new GuideError('local_skill_source_mismatch', 'Local Monica skill source does not match the target immutable release manifest.', { sourceRoot, mismatches });
  }
  return actual;
}
