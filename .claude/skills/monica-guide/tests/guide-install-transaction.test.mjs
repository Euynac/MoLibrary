import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import test from 'node:test';

import {
  retainedGlobalSkillTransactions,
  withGlobalSkillCompensation,
} from '../scripts/guide-install-transaction.mjs';
import { GuideError } from '../scripts/guide-shared.mjs';

const DISPLAY_NAME = new Map([
  ['codex', 'Codex'],
  ['claude-code', 'Claude Code'],
]);
const NULL_PROVENANCE = { source: null, sourceUrl: null, sourceType: null };
const MONICA_PROVENANCE = {
  source: 'Tairitsua/Monica',
  sourceUrl: 'https://github.com/Tairitsua/Monica.git',
  sourceType: 'github',
};

function temporaryDirectory(t) {
  const base = process.platform === 'win32' ? os.tmpdir() : '/tmp';
  const directory = fs.mkdtempSync(path.join(base, 'monica-guide-transaction-'));
  t.after(() => fs.rmSync(directory, { recursive: true, force: true }));
  return directory;
}

function writeSkill(root, name, content, mode = 0o640) {
  const directory = path.join(root, name);
  fs.rmSync(directory, { recursive: true, force: true });
  fs.mkdirSync(path.join(directory, 'scripts'), { recursive: true });
  fs.writeFileSync(path.join(directory, 'SKILL.md'), content, { encoding: 'utf8', mode });
  fs.writeFileSync(path.join(directory, 'scripts', 'run.mjs'), `// ${content}`, { encoding: 'utf8', mode });
  if (process.platform !== 'win32') {
    fs.chmodSync(path.join(directory, 'SKILL.md'), mode);
    fs.chmodSync(path.join(directory, 'scripts', 'run.mjs'), mode);
  }
  return directory;
}

function agentArguments(args) {
  const values = [];
  for (let index = 0; index < args.length - 1; index += 1) {
    if (args[index] === '-a') values.push(args[index + 1]);
  }
  return values;
}

