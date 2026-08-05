---
name: monica-contribution
description: Classify, reproduce, and prepare safe upstream Monica contributions, including application misuse, documentation defects, framework bugs, design discussions, pull requests, and private security reports. Use when a Monica user wants to report a problem, search for duplicates, draft an Issue or Discussion, prepare a PR plan, synchronize documentation, or decide whether a finding must follow the private vulnerability path.
---

# Monica Contribution

Prepare contributions locally. Do not perform remote mutation without explicit approval in the current session.

## Workflow

1. Classify the finding before drafting:

   ```bash
   node "<skill-dir>/scripts/classify-contribution.mjs" --json --text "<finding>"
   ```

2. Read [references/classification.md](references/classification.md), reproduce against the exact affected Monica version and source, and separate application misuse from a framework defect.
3. Search existing Issues, Discussions, and pull requests only when network access and repository access are authorized. Record likely duplicates in the draft.
4. Prepare the appropriate local artifact using [references/drafting.md](references/drafting.md). Keep expected behavior, minimal reproduction, actual behavior, version/ref, environment, and evidence distinct.
5. Apply [references/remote-safety.md](references/remote-safety.md) immediately before any external action.

## Hard boundaries

- Route suspected vulnerabilities privately according to `SECURITY.md`; never create a public Issue or Discussion for them.
- Treat `never`, `prepare`, and `ask` as preparation preferences only. They never authorize branch creation, pushing, Issue/PR creation, or draft PR publication.
- Ask for current-session approval naming the exact remote action and target immediately before it occurs.
- Keep secrets, private source paths, credentials, raw local state, and sensitive reproduction data out of drafts.
- Synchronize both English and Simplified Chinese documentation when a public contract or onboarding flow changes.
