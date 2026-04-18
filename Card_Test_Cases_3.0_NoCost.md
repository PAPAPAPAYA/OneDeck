# Card Functional Test Cases - 3.0 No Cost (Current)

Auto-generated from prefabs in `Assets/Prefabs/Cards/3.0 no cost (current)`.
Focus: effect functionality only.

## ADVANCE_PORTAL

### Effect Container 1 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify target's stage changes by ?.

## ALL_FOR_ONE

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: -2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + -2 damage (include Power buff if applicable).

## ALMIGHTY

### Effect Container 1 (Cost: None (0))

- **StageEffect**
  - `statusEffectToCheck`: None (0)
  - `tagToCheck`: 0
  - `targetFriendly`: Player (0)
- **BuryEffect**
  - `tagToCheck`: 0
- **AddTempCard**
  - `cardCount`: 1
  - `curseCardTypeID`: ref:11400000
- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
  - `powerCoefficient`: 1

**Functional Test Cases:**
- Verify 1 temporary card(s) are added.
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify a curse card is inserted into the target deck.
- Verify a temporary card is spawned and placed in the correct zone/deck.
- Verify target's stage changes by ?.

## ANTI_CREATURE_WEAPON

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

## BLACKSMITH

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)
- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).
- Verify target's stage changes by ?.

## BLACKSMITH

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 0
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 1
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify effect applies to 1 friendly card(s).
- Verify status effect `Power (4)` is given to target card(s).

## BLIND_COMBAT_PRIEST

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 1
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 3
  - `statusEffectToCount`: Power (4)
  - `statusEffectToGive`: None (0)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 3 stack(s) of the status effect are applied.
- Verify effect applies to the last 1 revealed card(s).
- Verify status effect `None (0)` is given to target card(s).

## BODY_CANON

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

## BODY_CANON

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 0
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 0 damage (include Power buff if applicable).

## BODY_CANON

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `statusEffectToCheck`: None (0)
  - `tagToCheck`: 0
  - `targetFriendly`: Player (0)

**Functional Test Cases:**
- Verify target's stage changes by ?.

## BONE_COMBINATION

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: -1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + -1 damage (include Power buff if applicable).

## BOOSTER

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `statusEffectToCheck`: None (0)
  - `tagToCheck`: 0
  - `targetFriendly`: Player (0)

**Functional Test Cases:**
- Verify target's stage changes by ?.

## COFFIN_MAKER

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)
- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

## CROW_CROWD

### Effect Container 1 (Cost: None (0))

- **TransferStatusEffectEffect**
  - `curseCardTypeID`: ref:11400000
  - `isFromFriendly`: Enemy (1)
  - `particleYOffset`: 0
  - `statusEffectToTransfer`: Power (4)

**Functional Test Cases:**
- Verify status effect `Power (4)` is transferred correctly.

## CURSE_CORPSE

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: -1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.
- Verify target takes baseDmg + -1 damage (include Power buff if applicable).

## CURSE_SUMMONER

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.
- Verify target's stage changes by ?.

## CURSE_SUMMONER

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.

## CURSE_SUMMONER

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.
- Verify target's stage changes by ?.

## CURSE_THIRST_BEAST

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify target's stage changes by ?.

## CURSE_THIRST_SHARMAN

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is given to target card(s).

## DEATHBED_CURSE

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: JU_ON
  - `statusEffectParticlePrefab`: ref:385316958995927269

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.
- Verify inserted curse card type is `JU_ON`.

## DETERIORATION

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
  - `powerCoefficient`: 2

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.

## DR_MANHATTON

### Effect Container 1 (Cost: None (0))

- **ConsumeStatusEffect**
  - `statusEffectToConsume`: Power (4)
- **StageEffect**
  - `tagToCheck`: 0
- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify status effect `Power (4)` is consumed/removed from target.
- Verify target's stage changes by ?.

## ELDER_SOURCEROR

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is given to target card(s).

## ETERNAL_GHOST

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: -1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + -1 damage (include Power buff if applicable).

## ETERNAL_GHOST

### Effect Container 1 (Cost: None (0))

- **PowerReactionEffect**
  - `canStatusEffectBeStacked`: 1
  - `excludeSelf`: 0
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `powerAmount`: 1
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: None (0)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify Power reaction triggers correctly (e.g., on reveal or on damage).

