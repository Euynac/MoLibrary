import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {
  GuideError,
  canonicalRepository,
  exists,
  externalCommandTimeout,
  git,
  gitInfo,
  gitStatusSnapshot,
  normalizePath,
  run,
} from './guide-shared.mjs';

export const SOURCE_REPOSITORIES = Object.freeze({
  monica: 'Tairitsua/Monica',
  docs: 'Tairitsua/Monica.Docs',
});

export function normalizeSourceRepository(value) {
  const normalized = String(value || '').trim().toLowerCase();
  if (normalized === 'monica' || normalized === 'tairitsua/monica') return SOURCE_REPOSITORIES.monica;
  if (normalized === 'docs' || normalized === 'monica.docs' || normalized === 'tairitsua/monica.docs') return SOURCE_REPOSITORIES.docs;
  throw new GuideError('source_repository_invalid', '--repository must be monica or docs.');
}

function resolverCandidates(explicitPath) {
  const candidates = [explicitPath, process.env.MONICA_INSPECT_SOURCE_CLI];
  for (const root of [path.join(os.homedir(), '.agents', 'skills'), path.join(os.homedir(), '.claude', 'skills'), path.join(os.homedir(), '.codex', 'skills')]) {
    candidates.push(path.join(root, 'inspect-dependency-source', 'scripts', 'inspect_dependency_source.py'));
  }
  return candidates.filter(Boolean).map((value) => normalizePath(value));
}

export function findSourceResolver(explicitPath = null) {
  return resolverCandidates(explicitPath).find(exists) || null;
}

function pythonCommand(timeoutMs) {
  for (const candidate of [process.env.MONICA_GUIDE_PYTHON, 'python3', 'python'].filter(Boolean)) {
    const result = run(candidate, ['--version'], { timeout: timeoutMs });
    if (result.status === 0) return candidate;
    if (result.error?.code === 'ETIMEDOUT') {
      throw new GuideError(
        'source_resolver_timeout',
        `${candidate} timed out while checking the inspect-dependency-source runtime. Verify the local Python/runtime cache and retry; offline mode never fetches a substitute.`,
        { command: candidate, timeoutMs },
      );
    }
  }
  throw new GuideError('python_unavailable', 'Python is unavailable; cannot call inspect-dependency-source resolve --json.');
}

