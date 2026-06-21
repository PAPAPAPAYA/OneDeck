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
| ~~1~~ | ~~Bury/Stage animation has no visible movement (distance-zero)~~ | ~~`BuryEffect`, `StageEffect`, `CombatUXManager`~~ | ~~2026-05-18~~ | ~~(Obsolete 2026-06-13)~~ | ~~Superseded by row 20: logic-phase sync removed; animation movement is now ensured by `ApplyAnimationResult` + `UpdateAllPhysicalCardTargets` in the animation phase.~~ |
| 2 | Bury-then-Stage reactive chain causes wrong animation target index | `BuryEffect`, `StageEffect`, `EffectChainManager` | 2026-05-15 | ✅ | **Card:** StoneShell buries RisingFlame (onMeBuried→StageSelf)<br>**Check:** Final deck position is correct |
| 5 | Bury/Stage inserts moved card before pending slot-in cards | `ApplyAnimationResult`, `BuryEffect`, `StageEffect` | 2026-05-24 | ✅ | **Card:** Chain AddTempCard then Bury/Stage<br>**Check:** Moved card lands after pending cards |
| 7 | Stage/Bury animation target offset when pending cards exist in deck | `CalculateAnimationPositionAtIndex`, `CombatUXManager`, `RecorderAnimationPlayer` | 2026-05-24 | ✅ | **Card:** sacrificial_spirit (creates pending JU_ON) then soldier_skeleton (StageSelf)<br>**Check:** Peak and slot-in positions match logical top index<br>同时验证 `RecorderAnimationPlayer` 使用 `actualPhysIndex` 而非 `correctedIndex` 作为动画目标索引（日志中 `actualPhysIndex == targetIndex`）。 |
| 23 | Arc midpoint z jumps to fixed -80 during Stage/Bury/Shuffle/Reveal-to-bottom | `CombatUXManager`, `showPos` | 2026-06-14 | ⚠️ | **Card:** Any Bury/Stage card (e.g. StoneShell, grave_punch, RisingFlame, BOOSTER) + Start Card shuffle + normal reveal-to-bottom<br>**Check:** Mid-arc card z should be midway between start z and target z, not `-80`. Cards remain visible throughout the arc. |
| 25 | ExileEffect still syncs physical deck in logic phase | `ExileEffect`, `RecorderAnimationPlayer`, `ApplyAnimationResult` | 2026-06-19 | ⚠️ | **Card:** Any Exile card (ExileSelf / ExileMyCards / ExileTheirCards)<br>**Check:** Exiled card pops up from current position, then plays destroy animation; remaining cards tween to correct positions after last destroy completes. Verify exiling the currently revealed card and chaining GiveStatusEffect → Exile on the same target. |

## Card Adding & Pending Cards

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 3 | Existing cards snap instantly when new card added + Bury/Stage | `CardPhysObjScript`, `AddPhysicalCardToDeck`, `CombatUXManager` | 2026-05-17 | ✅ | **Card:** RIFT_INSECT adds card then Bury triggers<br>**Check:** Existing cards tween visibly instead of snapping |
| 4 | Pending cards (RIFT/AddTempCard) have wrong pop-up peak / slot-in position | `AddTempCard`, `PopUp`, `SlotIn`, `CombatUXManager` | 2026-05-24 | ✅ | **Card:** RIFT_INSECT or BLACKSMITH<br>**Check:** Pop-up peak and slot-in target match logical deck index |
| 8 | Newly created curse card's projectile flies off-screen | `CurseEffect`, `MoveToPopUpPosition`, `CombatUXManager` | 2026-05-24 | ✅ | **Card:** Any curse card that enhances a type not present in deck (e.g. JU_ON)<br>**Check:** Projectile flies to visible deck peak, not off-screen |
| 32 | Overtime fatigue cards appear instantly without popup/slot-in animation | `CombatManager.AddFatigueCards`, `StartCardShuffleEffect`, `RecorderAnimationPlayer` | 2026-06-21 | ⚠️ | **Setup:** Set `overtimeRoundThreshold=1` and `fatigueAmount>=1`. Play combat past round 1.<br>**Check:** When the new round starts, fatigue cards fly from popup position into the deck before the Start Card shuffle animation plays. Cards do not snap instantly. |

