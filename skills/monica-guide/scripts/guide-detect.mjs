import fs from 'node:fs';
import path from 'node:path';
import { GuideError, canonicalRepository, exists, gitInfo, parseSemVer, readJson, readText, walkFiles } from './guide-shared.mjs';

const MONICA_PACKAGE = /^Monica(?:\.|$)/i;
const BUNDLED_SKILL_DIRECTORIES = ['skills', '.agents', '.claude', '.codex'];

function xmlAttributes(text, tagName) {
  const matches = [];
  const expression = new RegExp(`<${tagName}\\b([^>]*)>(?:([\\s\\S]*?)<\\/${tagName}>)?`, 'gi');
  for (const match of text.matchAll(expression)) {
    const attributes = {};
    for (const attribute of match[1].matchAll(/([\w:.-]+)\s*=\s*["']([^"']*)["']/g)) attributes[attribute[1]] = attribute[2];
    matches.push({ attributes, body: match[2] || '' });
  }
  return matches;
}

function versionFromProps(repositoryRoot) {
  for (const name of ['Directory.Build.props', 'Directory.Packages.props']) {
    const file = path.join(repositoryRoot, name);
    if (!exists(file)) continue;
    const text = readText(file);
    const direct = text.match(/<(?:PackageVersion|Version|VersionPrefix)>\s*([^<$\s][^<]*)<\//i)?.[1]?.trim();
    if (direct && parseSemVer(direct)) return direct;
  }
  return null;
}

function frameworkRootVersion(repositoryRoot) {
  if (!repositoryRoot) return null;
  const file = path.join(repositoryRoot, 'Directory.Build.props');
  if (!exists(file)) return null;
  const value = readText(file).match(/<Version>\s*([^<]+?)\s*<\/Version>/i)?.[1];
  if (!value) return null;
  return {
    version: exactVersion(value, file),
    tier: 'framework-root',
    entries: [{ version: exactVersion(value, file), origin: 'Directory.Build.props.Version', file }],
  };
}

function exactVersion(value, origin) {
  if (typeof value !== 'string' || !value.trim()) {
    throw new GuideError('version_unresolved', `Monica version from ${origin} is missing or unresolved.`);
  }
  const normalized = value.trim();
  if (/^\$\([^)]+\)$/.test(normalized)) {
    throw new GuideError('version_property_unresolved', `Monica version property ${normalized} from ${origin} could not be resolved.`);
  }
  if (!parseSemVer(normalized)) throw new GuideError('version_range_unsupported', `Monica version ${normalized} from ${origin} is not exact SemVer.`);
  return normalized;
}

function distinctVersions(entries, tier) {
  const values = [...new Set(entries.map((entry) => entry.version))].sort();
  if (values.length > 1) throw new GuideError('mixed_framework_versions', `Mixed Monica versions were resolved at ${tier}: ${values.join(', ')}.`, { entries });
  return values[0] || null;
}

function collectProperties(text) {
  const properties = {};
  for (const match of text.matchAll(/<([A-Za-z_][\w.-]*)>\s*([^<]+?)\s*<\/\1>/g)) {
    if (!match[2].includes('<')) properties[match[1]] = match[2].trim();
  }
  return properties;
}

function resolvePropertyVersion(value, properties, origin) {
  let resolved = value?.trim();
  const visited = new Set();
  while (resolved) {
    const property = resolved.match(/^\$\(([A-Za-z_][\w.-]*)\)$/)?.[1];
    if (!property) break;
    if (visited.has(property) || !properties[property]) {
      throw new GuideError('version_property_unresolved', `Monica version property $(${property}) from ${origin} could not be resolved.`);
    }
    visited.add(property);
    resolved = properties[property].trim();
  }
  return exactVersion(resolved, origin);
}