## FALL_INTO_RIFT

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0
- **MinionCostEffect**
  - (no additional serializable fields)

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify cost consumes ? Minion(s) of type `any`.

## FLESH_COMBINATION

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: -2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + -2 damage (include Power buff if applicable).

## GOBLIN_ASSASIN_TEAM

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

### Effect Container 2 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

## GRAVE_INVITATION

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

## GRAVE_KEEPER

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 4
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 4 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `statusEffectToCheck`: None (0)
  - `tagToCheck`: 0
  - `targetFriendly`: Player (0)

**Functional Test Cases:**
- Verify target's stage changes by ?.

## GRAVE_PORTAL

### Effect Container 1 (Cost: None (0))

- **StageEffect**
  - `statusEffectToCheck`: None (0)
  - `tagToCheck`: 0
  - `targetFriendly`: Player (0)

**Functional Test Cases:**
- Verify target's stage changes by ?.

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `statusEffectToCheck`: None (0)
  - `tagToCheck`: 0
  - `targetFriendly`: Player (0)

**Functional Test Cases:**
- Verify target's stage changes by ?.

## GRAVE_PUNCH

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

### Effect Container 3 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

## GRAVE_TOGETHER

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

### Effect Container 2 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

## GRUDGE

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 2
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 2
  - `statusEffectParticlePrefab`: ref:385316958995927269
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 2 stack(s) of the status effect are applied.
- Verify effect applies to the last 2 revealed card(s).
- Verify status effect `Power (4)` is given to target card(s).

## GRUDGE

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **AddTempCard**
  - `cardCount`: 1

**Functional Test Cases:**
- Verify 1 temporary card(s) are added.
- Verify a temporary card is spawned and placed in the correct zone/deck.

### Effect Container 3 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: 7
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `7` is given to target card(s).

## JU_ON

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: -2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: Power (4)

**Functional Test Cases:**
- Verify target takes baseDmg + -2 damage (include Power buff if applicable).

## LARGE_SCALE_DEATH

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

## MAD_SCIENTIST

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 3
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 2
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 2 stack(s) of the status effect are applied.
- Verify effect applies to the last 3 revealed card(s).
- Verify status effect `Power (4)` is given to target card(s).

## MARTYR

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectParticlePrefab`: ref:385316958995927269
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is given to target card(s).

## MOTH_MAN

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.

## MOTH_MAN

### Effect Container 1 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify target's stage changes by ?.

## OVERCHARGED_SUMMONER

### Effect Container 1 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0
- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 2
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify effect applies to the last 2 revealed card(s).
- Verify status effect `Power (4)` is given to target card(s).
- Verify target's stage changes by ?.

## POISNER

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

## POWER_CRAVER

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StatusEffectAmplifierEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectMultiplier`: 3
  - `statusEffectToCount`: Power (4)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify status effect `Power (4)` is amplified correctly.

## POWER_SIPHONER

### Effect Container 1 (Cost: None (0))

- **TransferStatusEffectEffect**
  - `curseCardTypeID`: ref:11400000
  - `isFromFriendly`: Enemy (1)
  - `particleYOffset`: 0
  - `statusEffectToTransfer`: Power (4)
- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 0
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify status effect `Power (4)` is transferred correctly.
- Verify target takes baseDmg + 0 damage (include Power buff if applicable).

## POWER_TRANSFER

### Effect Container 1 (Cost: None (0))

- **ConsumeStatusEffect**
  - `statusEffectToConsume`: Power (4)
- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is consumed/removed from target.
- Verify status effect `Power (4)` is given to target card(s).

## PREMATURE

### Effect Container 1 (Cost: None (0))

- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify a curse card is inserted into the target deck.
- Verify target's stage changes by ?.

## PROLIFERATING_CURSE

### Effect Container 1 (Cost: None (0))

- **AddTempCard**
  - `cardCount`: 1
  - `curseCardTypeID`: ref:11400000

**Functional Test Cases:**
- Verify 1 temporary card(s) are added.
- Verify a temporary card is spawned and placed in the correct zone/deck.

## QUICK_RESPONSE_PROTOCAL

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: 7
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `7` is given to target card(s).

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify target's stage changes by ?.

