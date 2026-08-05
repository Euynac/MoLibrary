"""Dependency-free validation and derivation for Monica skill revision metadata."""

from __future__ import annotations

from datetime import datetime
from typing import Any


class ReleaseContractError(RuntimeError):
    """Raised when an Agent Skill release violates the revision lineage contract."""


SkillMetadata = tuple[dict[str, str], dict[str, int], dict[str, str]]


def release_skill_metadata(
    release: dict[str, Any], *, tag: str, label: str
) -> SkillMetadata:
    """Return revision maps after enforcing their exact skill-key contract."""

    digests = release.get("skillDigests")
    revisions = release.get("skillRevisions")
    last_changed = release.get("skillLastChangedIn")
    if not all(isinstance(value, dict) for value in (digests, revisions, last_changed)):
        raise ReleaseContractError(
            f"{label} release {tag} has invalid skill revision metadata."
        )
    digest_keys = set(digests)
    if set(revisions) != digest_keys or set(last_changed) != digest_keys:
        raise ReleaseContractError(
            f"{label} release {tag} must use identical skill keys in skillDigests, "
            "skillRevisions, and skillLastChangedIn."
        )
    if any(type(revision) is not int or revision < 1 for revision in revisions.values()):
        raise ReleaseContractError(
            f"{label} release {tag} contains a non-positive skill revision."
        )
    return digests, revisions, last_changed


def release_timestamps(index: dict[str, Any], *, label: str) -> dict[str, datetime]:
    """Parse the unique timestamps that define the global release sequence."""

    releases = index.get("releases")
    if not isinstance(releases, dict):
        raise ReleaseContractError(f"{label} has an invalid releases collection.")
    timestamps: dict[str, datetime] = {}
    for tag, release in releases.items():
        if not isinstance(release, dict):
            raise ReleaseContractError(f"{label} release {tag} must be an object.")
        try:
            published_at = datetime.fromisoformat(
                release["publishedAt"].replace("Z", "+00:00")
            )
        except (KeyError, AttributeError, TypeError, ValueError) as exc:
            raise ReleaseContractError(
                f"{label} release {tag} has an invalid publishedAt."
            ) from exc
        if published_at.tzinfo is None:
            raise ReleaseContractError(
                f"{label} release {tag} publishedAt must include a timezone."
            )
        timestamps[tag] = published_at
    if len(set(timestamps.values())) != len(timestamps):
        raise ReleaseContractError(
            f"{label} release timestamps must be unique to define one global release sequence."
        )
    return timestamps


def validate_revision_history(index: dict[str, Any], *, label: str) -> None:
    """Reject forks, gaps, and ambiguous origins in the global release sequence."""

    releases = index.get("releases")
    if not isinstance(releases, dict):
        raise ReleaseContractError(f"{label} has an invalid releases collection.")
    metadata = {
        tag: release_skill_metadata(release, tag=tag, label=label)
        for tag, release in releases.items()
    }
    timestamps = release_timestamps(index, label=label)
    seen_skills: set[str] = set()
    previous_metadata: SkillMetadata | None = None

    for tag in sorted(releases, key=timestamps.__getitem__):
        digests, revisions, last_changed = metadata[tag]
        for skill_name, changed_tag in last_changed.items():
            changed_metadata = metadata.get(changed_tag)
            if changed_metadata is None:
                raise ReleaseContractError(
                    f"{label} release {tag} references missing skill change release "
                    f"{changed_tag!r} for {skill_name}."
                )
            changed_digests, changed_revisions, changed_origins = changed_metadata
            if (
                changed_origins.get(skill_name) != changed_tag
                or changed_digests.get(skill_name) != digests[skill_name]
                or changed_revisions.get(skill_name) != revisions[skill_name]
            ):
                raise ReleaseContractError(
                    f"{label} release {tag} has inconsistent revision history for {skill_name}."
                )
            if timestamps[changed_tag] > timestamps[tag]:
                raise ReleaseContractError(
                    f"{label} release {tag} points {skill_name} at a later change release."
                )

        if previous_metadata is None:
            for skill_name in digests:
                if revisions[skill_name] != 1 or last_changed[skill_name] != tag:
                    raise ReleaseContractError(
                        f"{label} first release must introduce {skill_name} as revision 1."
                    )
        else:
            previous_digests, previous_revisions, previous_last_changed = previous_metadata
            for skill_name, digest in digests.items():
                if skill_name not in previous_digests:
                    if skill_name in seen_skills:
                        raise ReleaseContractError(
                            f"{label} reintroduces retired skill name {skill_name}; revision "
                            "lineage would be ambiguous."
                        )
                    valid = revisions[skill_name] == 1 and last_changed[skill_name] == tag
                elif previous_digests[skill_name] == digest:
                    valid = (
                        revisions[skill_name] == previous_revisions[skill_name]
                        and last_changed[skill_name] == previous_last_changed[skill_name]
                    )
                else:
                    valid = (
                        revisions[skill_name] == previous_revisions[skill_name] + 1
                        and last_changed[skill_name] == tag
                    )
                if not valid:
                    raise ReleaseContractError(
                        f"{label} release {tag} breaks the consecutive revision lineage for "
                        f"{skill_name}."
                    )
        seen_skills.update(digests)
        previous_metadata = metadata[tag]