function collectProjectReferences(workspace, projectFiles, { allowDirtySourceRoot = null } = {}) {
  const entries = [];
  const sourceCache = new Map();
  for (const file of projectFiles) {
    const text = readText(file);
    for (const reference of xmlAttributes(text, 'ProjectReference')) {
      const include = reference.attributes.Include || reference.attributes.Update;
      if (!include || !/Monica/i.test(include)) continue;
      const referenced = path.resolve(path.dirname(file), include.replaceAll('\\', path.sep));
      const sourceGit = sourceCache.get(path.dirname(referenced)) || gitInfo(path.dirname(referenced));
      sourceCache.set(path.dirname(referenced), sourceGit);
      if (!sourceGit?.commit || !/^[0-9a-f]{40}$/i.test(sourceGit.commit)) {
        throw new GuideError('project_reference_source_unverified', `Cannot identify an exact Git commit for Monica ProjectReference ${include} in ${file}.`);
      }
      const identity = canonicalRepository(sourceGit.remote);
      if (!/^Tairitsua\/Monica$/i.test(identity || '')) {
        throw new GuideError('project_reference_source_identity_mismatch', `Monica ProjectReference ${include} does not resolve inside a canonical Tairitsua/Monica checkout.`, { sourcePath: sourceGit.root, identity });
      }
      const dirtyAllowed = allowDirtySourceRoot && path.resolve(sourceGit.root) === path.resolve(allowDirtySourceRoot);
      if (sourceGit.dirty && !dirtyAllowed) {
        throw new GuideError('dirty_project_reference_source', `Monica ProjectReference ${include} resolves to dirty source whose effective contents cannot be bound to commit ${sourceGit.commit}.`, { sourcePath: sourceGit.root, commit: sourceGit.commit });
      }
      const root = sourceGit.root;
      const version = versionFromProps(root);
      if (!version) throw new GuideError('project_reference_version_unresolved', `Cannot resolve an exact Monica version for ProjectReference ${include} in ${file}.`);
      entries.push({
        version: exactVersion(version, file),
        origin: 'ProjectReference',
        file,
        sourcePath: root,
        sourceCommit: sourceGit.commit.toLowerCase(),
        sourceRepository: identity,
        sourceDirty: sourceGit.dirty,
      });
    }
  }
  return entries;
}

function collectResolvedVersions(workspace, projectFiles, inventory = null) {
  const entries = [];
  const files = inventory ? inventory.filter((file) => path.basename(file) === 'packages.lock.json') : walkFiles(workspace, {
    maxDepth: 8,
    include: (_file, name) => name === 'packages.lock.json',
  });
  for (const projectFile of projectFiles) {
    const assets = path.join(path.dirname(projectFile), 'obj', 'project.assets.json');
    if (exists(assets)) files.push(assets);
  }
  for (const file of files) {
    let json;
    try { json = readJson(file); } catch { continue; }
    if (path.basename(file) === 'project.assets.json') {
      for (const key of Object.keys(json.libraries || {})) {
        const slash = key.lastIndexOf('/');
        if (slash > 0 && MONICA_PACKAGE.test(key.slice(0, slash))) entries.push({ version: exactVersion(key.slice(slash + 1), file), origin: 'project.assets.json', file });
      }
      continue;
    }
    const visit = (value, packageName = null) => {
      if (!value || typeof value !== 'object') return;
      for (const [key, child] of Object.entries(value)) {
        if (MONICA_PACKAGE.test(key) && child && typeof child === 'object' && child.resolved) {
          entries.push({ version: exactVersion(String(child.resolved), file), origin: 'packages.lock.json', file });
        } else if (typeof child === 'object') visit(child, key);
      }
    };
    visit(json.dependencies || json);
  }
  return entries;
}

function collectCentralVersions(workspace, inventory = null) {
  const entries = [];
  const files = inventory ? inventory.filter((file) => path.basename(file) === 'Directory.Packages.props') : walkFiles(workspace, { maxDepth: 5, include: (_file, name) => name === 'Directory.Packages.props' });
  for (const file of files) {
    const text = readText(file);
    const properties = collectProperties(text);
    for (const item of xmlAttributes(text, 'PackageVersion')) {
      const packageName = item.attributes.Include || item.attributes.Update;
      if (!MONICA_PACKAGE.test(packageName || '')) continue;
      let value = item.attributes.Version || item.body.match(/<Version>\s*([^<]+)<\/Version>/i)?.[1];
      entries.push({ version: resolvePropertyVersion(value, properties, file), origin: 'Directory.Packages.props', file, packageName });
    }
  }
  return entries;
}

function collectDeclaredVersions(projectFiles, centralEntries) {
  const entries = [];
  const central = new Map(centralEntries.map((entry) => [entry.packageName.toLowerCase(), entry]));
  for (const file of projectFiles) {
    const text = readText(file);
    const properties = collectProperties(text);
    for (const item of xmlAttributes(text, 'PackageReference')) {
      const packageName = item.attributes.Include || item.attributes.Update;
      if (!MONICA_PACKAGE.test(packageName || '')) continue;
      const override = item.attributes.VersionOverride || item.body.match(/<VersionOverride>\s*([^<]+)<\/VersionOverride>/i)?.[1];
      const declared = item.attributes.Version || item.body.match(/<Version>\s*([^<]+)<\/Version>/i)?.[1];
      if (override) {
        entries.push({ version: resolvePropertyVersion(override, properties, file), origin: 'PackageReference.VersionOverride', file, packageName });
      } else if (declared) {
        entries.push({ version: resolvePropertyVersion(declared, properties, file), origin: 'PackageReference', file, packageName });
      } else {
        const managed = central.get(packageName.toLowerCase());
        if (!managed) throw new GuideError('package_version_unresolved', `Monica PackageReference ${packageName} in ${file} has no exact version and no matching central PackageVersion.`);
        entries.push({ ...managed, origin: 'Directory.Packages.props (effective)', consumerFile: file });
      }
    }
  }
  return entries;
}

