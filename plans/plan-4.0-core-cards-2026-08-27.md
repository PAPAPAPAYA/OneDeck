# Plan: 4.0 Core Pool Prefab Configuration (2026-08-27)

Goal: configure the 24-card playable core of the 4.0 pool in Unity. Source of truth: Notion
`4.0 card database` (107 rows) + `docs/4.0_CardDesc_Spec.md`. No code changes required — every
card in this list maps to existing components.

Prerequisite: the session must have `unityMCP` tools (bridge `http://127.0.0.1:8080/mcp` is live;
tools register on session start). All reads/writes of prefab serialized data go through
`execute_code`, per `.agents/skills/unity-read-prefab-serialized`.

## Target Layout

```
Assets/Prefabs/Cards/4.0/
├── 0_Common/      # rarity = normal
├── 1_Uncommon/    # rarity = uncommon
└── 2_Rare/        # rarity = rare
```

Rarity mapping: DB `normal/uncommon/rare` → folder `0/1/2` → `CardScript.rarity` 0/1/2.

## Prefab Anatomy (verified against SPIKE_SKELETON / RIFT_INSECT)

- Root GameObject: `CardScript` + N × `GameEventListener`.
- One child GameObject per effect clause: `CostNEffectContainer` + effect component
  (`AttackEffect` / `BuryEffect` / `AddTempCard` / `CurseEffect` / `AttackGiverEffect` / …).
- Listener: `event` = GameEvent SO; `response` → `CostNEffectContainer.InvokeEffectEventVoid`
  (one listener may invoke several containers — see RIFT_INSECT reveal).
- Container child: `effectEvent` → effect method with serialized args; optional
  `checkCostEvent` → `CostNEffectContainer.CheckCost_*`.
- Resolved references:
  - `OnMeRevealed` SO guid `b9291c6dab76d934a8dfea097c0df6b4`
    (`Assets/SORefs/GameEvents/REVEAL/OnMeRevealed.asset`)
  - `OnMeBuried` SO guid `dbdfc6da826817e458050a266ddebda9`
  - RIFT token prefab guid `e8e562bae93b8c546a9a5d8c51f25855`
  - default price SO guid `c113456951addca44a8d86642dffd44e`
  - script guids: CardScript `f47b4b127fc943869d9dbca8f00704e8`, GameEventListener
    `3cc6290dc6e64dadb7d801d93a3ba7a2`, CostNEffectContainer `a21da06ba55646f29c59d9dbf90834b3`,
    AttackEffect `9f01314777625ad4bb0bc22828e9728c`, AddTempCard `e8da5b23dfa04f2bb3a8add21531e513`
- `CardScript` fields: `cardTypeID`, `displayName`, `cardDesc`, `rarity`, `takeUpSpace`,
  `isStartCard`, `isMinion`, `myTags` (bitmask; 3 = DeathRattle), `printedAttack`,
  `attackGrowth`, `attackModThisRound`, `extraAttackTimes` (attack xN = extraAttackTimes N−1),
  `price`, `shopRollWeightMultiplier`.
- Prefab `cardDesc` follows the in-game style (`揭晓时:…`, `<b>N</b>` numbers); the normalized
  DB desc is the mechanics source of truth. `displayName` temporarily uses CARD_TYPE_ID until a
  Chinese naming pass.

## Sample Card A — ID69 TWIN_STRIKER (攻击x2, ATK 1, normal)

Build: copy `RIFT_INSECT.prefab` as scaffold, delete the `add 1 [rift]` child, then set fields.

- CardScript: cardTypeID `TWIN_STRIKER`, cardDesc `揭晓时:攻击x2`, rarity 0, printedAttack 1,
  **extraAttackTimes 1** (this is the x2 — `GetAttackTimes() = 1 + extraAttackTimes`).
- Listener: OnMeRevealed → container.
- Child `attack`: CostNEffectContainer → `AttackEffect.Attack()` (no args; segments come from
  the card, not the call).