function fakeSkillsCli(installedRoot, initial, {
  removeNoop = false,
  restoreFails = false,
  failDiscoveryAt = null,
  remoteContent = {},
} = {}) {
  const model = new Map();
  const calls = [];
  let discoveryCalls = 0;
  for (const [name, entry] of Object.entries(initial)) {
    const skillPath = writeSkill(installedRoot, name, entry.content, entry.mode);
    model.set(name, {
      path: skillPath,
      agents: [...entry.agents],
      provenance: entry.provenance || { ...NULL_PROVENANCE },
      ref: entry.ref || null,
    });
  }
  const runner = (_executable, args) => {
    calls.push([...args]);
    if (args.includes('ls')) {
      discoveryCalls += 1;
      if (discoveryCalls === failDiscoveryAt) return { status: 1, stdout: '', stderr: 'injected discovery failure' };
      return {
        status: 0,
        stdout: JSON.stringify([...model.entries()].map(([name, entry]) => ({
          name,
          path: entry.path,
          scope: 'global',
          agents: entry.agents,
          ...entry.provenance,
        }))),
        stderr: '',
      };
    }
    const removeIndex = args.indexOf('remove');
    if (removeIndex >= 0) {
      const skill = args[removeIndex + 1];
      if (!removeNoop) {
        const entry = model.get(skill);
        if (entry) {
          const removed = new Set(agentArguments(args).map((agent) => DISPLAY_NAME.get(agent)));
          entry.agents = entry.agents.filter((agent) => !removed.has(agent));
          entry.provenance = { ...NULL_PROVENANCE };
          if (!entry.agents.length) {
            fs.rmSync(entry.path, { recursive: true, force: true });
            model.delete(skill);
          }
        }
      }
      return { status: 0, stdout: '', stderr: '' };
    }
    const addIndex = args.indexOf('add');
    if (addIndex >= 0) {
      if (restoreFails) return { status: 1, stdout: '', stderr: 'injected restore failure' };
      const source = args[addIndex + 1];
      const skill = args[args.indexOf('-s') + 1];
      const previous = model.get(skill);
      const destination = path.join(installedRoot, skill);
      fs.rmSync(destination, { recursive: true, force: true });
      if (path.isAbsolute(source) || path.win32.isAbsolute(source)) {
        fs.cpSync(source, destination, { recursive: true });
      } else {
        writeSkill(installedRoot, skill, remoteContent[source] || `remote ${source}\n`);
      }
      const addedAgents = agentArguments(args).map((agent) => DISPLAY_NAME.get(agent));
      const remoteRef = source.match(/\/tree\/([^/]+)\/skills\//)?.[1] || null;
      model.set(skill, {
        path: destination,
        agents: [...new Set([...(previous?.agents || []), ...addedAgents])].sort(),
        provenance: isRemote(source) ? { ...MONICA_PROVENANCE } : (previous?.provenance || { ...NULL_PROVENANCE }),
        ref: isRemote(source) ? remoteRef : previous?.ref || null,
      });
      return { status: 0, stdout: '', stderr: '' };
    }
    return { status: 1, stdout: '', stderr: `unexpected arguments: ${args.join(' ')}` };
  };
  return { model, calls, runner };
}

function isRemote(source) {
  return /^https:\/\//.test(source);
}

function transactionOptions(root, cli, skills, mutate, {
  offline = true,
  installSources = null,
  restoreSources = null,
  fileActions = [],
  cleanup,
} = {}) {
  const localSources = Object.fromEntries(skills.map((skill) => [skill, path.join(root, 'target', skill)]));
  return {
    statePath: path.join(root, 'state', 'state.json'),
    skills,
    agents: ['codex', 'claude-code'],
    cliSpec: 'skills@1.5.21',
    installSources: installSources || localSources,
    restoreSources: restoreSources || localSources,
    fileActions,
    offline,
    executable: 'fake-npx',
    runner: cli.runner,
    ...(cleanup ? { cleanup } : {}),
    mutate,
  };
}

test('failed mutation restores attempted skills and files while preserving unrelated state', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const cli = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'] },
    'user-skill': { content: 'unrelated\n', agents: ['Codex'] },
  });

  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide', 'monica-framework'], ({ runSkill }) => {
      runSkill('monica-guide', () => {
        writeSkill(installed, 'monica-guide', 'corrupt guide\n');
        cli.model.get('monica-guide').path = path.join(installed, 'monica-guide');
      });
      runSkill('monica-framework', () => {
        writeSkill(installed, 'monica-framework', 'new framework\n');
        cli.model.set('monica-framework', {
          path: path.join(installed, 'monica-framework'),
          agents: ['Codex', 'Claude Code'],
          provenance: { ...NULL_PROVENANCE },
          ref: null,
        });
      });
      throw new GuideError('skill_content_mismatch', 'injected verification failure');
    })),
    (error) => error.code === 'skill_content_mismatch' && error.details?.transaction?.status === 'compensated',
  );

  assert.equal(fs.readFileSync(path.join(installed, 'monica-guide', 'SKILL.md'), 'utf8'), 'old guide\n');
  assert.equal(cli.model.has('monica-framework'), false);
  assert.equal(fs.readFileSync(path.join(installed, 'user-skill', 'SKILL.md'), 'utf8'), 'unrelated\n');
  assert.equal(fs.existsSync(path.join(root, 'state', 'skill-install-transactions')), false);
  assert.ok(cli.calls.every((args) => args[0] === '--offline' && args.includes('skills@1.5.21')));
});