## REVENGER

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 1
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 1 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectParticlePrefab`: ref:385316958995927269
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is given to target card(s).

## RIFT

### Effect Container 1 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0
- **ExileEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify target card(s) are permanently removed from the deck.
- Verify target's stage changes by ?.

## RIFT_DEVOURER

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 0
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0

**Functional Test Cases:**
- Verify target takes baseDmg + 0 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 5
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectParticlePrefab`: ref:385316958995927269
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is given to target card(s).

## RIFT_DRAGON

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
- **MinionCostEffect**
  - (no additional serializable fields)

**Functional Test Cases:**
- Verify cost consumes ? Minion(s) of type `any`.
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

## RIFT_GUIDE

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

## RIFT_INSECT

### Effect Container 1 (Cost: None (0))

- **AddTempCard**
  - `cardCount`: 1

**Functional Test Cases:**
- Verify 1 temporary card(s) are added.
- Verify a temporary card is spawned and placed in the correct zone/deck.

## RIFT_MONSTER

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
- **MinionCostEffect**
  - (no additional serializable fields)

**Functional Test Cases:**
- Verify cost consumes ? Minion(s) of type `any`.
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

## RIFT_SUMMONER

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

## RIFT_SUMMONER

### Effect Container 1 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0
- **MinionCostEffect**
  - (no additional serializable fields)

**Functional Test Cases:**
- Verify cost consumes ? Minion(s) of type `any`.
- Verify target's stage changes by ?.

## SACRIFICE_RITUAL

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0
- **AddTempCard**
  - `cardCount`: 2

**Functional Test Cases:**
- Verify 2 temporary card(s) are added.
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify a temporary card is spawned and placed in the correct zone/deck.

## SACRIFICIAL_SPIRIT

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0
- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify a curse card is inserted into the target deck.

## TACTICAL_BREACHER

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is given to target card(s).

### Effect Container 2 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

## THE_FOOL

### Effect Container 1 (Cost: None (0))

- **HPAlterEffect**
  - `baseDmg`: default baseDmg SO
  - `dmgAmountAlter`: 0
  - `extraDmg`: 2
  - `healAmountAlter`: 0
  - `isStatusEffectDamage`: 0
  - `statusEffectToCheck`: None (0)

**Functional Test Cases:**
- Verify target takes baseDmg + 2 damage (include Power buff if applicable).

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `statusEffectToCheck`: Power (4)
  - `tagToCheck`: 0
  - `targetFriendly`: Player (0)

**Functional Test Cases:**
- Verify target's stage changes by ?.

## UNFINISHED_ROBOT

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 0
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 1
  - `statusEffectToCount`: Power (4)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 1 stack(s) of the status effect are applied.
- Verify status effect `Power (4)` is given to target card(s).

## UNSTABLE_PORTAL

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.

### Effect Container 2 (Cost: None (0))

- **StageEffect**
  - `tagToCheck`: 0

**Functional Test Cases:**
- Verify target's stage changes by ?.

## WISE_BURIAL

### Effect Container 1 (Cost: None (0))

- **StatusEffectGiverEffect**
  - `canStatusEffectBeStacked`: 1
  - `includeSelf`: 0
  - `lastXCardsCount`: 2
  - `particleYOffset`: 0
  - `spreadEvenly`: 0
  - `statusEffectLayerCount`: 2
  - `statusEffectParticlePrefab`: ref:385316958995927269
  - `statusEffectToCount`: None (0)
  - `statusEffectToGive`: Power (4)
  - `target`: Player (0)
  - `xFriendlyCount`: 0
  - `yFriendlyLayerCount`: 1

**Functional Test Cases:**
- Verify 2 stack(s) of the status effect are applied.
- Verify effect applies to the last 2 revealed card(s).
- Verify status effect `Power (4)` is given to target card(s).

## WISE_BURIAL

### Effect Container 1 (Cost: None (0))

- **BuryEffect**
  - `tagToCheck`: 0
- **CurseEffect**
  - `cardPrefab`: ref:1808145013887896563
  - `cardTypeID`: ref:11400000
  - `particleYOffset`: 0
  - `powerCoefficient`: 1

**Functional Test Cases:**
- Verify ? target card(s) are moved to the bottom of the deck.
- Verify a curse card is inserted into the target deck.
