---
name: unity-skill-review
description: Audit OneDeck project skills for staleness and factual drift against the current codebase. Use when asked to review, audit, or validate a skill under .agents/skills, refresh outdated skill content, or run the quarterly skill review rotation. Provides the three-phase process (static verification, live verification, follow-up log), the report template, and per-skill live-verification hooks.
last_reviewed: 2026-07-18
---

# Unity Skill Review (Meta-Skill)

This skill defines the standard process for reviewing any other skill under `.agents/skills/`. Skills rot silently: the codebase evolves and documented APIs, prefab bindings, and compiler constraints drift out of date. The inaugural review (`docs/SkillReview_PlayModeTestSkill_2026-07-18.md`) found three factual errors caused exactly by this — treat it as the worked example for format and depth.

## 1. Three-Phase Process

Run the phases in order. Do not skip Phase 2 — a review that only reads files misses broken workflows (the inaugural review's live test was initially blocked by an MCP disconnect and was only completed after reconnection).

### Phase 1: Static Verification

1. Read the target skill end to end.
2. Extract every **factual claim**: API/class/field names, file paths, line-number references, prefab bindings, compiler/tool constraints, expected values.
3. Verify each claim against the current source of truth:
	- Code claims -> the actual `.cs` files (`Grep`/`Read`, or `unity_reflect` for live API shape).
	- Prefab claims -> prefab YAML or `execute_code` serialized reads (use the `unity-read-prefab-serialized` skill).
	- Path claims -> `Glob`.
4. Sweep **sibling skills** for the same claim — errors found in one skill are often copy-pasted into others (the codedom ban had spread to 5 skills).

### Phase 2: Live Verification

Execute the skill's core workflow once, end to end, using its own instructions and nothing else. A skill passes live verification only if a reader following it literally reaches the documented expected output. Use the per-skill hook in Section 3.

- If live execution is blocked (MCP disconnected, missing fixture, etc.), record the blocker in the report's Section 1 and schedule completion — do not silently downgrade to static-only.
- Judge durable output (`read_console`, generated files, `run_tests` results), not vibes.

### Phase 3: Follow-up Log

1. Write the report to `docs/SkillReview_<SkillName>_<YYYY-MM-DD>.md` using `references/skill-review-template.md`.
2. Apply the accepted fixes (or hand them to the user, per the project's code-change policy).
3. Record what was applied in the report's Follow-up section, with the date.
4. Update `docs/SkillReview_Registry.md` (last-reviewed date, report link, open items).
5. Update the target skill's `last_reviewed:` frontmatter field.

## 2. Report Format

Fixed sections (see `references/skill-review-template.md`):

1. **Live Test Status** — executed/blocked, results, environment.
2. **Factual Errors** — each with evidence (`path:line`).
3. **Structural / Architectural Issues**.
4. **Verified-Accurate Claims** — claims confirmed correct, so the next review only needs to check deltas. Never skip this section; it is what makes reviews cheaper over time.
5. **Recommendations (Prioritized)**.
6. **Follow-up Log** — what was applied, when.

## 3. Per-Skill Live-Verification Hooks

| Skill | Live verification hook |
|-------|------------------------|
| `unity-card-playmode-test` | Run `.agents/skills/unity-card-playmode-test/references/avenger-smoke-test.cs` via `execute_code` in Play Mode at combat start; expect `[TEST PASS]` for AVENGER-1 and AVENGER-2, zero console errors |
| `unity-read-prefab-serialized` | Run one template against a known prefab; diff console output against the prefab YAML values |
| `unity-card-design-documenter` | Run the batch-inspect template on a small prefab folder; parse the log and verify the documented parsing pitfalls still hold |
| `unity-card-listener-check` | Run the extraction snippet + matcher on the current card set; spot-check 3 cards' report entries against their actual listener bindings |
| `unity-card-test-planning` | Mostly static: verify referenced source files, APIs, and template paths exist; dry-run plan generation for one card |
| `unity-card-infinity-check` | Run the check on one known infinite-combo card and one known-safe card; verify detection rules match the current effect system |
| `unity-onedeck-stats-report` | Generate the HTML report from the current persistence JSON; verify fields match the current schema |
| `check-default-enemy-deck-pool` | Run the script read-only (no `--fix`); compare its report against the actual scene/asset state |
| `unity-skill-review` (self) | Execute one review of another skill following this document literally; verify the report matches the template and the registry gets updated |

For a **new skill** not in the table: derive the hook from the skill's own "expected output" claims, run it, and add the hook to this table as part of the review.

## 4. Rotation & Tracking

- **Registry:** `docs/SkillReview_Registry.md` — one row per skill (last-reviewed date, report link, open items). Update it at the end of every review.
- **Cadence:** one skill per quarter, rotating through the registry.
- **Priority rule:** review skills that reference the most source code first — code evolves fastest, so their documents rot first. A skill whose claims touch files changed since its `last_reviewed` date jumps the queue.
- **Staleness rule:** any claim, challenge-table row, or template older than 3 months without re-verification is suspect — re-verify before relying on it, and record the new date.

## 5. Reviewer Checklist

- [ ] Skill read end to end; factual claims listed
- [ ] Every claim verified against current source/prefabs (evidence recorded as `path:line`)
- [ ] Sibling skills swept for the same claims
- [ ] Live hook executed (or blocker recorded and follow-up scheduled)
- [ ] Report written from the template, all 6 sections present
- [ ] Verified-accurate claims recorded (Section 4)
- [ ] Accepted fixes applied; Follow-up Log filled in
- [ ] `docs/SkillReview_Registry.md` updated
- [ ] Target skill's `last_reviewed:` frontmatter updated
- [ ] Hook table (Section 3) updated if the skill changed shape

## 6. Reference

- **Report template:** [references/skill-review-template.md](references/skill-review-template.md)
- **Worked example:** `docs/SkillReview_PlayModeTestSkill_2026-07-18.md`
- **Registry:** `docs/SkillReview_Registry.md`
