import assert from 'node:assert/strict';
import test from 'node:test';
import { classifyContribution } from '../scripts/classify-contribution.mjs';

test('suspected vulnerabilities always route privately', () => {
  const result = classifyContribution('I found an authentication bypass vulnerability with a minimal reproduction.');
  assert.equal(result.classification, 'security-report');
  assert.equal(result.publicAllowed, false);
  assert.equal(result.remoteMutationAuthorized, false);
  assert.match(result.safety, /Never create a public issue/i);
});

test('classification distinguishes docs, application misuse, bug candidates, and design changes', () => {
  assert.equal(classifyContribution('The quick start documentation has a broken link.').classification, 'documentation-defect');
  assert.equal(classifyContribution('How do I register this service in dependency injection?').classification, 'application-misuse');
  assert.equal(classifyContribution('This regression throws an exception with a minimal repro.').classification, 'framework-bug-candidate');
  assert.equal(classifyContribution('Confirmed framework bug reproduced against the exact release.').classification, 'confirmed-framework-bug');
  assert.equal(classifyContribution('Proposal for a breaking architecture change.').classification, 'broad-design-change');
});

test('preferences and classification never authorize remote mutation', () => {
  for (const text of ['docs typo', 'framework bug repro', 'new feature proposal', 'unknown behavior']) {
    assert.equal(classifyContribution(text).remoteMutationAuthorized, false);
  }
});