test('exit-zero remove no-op retains private recovery evidence and reports incomplete compensation', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const cli = fakeSkillsCli(installed, {}, { removeNoop: true });
  let failure;
  try {
    withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide'], ({ runSkill }) => {
      runSkill('monica-guide', () => {
        writeSkill(installed, 'monica-guide', 'new guide\n');
        cli.model.set('monica-guide', {
          path: path.join(installed, 'monica-guide'),
          agents: ['Codex', 'Claude Code'],
          provenance: { ...NULL_PROVENANCE },
          ref: null,
        });
      });
      throw new GuideError('skill_content_mismatch', 'injected verification failure');
    }));
  } catch (error) {
    failure = error;
  }
  assert.equal(failure?.code, 'global_skill_rollback_incomplete');
  assert.equal(failure?.details?.primary?.code, 'skill_content_mismatch');
  const snapshotPath = failure?.details?.transaction?.snapshotPath;
  assert.ok(snapshotPath && fs.existsSync(path.join(snapshotPath, 'transaction.json')));
  assert.equal(retainedGlobalSkillTransactions(path.join(root, 'state', 'state.json'))[0]?.status, 'rollback-incomplete');
  if (process.platform !== 'win32') {
    assert.equal(fs.statSync(snapshotPath).mode & 0o777, 0o700);
    assert.equal(fs.statSync(path.join(snapshotPath, 'transaction.json')).mode & 0o777, 0o600);
  }
});

test('restore failure and rollback discovery failure preserve the primary error envelope', (t) => {
  for (const mode of ['restore', 'discovery']) {
    const root = temporaryDirectory(t);
    const installed = path.join(root, 'installed');
    fs.mkdirSync(installed);
    const cli = fakeSkillsCli(installed, {
      'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'] },
    }, mode === 'restore' ? { restoreFails: true } : { failDiscoveryAt: 3 });
    assert.throws(
      () => withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide'], ({ runSkill }) => {
        runSkill('monica-guide', () => writeSkill(installed, 'monica-guide', 'corrupt guide\n'));
        throw new GuideError('skill_content_mismatch', 'primary failure');
      })),
      (error) => error.code === 'global_skill_rollback_incomplete'
        && error.details?.primary?.code === 'skill_content_mismatch'
        && error.details?.transaction?.failures.some((entry) => entry.phase === (mode === 'restore' ? 'restore' : 'verify-discovery')),
    );
  }
});

test('remote Monica provenance is restored from the original immutable ref, not the temporary snapshot', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const oldSource = 'https://github.com/Tairitsua/Monica/tree/v1.2.2/skills/monica-guide';
  const targetSource = 'https://github.com/Tairitsua/Monica/tree/v1.2.3/skills/monica-guide';
  const cli = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'], provenance: MONICA_PROVENANCE, ref: 'v1.2.2' },
  }, { remoteContent: { [oldSource]: 'old guide\n' } });

  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide'], ({ runSkill }) => {
      runSkill('monica-guide', () => {
        writeSkill(installed, 'monica-guide', 'new guide\n');
        Object.assign(cli.model.get('monica-guide'), { provenance: { ...MONICA_PROVENANCE }, ref: 'v1.2.3' });
      });
      throw new GuideError('skill_install_failed', 'primary failure');
    }, {
      offline: false,
      installSources: { 'monica-guide': targetSource },
      restoreSources: { 'monica-guide': oldSource },
    })),
    (error) => error.code === 'skill_install_failed' && error.details?.transaction?.status === 'compensated',
  );
  assert.equal(cli.model.get('monica-guide').ref, 'v1.2.2');
  assert.deepEqual(cli.model.get('monica-guide').provenance, MONICA_PROVENANCE);
  assert.ok(cli.calls.some((args) => args.includes('add') && args.includes(oldSource)));
  assert.equal(cli.calls.some((args) => args.includes('add') && args.some((value) => value.includes('skill-install-transactions'))), false);
});