## Layout & Focus

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 6 | Deck-move animations play in wrong peeled/focused layout | `RecorderAnimationPlayer`, `CombatUXManager` | 2026-05-18 | ✅ | **Step:** Click a card to focus deck, then reveal Bury/Stage card<br>**Check:** Animation uses normal (non-focused) layout |

## Cost Check Feedback

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 9 | Cost check failure has no visual feedback on card | `CostResultPresenter`, `CardPhysObjScript` | 2026-06-07 | ✅ | **Card:** Any card with a cost condition (e.g. Mana cost when Mana is 0)<br>**Check:** Revealed card shakes left-right via EffectRecorder sequence |
| 10 | afterShuffle Stage animation is invisible (zero-distance tween) | `CombatManager`, `StartCardShuffleEffect`, `StageEffect` | 2026-06-08 | ⚠️ | **Card:** BOOSTER (afterShuffle→Stage) after Start Card shuffle<br>**Check:** Dummy cards do NOT silently tween before Stage animation; `MoveToTopPopUpBatch` arc animation is visible.<br>**Note:** Re-verify after 2026-06-13 sync-removal change; `StageEffect` no longer syncs in logic phase. |
| 11 | afterShuffle effect animations not played until next player click | `CombatManager`, `EffectChainManager`, `RecorderAnimationPlayer` | 2026-06-09 | ✅ | **Card:** BOOSTER (afterShuffle→Stage) after Start Card shuffle<br>**Check:** Stage animation plays **automatically** after auto-reveal completes, without waiting for player click; `isPlayingEffectAnimations` blocks Phase 2 during playback |
| 12 | BOOSTER overlaps deck cards because afterShuffle starts before reveal-zone movement finishes | `CombatManager`, `ICombatVisuals`, `CombatUXManager`, `CardPhysObjScript` | 2026-06-09 | ✅ | **Card:** BOOSTER (afterShuffle→Stage) after Start Card shuffle<br>**Check:** BOOSTER fully reaches reveal zone before its emphasize/Stage animation starts; no overlap with deck cards |
| 13 | `MoveCardToTopPopUpBatch` dead-locks due to N `RegisterAnimation` calls vs 1 `CompleteAnimation` | `CombatUXManager`, `AnimationStateTracker`, `RecorderAnimationPlayer` | 2026-06-09 | ✅ | **Card:** BOOSTER (afterShuffle→Stage) after Start Card shuffle<br>**Check:** `PlayRecorderAnimationsAndWait` does **not** wait 5s on timeout; `AnimationStateTracker` log shows `pending` returns to 0 after batch completes; `BlockInput`/`UnblockInput` are paired 1:1 |
| 14 | Bury animation lost when buried card triggers reactive effects that close the recorder chain | `BuryEffect`, `EffectChainManager`, `RecorderAnimationPlayer` | 2026-06-10 | ✅ | **Card:** grave_punch + slime + start card deck<br>**Check:** Reveal grave_punch (BuryNextXCards). Verify slime plays `PopUpBatch` + `MoveToBottomBatch` animation visibly; console shows `[BuryEffect] Capture request to recorder=chain#...` (not `null`) |
| 15 | GiveSelfStatusEffect missing projectile animation | `StatusEffectGiverEffect`, `RecorderAnimationPlayer` | 2026-06-10 | ⚠️ | **Card:** Any card with GiveSelfStatusEffect (e.g. self-Power)<br>**Check:** Card pops up, projectile flies from self to self, then slots back in; `StatusEffectChange` already captured by `ApplyStatusEffectCore` |
| 29 | GivePowerToCardThatGotPower missing projectile animation | `PowerReactionEffect`, `StatusEffectGiverEffect`, `RecorderAnimationPlayer` | 2026-06-20 | ⚠️ | **Card:** WEAPON_SPIRIT (onFriendlyCardGotPower → GivePowerToCardThatGotPower)<br>**Check:** Target card pops up, projectile flies from source to target, then slots back in; `StatusEffectChange` already captured by `ApplyStatusEffectCore` |
| 30 | PowerReactionEffect nested Power text commits incrementally per projectile | `PowerReactionEffect`, `RecorderAnimationPlayer`, `CardScript`, `AnimationRequest`, `EffectScript`, `ConsumeStatusEffect` | 2026-06-20 | ⚠️ | **Card:** SACRIFICIAL_SWORD + POWER_CRAVER + WEAPON_SPIRIT in deck.<br>**Check:** After SACRIFICIAL_SWORD's projectile lands, POWER_CRAVER's text shows 1 Power. After WEAPON_SPIRIT's reaction projectile lands, text updates to 2 Power. The reaction's Power is not visible before its projectile arrives. |
| 31 | AmplifyStatusEffectGain missing projectile animation | `StatusEffectAmplifierEffect`, `StatusEffectGiverEffect`, `RecorderAnimationPlayer` | 2026-06-21 | ⚠️ | **Card:** Any card with `StatusEffectAmplifierEffect` on `onMeGotStatusEffect` (e.g. self-Power amplifier).<br>**Check:** When the amplifier triggers, the card pops up, projectile flies in, then slots back in; `StatusEffectChange` already captured by `ApplyStatusEffectCore` inside `GiveSelfStatusEffect`. |
| 20 | JU_ON consumed by PREMATURE then Staged slots back to wrong position | `StageEffect`, `BuryEffect`, `RecorderAnimationPlayer`, `ApplyAnimationResult` | 2026-06-13 | ⚠️ | **Card:** PREMATURE + JU_ON in enemy deck<br>**Check:** PREMATURE reveals and consumes JU_ON's curse Power; JU_ON pops up from original deck position, projectile flies to `statusEffectConsumePos`, slots back to original index, then Stage arc moves it to deck top. No zero-distance SlotIn or misplaced landing. |
| 33 | Off-reveal card activations have no visual cue before emphasize/shake | `RecorderAnimationPlayer`, `CombatUXManager`, `CardPhysObjScript`, `EffectRecorder`, `EffectChainManager`, `CostNEffectContainer` | 2026-06-21 | ⚠️ | **Card:** Any reactive deck effect (e.g. BOOSTER afterShuffle→StageSelf, WEAPON_SPIRIT onFriendlyCardGotPower) or a cost-fail reaction from a deck card.<br>**Check:** Success activations: popup -> emphasize -> slotin -> effect animation. Cost-fail activations: popup -> shake -> slotin (no emphasize). Reveal-zone cards do not popup. `AnimationStateTracker.pending` returns to 0 after popup/slot-in. |

