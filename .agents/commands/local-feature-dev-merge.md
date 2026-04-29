---
name: local-feature-dev-merge
description: Local Git branch sync and merge workflow for repositories that use fixed local branches `feature` and `dev`. Use when the user wants to sync the local `feature` branch with the local `dev` branch without pulling or fetching remote updates, then merge local `feature` changes into local `dev`, and does not want to push the `feature` branch to remote.
---

1. Verify the current repository is a Git repository.
2. Verify the working tree is clean. If there are uncommitted or untracked changes, stop and tell the user to commit or stash them first.
3. Verify local branches `feature` and `dev` both exist. Do not create branches automatically.
4. If the current branch is not `feature`, switch to local branch `feature`.
5. Stay fully local. Do not call `git pull`, `git fetch`, or `git push`.
6. On `feature`, run `git merge dev` to sync local `dev` into local `feature`.
7. If the merge in step 6 conflicts:
   - If the conflict is small and the correct resolution is clear from local context, help resolve it and continue.
   - If the conflict is broad, risky, or requires business decisions, stop and ask the user to resolve it manually.
8. After `feature` is updated, switch to `dev`.
9. Run `git merge --no-ff feature` on `dev` so the feature integration is recorded as an explicit merge commit.
10. If the merge in step 9 conflicts, use the same conflict policy as step 7.
11. Do not push any branch after the merge. In particular, do not push `feature`.
12. Summarize the result for the user:
    - whether `feature <- dev` completed
    - whether `dev <- feature` completed
    - the current branch after finishing
    - whether any conflicts remain unresolved
