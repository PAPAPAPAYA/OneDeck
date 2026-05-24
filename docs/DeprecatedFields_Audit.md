# Deprecated / Zombie Fields Audit — OneDeck

**Audit Date:** 2026-05-24
**Auditor:** Agent ( bury-batch-actual-phys-index-fix context )
**Scope:** `Assets/Scripts/Managers/`, `Assets/Scripts/Effects/`, `Assets/Scripts/UXPrototype/`, `Assets/Scripts/Card/`

---

## Summary

This audit identifies fields that are either pure dead code (written but never read), semi-retired (captured with intent but ignored by consumers), or functionally inert (inspector-facing with misleading documentation). The findings are grouped by severity and type.

Total fields found: **10**

| Category | Count | Description |
|----------|-------|-------------|
| A — Animation snapshot (semi-retired) | 2 | `snapshotDeckSize`, `targetIndices` |
| B — Pure dead code | 2 | `sessionID`, `open` |
| C — Debug-only logging | 1 | `chainID` |
| D — Zombie inspector fields | 4 | `insertDuration`, `addNewLine`, `secondaryLiftHeight`, `secondaryLiftDuration` |
| E — Functionally inert | 1 | `sellMode` |

---

## A. Animation System Snapshot Fields (Semi-Retired)

These fields are carefully computed during the logic phase with the stated intent of preserving animation targets across reactive effect chains, but the playback code (`RecorderAnimationPlayer`) has moved to runtime physical-deck lookups.

### A.1 `AnimationRequest.snapshotDeckSize`

| | |
|---|---|
| **File** | `Assets/Scripts/Managers/AnimationRequest.cs:39` |
| **Type** | `public int` |
| **Written by** | `BuryEffect.cs:355` (`snapshotDeckSize = _combinedDeck.Count`)<br>`StageEffect.cs:377` (`snapshotDeckSize = _combinedDeck.Count`) |
| **Read by** | **Debug logs only.**<br>`CombatUXManager.cs:965` (active log)<br>`RecorderAnimationPlayer.cs:171` / `224` (commented-out logs) |
| **Status** | Comment updated to: *"Historical deck size at time of effect capture, for debug logging only. No longer used for index calculation."* |
| **Recommended action** | Keep for debugging, comment is now accurate. Low priority to remove. |

### A.2 `AnimationRequest.targetIndices`

| | |
|---|---|
| **File** | `Assets/Scripts/Managers/AnimationRequest.cs:38` |
| **Type** | `public List<int>` |
| **Written by** | `BuryEffect.cs:354` (`targetIndices = buriedTargetIndices`)<br>`StageEffect.cs:376` (`targetIndices = stagedTargetIndices`) |
| **Read by** | **Partially retired.**<br>• `RecorderAnimationPlayer.cs` — only for `snapshotIdxStr` debug string (lines 177–200, 228–251, 277–297). **Not passed to `MoveCardToIndex`.**<br>• `CombatUXManager.MoveCardToTopPopUpBatch` — **still actively used** at lines 511 and 568 to compute `CalculateAnimationPositionAtIndex(finalIndex)`. |
| **Status** | Semi-retired. `MoveToBottomBatch` and `MoveToTopBatch` ignore it; `MoveCardToTopPopUpBatch` still depends on it. |
| **Recommended action** | Add a class-level comment on `AnimationRequest` clarifying which request types still consume `targetIndices`. Do not delete until `MoveCardToTopPopUpBatch` is migrated to `actualPhysIndex` as well. |

---

## B. Pure Dead Code (Written, Never Read)

Fields that are assigned on every chain creation / close but never referenced by any logic.

### B.1 `EffectRecorder.sessionID`

| | |
|---|---|
| **File** | `Assets/Scripts/Managers/EffectRecorder.cs:8` |
| **Type** | `public int` |
| **Written by** | `EffectChainManager.cs:86` (`newChainScript.sessionID = sessionNumberRef.value`) |
| **Read by** | **Zero references.** Only declaration and assignment exist. |
| **Status** | Dead code. |
| **Recommended action** | ~~Delete field and the single assignment line.~~ ✅ **Cleared 2026-05-24** |

### B.2 `EffectRecorder.open`

| | |
|---|---|
| **File** | `Assets/Scripts/Managers/EffectRecorder.cs:14` |
| **Type** | `public bool open = true` |
| **Written by** | `EffectChainManager.cs:90` (`open = true`)<br>`EffectChainManager.cs:180` (`rec.open = false`) |
| **Read by** | **Zero references.** Only the two assignments exist. |
| **Status** | Dead code. |
| **Recommended action** | Delete field and both assignment lines. |

---

## C. Debug-Only Fields

Fields whose only consumers are human-readable log strings; they do not drive any runtime logic.

### C.1 `EffectRecorder.chainID`

