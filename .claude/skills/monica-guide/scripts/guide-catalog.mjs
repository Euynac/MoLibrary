import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { GuideError, compareOrdinalUtf8, digest, fileDigest, parseSemVer, readJson, semverChannel } from './guide-shared.mjs';

const ASSET_ROOT = fileURLToPath(new URL('../assets/', import.meta.url));
const SUPPORTED_CATALOG_SCHEMA_VERSION = 2;
const SUPPORTED_RELEASE_INDEX_SCHEMA_VERSION = 2;
const SUPPORTED_RELEASE_MANIFEST_SCHEMA_VERSION = 2;

export function loadCatalog({ catalogPath, indexPath } = {}) {
  const resolvedCatalogPath = path.resolve(catalogPath || process.env.MONICA_GUIDE_CATALOG || path.join(ASSET_ROOT, 'default-catalog.json'));
  const resolvedIndexPath = path.resolve(indexPath || process.env.MONICA_GUIDE_INDEX || path.join(ASSET_ROOT, 'default-index.json'));
  const catalog = readJson(resolvedCatalogPath);
  const index = readJson(resolvedIndexPath);
  validateCatalog(catalog);
  validateIndex(index);
  return {
    catalog,
    index,
    catalogPath: resolvedCatalogPath,
    indexPath: resolvedIndexPath,
    catalogDigest: fileDigest(resolvedCatalogPath),
    indexDigest: fileDigest(resolvedIndexPath),
  };
}

function validateReleaseTag(tag) {
  if (!String(tag || '').startsWith('v') || !parseSemVer(String(tag).slice(1))) {
    throw new GuideError('invalid_release_tag', `Release tag must be an immutable vSemVer tag, found ${tag || 'missing'}.`);
  }
}

function requireSupportedSelectedSchema(payload, {
  asset,
  releaseTag,
  supportedVersion,
}) {
  const selectedVersion = payload?.schemaVersion;
  if (!Number.isInteger(selectedVersion) || selectedVersion <= supportedVersion) return;
  validateReleaseTag(releaseTag);
  const reinstallUrl = `https://github.com/Tairitsua/Monica/tree/${releaseTag}/skills/monica-guide`;
  throw new GuideError(
    'guide_upgrade_required',
    `The installed monica-guide supports ${asset} schemaVersion ${supportedVersion}, but requested immutable release ${releaseTag} uses schemaVersion ${selectedVersion}. Reinstall monica-guide from the requested immutable tag at ${reinstallUrl}, verify skill discovery, then retry the same Guide command.`,
    {
      asset,
      releaseTag,
      supportedSchemaVersion: supportedVersion,
      selectedSchemaVersion: selectedVersion,
      reinstallUrl,
    },
  );
}

function validateReleaseSkillContracts(releaseId, release) {
  if (release.skillDigestAlgorithm !== 'sha256-file-manifest-v1'
    || !release.skillDigests || typeof release.skillDigests !== 'object' || Array.isArray(release.skillDigests)
    || !release.skillRevisions || typeof release.skillRevisions !== 'object' || Array.isArray(release.skillRevisions)
    || !release.skillLastChangedIn || typeof release.skillLastChangedIn !== 'object' || Array.isArray(release.skillLastChangedIn)) {
    throw new GuideError('release_skill_digest_contract_invalid', `Release ${releaseId} has no supported per-skill version contract.`);
  }
  const digestSkills = Object.keys(release.skillDigests).sort(compareOrdinalUtf8);
  const revisionSkills = Object.keys(release.skillRevisions).sort(compareOrdinalUtf8);
  const changedSkills = Object.keys(release.skillLastChangedIn).sort(compareOrdinalUtf8);
  if (!digestSkills.length
    || JSON.stringify(digestSkills) !== JSON.stringify(revisionSkills)
    || JSON.stringify(digestSkills) !== JSON.stringify(changedSkills)) {
    throw new GuideError('release_skill_version_contract_invalid', `Release ${releaseId} per-skill digest, revision, and last-changed maps must have the same non-empty key set.`);
  }
  for (const skill of digestSkills) {
    const skillDigest = release.skillDigests[skill];
    const revision = release.skillRevisions[skill];
    const lastChangedIn = release.skillLastChangedIn[skill];
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(skill) || !/^sha256:[0-9a-f]{64}$/.test(skillDigest)) {
      throw new GuideError('release_skill_digest_contract_invalid', `Release ${releaseId} has invalid digest for ${skill}.`);
    }
    if (!Number.isInteger(revision) || revision < 1) {
      throw new GuideError('release_skill_revision_contract_invalid', `Release ${releaseId} has invalid revision for ${skill}.`);
    }
    validateReleaseTag(lastChangedIn);
  }
}

export function verifyReleaseIndex(index, tag) {
  validateReleaseTag(tag);
  requireSupportedSelectedSchema(index, {
    asset: 'agent-skill-index.json',
    releaseTag: tag,
    supportedVersion: SUPPORTED_RELEASE_INDEX_SCHEMA_VERSION,
  });
  validateIndex(index);
  const matches = Object.entries(index.releases).filter(([, release]) => release.tag === tag);
  if (matches.length !== 1) throw new GuideError('release_index_mismatch', `Release index must contain exactly one release for ${tag}.`);
  const [releaseId, release] = matches[0];
  if (!/^[0-9a-f]{40}$/i.test(release.commit || '')) throw new GuideError('release_not_immutable', `Release ${releaseId} has no exact commit.`);
  const expectedAssetBaseUrl = `https://github.com/Tairitsua/Monica/releases/download/${tag}`;
  if (release.assetBaseUrl && release.assetBaseUrl !== expectedAssetBaseUrl) {
    throw new GuideError('release_asset_url_mismatch', `Release ${releaseId} assetBaseUrl must be ${expectedAssetBaseUrl}.`);
  }
  if (release.catalogUrl !== `${expectedAssetBaseUrl}/agent-skill-catalog.json` || release.manifestUrl !== `${expectedAssetBaseUrl}/agent-skill-manifest.json`) {
    throw new GuideError('release_asset_url_mismatch', `Release ${releaseId} catalog/manifest URLs are not deterministic tag assets.`);
  }
  for (const field of ['catalogDigest', 'skillTreeDigest', 'manifestDigest']) {
    if (!/^sha256:[0-9a-f]{64}$/.test(release[field] || '')) throw new GuideError('release_digest_invalid', `Release ${releaseId} has invalid ${field}.`);
  }
  validateReleaseSkillContracts(releaseId, release);
  return { releaseId, release: { id: releaseId, ...release } };
}

function releaseIndexCachePath(statePath, tag) {
  const safeTag = tag.replace(/[^0-9A-Za-z.-]/g, '_');
  return path.join(path.dirname(statePath), 'release-indexes', `${safeTag}.json`);
}

