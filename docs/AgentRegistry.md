# Parallel Session Registry

Spec for `.agent_registry/` — the lightweight claim registry that lets multiple agent sessions work in the same workspace without silently overwriting each other. Added 2026-09-16. AGENTS.md carries only the 4-line rule; this file is the full contract.

## Why

Multiple agent sessions run against this workspace in parallel. Conflicts happen at two layers: shared source files, and the single Unity Editor instance (`execute_code` / `run_tests` / SaveScene / Play). The registry makes in-flight work visible so sessions can avoid each other. It is a soft convention ("save trail, not conflict detector"), not a lock: it converts unaware collisions into detectable, avoidable ones — it cannot stop a session that ignores it.

## Directory & Files

- Location: `.agent_registry/` at repo root. Gitignored — claims are per-machine ephemeral state, never committed.
- One file per session per task: `<YYYYMMDD-HHMMSS>-<task-slug>.md` (seconds included to avoid same-minute collisions; the timestamp doubles as the TTL base).
- Written once at task start, never edited afterwards. If scope changes materially (paths outside the claimed set), delete your own file and recreate it with the new scope.

### Template

```markdown
# Claim

- time: 2026-09-16 08:43:06 +08:00
- task: one-line task description
- files:
	- Assets/Scripts/Effects/
	- docs/AgentRegistry.md
- editor: no
```

- `files` may list directories to claim a whole subtree.
- `editor` values: `no`, or what you will occupy (`execute_code`, `run_tests`, `SaveScene`, `Play`). An editor claim is exclusive while held.

## Protocol

1. Before any task that edits files or uses Unity Editor tools: scan `.agent_registry/` and read every claim file.
2. Check overlap: your planned paths vs claimed paths; any held editor claim vs your editor usage.
3. Overlap → stop and report to the user ("conflicts with claim X"), let them decide (redirect / wait / proceed). Never block-wait by polling.
4. No overlap → create your claim file, then work.
5. On completion or abort — including failed tasks — delete your own claim file.

## Invariants

- You only ever create or delete your own claim file. Never modify another session's live claim.
- Claims are advisory: report conflicts, do not enforce with waits or locks.
- Never place claims under `Assets/` (Unity would import them and generate .meta files).

## Stale Cleanup (TTL 48h)

A claim whose file mtime is older than 48 hours is stale — its session most likely died without cleanup. Any session may delete stale claims. Creation time is embedded in both the filename and the `time:` field, so mtime drift is not a problem.

## Known Limits

- A session that never reads AGENTS.md (e.g. a narrow subagent) will not claim. The registry reduces collisions; it cannot eliminate them.
- Compliance is voluntary and the failure mode is silent (everyone assumes the other side checked). Keep the ritual cheap: write once, delete once — no status machine.
- If harder enforcement is ever needed, the upgrade path is a ZCode hook (PreToolUse on Edit/Write that checks the registry), not more prose in AGENTS.md.