| | |
|---|---|
| **File** | `Assets/Scripts/Managers/EffectRecorder.cs:9` |
| **Type** | `public int` |
| **Written by** | `EffectChainManager.cs:87` (`chainID = chainNumber`) |
| **Read by** | `AnimationStateTracker.cs:99`, `EffectChainManager.cs:133`, `BuryEffect.cs`, `StageEffect.cs`, `CostNEffectContainer.cs`, `RecorderAnimationPlayer.cs` — **all for log prefix strings** like `chain#123[cardName]`. |
| **Actual chain logic** | Chain matching and parenting use `GameObject` references and `Transform` hierarchy. `chainID` is never used for lookup or equality checks. |
| **Status** | VIEW ONLY (accurately described in `EffectChainManager` inspector header). |
| **Recommended action** | Add an inline comment on the field: `// VIEW ONLY — used for debug logging, not for chain logic.` |

---

## D. Zombie Inspector Fields

Public fields exposed in the Inspector with Tooltips or Headers, but the runtime code ignores them entirely.

### D.1 `CardPhysObjScript.insertDuration`

| | |
|---|---|
| **File** | `Assets/Scripts/UXPrototype/CardPhysObjScript.cs:88` |
| **Type** | `public float insertDuration = 0.4f` |
| **Tooltip** | `"Insert animation duration"` |
| **Read by** | **Zero references** across entire codebase. |
| **Status** | Misleading. The actual insert animation duration is hardcoded elsewhere. |
| **Recommended action** | ~~Delete field and Tooltip.~~ ✅ **Cleared 2026-05-24** |

### D.2 `AddTextEffect.addNewLine`

| | |
|---|---|
| **File** | `Assets/Scripts/Effects/AddTextEffect.cs:15` |
| **Type** | `public bool addNewLine = true` |
| **Tooltip** | `"Whether to automatically add newline at end of text"` |
| **Read by** | **Zero references.** `AddText()` and `AddTextWithCardPrefix()` both call `AppendLog(textToAdd)` directly without checking this flag. |
| **Status** | Misleading. The described behavior does not exist. |
| **Recommended action** | ~~Delete field and Tooltip.~~ ✅ **Cleared 2026-05-24** |

### D.3 `CombatUXManager.secondaryLiftHeight`

| | |
|---|---|
| **File** | `Assets/Scripts/UXPrototype/CombatUXManager.cs:102` |
| **Type** | `public float secondaryLiftHeight = 0.4f` |
| **Read by** | **Zero references.** |
| **Status** | Dead code, no comment explaining intent. |
| **Recommended action** | ~~Delete field.~~ ✅ **Cleared 2026-05-24** |

### D.4 `CombatUXManager.secondaryLiftDuration`

| | |
|---|---|
| **File** | `Assets/Scripts/UXPrototype/CombatUXManager.cs:103` |
| **Type** | `public float secondaryLiftDuration = 0.25f` |
| **Read by** | **Zero references.** |
| **Status** | Dead code, no comment explaining intent. |
| **Recommended action** | ~~Delete field.~~ ✅ **Cleared 2026-05-24** |

---

## E. Functionally Inert Fields

Fields that are technically read in runtime logic, but their write path is dead (commented out or unreachable), making them constants in practice.

### E.1 `ShopManager.sellMode`

| | |
|---|---|
| **File** | `Assets/Scripts/Managers/ShopManager.cs:38` |
| **Type** | `public bool sellMode = false` |
| **Comment** | `// if it's not sell mode then its buy mode` |
| **Written by** | Initialized to `false`. The only toggle (`sellMode = !sellMode`) is **commented out** at line 66. |
| **Read by** | Line 76 (`if (!sellMode)`) and line 361 (UI mode string). |
| **Status** | Functionally a constant `false`. The shop is permanently in buy mode. |
| **Recommended action** | Either (a) delete the field and simplify the two reads to assume buy mode, or (b) keep but add comment: `// Functionally always false — sell mode UI path is disabled.` |

---

## Recommended Cleanup Priority

| Priority | Fields | Action | Risk |
|----------|--------|--------|------|
| **P1 — Safe** | `sessionID`, `open`, `insertDuration`, `addNewLine`, `secondaryLiftHeight`, `secondaryLiftDuration` | Delete fields and associated writes/Tooltips | Zero — no readers exist |
| **P2 — Low risk** | `chainID` | Add `// VIEW ONLY` comment | Zero — only affects logs |
| **P2 — Low risk** | `targetIndices` | Add class-level comment clarifying partial retirement | Low — documentation only |
| **P3 — Design decision** | `sellMode` | Either delete or document as disabled | Low — but affects shop UI code |
| **P3 — Keep** | `snapshotDeckSize` | Already documented; keep for debug | Zero |

---

## Related Documents

- `docs/RegressionChecklist.md` — Row 7 covers the `actualPhysIndex` migration that made `targetIndices` / `snapshotDeckSize` semi-retired
- `plans/bury-batch-actual-phys-index-fix-prd.md` — PRD describing the migration from `correctedIndex` to `actualPhysIndex`
- `AGENTS.md` — Documents `targetIndices` snapshot semantics (now partially outdated)
