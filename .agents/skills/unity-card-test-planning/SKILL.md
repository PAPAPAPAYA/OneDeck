---
name: unity-card-test-planning
last_reviewed: never
description: Generate structured test plans for Unity card prefabs in the OneDeck project. Use when the user asks to evaluate, test, or plan testing for a specific card's effect functionality, or wants to verify that a card prefab behaves correctly.
---

# Unity Card Test Planning

This skill provides a reusable workflow for analyzing any card prefab and producing a complete test plan document.

## Prerequisites

- The project uses the `unity-read-prefab-serialized` skill (already available under `.agents/skills/unity-read-prefab-serialized/`).
- Card prefabs are located under `Assets/Prefabs/Cards/`.

## Workflow

### Step 1: Extract Prefab Serialized Data

Use `execute_code` (default `compiler: "auto"`, resolves to Roslyn / C# 12+) to read the target prefab programmatically.

**Components to inspect:**
- `CardScript` – `cardTypeID`, `cardDesc`, `isMinion`, cost fields (`buryCost`, `delayCost`, `exposeCost`, `minionCostCount`), `myStatusEffects`, `myTags`
- `CostNEffectContainer`(s) – `checkCostEvent`, `preEffectEvent`, `effectEvent` bindings
- `GameEventListener`(s) – which `GameEvent` triggers which `CostNEffectContainer`
- Effect components (e.g., `HPAlterEffect`, `ShieldAlterEffect`, `BuryEffect`, etc.) – public fields like `baseDmg`, `extraDmg`, `isStatusEffectDamage`, `statusEffectToCheck`

> **Compiler note:** Roslyn (C# 12+) is the default `execute_code` compiler since 2026-07-18 — `$""` interpolation, `?.`, and file-level `using` all work. Only if the `codedom` (C# 6) fallback is ever needed: use fully-qualified names and explicit null checks.

### Step 2: Trace the Effect Chain in Code

Read the relevant source files to understand the exact logic:

| If the effect is... | Read these scripts |
|---------------------|-------------------|
| Damage / Heal | `Assets/Scripts/Effects/HPAlterEffect.cs` |
| Shield | `Assets/Scripts/Effects/ShieldAlterEffect.cs` |
| Bury / Stage / Delay / Exile | `Assets/Scripts/Effects/BuryEffect.cs`, `StageEffect.cs`, `ExileEffect.cs`, etc. |
| Cost check | `Assets/Scripts/Card/CostNEffectContainer.cs` |
| Status-effect-based counts | `Assets/Scripts/Managers/ValueTrackerManager.cs` |
| Combat state | `Assets/Scripts/Managers/CombatManager.cs` |
| Event rules | `Assets/Scripts/Managers/GameEventStorage.cs` |

While reading, identify:
- **Trigger condition** (which event starts the chain)
- **Cost checks** (what prevents the effect from firing)
- **Pre-effect actions** (e.g., bury cost consumes cards before the effect)
- **Damage / effect formula** (base values, additive modifiers, dynamic counts)
- **Scope boundaries** (does the logic scan `combinedDeckZone` only, or include `revealZone`? Graveyard?)
- **Faction perspective** (does the method switch behavior based on `ownerPlayerStatusRef` vs `enemyPlayerStatusRef`?)

### Step 3: Identify Test-Critical Behaviors

For the analyzed card, write down:

1. **Boundary conditions** – zero targets, minimum cost, empty deck.
2. **Dynamic count cards** – cards whose effect scales with deck composition (e.g., "deal damage equal to friendly card count"). Always test:
   - Counting does / does not include `revealZone`.
   - Counting does / does not include the card itself.
   - Counting does / does not include neutral / Start Cards.
3. **Status-effect interactions** – Power (+1 damage), Rest (skip), Infected, Counter, etc.
4. **Enemy perspective** – instantiate the card for the enemy and verify the mirrored logic.
5. **Cost failure paths** – verify the card gracefully does nothing (or shows a message) when cost is unpaid.
6. **Double-application bugs** – e.g., `HPAlterEffect` automatically adds `baseDmg.value`; confirm the prefab's `extraDmg` is configured so the intended final number is correct.

### Step 4: Design Test Cases

Structure tests into two strategies:

> **Which strategy when:** see the decision table in `.agents/skills/unity-card-playmode-test/SKILL.md` Section 1.1 (Strategy B vs. EditMode NUnit) — the single source of truth for this choice.

#### Strategy A – Programmatic Unit Test (Editor Mode)
Directly invoke the effect method after constructing a controlled `CombatManager` state. This is the fastest way to validate arithmetic.

Design a table with columns:
- ID
- Scenario description
- Deck setup (`combinedDeckZone`, `revealZone`)
- Expected result (damage number, HP change, shield change, etc.)
- Validation point (what specific behavior is being proven)

#### Strategy B – Play Mode Integration Test
Run the real combat scene and observe the full flow (event binding, animation, logs, UI updates).

Design verification points for:
- Event binding correctness
- Animation queue behavior
- Console / combat-log output
- Final player state consistency

### Step 5: Output the Document

Write the complete test plan to `docs/<CardName>_TestPlan.md` (or a user-specified path).

Use the template in [references/test-plan-template.md](references/test-plan-template.md) as the baseline structure. Fill in:
- Card Overview
- Implementation Chain
- Damage / Effect Formula
- Important Implementation Details
- Test Strategies (A and B)
- Test Case Tables
- Key Script Quick-Reference

---

## Reference

- **Test plan markdown template:** [references/test-plan-template.md](references/test-plan-template.md)
- **Prefab reader skill:** `.agents/skills/unity-read-prefab-serialized/SKILL.md`
