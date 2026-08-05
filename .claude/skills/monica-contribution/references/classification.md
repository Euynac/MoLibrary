# Contribution classification

Classify conservatively and preserve uncertainty.

| Class | Required next step | Public route |
| --- | --- | --- |
| Application misuse | Reproduce in the application and compare with current docs and public contracts. | Support/docs clarification only if the contract is unclear. |
| Documentation defect | Verify current source behavior and both locales. | Documentation Issue or PR draft. |
| Framework bug candidate | Produce a minimal reproduction against the exact released source; rule out configuration and dependency mismatch. | Issue draft after reproduction. |
| Broad design change | Explain problem, constraints, alternatives, and migration impact. | Discussion or design Issue before implementation. |
| Security report | Minimize sensitive evidence and follow the repository security policy privately. | Never public. |

Do not label a defect “confirmed” from an error message alone. Record the exact Monica version, immutable tag/commit, package resolution, runtime, OS, minimal reproduction, expected behavior, actual behavior, and evidence that excludes application misuse.