test('online local-source mutation can restore remote provenance after adding an agent binding', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const oldSource = 'https://github.com/Tairitsua/Monica/tree/v1.2.2/skills/monica-guide';
  const cli = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex'], provenance: MONICA_PROVENANCE, ref: 'v1.2.2' },
  }, { remoteContent: { [oldSource]: 'old guide\n' } });
  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide'], ({ runSkill }) => {
      runSkill('monica-guide', () => {
        writeSkill(installed, 'monica-guide', 'local target guide\n');
        Object.assign(cli.model.get('monica-guide'), {
          agents: ['Codex', 'Claude Code'],
          path: path.join(installed, 'monica-guide'),
        });
      });
      throw new GuideError('skill_install_failed', 'primary failure');
    }, {
      offline: false,
      restoreSources: { 'monica-guide': oldSource },
    })),
    (error) => error.code === 'skill_install_failed' && error.details?.transaction?.status === 'compensated',
  );
  assert.deepEqual(cli.model.get('monica-guide').agents, ['Codex']);
  assert.deepEqual(cli.model.get('monica-guide').provenance, MONICA_PROVENANCE);
  assert.equal(cli.model.get('monica-guide').ref, 'v1.2.2');
});

test('only attempted skills are compensated after an early install failure', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const cli = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'] },
    'monica-framework': { content: 'old framework\n', agents: ['Codex', 'Claude Code'] },
  });
  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide', 'monica-framework'], ({ runSkill }) => {
      runSkill('monica-guide', () => { throw new GuideError('skill_install_failed', 'first add failed'); });
      runSkill('monica-framework', () => { throw new Error('must not run'); });
    })),
    (error) => error.code === 'skill_install_failed' && error.details?.transaction?.status === 'compensated',
  );
  const rollbackSkills = cli.calls
    .filter((args) => args.includes('add') || args.includes('remove'))
    .map((args) => args[args.indexOf('add') >= 0 ? args.indexOf('-s') + 1 : args.indexOf('remove') + 1]);
  assert.deepEqual([...new Set(rollbackSkills)], ['monica-guide']);
  assert.equal(fs.readFileSync(path.join(installed, 'monica-framework', 'SKILL.md'), 'utf8'), 'old framework\n');
});

test('post-install file failure compensates both global skills and prior file writes', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const cli = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'] },
  });
  const first = path.join(root, 'workspace', 'AGENTS.md');
  const second = path.join(root, 'state', 'state.json');
  fs.mkdirSync(path.dirname(first), { recursive: true });
  fs.writeFileSync(first, 'before\n', 'utf8');
  const fileActions = [{ path: first }, { path: second }];
  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide'], ({ runSkill, runFile }) => {
      runSkill('monica-guide', () => writeSkill(installed, 'monica-guide', 'new guide\n'));
      runFile(fileActions[0], () => fs.writeFileSync(first, 'after\n', 'utf8'));
      runFile(fileActions[1], () => { throw new GuideError('write_failed', 'state write failed'); });
    }, { fileActions })),
    (error) => error.code === 'write_failed' && error.details?.transaction?.status === 'compensated',
  );
  assert.equal(fs.readFileSync(first, 'utf8'), 'before\n');
  assert.equal(fs.existsSync(second), false);
  assert.equal(fs.readFileSync(path.join(installed, 'monica-guide', 'SKILL.md'), 'utf8'), 'old guide\n');
});

test('file-only release switches remain inside the compensating transaction', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const cli = fakeSkillsCli(installed, {});
  const statePath = path.join(root, 'state', 'state.json');
  fs.mkdirSync(path.dirname(statePath), { recursive: true });
  fs.writeFileSync(statePath, 'before\n', 'utf8');
  const fileActions = [{ path: statePath }];
  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, cli, [], ({ runFile }) => {
      runFile(fileActions[0], () => {
        fs.writeFileSync(statePath, 'after\n', 'utf8');
        throw new GuideError('write_failed', 'injected file-only failure');
      });
    }, { fileActions })),
    (error) => error.code === 'write_failed' && error.details?.transaction?.status === 'compensated',
  );
  assert.equal(fs.readFileSync(statePath, 'utf8'), 'before\n');
});