async function fetchText(url, fetchImplementation = globalThis.fetch) {
  if (typeof fetchImplementation !== 'function') throw new GuideError('fetch_unavailable', 'This Node.js runtime does not provide the built-in fetch API.');
  let parsed;
  try { parsed = new URL(url); } catch { throw new GuideError('invalid_release_index_url', `Invalid release index URL: ${url}.`); }
  if (parsed.protocol !== 'https:') throw new GuideError('insecure_release_index_url', 'Release index URL must use HTTPS.');
  const configuredTimeout = Number(process.env.MONICA_GUIDE_FETCH_TIMEOUT_MS || 15000);
  const timeoutMilliseconds = Number.isFinite(configuredTimeout) && configuredTimeout > 0 ? Math.min(configuredTimeout, 120000) : 15000;
  const controller = new AbortController();
  const timeout = setTimeout(() => controller.abort(), timeoutMilliseconds);
  let response;
  try {
    response = await fetchImplementation(url, {
      redirect: 'follow',
      headers: { accept: 'application/json', 'user-agent': 'Monica-Guide/1' },
      signal: controller.signal,
    });
  } catch (error) {
    if (controller.signal.aborted || error?.name === 'AbortError') throw new GuideError('release_fetch_timeout', `Release request timed out after ${timeoutMilliseconds} ms. Retry online or use a previously verified offline cache.`);
    throw new GuideError('release_fetch_failed', `Release request failed: ${error?.message || String(error)}.`);
  } finally {
    clearTimeout(timeout);
  }
  if (!response || typeof response.ok !== 'boolean' || typeof response.text !== 'function') throw new GuideError('release_fetch_failed', 'Release request returned an invalid fetch response.');
  if (!response.ok) throw new GuideError('release_index_fetch_failed', `Release index request failed with HTTP ${response.status}.`);
  const finalUrl = new URL(response.url || url);
  if (finalUrl.protocol !== 'https:') throw new GuideError('insecure_release_redirect', 'Release index redirected to a non-HTTPS URL.');
  const text = await response.text();
  if (Buffer.byteLength(text) > 4 * 1024 * 1024) throw new GuideError('release_index_too_large', 'Release index exceeds the 4 MiB safety limit.');
  return text;
}

export async function discoverChannelReleaseTag(channel, fetchImplementation = globalThis.fetch) {
  if (!['stable', 'preview'].includes(channel)) throw new GuideError('invalid_channel', `Cannot discover release tag for channel ${channel}.`);
  const url = channel === 'stable'
    ? 'https://api.github.com/repos/Tairitsua/Monica/releases/latest'
    : 'https://api.github.com/repos/Tairitsua/Monica/releases?per_page=30';
  const text = await fetchText(url, fetchImplementation);
  let payload;
  try { payload = JSON.parse(text); } catch (error) { throw new GuideError('release_metadata_invalid', `GitHub release metadata is invalid JSON: ${error.message}`); }
  const releases = Array.isArray(payload) ? payload : [payload];
  const release = releases.find((entry) => !entry.draft
    && (channel === 'preview' ? entry.prerelease === true : entry.prerelease !== true)
    && Array.isArray(entry.assets)
    && entry.assets.some((asset) => asset.name === 'agent-skill-index.json'));
  if (!release?.tag_name) throw new GuideError('channel_release_unavailable', `No published ${channel} Monica release contains agent-skill-index.json.`);
  validateReleaseTag(release.tag_name);
  return release.tag_name;
}

export async function loadReleaseIndex({
  releaseTag,
  releaseIndexPath,
  releaseIndexUrl,
  offline = false,
  state,
  statePath,
  fetchImplementation,
} = {}) {
  if (!releaseTag) return null;
  validateReleaseTag(releaseTag);
  const cachePath = releaseIndexCachePath(statePath, releaseTag);
  let text;
  let source;
  if (releaseIndexPath) {
    text = fs.readFileSync(path.resolve(releaseIndexPath), 'utf8');
    const verifiedRecord = state.verifiedReleaseIndexes?.[releaseTag];
    if (!verifiedRecord || verifiedRecord.digest !== digest(Buffer.from(text))) {
      throw new GuideError('unverified_release_index_file', `Explicit release index file for ${releaseTag} is not present in verified user cache state.`);
    }
    source = 'explicit-file';
  } else if (offline) {
    const record = state.verifiedReleaseIndexes?.[releaseTag];
    if (!record || !fs.existsSync(cachePath)) throw new GuideError('offline_release_index_unavailable', `No verified cached release index exists for ${releaseTag}.`);
    text = fs.readFileSync(cachePath, 'utf8');
    if (digest(Buffer.from(text)) !== record.digest) throw new GuideError('cached_release_index_changed', `Cached release index for ${releaseTag} no longer matches its verified digest.`);
    source = 'verified-cache';
  } else {
    const expectedUrl = `https://github.com/Tairitsua/Monica/releases/download/${releaseTag}/agent-skill-index.json`;
    const url = releaseIndexUrl || expectedUrl;
    if (url !== expectedUrl) throw new GuideError('release_index_url_mismatch', `Release index URL must be exactly ${expectedUrl}.`);
    text = await fetchText(url, fetchImplementation);
    source = url;
  }
  const cacheContent = text;
  const indexDigest = digest(Buffer.from(cacheContent));
  const existingRecord = state.verifiedReleaseIndexes?.[releaseTag];
  if (source !== 'verified-cache' && existingRecord && existingRecord.digest !== indexDigest) {
    throw new GuideError('immutable_release_index_changed', `Release index bytes for immutable tag ${releaseTag} differ from the previously verified digest.`);
  }
  let index;
  try { index = JSON.parse(text); } catch (error) { throw new GuideError('invalid_release_index', `Release index is not valid JSON: ${error.message}`); }
  const verified = verifyReleaseIndex(index, releaseTag);
  return {
    index,
    tag: releaseTag,
    ...verified,
    source,
    cachePath,
    cacheContent,
    indexDigest,
    needsCache: source !== 'verified-cache',
  };
}

function releaseArtifactCachePaths(statePath, tag) {
  const safeTag = tag.replace(/[^0-9A-Za-z.-]/g, '_');
  const root = path.join(path.dirname(statePath), 'release-artifacts', safeTag);
  return {
    catalog: path.join(root, 'agent-skill-catalog.json'),
    manifest: path.join(root, 'agent-skill-manifest.json'),
  };
}

function parseAssetJson(text, label) {
  try { return JSON.parse(text); } catch (error) { throw new GuideError('invalid_release_asset', `${label} is not valid JSON: ${error.message}`); }
}

