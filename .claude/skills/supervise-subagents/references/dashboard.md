# Local Dashboard

`init` starts or reuses one dashboard for the current project and returns its URL. It binds only to `127.0.0.1`, chooses an available port by default, and scans valid sessions under the project `.tmp/` directory.

The UI shows:

- task sessions, newest first
- native lifecycle and child-reported progress as separate fields
- delegated scope, runtime id, latest checkpoint, timestamps, and freshness
- aggregate lifecycle counts and an expandable event timeline

The browser refreshes every two seconds. A lifecycle or progress timestamp older than 90 seconds is labeled stale; staleness is informational and never overrides native lifecycle.

The language selector supports English and Simplified Chinese and stores the choice in browser-local storage. Automatic refreshes preserve the expanded state of each agent timeline for the current browser page.

Manage the reusable server with:

```bash
python <skill-dir>/scripts/supervise_subagents.py dashboard status
python <skill-dir>/scripts/supervise_subagents.py dashboard stop
```

The public HTTP surface is read-only. The server uses no external assets, sends a restrictive Content Security Policy, enables no CORS, and renders persisted content as text. A private token stored under `.tmp/` authenticates the local shutdown request; it is never returned by the public health endpoint or displayed in the UI.
