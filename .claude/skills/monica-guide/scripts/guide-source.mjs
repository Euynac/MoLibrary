import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { GuideError, canonicalRepository, exists, git, gitInfo, normalizePath, run } from './guide-shared.mjs';

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

function pythonCommand() {
  for (const candidate of [process.env.MONICA_GUIDE_PYTHON, 'python3', 'python'].filter(Boolean)) {
    const result = run(candidate, ['--version']);
    if (result.status === 0) return candidate;
  }
  throw new GuideError('python_unavailable', 'Python is unavailable; cannot call inspect-dependency-source resolve --json.');
}

export function verifyLocalSource(sourcePath, { access = 'read-only', expectedCommit = null, requireCanonical = true } = {}) {
  const normalized = normalizePath(sourcePath);
  if (!exists(normalized) || !fs.statSync(normalized).isDirectory()) throw new GuideError('source_unavailable', `Source directory does not exist: ${normalized}.`);
  const git = gitInfo(normalized);
  if (!git?.commit || !/^[0-9a-f]{40}$/i.test(git.commit)) throw new GuideError('source_commit_unresolved', `Cannot identify an exact Git commit for ${normalized}.`);
  const identity = canonicalRepository(git.remote);
  if (requireCanonical && !/^Tairitsua\/Monica$/i.test(identity || '')) throw new GuideError('source_identity_mismatch', `${normalized} is not a Tairitsua/Monica checkout.`, { identity });
  if (expectedCommit && git.commit.toLowerCase() !== expectedCommit.toLowerCase()) {
    throw new GuideError('source_commit_mismatch', `Source commit ${git.commit} does not match required commit ${expectedCommit}.`);
  }
  if (access === 'read-only' && git.dirty) throw new GuideError('dirty_readonly_source', `Read-only exact source ${normalized} contains uncommitted changes.`);
  return {
    ref: expectedCommit || git.commit,
    commit: git.commit.toLowerCase(),
    provenance: 'local-git',
    verificationState: 'verified',
    resolutionKind: 'exact_commit',
    access,
    sourcePath: git.root,
    repository: identity || 'Tairitsua/Monica',
    managed: false,
    dirty: git.dirty,
  };
}

export function resolveCachedSource({ exactRef, access = 'read-only', resolverPath = null } = {}) {
  if (!exactRef) throw new GuideError('source_ref_required', 'An exact source ref is required for cached resolution.');
  const cli = findSourceResolver(resolverPath);
  if (!cli) throw new GuideError('source_resolver_unavailable', 'inspect-dependency-source is not installed or MONICA_INSPECT_SOURCE_CLI is unset.');
  const python = pythonCommand();
  const result = run(python, [cli, 'resolve', 'Tairitsua/Monica', '--ref', exactRef, '--json']);
  if (result.status !== 0) {
    let envelope = null;
    try { envelope = JSON.parse(result.stdout); } catch { /* Use sanitized text below. */ }
    throw new GuideError(envelope?.error?.code || 'source_resolution_failed', envelope?.error?.message || 'Exact cached Monica source is unavailable.');
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
  const repository = payload.repository?.canonical_name;
  if (!/^Tairitsua\/Monica$/i.test(repository || '')) throw new GuideError('source_identity_mismatch', `Resolved source is ${repository || 'unknown'}, not Tairitsua/Monica.`);
  const resolvedPath = normalizePath(payload.source_path);
  if (!path.isAbsolute(resolvedPath) || !exists(resolvedPath) || !fs.statSync(resolvedPath).isDirectory()) {
    throw new GuideError('source_unavailable', 'Cached source path is not an available absolute directory.');
  }
  const localGit = gitInfo(resolvedPath);
  if (localGit?.commit && localGit.commit.toLowerCase() !== commit.toLowerCase()) {
    throw new GuideError('source_commit_mismatch', `Cached source path is at ${localGit.commit}, not verified commit ${commit}.`);
  }
  if (localGit?.dirty) throw new GuideError('dirty_readonly_source', `Cached read-only source ${resolvedPath} contains local changes.`);
  return {
    ref: payload.artifact?.ref || exactRef,
    commit: commit.toLowerCase(),
    provenance: {
      resolver: 'inspect-dependency-source',
      repositoryId: payload.repository?.id || null,
      artifactId: payload.artifact?.id || null,
      resolutionKind: payload.resolution_kind,
      expectedCommit: payload.artifact?.expected_commit || null,
    },
    access,
    sourcePath: resolvedPath,
    repository,
    managed: payload.artifact?.kind !== 'local',
    dirty: false,
    verificationState: 'verified',
    resolutionKind: payload.resolution_kind,
  };
}

export function sourceBindingForProfile({ workspace, profile, profileClosure, release, sourcePath, sourceAccess, resolverPath }) {
  const policyAccess = profile === 'framework-contributor' ? 'read-write' : 'read-only';
  if (sourceAccess && sourceAccess !== policyAccess) {
    throw new GuideError('source_access_forbidden', `Profile ${profile} requires ${policyAccess} Monica source; --source-access ${sourceAccess} cannot override that policy.`);
  }
  const access = policyAccess;
  const expectedCommit = release?.commit || null;
  if (profile === 'framework-contributor' && !sourcePath) {
    return verifyLocalSource(workspace, { access: 'read-write', expectedCommit: expectedCommit || null });
  }
  if (sourcePath) return verifyLocalSource(sourcePath, { access, expectedCommit: expectedCommit || null });
  if (!profileClosure.source.required) return null;
  return resolveCachedSource({ exactRef: expectedCommit || release?.tag || release?.monicaVersion, access, resolverPath });
}

export function assertSourceContractClean(sourcePath) {
  const status = git(sourcePath, ['status', '--porcelain=v1', '--', '.monica/agent-skill-catalog.json', 'skills']);
  if (status && status.trim()) throw new GuideError('dirty_source_contract', 'Source-channel catalog or skills contain local changes and cannot represent the selected commit.', { changes: status.split(/\r?\n/).filter(Boolean) });
}