export function verifyReleaseManifest(manifest, release, catalogDigest) {
  requireSupportedSelectedSchema(manifest, {
    asset: 'agent-skill-manifest.json',
    releaseTag: release.tag,
    supportedVersion: SUPPORTED_RELEASE_MANIFEST_SCHEMA_VERSION,
  });
  if (manifest?.schemaVersion !== SUPPORTED_RELEASE_MANIFEST_SCHEMA_VERSION) throw new GuideError('manifest_schema_mismatch', `Release manifest must use schemaVersion ${SUPPORTED_RELEASE_MANIFEST_SCHEMA_VERSION}.`);
  if (manifest.tag !== release.tag || manifest.resolvedCommit !== release.commit) throw new GuideError('manifest_release_mismatch', `Release manifest does not describe ${release.tag}@${release.commit}.`);
  if (manifest.catalogDigest !== catalogDigest || manifest.catalogDigest !== release.catalogDigest) throw new GuideError('manifest_catalog_mismatch', 'Release manifest catalog digest does not match the selected release.');
  if (manifest.skillTreeDigest !== release.skillTreeDigest) throw new GuideError('manifest_tree_mismatch', 'Release manifest skill-tree digest does not match the selected release.');
  const manifestSkillDigests = manifest.skillDigests && typeof manifest.skillDigests === 'object' && !Array.isArray(manifest.skillDigests)
    ? Object.entries(manifest.skillDigests).sort(([left], [right]) => compareOrdinalUtf8(left, right))
    : null;
  const releaseSkillDigests = Object.entries(release.skillDigests || {}).sort(([left], [right]) => compareOrdinalUtf8(left, right));
  if (manifest.skillDigestAlgorithm !== 'sha256-file-manifest-v1'
    || !manifestSkillDigests
    || JSON.stringify(manifestSkillDigests) !== JSON.stringify(releaseSkillDigests)) {
    throw new GuideError('manifest_skill_digest_mismatch', 'Release manifest per-skill digests do not match the selected release.');
  }
  for (const field of ['skillRevisions', 'skillLastChangedIn']) {
    const manifestEntries = manifest[field] && typeof manifest[field] === 'object' && !Array.isArray(manifest[field])
      ? Object.entries(manifest[field]).sort(([left], [right]) => compareOrdinalUtf8(left, right))
      : null;
    const releaseEntries = Object.entries(release[field] || {}).sort(([left], [right]) => compareOrdinalUtf8(left, right));
    if (!manifestEntries || JSON.stringify(manifestEntries) !== JSON.stringify(releaseEntries)) {
      throw new GuideError('manifest_skill_version_mismatch', `Release manifest ${field} does not match the selected release.`);
    }
  }
  if (manifest.fileManifestScope !== 'release-payload-except-index-v1') throw new GuideError('manifest_scope_mismatch', 'Release manifest has an unsupported file-manifest scope.');
  const expectedBase = release.assetBaseUrl;
  const urls = {
    indexUrl: `${expectedBase}/agent-skill-index.json`,
    catalogUrl: `${expectedBase}/agent-skill-catalog.json`,
    archiveUrl: `${expectedBase}/monica-agent-skills-${release.tag}.zip`,
  };
  for (const [field, expected] of Object.entries(urls)) if (manifest[field] !== expected) throw new GuideError('manifest_url_mismatch', `Release manifest ${field} must be ${expected}.`);
  if (!manifest.files || typeof manifest.files !== 'object' || Array.isArray(manifest.files)) throw new GuideError('manifest_files_invalid', 'Release manifest files must be an object.');
  for (const [relative, hash] of Object.entries(manifest.files)) {
    if (relative.startsWith('/') || relative.includes('..') || relative.includes('\\')) throw new GuideError('manifest_path_invalid', `Unsafe release manifest path: ${relative}.`);
    if (!/^sha256:[0-9a-f]{64}$/.test(hash)) throw new GuideError('manifest_digest_invalid', `Invalid digest for ${relative}.`);
  }
  for (const [skill, expectedDigest] of Object.entries(release.skillDigests)) {
    const prefix = `skills/${skill}/`;
    const fileManifest = Object.entries(manifest.files).filter(([filePath]) => filePath.startsWith(prefix))
      .map(([filePath, hash]) => [filePath.slice(prefix.length), hash])
      .sort(([left], [right]) => compareOrdinalUtf8(left, right))
      .map(([relative, hash]) => `${hash.slice('sha256:'.length)}  ${relative}\n`).join('');
    if (!fileManifest || digest(fileManifest) !== expectedDigest) throw new GuideError('manifest_skill_digest_mismatch', `Release manifest files do not produce the declared digest for ${skill}.`);
  }
}

export async function loadReleaseArtifacts({
  release,
  releaseCatalogPath,
  releaseManifestPath,
  offline = false,
  state,
  statePath,
  fetchImplementation,
} = {}) {
  if (!release?.tag) throw new GuideError('release_required', 'A selected immutable release is required before loading release artifacts.');
  const cachePaths = releaseArtifactCachePaths(statePath, release.tag);
  const record = state.verifiedReleaseArtifacts?.[release.tag];
  let catalogText;
  let manifestText;
  let source;
  if (releaseCatalogPath || releaseManifestPath) {
    if (!releaseCatalogPath || !releaseManifestPath) throw new GuideError('release_asset_pair_required', 'Explicit release catalog and manifest paths must be supplied together.');
    catalogText = fs.readFileSync(path.resolve(releaseCatalogPath), 'utf8');
    manifestText = fs.readFileSync(path.resolve(releaseManifestPath), 'utf8');
    if (!record || record.catalogDigest !== digest(Buffer.from(catalogText)) || record.manifestDigest !== digest(Buffer.from(manifestText))) {
      throw new GuideError('unverified_release_asset_file', `Explicit release assets for ${release.tag} do not match verified user cache state.`);
    }
    source = 'explicit-files';
  } else if (offline) {
    if (!record || !fs.existsSync(cachePaths.catalog) || !fs.existsSync(cachePaths.manifest)) throw new GuideError('offline_release_artifacts_unavailable', `Verified catalog and manifest cache is unavailable for ${release.tag}.`);
    catalogText = fs.readFileSync(cachePaths.catalog, 'utf8');
    manifestText = fs.readFileSync(cachePaths.manifest, 'utf8');
    if (digest(Buffer.from(catalogText)) !== record.catalogDigest || digest(Buffer.from(manifestText)) !== record.manifestDigest) {
      throw new GuideError('cached_release_artifacts_changed', `Cached release artifacts for ${release.tag} no longer match verified state.`);
    }
    source = 'verified-cache';
  } else {
    catalogText = await fetchText(release.catalogUrl, fetchImplementation);
    manifestText = await fetchText(release.manifestUrl, fetchImplementation);
    source = release.assetBaseUrl;
  }
  const catalogDigest = digest(Buffer.from(catalogText));
  const manifestDigest = digest(Buffer.from(manifestText));
  if (source !== 'verified-cache' && record
    && (record.catalogDigest !== catalogDigest || record.manifestDigest !== manifestDigest)) {
    throw new GuideError('immutable_release_artifacts_changed', `Catalog or manifest bytes for immutable tag ${release.tag} differ from previously verified digests.`);
  }
  if (catalogDigest !== release.catalogDigest) throw new GuideError('catalog_digest_mismatch', `Fetched catalog ${catalogDigest} does not match release ${release.catalogDigest}.`);
  const catalog = parseAssetJson(catalogText, 'Release catalog');
  requireSupportedSelectedSchema(catalog, {
    asset: 'agent-skill-catalog.json',
    releaseTag: release.tag,
    supportedVersion: SUPPORTED_CATALOG_SCHEMA_VERSION,
  });
  validateCatalog(catalog);
  const catalogSkills = Object.entries(catalog.skills)
    .filter(([, entry]) => entry.ownership === 'monica' && entry.managed === true)
    .map(([name]) => name)
    .sort(compareOrdinalUtf8);
  const releaseSkills = Object.keys(release.skillDigests).sort(compareOrdinalUtf8);
  if (JSON.stringify(catalogSkills) !== JSON.stringify(releaseSkills)) {
    throw new GuideError(
      'release_catalog_skill_set_mismatch',
      'Release per-skill version maps do not exactly match the catalog-managed Monica skill set.',
      { catalogSkills, releaseSkills },
    );
  }
  const manifest = parseAssetJson(manifestText, 'Release manifest');
  if (manifestDigest !== release.manifestDigest) throw new GuideError('manifest_digest_mismatch', `Fetched manifest ${manifestDigest} does not match release ${release.manifestDigest}.`);
  verifyReleaseManifest(manifest, release, catalogDigest);
  return {
    tag: release.tag,
    catalog,
    catalogDigest,
    catalogContent: catalogText,
    manifest,
    manifestDigest,
    manifestContent: manifestText,
    cachePaths,
    source,
    needsCache: source !== 'verified-cache',
  };
}

