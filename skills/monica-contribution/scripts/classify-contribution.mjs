#!/usr/bin/env node

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const RULES = [
  {
    classification: 'security-report',
    terms: ['vulnerability', 'security', 'cve', 'credential leak', 'secret leak', 'auth bypass', 'authentication bypass', 'remote code execution', 'rce', 'sql injection', 'xss', 'path traversal', 'privilege escalation'],
    route: 'private-security-report',
    publicAllowed: false,
    nextStep: 'Read SECURITY.md and prepare a minimal private report. Do not search or post publicly.',
  },
  {
    classification: 'documentation-defect',
    terms: ['documentation', 'docs', 'typo', 'broken link', 'quick start', 'example is wrong', 'missing guide', 'translation', 'en-us', 'zh-cn'],
    route: 'documentation-issue-or-pr-draft',
    publicAllowed: true,
    nextStep: 'Verify current source behavior and update both supported locales before drafting.',
  },
  {
    classification: 'confirmed-framework-bug',
    terms: ['confirmed framework bug', 'confirmed bug against exact', 'reproduced against the exact release'],
    route: 'issue-or-approved-pr-draft',
    publicAllowed: true,
    nextStep: 'Preserve the exact-version reproduction and prepare an Issue or approved PR plan with regression tests.',
  },
  {
    classification: 'broad-design-change',
    terms: ['design change', 'architecture change', 'proposal', 'rfc', 'new feature', 'breaking change', 'redesign', 'alternative api', 'should support'],
    route: 'discussion-or-design-issue-draft',
    publicAllowed: true,
    nextStep: 'Describe the problem, constraints, alternatives, migration impact, and non-goals before implementation.',
  },
  {
    classification: 'application-misuse',
    terms: ['configuration', 'configured', 'registration', 'register', 'dependency injection', 'service not registered', 'wrong option', 'usage', 'how do i', 'how to'],
    route: 'local-support-or-docs-clarification',
    publicAllowed: true,
    nextStep: 'Reproduce in the application and compare configuration with the exact-version public contract before reporting upstream.',
  },
  {
    classification: 'framework-bug-candidate',
    terms: ['exception', 'crash', 'regression', 'deadlock', 'incorrect result', 'throws', 'fails', 'framework bug', 'minimal reproduction', 'repro'],
    route: 'issue-draft-after-reproduction',
    publicAllowed: true,
    nextStep: 'Produce a minimal reproduction against exact released source and rule out application misuse before calling it confirmed.',
  },
];

export function classifyContribution(text) {
  const normalized = text.toLowerCase();
  const scored = RULES.map((rule, index) => ({
    rule,
    index,
    matches: rule.terms.filter((term) => normalized.includes(term)),
  })).filter((entry) => entry.matches.length);
  scored.sort((left, right) => {
    if (left.rule.classification === 'security-report') return -1;
    if (right.rule.classification === 'security-report') return 1;
    return right.matches.length - left.matches.length || left.index - right.index;
  });
  const selected = scored[0]?.rule || {
    classification: 'needs-triage',
    route: 'local-triage',
    publicAllowed: false,
    nextStep: 'Collect exact version, source commit, expected behavior, actual behavior, and a minimal reproduction before choosing a public route.',
  };
  return {
    schemaVersion: 1,
    classification: selected.classification,
    confidence: scored.length ? (scored[0].matches.length > 1 ? 'medium' : 'low') : 'unresolved',
    matchedSignals: scored[0]?.matches || [],
    route: selected.route,
    publicAllowed: selected.publicAllowed,
    remoteMutationAuthorized: false,
    nextStep: selected.nextStep,
    safety: selected.publicAllowed
      ? 'Any remote action still requires explicit current-session approval.'
      : selected.classification === 'security-report'
        ? 'Never create a public issue, discussion, or pull request for this finding.'
        : 'Do not publish until classification is resolved.',
  };
}

function parse(argv) {
  let text = '';
  let json = false;
  for (let index = 0; index < argv.length; index += 1) {
    if (argv[index] === '--json') json = true;
    else if (argv[index] === '--text') text = argv[++index] || '';
    else if (argv[index] === '--stdin') text = fs.readFileSync(0, 'utf8');
    else throw new Error(`Unknown option: ${argv[index]}`);
  }
  if (!text.trim()) throw new Error('Provide finding text with --text or --stdin.');
  return { text, json };
}

function render(result) {
  return [
    `Classification: ${result.classification}`,
    `Route: ${result.route}`,
    `Public route allowed: ${result.publicAllowed ? 'yes, after safety checks' : 'no'}`,
    `Remote mutation authorized: no`,
    `Next step: ${result.nextStep}`,
    `Safety: ${result.safety}`,
  ].join('\n').concat('\n');
}

if (process.argv[1] && fileURLToPath(import.meta.url) === path.resolve(process.argv[1])) {
  try {
    const options = parse(process.argv.slice(2));
    const result = classifyContribution(options.text);
    process.stdout.write(options.json ? `${JSON.stringify(result, null, 2)}\n` : render(result));
  } catch (error) {
    process.stderr.write(`ERROR: ${error.message}\n`);
    process.exitCode = 2;
  }
}