## Status Effect Consumption Animation

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 16 | ConsumeOwnStatusEffect missing projectile animation | `ConsumeStatusEffect`, `AnimationRequest`, `RecorderAnimationPlayer`, `CombatUXManager`, `ICombatVisuals` | 2026-06-12 | ⚠️ | **Card:** OVERCHARGED_SUMMONER (Power×1), DR_MANHATTAN (Power×4), ADVANCE_PORTAL (Counter×2), ALMIGHTY (Counter×2), SLIME (Counter×2)<br>**Check:** Revealed card pops up, projectile flies from card toward `statusEffectConsumePos`, status text updates after projectile lands, then card slots back in. No freeze or missing SlotIn. |
| 17 | Status effect projectiles do not reflect stack count | `AnimationRequest`, `ICombatVisuals`, `RecorderAnimationPlayer`, `CombatUXManager`, `StatusEffectGiverEffect`, `CurseEffect`, `ConsumeStatusEffect` | 2026-06-12 | ⚠️ | **Card:** Any card that gives/consumes multiple status effect layers (e.g. DR_MANHATTAN consumes 4 Power, a card giving Power×3 to all friendly cards)<br>**Check:** One projectile spawns per status effect layer; start positions are randomized within `projectileStartRandomOffsetRange`; launch times are staggered within `projectileStartTimeStaggerRange`; animation waits until the last projectile lands before SlotIn/StatusEffectChange commit. Total projectiles respect `maxProjectilesPerRequest`. |
| 18 | ConsumeRandomEnemyCardsStatusEffect has no projectile and queues per target | `ConsumeStatusEffect`, `EffectScript`, `RecorderAnimationPlayer`, `CombatUXManager` | 2026-06-13 | ⚠️ | **Card:** POWER_TRANSFER (Power×2) with multiple enemy Power cards in deck/reveal zone<br>**Check:** All target enemy cards pop up together; projectiles fly from each target back to the source card in parallel (absorb); status text updates after projectiles land; all targets slot back in together. No freeze or missing SlotIn. |
| 19 | ConsumeHostileCursePower has no PopUp/SlotIn/Projectile animation | `CurseEffect`, `EffectScript`, `RecorderAnimationPlayer`, `CombatUXManager`, `AnimationRequest`, `ICombatVisuals` | 2026-06-13 | ⚠️ | **Card:** CURSE_SUMMONER or PREMATURE when enemy curse cards carry Power<br>**Check:** All target enemy curse cards pop up together; projectiles fly from each target toward `statusEffectConsumePos` in parallel with one projectile per consumed layer; status text updates after projectiles land; all targets slot back in together. Source card does not PopUp separately. No freeze or missing SlotIn. |
| 21 | TransferAllStatusEffectToHostileCurse has no PopUp/SlotIn/Projectile animation | `TransferStatusEffectEffect`, `EffectScript`, `RecorderAnimationPlayer`, `CombatUXManager`, `AnimationRequest` | 2026-06-13 | ⚠️ | **Card:** CROW_CROWD when friendly/hostile cards carry Power and a hostile curse card is present<br>**Check:** Source cards pop up together; target curse card pops up; projectiles fly from each source to the target curse card **in parallel**; target curse status text commits only after the **last** projectile lands; source cards and target curse card slot back in together. No freeze or missing SlotIn. |
| 22 | TransferOneStatusEffectToSelf source cards do not PopUp/SlotIn | `TransferStatusEffectEffect`, `EffectScript`, `RecorderAnimationPlayer`, `CombatUXManager`, `AnimationRequest` | 2026-06-13 | ⚠️ | **Card:** POWER_SIPHONER when friendly/hostile cards carry the target status effect<br>**Check:** Source cards pop up together; self card pops up; projectiles fly from each source to self **in parallel**; self card status text commits only after the **last** projectile lands; source cards and self card slot back in together. No freeze or missing SlotIn. |
| 24 | Single-layer status effect projectile has random start offset | `CombatUXManager` | 2026-06-14 | ⚠️ | **Card:** Any card that gives/consumes exactly 1 status effect layer (e.g. self-Power×1, ConsumeOwnStatusEffect×1, GiveStatusEffect×1 to a single target)<br>**Check:** Projectile starts at the center of the source card (`effectiveOffsetRange == Vector2.zero`) and flies straight to the target; no visible XY jitter. Multi-layer effects still randomize. |
| 34 | Consume/transfer source cards update status text when projectile spawns | `RecorderAnimationPlayer`, `AnimationRequest`, `EffectScript`, `ConsumeStatusEffect`, `TransferStatusEffectEffect` | 2026-06-21 | ⚠️ | **Card:** OVERCHARGED_SUMMONER (ConsumeOwnStatusEffect), POWER_TRANSFER (ConsumeRandomEnemyCardsStatusEffect), CROW_CROWD/POWER_SIPHONER (TransferStatusEffectEffect)<br>**Check:** Source/target cards losing status effects show updated text the moment the projectile is spawned (flight starts). Cards receiving status effects still update only after the projectile lands. |