export function validateCatalog(catalog) {
  if (catalog?.schemaVersion !== SUPPORTED_CATALOG_SCHEMA_VERSION) {
    const actualSchemaVersion = catalog?.schemaVersion ?? 'missing';
    const remediation = 'Use Monica Guide from the immutable release that produced this catalog, or explicitly upgrade/switch the workspace to a release supported by the installed Guide. Guide will not reinterpret or substitute incompatible catalog bytes.';
    throw new GuideError(
      'catalog_schema_mismatch',
      `This Monica Guide supports catalog schema ${SUPPORTED_CATALOG_SCHEMA_VERSION}, but the selected immutable release uses schema ${actualSchemaVersion}. ${remediation}`,
      { expectedSchemaVersion: SUPPORTED_CATALOG_SCHEMA_VERSION, actualSchemaVersion, remediation },
    );
  }
  if (catalog.$schema !== './schemas/agent-skill-catalog.schema.json') throw new GuideError('catalog_schema_reference_mismatch', 'Catalog must identify the canonical agent-skill-catalog schema.');
  const requiredTopLevel = ['$schema', 'schemaVersion', 'catalogVersion', 'skills', 'externalSkills', 'profiles', 'profileClosurePolicy', 'sourceRepositories', 'sourcePolicies', 'aliases', 'managedInstructions', 'prompts', 'distribution'];
  const unexpectedTopLevel = Object.keys(catalog).filter((key) => !requiredTopLevel.includes(key));
  if (requiredTopLevel.some((key) => !Object.hasOwn(catalog, key)) || unexpectedTopLevel.length) throw new GuideError('invalid_catalog', 'Catalog top-level fields do not match the canonical schema.', { unexpectedTopLevel });
  if (!/^\d+\.\d+\.\d+$/.test(catalog.catalogVersion || '')) throw new GuideError('invalid_catalog_version', `Catalog version ${catalog.catalogVersion || 'missing'} is invalid.`);
  if (!catalog.skills || typeof catalog.skills !== 'object' || Array.isArray(catalog.skills)) throw new GuideError('invalid_catalog', 'Catalog skills must be an object keyed by canonical skill name.');
  if (Object.keys(catalog.skills).length === 0) throw new GuideError('invalid_catalog', 'Catalog must contain at least one Monica skill.');
  if (!catalog.externalSkills || typeof catalog.externalSkills !== 'object' || Array.isArray(catalog.externalSkills)) throw new GuideError('invalid_catalog', 'Catalog externalSkills must be an object.');
  if (!catalog.profiles || typeof catalog.profiles !== 'object' || Array.isArray(catalog.profiles)) throw new GuideError('invalid_catalog', 'Catalog profiles must be an object.');
  const skillNamePattern = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
  const safeRelativePath = (value) => typeof value === 'string'
    && value.length > 0
    && !value.startsWith('/')
    && !/^[A-Za-z]:/.test(value)
    && !value.split('/').includes('..')
    && !value.includes('\\');
  const exactKeys = (value, required, optional, label) => {
    if (!value || typeof value !== 'object' || Array.isArray(value)) throw new GuideError('invalid_catalog', `${label} must be an object.`);
    const allowed = new Set([...required, ...optional]);
    const missing = required.filter((key) => !Object.hasOwn(value, key));
    const unexpected = Object.keys(value).filter((key) => !allowed.has(key));
    if (missing.length || unexpected.length) throw new GuideError('invalid_catalog', `${label} fields do not match the canonical schema.`, { missing, unexpected });
  };
  const nameList = (value, label) => {
    if (!Array.isArray(value) || new Set(value).size !== value.length || value.some((name) => !skillNamePattern.test(name))) {
      throw new GuideError('invalid_catalog', `${label} must be a unique list of skill names.`);
    }
    return value;
  };
  const conditionals = (value, label) => {
    if (!Array.isArray(value)) throw new GuideError('invalid_catalog', `${label} must be an array.`);
    for (const [index, conditional] of value.entries()) {
      exactKeys(conditional, ['capability', 'skills'], [], `${label}[${index}]`);
      if (!skillNamePattern.test(conditional.capability || '')) throw new GuideError('invalid_catalog', `${label}[${index}] has an invalid capability.`);
      nameList(conditional.skills, `${label}[${index}].skills`);
    }
  };
  for (const [name, skill] of Object.entries(catalog.skills)) {
    if (!skillNamePattern.test(name)) throw new GuideError('invalid_catalog', `Invalid canonical skill name ${name}.`);
    exactKeys(skill, ['path', 'role', 'ownership', 'managed', 'dependencies'], ['routes'], `skills.${name}`);
    if (!safeRelativePath(skill.path) || skill.path !== `skills/${name}`) throw new GuideError('invalid_catalog', `Managed Monica skill ${name} has unsafe or noncanonical path ${skill.path}.`);
    if (!['router', 'capability'].includes(skill.role) || skill.ownership !== 'monica' || skill.managed !== true) throw new GuideError('invalid_catalog', `Skill ${name} has invalid role or ownership policy.`);
    if (skill.routes !== undefined) nameList(skill.routes, `skills.${name}.routes`);
    exactKeys(skill.dependencies, ['required', 'recommended', 'conditional', 'external'], [], `skills.${name}.dependencies`);
    nameList(skill.dependencies.required, `skills.${name}.dependencies.required`);
    nameList(skill.dependencies.recommended, `skills.${name}.dependencies.recommended`);
    nameList(skill.dependencies.external, `skills.${name}.dependencies.external`);
    conditionals(skill.dependencies.conditional, `skills.${name}.dependencies.conditional`);
  }
  for (const [name, external] of Object.entries(catalog.externalSkills)) {
    if (!skillNamePattern.test(name)) throw new GuideError('invalid_catalog', `Invalid external skill name ${name}.`);
    exactKeys(external, ['ownership', 'managed', 'purpose'], ['distribution'], `externalSkills.${name}`);
    if (external.ownership !== 'external' || external.managed !== false || typeof external.purpose !== 'string' || !external.purpose.trim()) throw new GuideError('invalid_catalog', `External skill ${name} has an invalid ownership contract.`);
    if (external.distribution !== undefined) {
      const distribution = external.distribution;
      exactKeys(distribution, ['repository', 'ref', 'commit', 'immutableSkillUrl', 'digest', 'digestAlgorithm', 'requiredFor'], [], `externalSkills.${name}.distribution`);
      nameList(distribution.requiredFor, `externalSkills.${name}.distribution.requiredFor`);
      const expectedUrl = `https://github.com/${distribution.repository}/tree/${distribution.commit}`;
      if (!/^[A-Za-z0-9_.-]+\/[A-Za-z0-9_.-]+$/.test(distribution.repository || '')
        || typeof distribution.ref !== 'string'
        || !distribution.ref.trim()
        || !/^[0-9a-f]{40}$/.test(distribution.commit || '')
        || distribution.immutableSkillUrl !== expectedUrl
        || !/^sha256:[0-9a-f]{64}$/.test(distribution.digest || '')
        || distribution.digestAlgorithm !== 'sha256-file-manifest-v1') {
        throw new GuideError('invalid_catalog', `External skill ${name} has an invalid immutable distribution contract.`);
      }
    }
  }
  const assertSkillReferences = (names, label, collection = catalog.skills) => {
    for (const name of names) if (!Object.hasOwn(collection, name)) throw new GuideError('invalid_catalog_reference', `${label} references missing skill ${name}.`);
  };
  for (const [name, skill] of Object.entries(catalog.skills)) {
    assertSkillReferences([...(skill.routes || []), ...skill.dependencies.required, ...skill.dependencies.recommended, ...skill.dependencies.conditional.flatMap((entry) => entry.skills)], `skills.${name}`);
    assertSkillReferences(skill.dependencies.external, `skills.${name}.dependencies.external`, catalog.externalSkills);
  }
  const visiting = new Set();
  const visited = new Set();
  const visitRequired = (name) => {
    if (visiting.has(name)) throw new GuideError('catalog_dependency_cycle', `Required skill dependencies contain a cycle at ${name}.`);
    if (visited.has(name)) return;
    visiting.add(name);
    for (const dependency of catalog.skills[name].dependencies.required) visitRequired(dependency);
    visiting.delete(name);
    visited.add(name);
  };
  for (const name of Object.keys(catalog.skills)) visitRequired(name);
  exactKeys(catalog.sourceRepositories, ['Tairitsua/Monica', 'Tairitsua/Monica.Docs'], [], 'sourceRepositories');
  const expectedSourceAliases = new Map([
    ['Tairitsua/Monica', ['monica']],
    ['Tairitsua/Monica.Docs', ['docs']],
  ]);
  const sourceAliases = new Set();
  for (const [repositoryName, repository] of Object.entries(catalog.sourceRepositories)) {
    exactKeys(repository, ['aliases', 'resolverQuery'], [], `sourceRepositories.${repositoryName}`);
    nameList(repository.aliases, `sourceRepositories.${repositoryName}.aliases`);
    if (repository.resolverQuery !== repositoryName
      || JSON.stringify(repository.aliases) !== JSON.stringify(expectedSourceAliases.get(repositoryName))) {
      throw new GuideError('invalid_catalog', `Source repository ${repositoryName} has an unsupported identity or alias contract.`);
    }
    for (const alias of repository.aliases) {
      if (sourceAliases.has(alias)) throw new GuideError('invalid_catalog', `Source repository alias ${alias} is assigned more than once.`);
      sourceAliases.add(alias);
    }
  }
  for (const profileName of ['application', 'extension-author', 'framework-contributor', 'docs-contributor']) {
    const profile = catalog.profiles[profileName];
    if (!profile) throw new GuideError('invalid_catalog', `Catalog is missing required profile ${profileName}.`);
    exactKeys(profile, ['description', 'skills', 'sourceRequirements', 'repositories', 'inference'], [], `profiles.${profileName}`);
    if (typeof profile.description !== 'string' || !profile.description.trim()) throw new GuideError('invalid_catalog', `Profile ${profileName} has no description.`);
    exactKeys(profile.skills, ['required', 'recommended', 'conditional'], [], `profiles.${profileName}.skills`);
    nameList(profile.skills.required, `profiles.${profileName}.skills.required`);
    nameList(profile.skills.recommended, `profiles.${profileName}.skills.recommended`);
    conditionals(profile.skills.conditional, `profiles.${profileName}.skills.conditional`);
    assertSkillReferences([...profile.skills.required, ...profile.skills.recommended, ...profile.skills.conditional.flatMap((entry) => entry.skills)], `profiles.${profileName}`);
    if (!Array.isArray(profile.sourceRequirements)) throw new GuideError('invalid_catalog', `Profile ${profileName} sourceRequirements must be an array.`);
    const requiredRepositories = new Set();
    for (const [index, requirement] of profile.sourceRequirements.entries()) {
      const label = `profiles.${profileName}.sourceRequirements[${index}]`;
      exactKeys(requirement, ['repository', 'requirement', 'compatibility'], ['condition'], label);
      if (!Object.hasOwn(catalog.sourceRepositories, requirement.repository)
        || !['required', 'conditional'].includes(requirement.requirement)
        || !['framework-version', 'workspace-commit'].includes(requirement.compatibility)
        || (requirement.requirement === 'conditional') !== (typeof requirement.condition === 'string' && skillNamePattern.test(requirement.condition))) {
        throw new GuideError('invalid_catalog', `${label} has an invalid source requirement.`);
      }
      if (requiredRepositories.has(requirement.repository)) throw new GuideError('invalid_catalog', `Profile ${profileName} repeats source repository ${requirement.repository}.`);
      requiredRepositories.add(requirement.repository);
    }
    if (!Array.isArray(profile.repositories)) throw new GuideError('invalid_catalog', `Profile ${profileName} repositories must be an array.`);
    for (const [index, repository] of profile.repositories.entries()) {
      exactKeys(repository, ['repository', 'access'], ['purpose'], `profiles.${profileName}.repositories[${index}]`);
      if (typeof repository.repository !== 'string' || !repository.repository.trim() || !['read-only', 'read-write'].includes(repository.access) || (repository.purpose !== undefined && (typeof repository.purpose !== 'string' || !repository.purpose.trim()))) throw new GuideError('invalid_catalog', `Profile ${profileName} has an invalid repository requirement.`);
    }
    exactKeys(profile.inference, ['repositoryIdentities', 'characteristicPaths'], [], `profiles.${profileName}.inference`);
    for (const field of ['repositoryIdentities', 'characteristicPaths']) if (!Array.isArray(profile.inference[field]) || new Set(profile.inference[field]).size !== profile.inference[field].length) throw new GuideError('invalid_catalog', `Profile ${profileName} inference ${field} must be a unique array.`);
    if (profile.inference.characteristicPaths.some((entry) => !safeRelativePath(entry))) throw new GuideError('invalid_catalog', `Profile ${profileName} has an unsafe characteristic path.`);
  }
  const sourceResolver = catalog.externalSkills[catalog.sourcePolicies?.immutableBinding?.resolverSkill];
  if (JSON.stringify(sourceResolver?.distribution?.requiredFor) !== '["cached-source-resolution"]') throw new GuideError('invalid_catalog_reference', 'The immutable source resolver must declare the cached-source-resolution capability.');
  exactKeys(catalog.profileClosurePolicy, ['traverseSkillDependencies', 'recommendations', 'conditional'], [], 'profileClosurePolicy');
  if (JSON.stringify(catalog.profileClosurePolicy.traverseSkillDependencies) !== '["required"]' || catalog.profileClosurePolicy.recommendations !== 'profile-explicit' || catalog.profileClosurePolicy.conditional !== 'selected-capabilities-only') throw new GuideError('invalid_catalog', 'Catalog profile closure policy is unsupported.');
  exactKeys(catalog.sourcePolicies, ['bindingScope', 'bindingCardinality', 'channels', 'versionResolutionOrder', 'immutableBinding', 'failClosedOn', 'offline'], [], 'sourcePolicies');
  if (catalog.sourcePolicies.bindingScope !== 'global-user' || catalog.sourcePolicies.bindingCardinality !== 'one-per-repository') throw new GuideError('invalid_catalog', 'Catalog source bindings must be one global user binding per first-party repository.');
  if (new Set(catalog.sourcePolicies.channels || []).size !== 3 || !['stable', 'preview', 'source'].every((channel) => catalog.sourcePolicies.channels.includes(channel))) throw new GuideError('invalid_catalog', 'Catalog source channels must be stable, preview, and source.');
  for (const field of ['versionResolutionOrder', 'failClosedOn']) if (!Array.isArray(catalog.sourcePolicies[field]) || !catalog.sourcePolicies[field].length || new Set(catalog.sourcePolicies[field]).size !== catalog.sourcePolicies[field].length) throw new GuideError('invalid_catalog', `sourcePolicies.${field} must be a nonempty unique array.`);
  exactKeys(catalog.sourcePolicies.immutableBinding, ['resolverSkill', 'command', 'storedFields'], [], 'sourcePolicies.immutableBinding');
  const expectedStoredFields = ['repository', 'ref', 'commit', 'provenance', 'resolutionKind', 'sourcePath'];
  if (!Object.hasOwn(catalog.externalSkills, catalog.sourcePolicies.immutableBinding.resolverSkill)
    || catalog.sourcePolicies.immutableBinding.command !== 'resolve <repository> --ref <immutable-ref> --json'
    || JSON.stringify(catalog.sourcePolicies.immutableBinding.storedFields) !== JSON.stringify(expectedStoredFields)) {
    throw new GuideError('invalid_catalog', 'Catalog immutable source binding contract is invalid.');
  }
  if (typeof catalog.sourcePolicies.offline !== 'string' || !catalog.sourcePolicies.offline.trim()) throw new GuideError('invalid_catalog', 'Catalog offline source policy is missing.');
  for (const [alias, entry] of Object.entries(catalog.aliases)) {
    if (!skillNamePattern.test(alias)) throw new GuideError('invalid_catalog', `Invalid alias ${alias}.`);
    exactKeys(entry, ['canonical', 'diagnostic'], [], `aliases.${alias}`);
    if (!Object.hasOwn(catalog.skills, entry.canonical) || typeof entry.diagnostic !== 'string' || !entry.diagnostic.trim()) throw new GuideError('invalid_catalog_reference', `Alias ${alias} has an invalid canonical target.`);
  }
  exactKeys(catalog.managedInstructions, ['version', 'markers', 'templates'], [], 'managedInstructions');
  if (!Number.isInteger(catalog.managedInstructions.version) || catalog.managedInstructions.version < 1) throw new GuideError('invalid_catalog', 'Managed instruction version must be a positive integer.');
  exactKeys(catalog.managedInstructions.markers, ['start', 'end'], [], 'managedInstructions.markers');
  if (!catalog.managedInstructions.markers.start || !catalog.managedInstructions.markers.end || catalog.managedInstructions.markers.start === catalog.managedInstructions.markers.end) throw new GuideError('invalid_catalog', 'Managed instruction markers are invalid.');
  for (const profileName of ['application', 'extension-author', 'framework-contributor', 'docs-contributor']) {
    const template = catalog.managedInstructions.templates?.[profileName];
    exactKeys(template, ['skills', 'rules'], [], `managedInstructions.templates.${profileName}`);
    nameList(template.skills, `managedInstructions.templates.${profileName}.skills`);
    assertSkillReferences(template.skills, `managedInstructions.templates.${profileName}`);
    if (!Array.isArray(template.rules) || !template.rules.length || template.rules.some((rule) => typeof rule !== 'string' || !rule.trim())) throw new GuideError('invalid_catalog', `Managed instruction template ${profileName} has invalid rules.`);
  }
  exactKeys(catalog.prompts, ['bootstrapAsset', 'bootstrapSchema'], [], 'prompts');
  if (!safeRelativePath(catalog.prompts.bootstrapAsset) || !safeRelativePath(catalog.prompts.bootstrapSchema)) throw new GuideError('invalid_catalog', 'Catalog bootstrap prompt paths are unsafe.');
  exactKeys(catalog.distribution, ['repository', 'skillsCli', 'immutableSkillUrlTemplate'], [], 'distribution');
  exactKeys(catalog.distribution.skillsCli, ['package', 'version'], [], 'distribution.skillsCli');
  if (catalog.distribution.repository !== 'Tairitsua/Monica'
    || catalog.distribution.skillsCli.package !== 'skills'
    || !/^\d+\.\d+\.\d+$/.test(catalog.distribution.skillsCli.version || '')
    || catalog.distribution.immutableSkillUrlTemplate !== 'https://github.com/Tairitsua/Monica/tree/{tag}/skills/{skill}') {
    throw new GuideError('invalid_catalog', 'Catalog distribution contract is invalid.');
  }
}

