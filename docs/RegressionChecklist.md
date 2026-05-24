# Regression Checklist — OneDeck Visual Bugs

Every bug-fix PR / commit must **append or update at least one row** in this table.
If a row becomes obsolete (code refactored away), mark it `~~strikethrough~~` and add `(Obsolete YYYY-MM-DD)` rather than deleting it.

---

| # | Scenario | System / Effect | Fixed Date | Verification Method |
|---|----------|-----------------|------------|---------------------|
| 1 | Bury/Stage animation has no visible movement (distance-zero) | BuryEffect, StageEffect, CombatUXManager | 2026-05-18 | Play Mode: Reveal StoneShell (Bury) or RisingFlame (Stage); verify cards animate with visible movement |
| 2 | Bury-then-Stage reactive chain causes wrong animation target index | BuryEffect, StageEffect, EffectChainManager | 2026-05-15 | Play Mode: StoneShell buries RisingFlame, RisingFlame has onMeBuried→StageSelf; verify final deck position |
| 3 | Existing cards snap instantly when new card added + Bury/Stage | CardPhysObjScript, AddPhysicalCardToDeck, CombatUXManager | 2026-05-17 | Play Mode: RIFT\_INSECT adds card then Bury triggers; verify existing cards tween visibly |
| 4 | Pending cards (RIFT/AddTempCard) have wrong pop-up peak / slot-in position | AddTempCard, PopUp, SlotIn, CombatUXManager | 2026-05-24 | Play Mode: RIFT\_INSECT or BLACKSMITH; verify pop-up peak and slot-in target match logical deck index |
| 5 | Bury/Stage inserts moved card before pending slot-in cards | ApplyAnimationResult, BuryEffect, StageEffect | 2026-05-24 | Play Mode: Chain AddTempCard then Bury/Stage; verify moved card lands after pending cards |
| 6 | Deck-move animations play in wrong peeled/focused layout | RecorderAnimationPlayer, CombatUXManager | 2026-05-18 | Play Mode: Click a card to focus deck, then reveal Bury/Stage card; verify animation uses normal layout |

---

## Quick Search

Before editing any code in `Effects/`, `UXPrototype/`, or `Managers/Animation*.cs`, grep for `VISUAL-FIX` in the files you modified and confirm you did not break the `Regress` scenario.
