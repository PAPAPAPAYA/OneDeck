# Regression Checklist — OneDeck Visual Bugs

Every bug-fix PR / commit must **append or update at least one row** in this table.
If a row becomes obsolete (code refactored away), mark it `~~strikethrough~~` and add `(Obsolete YYYY-MM-DD)` rather than deleting it.

---

## Legend

| Symbol | Meaning |
|--------|---------|
| ✅ | Verified & stable |
| ⚠️ | Fixed but needs re-verification |
| ~~strikethrough~~ | Obsolete (code refactored away) |

---

## Deck Movement & Positioning

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 1 | Bury/Stage animation has no visible movement (distance-zero) | `BuryEffect`, `StageEffect`, `CombatUXManager` | 2026-05-18 | ✅ | **Card:** StoneShell (Bury) or RisingFlame (Stage)<br>**Check:** Cards animate with visible movement |
| 2 | Bury-then-Stage reactive chain causes wrong animation target index | `BuryEffect`, `StageEffect`, `EffectChainManager` | 2026-05-15 | ✅ | **Card:** StoneShell buries RisingFlame (onMeBuried→StageSelf)<br>**Check:** Final deck position is correct |
| 5 | Bury/Stage inserts moved card before pending slot-in cards | `ApplyAnimationResult`, `BuryEffect`, `StageEffect` | 2026-05-24 | ✅ | **Card:** Chain AddTempCard then Bury/Stage<br>**Check:** Moved card lands after pending cards |
| 7 | Stage/Bury animation target offset when pending cards exist in deck | `CalculateAnimationPositionAtIndex`, `CombatUXManager`, `RecorderAnimationPlayer` | 2026-05-24 | ✅ | **Card:** sacrificial_spirit (creates pending JU_ON) then soldier_skeleton (StageSelf)<br>**Check:** Peak and slot-in positions match logical top index<br>同时验证 `RecorderAnimationPlayer` 使用 `actualPhysIndex` 而非 `correctedIndex` 作为动画目标索引（日志中 `actualPhysIndex == targetIndex`）。 |

## Card Adding & Pending Cards

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 3 | Existing cards snap instantly when new card added + Bury/Stage | `CardPhysObjScript`, `AddPhysicalCardToDeck`, `CombatUXManager` | 2026-05-17 | ✅ | **Card:** RIFT_INSECT adds card then Bury triggers<br>**Check:** Existing cards tween visibly instead of snapping |
| 4 | Pending cards (RIFT/AddTempCard) have wrong pop-up peak / slot-in position | `AddTempCard`, `PopUp`, `SlotIn`, `CombatUXManager` | 2026-05-24 | ✅ | **Card:** RIFT_INSECT or BLACKSMITH<br>**Check:** Pop-up peak and slot-in target match logical deck index |
| 8 | Newly created curse card's projectile flies off-screen | `CurseEffect`, `MoveToPopUpPosition`, `CombatUXManager` | 2026-05-24 | ✅ | **Card:** Any curse card that enhances a type not present in deck (e.g. JU_ON)<br>**Check:** Projectile flies to visible deck peak, not off-screen |

## Layout & Focus

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 6 | Deck-move animations play in wrong peeled/focused layout | `RecorderAnimationPlayer`, `CombatUXManager` | 2026-05-18 | ✅ | **Step:** Click a card to focus deck, then reveal Bury/Stage card<br>**Check:** Animation uses normal (non-focused) layout |

---

## Quick Search

Before editing any code in `Effects/`, `UXPrototype/`, or `Managers/Animation*.cs`:

1. Grep for `VISUAL-FIX` in the files you modified
2. Find the matching scenario number above
3. Confirm you did not break the **Regress** scenario

### Search by File

| File(s) | Related Rows |
|---------|-------------|
| `BuryEffect.cs` | 1, 2, 5, 7 |
| `StageEffect.cs` | 1, 2, 5, 7 |
| `CombatUXManager.cs` | 1, 3, 4, 6, 7 |
| `EffectChainManager.cs` | 2 |
| `CardPhysObjScript.cs` | 3 |
| `CurseEffect.cs` | 8 |
| `AddTempCard.cs` | 4 |
| `RecorderAnimationPlayer.cs` | 6 |
| `ApplyAnimationResult` | 5 |
| `CalculateAnimationPositionAtIndex` | 7 |