export function detectFrameworkVersion(workspace, inventory = null, options = {}) {
  const projectFiles = inventory
    ? inventory.filter((file) => file.endsWith('.csproj') || file.endsWith('.fsproj'))
    : walkFiles(workspace, {
      maxDepth: 7,
      ignoredDirectories: BUNDLED_SKILL_DIRECTORIES,
      include: (_file, name) => name.endsWith('.csproj') || name.endsWith('.fsproj'),
    });
  const projectReferences = collectProjectReferences(workspace, projectFiles, options);
  const resolved = collectResolvedVersions(workspace, projectFiles, inventory);
  const central = collectCentralVersions(workspace, inventory);
  const declared = collectDeclaredVersions(projectFiles, central);
  const tiers = [
    ['project-reference', projectReferences],
    ['resolved-assets', resolved],
    ['central-package-management', projectFiles.length ? declared.filter((entry) => entry.origin === 'Directory.Packages.props (effective)') : central],
    ['project-declaration', declared.filter((entry) => entry.origin !== 'Directory.Packages.props (effective)')],
  ];
  const effectiveEntries = [
    ...projectReferences,
    ...resolved,
    ...(projectFiles.length ? declared : central),
  ];
  distinctVersions(effectiveEntries, 'effective repository');
  for (const [tier, entries] of tiers) {
    if (!entries.length) continue;
    return { version: distinctVersions(entries, tier), tier, entries };
  }
  const fallback = frameworkRootVersion(options.frameworkRoot);
  if (fallback) return fallback;
  return { version: null, tier: null, entries: [] };
}

export function assertProjectReferenceRelease(frameworkVersion, release) {
  if (!release?.commit) return;
  const entries = (frameworkVersion?.entries || []).filter((entry) => entry.origin === 'ProjectReference');
  const mismatches = entries.filter((entry) => entry.sourceCommit !== release.commit.toLowerCase());
  if (mismatches.length) {
    throw new GuideError(
      'project_reference_release_mismatch',
      `Monica ProjectReference source does not match immutable release commit ${release.commit}.`,
      { expectedCommit: release.commit, entries: mismatches },
    );
  }
}