test('cleanup failure never triggers rollback after a committed or compensated mutation', (t) => {
  const root = temporaryDirectory(t);
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const cli = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'] },
  });
  const cleanup = () => ({ code: 'EPERM', message: 'injected cleanup lock' });
  const committed = withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide'], ({ runSkill }) => {
    runSkill('monica-guide', () => writeSkill(installed, 'monica-guide', 'committed guide\n'));
    return 'applied';
  }, { cleanup }));
  assert.equal(committed.result, 'applied');
  assert.equal(committed.transaction.status, 'committed-cleanup-pending');
  assert.equal(fs.readFileSync(path.join(installed, 'monica-guide', 'SKILL.md'), 'utf8'), 'committed guide\n');
  assert.equal(retainedGlobalSkillTransactions(path.join(root, 'state', 'state.json'))[0]?.status, 'committed');

  const compensatedRoot = temporaryDirectory(t);
  const compensatedInstalled = path.join(compensatedRoot, 'installed');
  fs.mkdirSync(compensatedInstalled);
  const compensatedCli = fakeSkillsCli(compensatedInstalled, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'] },
  });
  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(compensatedRoot, compensatedCli, ['monica-guide'], ({ runSkill }) => {
      runSkill('monica-guide', () => writeSkill(compensatedInstalled, 'monica-guide', 'failed guide\n'));
      throw new GuideError('skill_install_failed', 'primary failure');
    }, { cleanup })),
    (error) => error.code === 'skill_install_failed'
      && error.details?.transaction?.status === 'compensated-cleanup-pending'
      && fs.existsSync(error.details.transaction.snapshotPath),
  );
  assert.equal(fs.readFileSync(path.join(compensatedInstalled, 'monica-guide', 'SKILL.md'), 'utf8'), 'old guide\n');
  assert.equal(retainedGlobalSkillTransactions(path.join(compensatedRoot, 'state', 'state.json'))[0]?.status, 'compensated');
});

test('retained crash evidence and unsupported membership block before mutation', (t) => {
  const root = temporaryDirectory(t);
  const statePath = path.join(root, 'state', 'state.json');
  const retained = path.join(root, 'state', 'skill-install-transactions', 'transaction-crash');
  fs.mkdirSync(retained, { recursive: true });
  fs.writeFileSync(path.join(retained, 'transaction.json'), JSON.stringify({ status: 'mutating', attemptedSkills: ['monica-guide'] }));
  let called = false;
  const neverCli = { runner: () => { called = true; return { status: 0, stdout: '[]', stderr: '' }; } };
  assert.throws(
    () => withGlobalSkillCompensation({ ...transactionOptions(root, neverCli, ['monica-guide'], () => {}), statePath }),
    (error) => error.code === 'global_skill_recovery_required',
  );
  assert.equal(called, false);

  fs.rmSync(path.join(root, 'state', 'skill-install-transactions'), { recursive: true, force: true });
  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const invalidMembership = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['???'] },
  });
  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, invalidMembership, ['monica-guide'], () => {})),
    (error) => error.code === 'global_skill_membership_not_restorable',
  );
  assert.equal(invalidMembership.calls.some((args) => args.includes('add') || args.includes('remove')), false);
});

test('malformed preflight blocks before mutation and successful mutation cleans its snapshot', (t) => {
  const root = temporaryDirectory(t);
  let mutated = false;
  const malformed = { runner: () => ({ status: 0, stdout: '{}', stderr: '' }) };
  assert.throws(
    () => withGlobalSkillCompensation(transactionOptions(root, malformed, ['monica-guide'], () => { mutated = true; })),
    (error) => error.code === 'global_skill_transaction_discovery_invalid',
  );
  assert.equal(mutated, false);

  const installed = path.join(root, 'installed');
  fs.mkdirSync(installed);
  const cli = fakeSkillsCli(installed, {
    'monica-guide': { content: 'old guide\n', agents: ['Codex', 'Claude Code'] },
  });
  const outcome = withGlobalSkillCompensation(transactionOptions(root, cli, ['monica-guide'], () => 'applied', { offline: false }));
  assert.equal(outcome.result, 'applied');
  assert.equal(outcome.transaction.status, 'committed');
  assert.equal(fs.existsSync(path.join(root, 'state', 'skill-install-transactions')), false);
});