def derive_skill_revision_metadata(
    index: dict[str, Any],
    *,
    previous_tag: str | None,
    current_tag: str,
    current_published_at: str,
    skill_digests: dict[str, str],
) -> tuple[dict[str, int], dict[str, str]]:
    """Derive revisions from the latest release in the verified global sequence."""

    validate_revision_history(index, label="Release history")
    releases = index["releases"]
    try:
        published_at = datetime.fromisoformat(current_published_at.replace("Z", "+00:00"))
    except (AttributeError, ValueError) as exc:
        raise ReleaseContractError("The current release publishedAt is invalid.") from exc
    if published_at.tzinfo is None:
        raise ReleaseContractError("The current release publishedAt must include a timezone.")
    if previous_tag is None:
        if releases:
            raise ReleaseContractError(
                "A previous immutable release index and tag are required when history exists."
            )
        return (
            {skill_name: 1 for skill_name in skill_digests},
            {skill_name: current_tag for skill_name in skill_digests},
        )

    if previous_tag == current_tag:
        raise ReleaseContractError("The previous release tag must differ from the current tag.")
    previous_release = releases.get(previous_tag)
    if previous_release is None:
        raise ReleaseContractError(
            f"Release history does not contain previous release {previous_tag}."
        )
    timestamps = release_timestamps(index, label="Release history")
    latest_tag = max(timestamps, key=timestamps.__getitem__)
    if previous_tag != latest_tag:
        raise ReleaseContractError(
            f"Previous release {previous_tag} is not the latest verified release {latest_tag}."
        )
    if published_at <= timestamps[previous_tag]:
        raise ReleaseContractError(
            f"Current release publishedAt must be later than previous release {previous_tag}."
        )

    previous_digests, previous_revisions, previous_last_changed = release_skill_metadata(
        previous_release,
        tag=previous_tag,
        label="Release history",
    )
    revisions: dict[str, int] = {}
    last_changed: dict[str, str] = {}
    for skill_name, digest in skill_digests.items():
        if skill_name not in previous_digests:
            if any(
                skill_name in historic_release.get("skillDigests", {})
                for historic_release in releases.values()
            ):
                raise ReleaseContractError(
                    f"Retired skill name {skill_name} cannot be reintroduced with a new lineage."
                )
            revisions[skill_name] = 1
            last_changed[skill_name] = current_tag
        elif previous_digests[skill_name] == digest:
            revisions[skill_name] = previous_revisions[skill_name]
            last_changed[skill_name] = previous_last_changed[skill_name]
        else:
            revisions[skill_name] = previous_revisions[skill_name] + 1
            last_changed[skill_name] = current_tag
    return revisions, last_changed