---

## Card Description & Dynamic Damage

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 32 | Dynamic damage `<dmg>` placeholder shows as raw text during attack animation | `CardScript`, `RecorderAnimationPlayer` | 2026-06-21 | ⚠️ | **Card:** UNFINISHED_ROBOT (or any card with `<dmg>` in description that also receives a pending StatusEffectChange during its attack animation).<br>**Check:** During the attack animation, the card description shows the resolved number (e.g. `0 (+1)`) instead of `<dmg>`; Console shows no `[DynamicDamageDisplay] ... raw <dmg> ...` warnings. |

---

## Quick Search

Before editing any code in `Effects/`, `UXPrototype/`, or `Managers/Animation*.cs`:

1. Grep for `VISUAL-FIX` in the files you modified
2. Find the matching scenario number above
3. Confirm you did not break the **Regress** scenario

### Search by File

| File(s) | Related Rows |
|---------|-------------|
| `BuryEffect.cs` | 1, 2, 5, 7, 14 |
| `StageEffect.cs` | 1, 2, 5, 7 |
| `CombatUXManager.cs` | 1, 3, 4, 6, 7, 12, 13, 17, 19 |
| `EffectChainManager.cs` | 2, 11 |
| `CardPhysObjScript.cs` | 3, 12 |
| `CurseEffect.cs` | 8, 17, 19 |
| `AddTempCard.cs` | 4 |
| `RecorderAnimationPlayer.cs` | 6, 9, 11, 13, 17, 19, 33 |
| `ApplyAnimationResult` | 5 |
| `CalculateAnimationPositionAtIndex` | 7 |
| `CostResultPresenter.cs` | 9 |
| `CostNEffectContainer.cs` | 9, 33 |
| `EffectRecorder.cs` | 33 |
| `AnimationRequest.cs` | 9, 16, 17, 19, 30 |
| `CardScript.cs` | 30, 32 |
| `CombatManager.cs` | 10, 11, 12 |
| `ConsumeStatusEffect.cs` | 16, 17, 18, 30 |
| `ICombatVisuals.cs` | 16, 17, 19 |
| `NullCombatVisuals.cs` | 16, 17, 19 |
| `NullCombatVisualsBehaviour.cs` | 16, 17, 19 |
| `StatusEffectGiverEffect.cs` | 17 |
| `PowerReactionEffect.cs` | 30 |
| `EffectScript.cs` | 18, 19, 30 |

