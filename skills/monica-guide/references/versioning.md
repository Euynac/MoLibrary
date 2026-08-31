# Releases and skill revisions

Use three separate version axes. Do not describe one as a substitute for another.

| Axis | Meaning | Authority |
| --- | --- | --- |
| Monica framework version | The NuGet or source version used by the project | Resolved project state and the global Monica binding |
| Monica skill release | The immutable Monica tag and its bundled skill catalog | The release manifest and `skills/catalog.json` inside the installed bundle |
| Per-skill revision | A human-readable change counter within the immutable release sequence | Release index `skillRevisions` and `skillLastChangedIn` metadata |

Digests are the verification contract. Revisions help people see which named skills changed; they cannot prove installed bytes by themselves — the catalog's per-skill tree digests do.

## When versions change

Ordinary push and pull-request CI validates schemas, canonical projections, prompt parity, skill structure, and tests. It does not publish a skill release or assign new per-skill revisions.

The Monica release workflow is the only publication path:

1. A Monica tag selects an immutable commit and its channel from the semantic version.
2. Release packaging hashes the catalog and every managed skill tree, projects the installer catalog (files, digests, profiles, managed instructions, source repositories, aliases), and builds the unified guide bundle asset (`monica-guide-v<version>-win-x64.zip`) plus `SHA256SUMS`.
3. Unchanged skill digests preserve their revision and `lastChangedIn`; new or changed digests receive the next revision and current tag.
4. A guide-executable smoke test installs, verifies, and removes the released skill set before the release is published.
5. The published index is rolled into source history before the next release. History rewrites fail closed.

## When users update

`status` and `doctor` compare the recorded release identity and every installed skill tree against the catalog. Run `configure` from a newer bundle only after the user chooses the target release; the wizard's Update page performs the same digest-locked flow after verifying the release `SHA256SUMS`. A full configure repairs missing or tampered skills wholesale — skill directories are replaced atomically, never merged — and `--skill <name>` narrows installation to an explicitly requested skill plus its dependencies. The guide never modifies unrelated user skills or claims simultaneous global versions; one product holds exactly one active release in the unified ledger.