## Sample Card B — ID9 RIFT_INSECT (攻击;生成1信徒, ATK 3, normal)

Same cardTypeID as the 3.0 card, new 4.0 stats: copy the existing `RIFT_INSECT.prefab` into
`4.0/0_Common/`, then:

- printedAttack 1 → **3**.
- cardDesc → `揭晓时:攻击;生成 <b>1</b> [次元裂缝]` (keep in-game token name until a display pass).
- Everything else unchanged: OnMeRevealed listener already invokes both containers
  (`AttackEffect.Attack` + `AddTempCard.AddCardToMe(RIFT, cardCount 1)`).

## Verification

1. After each prefab: run the listener/desc binding check (`.agents/skills/unity-card-listener-check`).
2. After the two samples: show the user one serialized dump for sign-off before batch work.
3. After the batch: Play Mode spot-checks (Strategy B, `.agents/skills/unity-card-playmode-test`)
   on one card per group: GRAVE_PUNCH (bury+deathrattle chain), RIFT_MONSTER (exile cost),
   DETERIORATION (curse scaling), SNOWBALL (onMeGainedAttack).

## The 24-Card Core List

Groups follow the required supply→payoff loops. "Recipe" lists containers in execution order
per trigger; `Attack` = `AttackEffect.Attack()`.

### Group 1 — 埋葬 + 遗言 (bury fuels deathrattle)

| ID | CARD_TYPE_ID | desc (normalized) | ATK | rarity | Recipe |
|----|--------------|-------------------|-----|--------|--------|
| 1 | SPIKE_SKELETON | 攻击;遗言:攻击x2 | 1 | uncommon | already configured — reference only |
| 10 | GRAVE_FIST | 埋葬1友方;攻击 | 3 | normal | reveal: BuryMyCards(1) + Attack |
| 34 | GRAVE_TOGETHER | 攻击;埋葬1友方;埋葬2敌方 | 2 | normal | reveal: Attack + BuryMyCards(1) + BuryTheirCards(2) |
| 56 | GRAVE_PUNCH | 埋葬1友方;攻击x2 | 2 | uncommon | extraAttackTimes 1; reveal: BuryMyCards(1) + Attack |
| 17 | GRAVE_MILLER | 攻击;埋葬2友方,埋葬卡组顶5卡 | 2 | rare | reveal: Attack + BuryMyCards(2) + BuryNextXCards(5) |
| 30 | GRAVE_DREDGER | 攻击;埋葬卡组顶3卡 | 2 | normal | reveal: Attack + BuryNextXCards(3) |
| 33 | SLIME | 遗言:复制自身;攻击 | 2 | rare | myTags DeathRattle; buried: AddSelfToMe + Attack |
| 37 | EULOGIST | 埋葬1友方遗言卡;攻击 | 2 | uncommon | reveal: BuryMyCardsWithTag(DeathRattle,1) + Attack |

### Group 2 — 信徒 + 放逐 (believers fuel exile payoffs; believer = revive engine)

| ID | CARD_TYPE_ID | desc (normalized) | ATK | rarity | Recipe |
|----|--------------|-------------------|-----|--------|--------|
| 9 | RIFT_INSECT | 攻击;生成1信徒 | 3 | normal | sample B |
| 47 | RIFT_TWINS | 攻击;生成2信徒 | 1 | normal | reveal: Attack + AddCardToMe(RIFT,2) |
| 8 | RIFT_STRIKER | 攻击x2;生成1信徒 | 1 | uncommon | extraAttackTimes 1; reveal: Attack + AddCardToMe(RIFT,1) |
| 29 | RIFT_HATCHERY | 回合开始:埋葬自身;生成3信徒 | — | uncommon | beforeRoundStart: BurySelf + AddCardToMe(RIFT,3) |
| 63 | RIFT_PRIEST | 生成2信徒;强化1友方 | — | uncommon | reveal: AddCardToMe(RIFT,2) + AttackGiver XFriendly(1,1) |
| 31 | RIFT_MONSTER | 放逐1信徒,攻击x3 | 1 | uncommon | extraAttackTimes 2; reveal: CheckCost_HasOwnCardOfType(RIFT,1) + ExileMyCardsWithTypeID(RIFT,1) + Attack |