function resolveRefCommit(sourceRoot, exactRef) {
  if (!exactRef) return null;
  const value = String(exactRef);
  let revision = value;
  if (!/^[0-9a-f]{40}$/i.test(value)) {
    const tag = value.replace(/^refs\/tags\//, '');
    if (!tag || value.startsWith('refs/heads/') || !git(sourceRoot, ['show-ref', '--verify', `refs/tags/${tag}`])) {
      throw new GuideError('source_ref_not_immutable', `Source ref ${exactRef} is not a 40-character commit or an existing Git tag.`);
    }
    revision = `refs/tags/${tag}`;
  }
  const commit = git(sourceRoot, ['rev-parse', '--verify', `${revision}^{commit}`]);
  if (!commit || !/^[0-9a-f]{40}$/i.test(commit)) throw new GuideError('source_ref_unresolved', `Source ref ${exactRef} does not resolve to an exact commit.`);
  return commit.toLowerCase();
}

export function verifyLocalSource(sourcePath, {
  expectedCommit = null,
  exactRef = null,
  repository = SOURCE_REPOSITORIES.monica,
  requireCanonical = true,
} = {}) {
  const normalized = normalizePath(sourcePath);
  if (!exists(normalized) || !fs.statSync(normalized).isDirectory()) throw new GuideError('source_unavailable', `Source directory does not exist: ${normalized}.`);
  const sourceGit = gitInfo(normalized);
  if (!sourceGit?.commit || !/^[0-9a-f]{40}$/i.test(sourceGit.commit)) throw new GuideError('source_commit_unresolved', `Cannot identify an exact Git commit for ${normalized}.`);
  const identity = canonicalRepository(sourceGit.remote);
  const expectedRepository = normalizeSourceRepository(repository);
  if (requireCanonical && identity?.toLowerCase() !== expectedRepository.toLowerCase()) {
    throw new GuideError('source_identity_mismatch', `${normalized} is not a canonical ${expectedRepository} checkout.`, { identity, expectedRepository });
  }
  const resolvedRefCommit = resolveRefCommit(sourceGit.root, exactRef);
  const requiredCommit = resolvedRefCommit || expectedCommit;
  if (requiredCommit && sourceGit.commit.toLowerCase() !== requiredCommit.toLowerCase()) {
    throw new GuideError('source_commit_mismatch', `Source commit ${sourceGit.commit} does not match required commit ${requiredCommit}.`);
  }
  return {
    repository: expectedRepository,
    ref: exactRef || expectedCommit || sourceGit.commit.toLowerCase(),
    commit: sourceGit.commit.toLowerCase(),
    sourcePath: sourceGit.root,
    resolutionKind: exactRef && !/^[0-9a-f]{40}$/i.test(exactRef) ? 'exact_tag' : 'exact_commit',
    provenance: 'local-git',
  };
}

export function resolveCachedSource({ exactRef, repository = SOURCE_REPOSITORIES.monica, resolverPath = null, timeoutMs = undefined } = {}) {
  if (!exactRef) throw new GuideError('source_ref_required', 'An exact source ref is required for cached resolution.');
  const expectedRepository = normalizeSourceRepository(repository);
  const cli = findSourceResolver(resolverPath);
  if (!cli) throw new GuideError('source_resolver_unavailable', 'inspect-dependency-source is not installed or MONICA_INSPECT_SOURCE_CLI is unset.');
  const timeout = externalCommandTimeout(timeoutMs);
  const python = pythonCommand(timeout);
  const result = run(python, [cli, 'resolve', expectedRepository, '--ref', exactRef, '--json'], { timeout });
  if (result.error?.code === 'ETIMEDOUT') {
    throw new GuideError(
      'source_resolver_timeout',
      `inspect-dependency-source timed out after ${timeout} ms while resolving cached ${expectedRepository} source. Verify the local resolver cache and retry; offline mode never fetches or substitutes another ref.`,
      { repository: expectedRepository, exactRef, timeoutMs: timeout },
    );
  }
  if (result.status !== 0) {
    let envelope = null;
    try { envelope = JSON.parse(result.stdout); } catch { /* Use sanitized text below. */ }
    throw new GuideError(envelope?.error?.code || 'source_resolution_failed', envelope?.error?.message || `Exact cached ${expectedRepository} source is unavailable.`);
  }
  let payload;
  try { payload = JSON.parse(result.stdout); } catch (error) {
    throw new GuideError('source_contract_invalid', `inspect-dependency-source returned invalid JSON: ${error.message}`);
  }
  const allowedKinds = new Set(['exact_commit', 'exact_tag']);
  if (payload.status !== 'ok' || payload.verification_state !== 'verified' || !allowedKinds.has(payload.resolution_kind)) {
    throw new GuideError('source_provenance_unverified', 'Cached Monica source does not have verified exact provenance.', {
      status: payload.status,
      verificationState: payload.verification_state,
      resolutionKind: payload.resolution_kind,
    });
  }
  const commit = payload.artifact?.actual_commit;
  if (!commit || !/^[0-9a-f]{40}$/i.test(commit)) throw new GuideError('source_commit_unresolved', 'Cached source result has no exact observed commit.');
  if (/^[0-9a-f]{40}$/i.test(exactRef) && commit.toLowerCase() !== exactRef.toLowerCase()) {
    throw new GuideError('source_commit_mismatch', `Cached source resolved ${commit}, not requested commit ${exactRef}.`);
  }
  if (payload.artifact?.expected_commit && payload.artifact.expected_commit.toLowerCase() !== commit.toLowerCase()) {
    throw new GuideError('source_commit_mismatch', `Cached source observed ${commit}, not resolver-expected commit ${payload.artifact.expected_commit}.`);
  }
  if (payload.artifact?.ref && String(payload.artifact.ref).toLowerCase() !== String(exactRef).toLowerCase()) {
    throw new GuideError('source_ref_mismatch', `Cached source resolver returned ref ${payload.artifact.ref}, not requested ref ${exactRef}.`);
  }
  const resolvedRepository = payload.repository?.canonical_name;
  if (resolvedRepository?.toLowerCase() !== expectedRepository.toLowerCase()) throw new GuideError('source_identity_mismatch', `Resolved source is ${resolvedRepository || 'unknown'}, not ${expectedRepository}.`);
  const rawSourcePath = payload.source_path;
  if (typeof rawSourcePath !== 'string' || !rawSourcePath.trim()
    || (!path.isAbsolute(rawSourcePath) && !path.win32.isAbsolute(rawSourcePath))) {
    throw new GuideError('source_contract_invalid', 'Cached source result must contain a non-empty absolute source_path.');
  }
  const resolvedPath = normalizePath(rawSourcePath);
  if (!path.isAbsolute(resolvedPath) || !exists(resolvedPath) || !fs.statSync(resolvedPath).isDirectory()) {
    throw new GuideError('source_unavailable', 'Cached source path is not an available absolute directory.');
  }
  const localGit = gitInfo(resolvedPath);
  if (localGit?.commit && localGit.commit.toLowerCase() !== commit.toLowerCase()) {
    throw new GuideError('source_commit_mismatch', `Cached source path is at ${localGit.commit}, not verified commit ${commit}.`);
  }
  return {
    repository: expectedRepository,
    ref: payload.artifact?.ref || exactRef,
    commit: commit.toLowerCase(),
    provenance: {
      resolver: 'inspect-dependency-source',
      repositoryId: payload.repository?.id || null,
      artifactId: payload.artifact?.id || null,
      resolutionKind: payload.resolution_kind,
      expectedCommit: payload.artifact?.expected_commit || null,
    },
    sourcePath: resolvedPath,
    resolutionKind: payload.resolution_kind,
  };
}

export function observeSourceBinding(binding, { expectedCommit = null } = {}) {
  const observation = {
    repository: binding.repository,
    sourcePath: binding.sourcePath,
    storedRef: binding.ref,
    storedCommit: binding.commit,
    observedCommit: null,
    pathHealth: 'missing',
    dirty: null,
    identity: null,
    compatibility: expectedCommit ? 'unknown' : 'not-evaluated',
    expectedCommit,
    warnings: [],
  };
  if (!exists(binding.sourcePath)) {
    observation.warnings.push({ code: 'source_path_unavailable', message: `Stored source path is unavailable: ${binding.sourcePath}.` });
    return observation;
  }
  let sourceGit;
  try {
    sourceGit = gitInfo(binding.sourcePath);
  } catch (error) {
    observation.pathHealth = 'invalid';
    observation.warnings.push({ code: 'source_observation_failed', message: error.message });
    return observation;
  }
  observation.observedCommit = sourceGit?.commit?.toLowerCase() || null;
  observation.dirty = sourceGit?.dirty ?? null;
  observation.identity = canonicalRepository(sourceGit?.remote);
  if (!sourceGit?.commit && binding.provenance?.resolver === 'inspect-dependency-source') {
    observation.pathHealth = 'path-only';
  } else if (!sourceGit?.commit || observation.identity?.toLowerCase() !== binding.repository.toLowerCase()) {
    observation.pathHealth = 'invalid';
    observation.warnings.push({ code: 'source_identity_mismatch', message: `Stored path is not a canonical ${binding.repository} checkout.` });
  } else if (observation.observedCommit !== binding.commit.toLowerCase()) {
    observation.pathHealth = 'moved';
    observation.warnings.push({ code: 'source_checkout_moved', message: `Checkout moved from ${binding.commit} to ${observation.observedCommit}.` });
  } else {
    observation.pathHealth = 'available';
  }
  if (observation.dirty) observation.warnings.push({ code: 'source_checkout_dirty', message: 'Checkout contains local changes; the binding remains a lookup locator only.' });
  if (expectedCommit) {
    observation.compatibility = binding.commit.toLowerCase() === expectedCommit.toLowerCase() ? 'compatible' : 'mismatch';
    if (observation.compatibility === 'mismatch') {
      observation.warnings.push({ code: 'source_version_mismatch', message: `Bound commit ${binding.commit} does not match expected commit ${expectedCommit}.` });
    }
  }
  return observation;
}

export function verifySourceBinding(binding, { expectedCommit = null, resolverPath = null, timeoutMs = undefined } = {}) {
  let observation = observeSourceBinding(binding, { expectedCommit });
  if (binding.provenance?.resolver === 'inspect-dependency-source') {
    const refreshed = resolveCachedSource({ exactRef: binding.ref, repository: binding.repository, resolverPath, timeoutMs });
    if (path.resolve(refreshed.sourcePath) !== path.resolve(binding.sourcePath) || refreshed.commit !== binding.commit) {
      throw new GuideError('source_binding_drift', `Verified cached ${binding.repository} source no longer matches the stored global binding.`, {
        storedPath: binding.sourcePath,
        resolvedPath: refreshed.sourcePath,
        storedCommit: binding.commit,
        resolvedCommit: refreshed.commit,
      });
    }
    observation = observeSourceBinding(binding, { expectedCommit });
    observation.observedCommit = refreshed.commit;
    observation.identity = refreshed.repository;
    observation.pathHealth = 'available';
  }
  if (observation.pathHealth !== 'available') {
    throw new GuideError('source_binding_unavailable', observation.warnings[0]?.message || `Stored ${binding.repository} source binding is unavailable.`, observation);
  }
  return observation;
}

export function assertSourceContractClean(sourcePath) {
  const status = gitStatusSnapshot(sourcePath, {
    pathspec: ['.monica/agent-skill-catalog.json', 'skills'],
    sampleLimit: 100,
  });
  if (status.dirty) {
    throw new GuideError(
      'dirty_source_contract',
      'Source-channel catalog or skills contain local changes and cannot represent the selected commit.',
      { digest: status.digest, bytes: status.bytes, changes: status.changes, truncated: status.truncated },
    );
  }
}
