# Skill Review: `unity-card-playmode-test`

**Date:** 2026-07-18
**Subject:** `.agents/skills/unity-card-playmode-test/SKILL.md` (394 lines)
**Method:** Static verification of every code/prefab claim (source files + prefab YAML), sibling-skill sweep, MCP infrastructure inspection. Live Play Mode execution was initially blocked, then completed the same day (see Section 1).

---

## 1. Live Test Status (Executed 2026-07-18 — PASSED)

~~A real Play Mode test run could not be executed: the Unity Editor MCP plugin is disconnected.~~ **Resolved later the same day** — after the MCP session recovered, the AVENGER Strategy B suite was executed live in Play Mode (Unity 6000.3.9f1, Roslyn compiler):

- `[TEST PASS] AVENGER-1 Reveal Damage | Expected: 3, Actual: 3` — via the event path only (reflection-invoked `TriggerRevealedCardEffect`), confirming Section 2.2: direct container iteration is unnecessary and harmful.
- `[TEST PASS] AVENGER-2 Buried Power | Expected: 2, Gained: 2` — expected value read back dynamically from the prefab binding (`m_IntArgument: 2`), confirming Section 2.4 (old fallback of 1 was stale).
- Wiring probe confirmed Section 2.1: `HPAlterEffect` and `StatusEffectGiverEffect` had `myCard` pre-wired by `OnEnable()` immediately after `Instantiate` — mandatory reflection wiring is obsolete.
- Full run produced zero console errors/warnings; HP/phase/chains were snapshotted and restored via `try/finally`; Play Mode was exited cleanly.

Original blockage note (for the record): the MCP plugin had disconnected (`no_unity_session` on every `execute_code` call); live testing was restored by reconnecting the MCP session.

---

## 2. Factual Errors in the Skill

### 2.1 Challenge #1 (mandatory reflection wiring) is obsolete

The skill claims `EffectScript` protected fields (`myCard`, `myCardScript`, `combatManager`) are `null` after `Instantiate` and must be wired via reflection.

Current code contradicts this: `EffectScript.OnEnable()` wires all three fields — `combatManager = CombatManager.Me`, `myCard = transform.parent.gameObject`, `myCardScript = myCard.GetComponent<CardScript>()` (`Assets/Scripts/Effects/EffectScript.cs:13-18`). `Instantiate` runs `OnEnable` synchronously for active objects, and in the AVENGER prefab the effect child objects (`deal dmg`, `gain power`) are direct children of the card root, so `transform.parent` resolves correctly.

The entire reflection-wiring block in the skill's `CreateTestCard` / `CreateEnemyCard` helpers is redundant defensive code in Play Mode.

### 2.2 Challenge #9 is doubly wrong — and dangerous

The skill claims `TriggerRevealedCardEffect` "only invokes the first `CostNEffectContainer`" and advises iterating **all** containers with `InvokeEffectEvent()` on each.

- `TriggerRevealedCardEffect` (`Assets/Scripts/Managers/CombatManager.cs:804`) does not invoke containers directly at all — it raises `onAnyCardRevealed` and `onMeRevealed.RaiseSpecific(revealZone)`, which fires **every** `GameEventListener` on the card. Only the Start Card branch (`CombatManager.cs:756`) uses the singular `GetComponentInChildren<CostNEffectContainer>()`.
- Prefab check on `CURSED_CORPSE.prefab`: its two containers are bound to **different** events — onMeRevealed -> `HPAlterEffect.DecreaseTheirHpTimesX`, onMeBuried -> `CurseEffect.EnhanceCurse`. They are not "all bound to onMeRevealed" as the skill states.
- Following the skill's advice on a listener-bound card double-triggers effects (the event path already invoked them). On AVENGER this means double damage; on CURSED_CORPSE it means firing the bury effect (`EnhanceCurse`) during a reveal test.

### 2.3 The codedom ban is stale — and has spread to 5 skills

`AGENTS.md` records that the Roslyn compiler was verified on 2026-07-18 and that `compiler: "auto"` resolves to C# 12+. The outdated C# 6 constraint still appears in:

- `unity-card-playmode-test` — Challenge #3 and the "Compilation error: Unexpected symbol" troubleshooting row
- `unity-card-test-planning` — lines 19, 27
- `unity-card-design-documenter` — lines 36, 38 plus `references/batch-inspect-template.cs`
- `unity-card-listener-check` — line 34
- `unity-read-prefab-serialized` — lines 10-22 (worst offender: claims codedom "is the default", which is now false) plus `references/csharp-templates.md`

### 2.4 AVENGER-2 example fallback value is stale

The example hardcodes `expectedPower = 1` as fallback, but the prefab binds `m_IntArgument: 2` (`GiveSelfStatusEffect`), and `cardDesc` also says "gain **2** Power". The dynamic `SerializedObject` readback masks the error at runtime, but the fallback value and comment are misleading.

---

## 3. Structural / Architectural Issues

### 3.1 Disconnected from the existing NUnit infrastructure

The project has `com.unity.test-framework 1.6.0` installed and 20+ EditMode test classes under `Assets/Scripts/Editor/Tests/` built on `HeadlessCombatTestFixture`, which provides a full headless combat environment via `NullCombatVisuals` — HP reset, deck cleanup, chain closing, card creation and wiring. That is exactly the setup the skill tells every test to hand-write. The skill never mentions this infrastructure and gives no decision criteria for "write an NUnit test" vs. "run a one-off `execute_code` script". Much of Strategy B's original purpose (synchronous logic validation) is now covered by repeatable, CI-able tests.

### 3.2 No state backup / restore — destructive to a live session

The Section 3 setup template sets both players' HP to 100, clears `combinedDeckZone`, `DestroyImmediate`s the current `revealZone`, forces the Combat phase, and closes effect chains — with no snapshot and no restore. Running it mid-game irreversibly corrupts the ongoing session. The skill also never states a precondition such as "run at combat start or in a dedicated test scene".

### 3.3 No durable artifacts

Every test is a freshly pasted `execute_code` blob; results are judged by eyeballing the Console. The project already exposes `read_console` (collect `[TEST PASS/FAIL]` lines programmatically) and `run_tests` (drive the NUnit suite) MCP tools — neither is mentioned.

### 3.4 Massive duplication

`CreateTestCard` and `CreateEnemyCard` are ~95% identical (a single faction parameter would merge them), and every test case repeats the full setup block.

### 3.5 Dual maintenance with the SOP — already drifted

`docs/StrategyB_PlayMode_SOP.md` (515 lines) and the skill (394 lines) are two copies of the same content that have diverged. The SOP's challenge rows #4 (close chain between consecutive triggers), #5 (`CheckCost_IndexBeforeStartCard` fails after reveal), and #6 (async `PlayMultiStatusEffectProjectile` — verify via reflection on the private `ApplyStatusEffectToXxxSingle`) are missing from the skill.

### 3.6 Section 11 is misplaced

The "commit `RecordedDeck_*.asset` artifacts" guidance belongs to the DeckSaver / deck-recording workflow, not to Play Mode testing.

### 3.7 Missing risk warning

`TriggerRevealEffect` raises the **global** `onAnyCardRevealed` event — every linger card in the scene responds, which can pollute test state. The skill never warns about this collateral trigger.

---

## 4. Verified-Accurate Claims (for the record)

These parts of the skill checked out against current code/assets and should be preserved:

- `CombatManager.TriggerRevealedCardEffect` exists (private, `CombatManager.cs:804`) — reflection target valid.
- `EffectChainManager.lastEffectObject` / `closedEffectRecorders` exist (`EffectChainManager.cs:29,31`); `CloseOpenedChain()` populates `closedEffectRecorders` (line 210).
- `HPAlterEffect.isStatusEffectDamage` exists (`HPAlterEffect.cs:25`); setting it `true` makes damage synchronous (line 140).
- `CombatManager.playerDeckParent` / `enemyDeckParent` exist (`CombatManager.cs:89-90`).
- `awaitingRevealConfirm` exists (`CombatManager.cs:100`); `PlayRecorderAnimationsAndWait` is a private coroutine (`CombatManager.cs:468`).
- `GameEventListener` serialized field is `event` (not `gameEvent`) — confirmed in prefab YAML.
- `EnumStorage.GetStatusEffectCount` exists and matches the example's usage.
- AVENGER example path exists; AVENGER-1 expectation verified: `baseDmg` IntSO (`Assets/BaseDmgRef.asset`) `value: 2` + `extraDmg: 1` = 3 damage; listeners bind onMeRevealed -> damage container, onMeBuried -> power container.
- `DefaultNamespace.Effects.StatusEffectGiverEffect` namespace is correct.