Believer token: existing `RIFT` prefab (stage-approximation of 复活1友方 until ReviveEffect
lands; no awaken card exists in this pool, so nothing breaks).

### Group 3 — 诅咒 (feed the enemy curse; revive-curse leg waits for ReviveEffect)

| ID | CARD_TYPE_ID | desc (normalized) | ATK | rarity | Recipe |
|----|--------------|-------------------|-----|--------|--------|
| 90 | HEXER | 攻击;强化4敌方诅咒 | 0 | uncommon | reveal: Attack + EnhanceCurse(4) |
| 50 | DETERIORATION | 强化2敌方诅咒;敌方诅咒每有2攻击力,额外强化1 | — | rare | template: existing prefab (EnhanceCurse(2) + EnhanceCurseWithCoefficient_BasedOnIntSO) |
| 83 | SACRIFICIAL_SPIRIT | 攻击;埋葬2友方;强化5敌方诅咒 | 0 | uncommon | reveal: Attack + BuryMyCards(2) + EnhanceCurse(5) |

Curse token: existing `JU_ON` prefab (guid resolved at build time). ATK-0 note: `Attack()`
no-ops at attack 0 and fires no attack event — accepted for HEXER/SACRIFICIAL_SPIRIT (their
攻击 clause is decorative); if a 0-attack action should still raise attack events, that is an
engine decision for the ReviveEffect plan (see ID84 note in the spec).

### Group 4 — 强化 + 多次攻击 (attack buffs multiply on multi-hit)

| ID | CARD_TYPE_ID | desc (normalized) | ATK | rarity | Recipe |
|----|--------------|-------------------|-----|--------|--------|
| 87 | BLACKSMITH | 攻击;强化1友方 | 2 | normal | reveal: Attack + AttackGiver XFriendly(1,1) |
| 79 | WAR_TRAINER | 强化2友方 | — | normal | reveal: AttackGiver XFriendly(2,1) |
| 84 | UNFINISHED_ROBOT | 攻击;攻击力翻倍 | 0 | rare | reveal: Attack + AttackGiver DoubleSelfAttack |
| 69 | TWIN_STRIKER | 攻击x2 | 1 | normal | sample A |
| 96 | QUAD_STRIKER | 攻击x4 | 1 | rare | extraAttackTimes 3; reveal: Attack |
| 92 | SNOWBALL | 攻击x2;被强化:强化自身1 | 2 | uncommon | extraAttackTimes 1; reveal: Attack; gainedAttack: GiveSelfAttack(1) |
| 55 | COMBO_WARRIOR | 攻击;被强化:强化自身1 | 2 | uncommon | reveal: Attack; gainedAttack: GiveSelfAttack(1) |

## Execution Order

1. Create folder + sample A (TWIN_STRIKER) + sample B (RIFT_INSECT 4.0) → user sign-off on the
   serialized dumps.
2. Batch 1 (vanilla/simple): 96, 69(done), 9(done), 47, 30, 10, 79, 87.
3. Batch 2 (bury group): 34, 56, 17, 37, 33.
4. Batch 3 (rift group): 8, 29, 63, 31.
5. Batch 4 (curse + buff-reactive): 90, 50, 83, 84, 92, 55.
6. Verification pass (listener-check on all 24; Strategy B spot-checks ×4).

Out of scope this round: ReviveEffect / awaken / passive relics / attack-time granting /
round-end events (engine plan), rarity for the other 83 cards, displayName/cardDesc display pass.
