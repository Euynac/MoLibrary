# Releases and skill revisions

Use three separate version axes. Do not describe one as a substitute for another.

| Axis | Meaning | Authority |
| --- | --- | --- |
| Monica framework version | The NuGet or source version used by the project | Resolved project state and compatibility with the global Monica binding |
| Monica skill release | The immutable Monica tag and catalog selected for the one global installation | `agent-skill-index.json` |
| Per-skill revision | A human-readable change counter within the immutable release sequence | Release index `skillRevisions` and `skillLastChangedIn` metadata |

Digests are the verification contract. Revisions help people see which named skills changed; they are not `SKILL.md` frontmatter and cannot prove installed bytes by themselves.

## When versions change

Ordinary push and pull-request CI validates schemas, canonical projections, prompt parity, skill structure, and tests. It does not publish a skill release, advance `stable` or `preview`, or assign new per-skill revisions.

The Monica release workflow is the only publication path:

1. A Monica tag selects an immutable commit and either the `stable` or `preview` channel from its semantic version.
2. Release packaging hashes the catalog and every managed skill tree.
3. Unchanged skill digests preserve their revision and `lastChangedIn`; new or changed digests receive the next revision and current tag.
4. Tag-based install and discovery smoke tests must pass before that immutable release is advertised.
5. The published index is rolled into source history before the next release. History rewrites fail closed.

The `source` channel is different: it installs from the exact globally bound Monica commit and identifies skill content by exact digest. It never follows a moving branch and does not invent release revisions. The independent Monica.Docs binding is a lookup source and does not select a Monica skill release.

If a selected release uses a newer index, catalog, or manifest schema than the installed Guide supports, stop with `guide_upgrade_required`. Reinstall only `monica-guide` from the exact `reinstallUrl` for that requested tag, verify host discovery, and retry the same command. Do not fall back to another release or interpret the newer contract with the old Guide.

## When users update

`status` and `doctor` compare the repository expectation, active global release, and installed skill bytes. Run `update` only after the user chooses the target release or channel. A full update repairs missing or tampered skills and reinstalls only catalog-selected Monica skills that are new, changed, unknown, or unhealthy while verifying the whole managed set.

Use `update --skill <name>` only for an explicitly requested named update. The Guide adds required dependencies and blocks the targeted update if another managed skill would need to change or prevent full-release verification. It never modifies unrelated user skills or claims simultaneous global versions.