## Lifecycle & Destroy Guards

| # | Scenario | System / Effect | Fixed Date | Status | Verification |
|---|----------|-----------------|------------|--------|--------------|
| 26 | `MissingReferenceException` when exiting combat while effect animations are still playing | `CombatManager`, `PhaseManager`, `EffectChainManager`, `RecorderAnimationPlayer` | 2026-06-19 | ⚠️ | **Step:** Trigger a combat-ending effect (e.g. last revealed card kills enemy) while `isPlayingEffectAnimations == true`, or spam-click/auto-reveal through combat end.<br>**Check:** PhaseManager waits for `isPlayingEffectAnimations == false` before calling `ExitCombat()`; `HandleCombatFinished()` does not set `combatFinished` until animations complete; `CloseOpenedChain()` and `RecorderAnimationPlayer` skip destroyed cards/recorders instead of crashing; console shows no `MissingReferenceException` from `PlayRecorderAnimationsAndWait`. |
| 27 | Combat-scoped value trackers leak stale values into the shop phase | `PhaseManager`, `ValueTrackerManager` | 2026-06-19 | ⚠️ | **Step:** Finish a combat where trackers such as `totalPowerCountInDeckRef`, `ownerCardsBuriedCountRef`, `stagedOwnerRef`, or `lastAppliedStatusEffectAmountRef` are non-zero, then enter the shop phase.<br>**Check:** After `EnteringShopPhase()` runs, all `ValueTrackerManager` IntSOs are reset to their original values and `lastAppliedStatusEffectRef.value` is restored to `valueOg`; no stale combat values affect shop/result logic. |
| 28 | Exiled reveal-zone card causes next card to be triggered mid-flight when autoReveal is on | `CombatUXManager`, `CombatManager`, `RevealCards` | 2026-06-19 | ⚠️ | **Card:** Any Exile card that exiles the currently revealed card (e.g. ExileSelf) with `autoReveal = true`.<br>**Check:** After the exiled card's Destroy animation completes, the next card auto-reveals and flies to reveal zone; input and auto-trigger are blocked until the card fully reaches reveal zone. Verify normal manual reveal and Round Start Start Card shuffle still behave correctly. |