export function validateIndex(index) {
  if (index?.schemaVersion !== SUPPORTED_RELEASE_INDEX_SCHEMA_VERSION) throw new GuideError('index_schema_mismatch', `Expected index schema ${SUPPORTED_RELEASE_INDEX_SCHEMA_VERSION}, found ${index?.schemaVersion ?? 'missing'}.`);
  if (index.$schema !== './schemas/agent-skill-index.schema.json') throw new GuideError('index_schema_reference_mismatch', 'Release index must identify the canonical agent-skill-index schema.');
  for (const field of ['channels', 'versions', 'releases']) {
    if (!index[field] || typeof index[field] !== 'object' || Array.isArray(index[field])) throw new GuideError('invalid_index', `Index ${field} must be an object.`);
  }
  const topLevelFields = new Set(['$schema', 'schemaVersion', 'channels', 'versions', 'releases']);
  const unexpectedTopLevel = Object.keys(index).filter((key) => !topLevelFields.has(key));
  const missingTopLevel = [...topLevelFields].filter((key) => !Object.hasOwn(index, key));
  if (unexpectedTopLevel.length || missingTopLevel.length) throw new GuideError('invalid_index', 'Index top-level fields do not match the canonical schema.', { unexpectedTopLevel, missingTopLevel });
  if (!Object.hasOwn(index.channels, 'stable') || !Object.hasOwn(index.channels, 'preview')
    || Object.keys(index.channels).some((channel) => !['stable', 'preview'].includes(channel))) {
    throw new GuideError('invalid_index_channels', 'Index channels must contain exactly stable and preview pointers.');
  }
  for (const [releaseId, release] of Object.entries(index.releases)) {
    validateReleaseTag(releaseId);
    if (!release || typeof release !== 'object' || Array.isArray(release)) throw new GuideError('invalid_release', `Index release ${releaseId} must be an object.`);
    const releaseFields = new Set(['monicaVersion', 'tag', 'commit', 'catalogDigest', 'skillTreeDigest', 'skillDigestAlgorithm', 'skillDigests', 'skillRevisions', 'skillLastChangedIn', 'manifestDigest', 'publishedAt', 'assetBaseUrl', 'catalogUrl', 'manifestUrl']);
    const unexpectedReleaseFields = Object.keys(release).filter((key) => !releaseFields.has(key));
    const missingReleaseFields = [...releaseFields].filter((key) => !Object.hasOwn(release, key));
    if (unexpectedReleaseFields.length || missingReleaseFields.length) throw new GuideError('invalid_release', `Release ${releaseId} fields do not match the canonical schema.`, { unexpectedReleaseFields, missingReleaseFields });
    if (release.tag !== releaseId) throw new GuideError('release_id_tag_mismatch', `Release key ${releaseId} must equal its immutable tag ${release.tag || 'missing'}.`);
    if (!parseSemVer(release.monicaVersion) || releaseId.slice(1) !== release.monicaVersion) {
      throw new GuideError('release_version_mismatch', `Release ${releaseId} must describe matching Monica version ${releaseId.slice(1)}.`);
    }
    if (!/^[0-9a-f]{40}$/.test(release.commit || '')) throw new GuideError('release_not_immutable', `Release ${releaseId} has no lowercase exact commit.`);
    for (const field of ['catalogDigest', 'skillTreeDigest', 'manifestDigest']) {
      if (!/^sha256:[0-9a-f]{64}$/.test(release[field] || '')) throw new GuideError('release_digest_invalid', `Release ${releaseId} has invalid ${field}.`);
    }
    validateReleaseSkillContracts(releaseId, release);
    if (!release.publishedAt || Number.isNaN(Date.parse(release.publishedAt))) throw new GuideError('release_timestamp_invalid', `Release ${releaseId} has invalid publishedAt.`);
    const expectedBase = `https://github.com/Tairitsua/Monica/releases/download/${releaseId}`;
    if (release.assetBaseUrl !== expectedBase
      || release.catalogUrl !== `${expectedBase}/agent-skill-catalog.json`
      || release.manifestUrl !== `${expectedBase}/agent-skill-manifest.json`) {
      throw new GuideError('release_asset_url_mismatch', `Release ${releaseId} asset URLs must be deterministic immutable tag assets.`);
    }
  }
  for (const [version, releaseId] of Object.entries(index.versions)) {
    if (!parseSemVer(version)) throw new GuideError('index_version_invalid', `Index version key ${version} is not exact SemVer.`);
    const release = index.releases[releaseId];
    if (!release) throw new GuideError('index_release_reference_missing', `Version ${version} references missing release ${releaseId}.`);
    if (release.monicaVersion !== version) throw new GuideError('index_version_mapping_mismatch', `Version ${version} maps to release ${releaseId} for ${release.monicaVersion}.`);
  }
  for (const [releaseId, release] of Object.entries(index.releases)) {
    if (index.versions[release.monicaVersion] !== releaseId) throw new GuideError('index_release_unmapped', `Release ${releaseId} is not exactly mapped by version ${release.monicaVersion}.`);
    for (const skill of Object.keys(release.skillDigests)) {
      const changedReleaseId = release.skillLastChangedIn[skill];
      const changedRelease = index.releases[changedReleaseId];
      if (!changedRelease) throw new GuideError('skill_last_changed_release_missing', `Release ${releaseId} records ${skill} as last changed in missing release ${changedReleaseId}.`);
      if (changedRelease.skillDigests?.[skill] !== release.skillDigests[skill]
        || changedRelease.skillRevisions?.[skill] !== release.skillRevisions[skill]) {
        throw new GuideError('skill_last_changed_contract_mismatch', `Release ${releaseId} metadata for ${skill} does not match last-changed release ${changedReleaseId}.`);
      }
    }
  }
  const chronological = Object.entries(index.releases)
    .map(([releaseId, release]) => ({ releaseId, release, publishedAt: Date.parse(release.publishedAt) }))
    .sort((left, right) => left.publishedAt - right.publishedAt);
  for (let indexPosition = 1; indexPosition < chronological.length; indexPosition += 1) {
    if (chronological[indexPosition - 1].publishedAt === chronological[indexPosition].publishedAt) {
      throw new GuideError(
        'release_timestamp_not_sequential',
        `Releases ${chronological[indexPosition - 1].releaseId} and ${chronological[indexPosition].releaseId} have the same publishedAt instant; global skill revision history requires a strict total order.`,
      );
    }
  }
  const historicallySeenSkills = new Set();
  for (let indexPosition = 0; indexPosition < chronological.length; indexPosition += 1) {
    const { releaseId, release } = chronological[indexPosition];
    const previous = chronological[indexPosition - 1]?.release || null;
    for (const skill of Object.keys(release.skillDigests)) {
      const previousDigest = previous?.skillDigests?.[skill];
      if (previous && previousDigest === undefined && historicallySeenSkills.has(skill)) {
        throw new GuideError(
          'skill_revision_lineage_reintroduced',
          `Release ${releaseId} reintroduces ${skill} after it was absent from the immediately previous release; revision lineage cannot be restarted.`,
        );
      }
      const expectedRevision = previousDigest === undefined
        ? 1
        : previousDigest === release.skillDigests[skill]
          ? previous.skillRevisions[skill]
          : previous.skillRevisions[skill] + 1;
      const expectedOrigin = previousDigest !== undefined && previousDigest === release.skillDigests[skill]
        ? previous.skillLastChangedIn[skill]
        : releaseId;
      if (release.skillRevisions[skill] !== expectedRevision || release.skillLastChangedIn[skill] !== expectedOrigin) {
        throw new GuideError(
          'skill_revision_sequence_invalid',
          `Release ${releaseId} violates the global sequential revision contract for ${skill}.`,
          {
            previousRelease: chronological[indexPosition - 1]?.releaseId || null,
            expectedRevision,
            actualRevision: release.skillRevisions[skill],
            expectedLastChangedIn: expectedOrigin,
            actualLastChangedIn: release.skillLastChangedIn[skill],
          },
        );
      }
      historicallySeenSkills.add(skill);
    }
  }
  for (const channel of ['stable', 'preview']) {
    const releaseId = index.channels[channel];
    if (releaseId === null) continue;
    const release = index.releases[releaseId];
    if (!release) throw new GuideError('index_release_reference_missing', `Channel ${channel} references missing release ${releaseId}.`);
    if (semverChannel(release.monicaVersion) !== channel) throw new GuideError('index_channel_mapping_mismatch', `Channel ${channel} points to ${releaseId}, which is ${semverChannel(release.monicaVersion)}.`);
  }
}