export function detectRepository(workspace, inventory = null) {
  const git = gitInfo(workspace, { includeDirty: false });
  const identity = canonicalRepository(git?.remote);
  const characteristics = {
    monicaFramework: exists(path.join(workspace, 'Monica.slnx')) && exists(path.join(workspace, 'Monica.Core')),
    monicaDocs: exists(path.join(workspace, 'docs', 'en-US'))
      && exists(path.join(workspace, 'docs', 'zh-CN'))
      && exists(path.join(workspace, 'frontend', 'monica-docs-web')),
  };
  const relevantFiles = inventory ? inventory.filter((file) => {
    const name = path.basename(file);
    return name.endsWith('.csproj') || name.endsWith('.razor') || name === 'Directory.Build.props';
  }) : walkFiles(workspace, {
    maxDepth: 6,
    ignoredDirectories: BUNDLED_SKILL_DIRECTORIES,
    include: (_file, name) => name.endsWith('.csproj') || name.endsWith('.razor') || name === 'Directory.Build.props',
  });
  const snippets = relevantFiles.slice(0, 250).map((file) => readText(file, '').slice(0, 65536)).join('\n');
  characteristics.ui = relevantFiles.some((file) => file.endsWith('.razor')) || /Monica\.[\w.]*UI|MudBlazor/i.test(snippets);
  characteristics.extension = /monica-third-party|Module\w+Guide|IMoModule/i.test(snippets) || relevantFiles.some((file) => /Extension|Provider|Connector/i.test(path.basename(file)));
  characteristics.application = /PackageReference[^>]+Include=["']Monica\.|ProjectReference[^>]+Monica\./i.test(snippets);
  characteristics.projectReference = /ProjectReference[^>]+(?:Include|Update)=["'][^"']*Monica/i.test(snippets);

  let candidateProfile = null;
  let confidence = 'ambiguous';
  let reason = 'No canonical Monica repository identity or characteristic files were found.';
  if (/^Tairitsua\/Monica$/i.test(identity || '')) {
    candidateProfile = 'framework-contributor'; confidence = 'canonical'; reason = 'Canonical Monica framework repository detected.';
  } else if (characteristics.monicaFramework) {
    candidateProfile = 'framework-contributor'; confidence = 'characteristic'; reason = 'Monica framework characteristic paths detected.';
  } else if (/^Tairitsua\/Monica\.Docs$/i.test(identity || '')) {
    candidateProfile = 'docs-contributor'; confidence = 'canonical'; reason = 'Canonical Monica.Docs repository detected.';
  } else if (characteristics.monicaDocs) {
    candidateProfile = 'docs-contributor'; confidence = 'characteristic'; reason = 'Bilingual Monica.Docs characteristic paths detected.';
  } else if (characteristics.extension && characteristics.application) {
    candidateProfile = null; confidence = 'ambiguous'; reason = 'Both application and extension characteristics were detected; select a profile explicitly.';
  } else if (characteristics.extension) {
    candidateProfile = 'extension-author'; confidence = 'characteristic'; reason = 'Monica extension/module characteristics detected.';
  } else if (characteristics.application) {
    candidateProfile = 'application'; confidence = 'characteristic'; reason = 'Monica package or project references detected.';
  }
  const capabilities = characteristics.ui ? ['ui'] : [];
  return { git, identity, characteristics, candidateProfile, confidence, reason, capabilities };
}

export function profileRepositoryIssues(workspace, profile, repository) {
  const contracts = {
    'framework-contributor': {
      identity: /^Tairitsua\/Monica$/i,
      requiredCode: 'framework_repository_required',
      rootCode: 'framework_repository_root_required',
      writableCode: 'framework_repository_not_writable',
      name: 'Tairitsua/Monica',
    },
    'docs-contributor': {
      identity: /^Tairitsua\/Monica\.Docs$/i,
      requiredCode: 'docs_repository_required',
      rootCode: 'docs_repository_root_required',
      writableCode: 'docs_repository_not_writable',
      name: 'Tairitsua/Monica.Docs',
    },
  };
  const contract = contracts[profile];
  if (!contract) return [];
  const issues = [];
  if (!contract.identity.test(repository.identity || '')) {
    issues.push({ code: contract.requiredCode, message: `${profile} requires a canonical ${contract.name} checkout through origin or upstream.` });
  }
  const selectedWorkspace = path.resolve(workspace);
  const gitRoot = repository.git?.root ? path.resolve(repository.git.root) : null;
  const selectedRealPath = fs.realpathSync(selectedWorkspace);
  const gitRealPath = gitRoot && exists(gitRoot) ? fs.realpathSync(gitRoot) : null;
  const sameRoot = gitRealPath
    && path.relative(selectedRealPath, gitRealPath) === ''
    && path.relative(gitRealPath, selectedRealPath) === '';
  if (!sameRoot) {
    issues.push({
      code: contract.rootCode,
      message: `${profile} must select the canonical Git repository root${gitRoot ? `: ${gitRoot}` : '.'}`,
      details: { workspace: selectedWorkspace, gitRoot },
    });
  }
  try {
    fs.accessSync(workspace, fs.constants.W_OK);
  } catch {
    issues.push({ code: contract.writableCode, message: `The ${contract.name} checkout is not writable.` });
  }
  return issues;
}

export function nestedInstructionFiles(workspace) {
  const files = walkFiles(workspace, {
    maxDepth: Number.POSITIVE_INFINITY,
    ignoredDirectories: BUNDLED_SKILL_DIRECTORIES,
    include: (file, name) => (name === 'AGENTS.md' || name === 'CLAUDE.md') && path.dirname(file) !== workspace,
  });
  return files.filter((file) => {
    const name = path.basename(file);
    return (name === 'AGENTS.md' || name === 'CLAUDE.md') && path.dirname(file) !== workspace;
  });
}

export function workspaceDetection(workspace) {
  const inventory = walkFiles(workspace, {
    maxDepth: 8,
    ignoredDirectories: BUNDLED_SKILL_DIRECTORIES,
    include: (_file, name) => name.endsWith('.csproj')
      || name.endsWith('.fsproj')
      || name.endsWith('.razor')
      || name === 'Directory.Build.props'
      || name === 'Directory.Packages.props'
      || name === 'packages.lock.json'
      || name === 'AGENTS.md'
      || name === 'CLAUDE.md',
  });
  const repository = detectRepository(workspace, inventory);
  let frameworkVersion;
  let versionError = null;
  try {
    frameworkVersion = detectFrameworkVersion(workspace, inventory, {
      allowDirtySourceRoot: repository.candidateProfile === 'framework-contributor' ? repository.git?.root : null,
      frameworkRoot: /^Tairitsua\/Monica$/i.test(repository.identity || '') ? repository.git?.root : null,
    });
  } catch (error) {
    if (!(error instanceof GuideError)) throw error;
    frameworkVersion = { version: null, tier: null, entries: [] };
    versionError = { code: error.code, message: error.message, details: error.details };
  }
  return { repository, frameworkVersion, versionError, nestedInstructions: nestedInstructionFiles(workspace) };
}