---

## 5. Recommendations (Prioritized)

1. **Fix the factual errors.** Drop Challenge #1's reflection necessity (or demote it to a fallback note); rewrite Challenge #9 as "event path vs. direct container invocation — pick one, never both"; correct the AVENGER-2 fallback to 2.
2. **Purge the codedom ban across all 5 skills** (playmode-test, test-planning, design-documenter, listener-check, read-prefab-serialized). State that Roslyn (C# 12+) is the default; keep codedom constraints only as a fallback note, mirroring `AGENTS.md`.
3. **Add a decision table: NUnit vs. execute_code.** Logic/numeric validation -> EditMode NUnit on `HeadlessCombatTestFixture` (repeatable, TearDown-safe). Play Mode `execute_code` only for what genuinely needs the live scene: prefab serialized-binding audits, `RecorderAnimationPlayer` animation-queue behavior, UX/visual verification.
4. **Add state snapshot/restore to the setup template** (save and restore HP, phase, deck, revealZone; with Roslyn, `try/finally` is available) and declare the "run at combat start" precondition.
5. **Merge the duplicated helpers** into one parameterized `CreateTestCard(prefabPath, faction)`; collect results via `read_console` at the end of a run.
6. **Single source of truth.** Merge the skill and `docs/StrategyB_PlayMode_SOP.md`: keep executable templates in the skill, keep the challenge/troubleshooting table in one place only, cross-reference the other; port SOP rows #4/#5/#6 into the merged version.

---

## 6. Follow-up: Recommendations Applied (2026-07-18)

All recommendations above were implemented the same day, after the live test in Section 1:

1. **Factual errors fixed** in `.agents/skills/unity-card-playmode-test/SKILL.md`: Challenge #1 marked obsolete (`OnEnable()` wiring), Challenge #9 rewritten as "event path vs. direct container invocation — pick one, never both", AVENGER-2 fallback corrected to 2 and the example replaced with the live-verified version.
2. **Codedom ban purged** across all 5 skills plus 2 reference files (`batch-inspect-template.cs`, `csharp-templates.md`); each now states Roslyn (C# 12+) is the default with codedom as fallback only, mirroring `AGENTS.md`. A latent compile bug in `batch-inspect-template.cs` (`string safeName = safeName.Replace(...)` self-reference) was also fixed.
3. **Decision table added** (skill Section 1.1): EditMode NUnit on `HeadlessCombatTestFixture` for logic/numeric validation vs. Play Mode `execute_code` for live-scene-only concerns.
4. **Snapshot/restore added** to the setup template (skill Section 4, `try/finally`) with the "run at combat start or dedicated test scene" precondition declared (skill Section 2).
5. **Helpers merged** into one parameterized `CreateTestCard(prefabPath, isEnemy)`; result collection via `read_console` added to the test pattern and checklist.
6. **Single source of truth established**: the skill absorbed the SOP — SOP rows #4/#5/#6 ported as Challenges #7/#10/#18 plus the `ResetState`/`CloseChain`/`StartAnimationPhase` helpers, the async projectile workaround, and the GRUDGE example; `docs/StrategyB_PlayMode_SOP.md` is now a redirect stub.
7. **RecordedDeck commit guidance moved** from the skill's Section 11 to the `check-default-enemy-deck-pool` skill, which owns the `Assets/SORefs/Decks/Recorded` workflow.
8. **Global-event risk warning added** (skill Sections 2 and 5.2): `TriggerRevealEffect` raises the global `onAnyCardRevealed`.

---

## Appendix: Sibling-Skill codedom References

| Skill | Location | Problem |
|-------|----------|---------|
| `unity-card-playmode-test` | Challenge #3, troubleshooting row | Stale C# 6 ban |
| `unity-card-test-planning` | lines 19, 27 | Stale C# 6 ban |
| `unity-card-design-documenter` | lines 36, 38, template cs | Stale C# 6 ban |
| `unity-card-listener-check` | line 34 | Stale "default codedom" |
| `unity-read-prefab-serialized` | lines 10-22, templates md | Stale; incorrectly claims codedom is the default compiler |
