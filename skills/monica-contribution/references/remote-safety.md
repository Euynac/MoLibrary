# Remote safety gate

Immediately before any external action:

1. Re-check classification. Stop public handling if security relevance is plausible.
2. Show the exact repository, action, title/branch, and visibility.
3. Ask for explicit current-session approval for that exact action.
4. Re-check authentication target and current branch after approval.
5. Perform only the approved action; do not infer approval for follow-on actions.

Issue creation does not authorize branch creation. Branch creation does not authorize commit, push, or PR publication. A local draft does not authorize any remote mutation. Persisted `never`, `prepare`, or `ask` preferences are never authorization.

For suspected vulnerabilities, read the checked-out repository's `SECURITY.md`, use its private reporting route, and avoid public duplicate searches that disclose the finding.
