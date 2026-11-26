# Plan: Attack-Buff Full-Chain Consistency (2026-11-26)

Status: IMPLEMENTED (this commit). Supersedes the F4 follow-ups listed in the 2026-11-26
display-guard commit (d3019ea's RegressionChecklist row 158).

## Problem

Masked-attack cards (dynamic resolver: GRAVE_GIANT / ALL_FOR_ONE / FLESH_COMBINATION /
ALMIGHTY / CURSE_EATER / MIMIC_BLADE; GRAVE_ROBBER snapshot hold; RELIC_GRAVE_CURSE override)
skip `attackGrowth` / `attackModThisRound` in `GetAttack()`'s override branches, but the rest
of the pipeline still claimed the buff landed: combat log ("给予N点攻击力"), settlement stats
(RecordAttackGiven/Received), attack-gain events (reactions), AttackChange animation capture,
and the `enhancedOnly` / `onlyEnhanced` predicates (raw `attackGrowth > 0` reads). The
display-side guard (d3019ea) fixed only the face.

## User rulings (2026-11-26)

- D1 deterministic-target edges (override-armed curse enhancement, masked self-gain):
  full skip — no projectile/particle at all.
- D2 log: explicit immune line 「对X无效(不受攻击力强化影响)」.
- D3 predicate rename: `AttackDisplayIgnoresBuffs` -> `IgnoresAttackBuffs` (display +
  settlement + selectors share one set).
- D4 grant pools: selector pre-exclusion (masked cards never enter attack-gain candidate
  pools). D4a: drain pools (ConsumeStatusEffect) excluded symmetrically. Pre-exclusion only
  where selection exists; deterministic targets keep the immune-line + full-skip backstop.

## Changes

1. `CardScript.cs`: rename; `ModifyAttack` / `ModifyAttackThisRound` write gates (ledger
   stays 0 for masked cards -> predicates auto-correct, no hidden growth detonation when
   AttackResolverSource unbinds).
2. `EffectScript.cs`: `ApplyAttackCore` returns bool; masked gate skips stats/events/
   growth/AttackChange capture; `LogAttackImmune` line (NOT bound by suppressLog — that only
   suppresses the standard grant line).
3. `AttackGiverEffect.cs`: pre-exclusion in every candidate pool (GiveAttack filter loop,
   GiveFriendlyCardWithMinAttack predicate, GiveAttackToLastXCards loop, CollectFriendlyCards
   consumers via RemoveAll); bool split on deterministic self/target grants.
4. `AttackGainReactionEffect.cs`: capture split by applied.
5. `CurseEffect.cs`: `ApplyAttackToCardInternal` bool; inline capture guarded; curse
   gained-attack raise gated by applied (latent leak, same shape as the COMBO_STARTER one).
6. `GraveRobberEffect.cs`: snapshot clears `attackGrowth` / `attackModThisRound` (pre-snapshot
   buffs must not survive as `onlyEnhanced` residue).
7. `ConsumeStatusEffect.cs`: drain pools pre-exclude masked cards (cannot weaken a
   rule-defined attack).
8. Tests: `AttackBuffConsistencyTests` (gate assertions, ledger clear, predicate pools,
   pre-exclusion, immune log via CombatLog); `AttackDisplayMaskedCardTests` preconditions
   switched to direct `attackGrowth =` writes (ModifyAttack is now a no-op on masked cards).

## Behavior changes / rollout notes

- ELITE_REVIVER (`onlyEnhanced` revive pool) pool contents change -> RNG stream shifts from
  the first affected pick. Old `Seed_*.txt` / `DeterminismDigest_*.txt` files are NOT
  comparable across this version boundary (same-version determinism tests must stay green).
- Pre-exclusion also shifts RngChannel.Target pool sizes for grant/drain effects.
- Armed JU_ON no longer accumulates phantom growth -> drops out of HasAttackAttribute /
  Damager pools when previously (phantom-)buffed; accepted as consistency.
- Settlement stats + combat logs differ (honest no-op).
- docs/Sim4_Approximation_Ledger.md R6 gains a divergence note (Python sim `verb_set_attack`
  absorbed the delta; Unity never writes growth on masked cards).
- Rollback = revert this commit (growth fields are combat-scoped runtime state, no save
  migration).

## Non-goals

- Attack-times axis (GetAttackTimes absorbs times buffs for masked cards — both sides
  agree; AttackTimesGiverEffect untouched).
- GRAVE_ROBBER steal itself (direct snapshot write, untouched).
- `positiveAttackOnly` (reads live GetAttack — already correct).
- `HasAttackAttribute` not covering snapshot/override (pre-existing gap, follow-up).