function dependencySet(value) {
  return Array.isArray(value) ? value : [];
}

function conditionalSkills(entries, capabilities) {
  if (!Array.isArray(entries)) return [];
  return entries.flatMap((entry) => capabilities.has(entry.capability) ? dependencySet(entry.skills) : []);
}

export function resolveRequiredSkillClosure(catalog, roots) {
  const selected = new Set();
  const visit = (name) => {
    if (selected.has(name)) return;
    const entry = catalog.skills[name];
    if (!entry || entry.ownership !== 'monica' || entry.managed === false) {
      throw new GuideError('managed_skill_dependency_invalid', `Monica Guide cannot manage required skill ${name}.`);
    }
    selected.add(name);
    for (const dependency of dependencySet(entry.dependencies?.required)) visit(dependency);
  };
  for (const name of roots) visit(name);
  return [...selected].sort(compareOrdinalUtf8);
}

export function resolveProfileClosure(catalog, profileName, selectedCapabilities = []) {
  const profile = catalog.profiles[profileName];
  if (!profile) throw new GuideError('unknown_profile', `Unknown Monica profile: ${profileName}.`, { profiles: Object.keys(catalog.profiles).sort() });
  const capabilities = new Set(selectedCapabilities);
  const required = new Set();
  const recommended = new Set();
  const conditional = new Set();
  const external = new Set();
  const assigned = new Map();
  const buckets = [required, recommended, conditional];

  const visit = (name, priority) => {
    const skill = catalog.skills[name];
    if (!skill) throw new GuideError('missing_catalog_skill', `Profile ${profileName} references missing skill ${name}.`);
    const previous = assigned.get(name);
    if (previous !== undefined && previous <= priority) return;
    if (previous !== undefined) buckets[previous].delete(name);
    assigned.set(name, priority);
    buckets[priority].add(name);
    const dependencies = skill.dependencies || {};
    for (const dependency of dependencySet(dependencies.required)) {
      visit(dependency, priority);
    }
    for (const dependency of dependencySet(dependencies.external)) external.add(dependency);
  };

  for (const name of dependencySet(profile.skills?.required)) visit(name, 0);
  for (const name of dependencySet(profile.skills?.recommended)) visit(name, 1);
  for (const name of conditionalSkills(profile.skills?.conditional, capabilities)) visit(name, 2);
  const selected = [...new Set([...required, ...recommended, ...conditional])]
    .filter((name) => catalog.skills[name]?.ownership === 'monica' && catalog.skills[name]?.managed !== false)
    .sort();
  return {
    profile: profileName,
    capabilities: [...capabilities].sort(),
    required: [...required].sort(),
    recommended: [...recommended].sort(),
    conditional: [...conditional].sort(),
    external: [...external].sort(),
    selected,
    sourceRequirements: profile.sourceRequirements.map((requirement) => ({ ...requirement })),
  };
}

