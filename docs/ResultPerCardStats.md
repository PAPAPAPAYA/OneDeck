# Result Screen Per-Card Stats

(Moved from AGENTS.md on 2026-09-05 during the size diet; that file keeps a condensed summary.)

Two-half panel (player-created top / enemy-created bottom) for the combat that just finished (plan: `plans/plan-result-per-card-stats-2026-07-23.md`).

- **Store**: `CombatPerCardStatsTracker.Me` (auto-created by `CombatManager.Awake()`), session-scoped: `BeginSession()` wipes all state in `GatherDecks()`. Rows keyed `(cardTypeID, creatorSide)` — the faction that CREATED the card; initial deck via `RegisterDeckComposition(combinedDeckZone)`, mid-combat via `RegisterGeneratedCard` (funneled through `CombatFuncs.AddCard_TargetSpecific`); both paths pre-create all-zero rows.
- **Exclusions** (`EnsureRecord` — the single exclusion point): neutral/start cards (`IsNeutralCard`) and `IsUtilityPassive` (passives have no combat effect chain — would only ever be all-zero rows).
- **Damage** = actual HP lost (ProcessDamage delta), creator-relative: `DamageDealtToOpponent` when the victim opposes the creator, else `DamageDealtToSelf`. Bury/Stage split by SOURCE's owner; neutral victims count neither side.
- **Hooks**: `CostNEffectContainer.InvokeEffectEvent()`, `HPAlterEffect.CheckDmgTargets_DealingDmgToOpponent/Self`, `EffectScript.ApplyStatusEffectCore` Power branch, `BuryEffect`/`StageEffect` moved-cards loops.
- **UI**: `ResultStatsPanel` builds itself fully at runtime (no prefab/scene wiring); `PhaseManager.resultStatsPanelLayout` tunes it (Play Mode Inspector edits rebuild via OnValidate). `Rounds: N` = `roundsLastCombat - 1` — the start card's opening shuffle is not a round.
- **Pitfall**: after setting stretch anchors on a fresh RectTransform, always zero `offsetMin/offsetMax` — the default 100×100 sizeDelta otherwise leaks into the final rect.
- EditMode tests: `Assets/Scripts/Editor/Tests/CombatPerCardStatsTrackerTests.cs`.