export function resolveRelease(index, { channel = 'stable', frameworkVersion = null, sourceRef = null } = {}) {
  if (channel === 'source') {
    if (!sourceRef || !/^[0-9a-f]{40}$/i.test(sourceRef)) throw new GuideError('source_commit_required', 'The source channel requires --source-ref with an explicit 40-character commit.');
    return {
      id: `source:${sourceRef.toLowerCase()}`,
      monicaVersion: frameworkVersion,
      tag: null,
      commit: sourceRef.toLowerCase(),
      catalogDigest: null,
      skillTreeDigest: null,
      publishedAt: null,
      channel: 'source',
      installRef: sourceRef.toLowerCase(),
    };
  }
  if (!['stable', 'preview'].includes(channel)) throw new GuideError('invalid_channel', `Unsupported channel: ${channel}.`);
  let releaseId = frameworkVersion ? index.versions[frameworkVersion] : null;
  if (frameworkVersion && !releaseId) throw new GuideError('version_unpublished', `No immutable Monica skill release is mapped to framework version ${frameworkVersion}.`);
  releaseId ||= index.channels[channel];
  if (!releaseId) throw new GuideError('channel_unpublished', `The ${channel} channel has no published immutable Monica skill release.`);
  const release = index.releases[releaseId];
  if (!release) throw new GuideError('release_missing', `Index release ${releaseId} does not exist.`);
  if (!release.tag || !String(release.tag).startsWith('v') || !parseSemVer(String(release.tag).slice(1))) throw new GuideError('release_not_immutable', `Release ${releaseId} does not contain a valid immutable tag.`);
  if (!release.commit || !/^[0-9a-f]{40}$/i.test(release.commit)) throw new GuideError('release_not_immutable', `Release ${releaseId} does not contain an exact commit.`);
  if (frameworkVersion && release.monicaVersion !== frameworkVersion) throw new GuideError('release_version_mismatch', `Release ${releaseId} targets ${release.monicaVersion}, not detected framework ${frameworkVersion}.`);
  const releaseChannel = semverChannel(release.monicaVersion);
  if (!releaseChannel) throw new GuideError('release_version_invalid', `Release ${releaseId} has invalid Monica SemVer ${release.monicaVersion}.`);
  if (channel !== releaseChannel) throw new GuideError('release_channel_mismatch', `Release ${releaseId} is ${releaseChannel}, not requested channel ${channel}.`);
  return { id: releaseId, ...release, channel, installRef: release.tag };
}

export function resolveTaggedRelease(index, releaseTag) {
  const { release } = verifyReleaseIndex(index, releaseTag);
  const channel = semverChannel(release.monicaVersion);
  if (!channel) throw new GuideError('release_version_invalid', `Release ${release.id} has invalid Monica SemVer ${release.monicaVersion}.`);
  return { ...release, channel, installRef: release.tag };
}

export function assertReleaseSelectorCompatibility({ releaseTag = null, sourceRef = null, explicitChannel = null, persistedChannel = null } = {}) {
  const hasExactSelector = Boolean(releaseTag || sourceRef);
  if (!hasExactSelector) return;
  if (releaseTag && sourceRef) {
    throw new GuideError('release_selector_conflict', '--release-tag and --source-ref select different release kinds and cannot be combined.');
  }
  if (explicitChannel && persistedChannel && explicitChannel !== persistedChannel) {
    throw new GuideError(
      'release_channel_constraint_conflict',
      `Explicit channel ${explicitChannel} conflicts with repository channel ${persistedChannel}.`,
      { explicitChannel, persistedChannel },
    );
  }
  const constrainedChannel = explicitChannel || persistedChannel;
  if (releaseTag && constrainedChannel === 'source') {
    throw new GuideError('release_selector_conflict', 'An immutable --release-tag cannot be combined with the source channel.');
  }
  if (sourceRef && constrainedChannel !== 'source') {
    throw new GuideError('release_selector_conflict', '--source-ref is accepted only when the selected channel is source.');
  }
}

export function assertTaggedReleaseConstraints(release, {
  explicitChannel = null,
  persistedChannel = null,
  expectedRelease = null,
  frameworkVersion = null,
} = {}) {
  if (explicitChannel && explicitChannel !== release.channel) {
    throw new GuideError(
      'release_channel_mismatch',
      `Release ${release.id} is ${release.channel}, not explicitly requested channel ${explicitChannel}.`,
      { release: release.id, releaseChannel: release.channel, explicitChannel },
    );
  }
  if (persistedChannel && persistedChannel !== release.channel) {
    throw new GuideError(
      'project_channel_conflict',
      `Release ${release.id} is ${release.channel}, but this repository is configured for ${persistedChannel}.`,
      { release: release.id, releaseChannel: release.channel, persistedChannel },
    );
  }
  if (expectedRelease) {
    const mismatches = {};
    for (const field of ['id', 'tag', 'commit', 'catalogDigest']) {
      if (expectedRelease[field] != null && expectedRelease[field] !== release[field]) {
        mismatches[field] = { expected: expectedRelease[field], actual: release[field] ?? null };
      }
    }
    if (Object.keys(mismatches).length) {
      throw new GuideError(
        'project_release_conflict',
        `Explicit release ${release.id} conflicts with repository release ${expectedRelease.id || expectedRelease.tag || 'unknown'}.`,
        { mismatches },
      );
    }
  }
  if (frameworkVersion && frameworkVersion !== release.monicaVersion) {
    throw new GuideError(
      'release_version_mismatch',
      `Release ${release.id} targets Monica ${release.monicaVersion}, not detected framework ${frameworkVersion}.`,
      { release: release.id, releaseVersion: release.monicaVersion, frameworkVersion },
    );
  }
}

export function assertCatalogDigest(release, catalogDigest) {
  if (!release?.catalogDigest) return;
  const expected = release.catalogDigest.startsWith('sha256:') ? release.catalogDigest : `sha256:${release.catalogDigest}`;
  if (expected !== catalogDigest) throw new GuideError('catalog_digest_mismatch', `Catalog digest ${catalogDigest} does not match release ${release.id} (${expected}).`);
}

export function renderManagedInstructions(catalog, context) {
  const template = catalog.managedInstructions?.templates?.[context.profile] || catalog.managedInstructions?.templates?.root;
  if (!template) throw new GuideError('instruction_template_missing', 'Catalog does not define managedInstructions.templates.root.');
  if (typeof template === 'object') {
    const skills = Array.isArray(template.skills) ? template.skills.map((name) => `\`$${name}\``).join(', ') : '';
    const rules = Array.isArray(template.rules) ? template.rules.map((rule) => `- ${rule}`).join('\n') : '';
    return `## Monica agent workflow\n\n${skills ? `Profile skills: ${skills}.\n\n` : ''}${rules}`;
  }
  return template
    .replaceAll('{{profile}}', context.profile)
    .replaceAll('{{release}}', context.release?.id || 'unresolved')
    .replaceAll('{{channel}}', context.channel);
}

export function canonicalSkillUrl(releaseRef, skillName) {
  if (!releaseRef || !skillName) throw new GuideError('install_url_unresolved', 'An immutable release ref and skill name are required.');
  return `https://github.com/Tairitsua/Monica/tree/${releaseRef}/skills/${skillName}`;
}

export function skillsCliSpec(catalog) {
  const packageName = catalog.distribution?.skillsCli?.package;
  const version = catalog.distribution?.skillsCli?.version;
  if (packageName !== 'skills' || !parseSemVer(version)) {
    throw new GuideError('skills_cli_contract_invalid', 'Catalog must pin distribution.skillsCli to an exact skills package version.');
  }
  return `${packageName}@${version}`;
}
