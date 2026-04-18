#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""Batch generate test plans for all card prefabs in OneDeck."""

import json
import os
import re
from collections import defaultdict

SCAN_PATH = "Assets/prefab_scan.json"
LIST_PATH = "prefab_list.txt"
OUTPUT_DIR = "docs/TestPlans"

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

def load_scan():
    with open(SCAN_PATH, "r", encoding="utf-8") as f:
        return json.load(f)


def load_name_map():
    """Map cardTypeID -> (chinese_name, relative_path)"""
    mapping = {}
    with open(LIST_PATH, "r", encoding="utf-8") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            rel = line.replace("\\", "/")
            chinese_name = os.path.splitext(os.path.basename(rel))[0]
            mapping[rel] = chinese_name
    return mapping


def find_chinese_name(card, name_map):
    """Try to match path or name from scan data."""
    path = card["path"]
    # Direct match on path suffix
    for rel, cn in name_map.items():
        if path.endswith(rel):
            return cn, rel
    # Fallback: use directory + cardTypeID heuristic
    return card["name"], ""


# ---------------------------------------------------------------------------
# Effect analysers
# ---------------------------------------------------------------------------

def analyse_hp_alter(hp):
    total = hp["baseDmg"] + hp["extraDmg"]
    kind = "damage" if total >= 0 else "heal"
    note = "status-effect based" if hp["isStatusEffectDamage"] else "direct"
    return {
        "total": total,
        "kind": kind,
        "note": note,
        "baseDmg": hp["baseDmg"],
        "extraDmg": hp["extraDmg"],
        "statusEffectToCheck": hp["statusEffectToCheck"],
    }


def describe_container_methods(methods):
    """Human-readable description of effect methods."""
    descs = []
    for m in methods:
        if m in ("", "none", None):
            continue
        if "Decrease" in m and "Hp" in m:
            descs.append("deal damage")
        elif "Increase" in m and "Hp" in m:
            descs.append("heal")
        elif "Bury" in m:
            descs.append("bury cards")
        elif "Stage" in m:
            descs.append("stage cards")
        elif "Exile" in m:
            descs.append("exile cards")
        elif "Shield" in m or "UpMyShield" in m or "UpTheirShield" in m:
            descs.append("grant shield")
        elif "StatusEffect" in m or "Give" in m:
            descs.append("apply status effect")
        elif "Add" in m and "ToMe" in m:
            descs.append("add temporary card")
        elif "Curse" in m:
            descs.append("apply curse")
        else:
            descs.append(m)
    return ", ".join(descs) if descs else "trigger effect"


def infer_trigger_event(card):
    """Infer the primary trigger event from listeners."""
    events = [l["eventName"] for l in card.get("listeners", [])]
    if not events:
        return "OnMeRevealed (inferred)"
    # Prefer OnMeRevealed if present
    for e in events:
        if "Revealed" in e:
            return e
    return events[0]


def has_cost_checks(card):
    for c in card.get("containers", []):
        if c.get("checkCostMethods"):
            return True
    return False


def get_cost_type(card):
    """Detect cost type from checkCostMethods."""
    types = set()
    for c in card.get("containers", []):
        for m in c.get("checkCostMethods", []):
            if "Bury" in m:
                types.add("bury cost")
            elif "Delay" in m:
                types.add("delay cost")
            elif "Expose" in m:
                types.add("expose cost")
            elif "Minion" in m:
                types.add("minion cost")
            elif "Mana" in m:
                types.add("mana cost")
            elif "Rest" in m:
                types.add("rest cost")
            elif "Revive" in m:
                types.add("revive cost")
    return ", ".join(types) if types else "none"


# ---------------------------------------------------------------------------
# Test case generators
# ---------------------------------------------------------------------------

def generate_test_cases(card, hp_infos, shield_infos):
    """Generate Strategy-A test cases based on card data."""
    cases = []
    cid = 0

    def add(scenario, setup, expected, validation):
        nonlocal cid
        cid += 1
        cases.append({
            "id": f"A-{cid}",
            "scenario": scenario,
            "setup": setup,
            "expected": expected,
            "validation": validation,
        })

    # Base case: normal trigger
    add(
        "Normal trigger in standard deck",
        "combinedDeckZone contains 10 mixed cards including target card at revealZone",
        "Effect executes according to card description",
        "Verify effect triggers without exceptions",
    )

    # Cost failure case
    if has_cost_checks(card):
        cost_type = get_cost_type(card)
        add(
            f"Cost not met ({cost_type})",
            f"Insufficient resources to pay {cost_type}",
            "Effect is skipped; no state change occurs",
            "Verify graceful skip when cost is unpaid",
        )

    # HP-related cases
    for hp in hp_infos:
        total = hp["total"]
        kind = hp["kind"]
        if kind == "damage":
            add(
                f"Deal {total} damage",
                "Enemy player has 20 HP, no shield",
                f"Enemy HP reduced by {total}",
                "Verify HPAlterEffect arithmetic: baseDmg + extraDmg = total",
            )
            add(
                "Damage with Power status effect",
                "Enemy player has 20 HP; card gains Power (+1 damage)",
                f"Enemy HP reduced by {total + 1}",
                "Verify Power modifier is applied",
            )
        else:
            add(
                f"Heal {abs(total)} HP",
                "Friendly player has 10 HP (max 20)",
                f"Friendly HP increased by {abs(total)}",
                "Verify heal amount is correct",
            )

    # Shield-related cases
    for sh in shield_infos:
        amount = sh["shieldUpAmountAlter"]
        add(
            f"Grant {amount} shield",
            "Friendly player has 0 shield",
            f"Friendly shield becomes {amount}",
            "Verify ShieldAlterEffect applies correct amount",
        )

    # Component-specific cases
    comps = card.get("components", [])
    if "BuryEffect" in comps:
        add(
            "Bury effect with empty valid targets",
            "All valid target cards are already at bottom or are minions",
            "No cards are buried; deck order unchanged",
            "Verify BuryEffect handles empty target list gracefully",
        )
        add(
            "Bury effect on friendly cards",
            "combinedDeckZone has 5 friendly non-minion cards",
            "Up to N friendly cards moved to bottom",
            "Verify only non-minion, non-bottom cards are chosen",
        )
    if "StageEffect" in comps:
        add(
            "Stage effect with empty valid targets",
            "All valid target cards are already at top or are minions",
            "No cards are staged; deck order unchanged",
            "Verify StageEffect handles empty target list gracefully",
        )
    if "ExileEffect" in comps:
        add(
            "Exile effect with no valid targets",
            "No matching cards in combinedDeckZone",
            "No cards are exiled",
            "Verify ExileEffect handles empty target list gracefully",
        )
    if "StatusEffectGiverEffect" in comps:
        add(
            "Apply status effect",
            "Target card does not have the status effect",
            "Target card receives status effect stack",
            "Verify StatusEffectGiverEffect adds the correct effect",
        )
    if "AddTempCard" in comps:
        add(
            "Add temporary card",
            "Deck has space",
            "New temporary card appears in combinedDeckZone",
            "Verify card instantiation and deck sync",
        )

    # Minion-related
    if card.get("isMinion"):
        add(
            "Minion card is skipped by effect processing",
            "Card is revealed as a minion",
            "Effect processing skipped by CombatManager.ShouldSkipEffectProcessing",
            "Verify minion cards do not trigger combat effects directly",
        )

    # Mirror/Enemy perspective
    add(
        "Enemy perspective (mirrored logic)",
        "Card is owned by enemy player",
        "Effect targets friendly player instead of enemy",
        "Verify faction perspective flip in HPAlterEffect / ShieldAlterEffect",
    )

    return cases


# ---------------------------------------------------------------------------
# Markdown renderer
# ---------------------------------------------------------------------------

def render_plan(card, chinese_name, rel_path, cases, hp_infos, shield_infos):
    name = chinese_name or card["name"]
    prefab_path = card["path"]
    card_type_id = card["cardTypeID"]
    is_minion = "Yes" if card["isMinion"] else "No"
    trigger = infer_trigger_event(card)
    cost_type = get_cost_type(card)
    has_cost = has_cost_checks(card)

    # Implementation chain description
    comp_set = set(card.get("components", []))
    chain_steps = []
    listeners = card.get("listeners", [])
    if listeners:
        chain_steps.append(f"GameEventListener(s) registered to: {', '.join(l['eventName'] for l in listeners)}")
    containers = card.get("containers", [])
    if containers:
        for i, c in enumerate(containers, 1):
            checks = ", ".join(c["checkCostMethods"]) if c["checkCostMethods"] else "none"
            effects = describe_container_methods(c["effectMethods"])
            chain_steps.append(f"CostNEffectContainer #{i} (`{c['name']}`): cost checks=[{checks}], effects=[{effects}]")

    # Formula
    formulas = []
    for hp in hp_infos:
        sign = "+" if hp["extraDmg"] >= 0 else ""
        formulas.append(f"Damage = baseDmg({hp['baseDmg']}) {sign} extraDmg({hp['extraDmg']}) = {hp['total']}")
    for sh in shield_infos:
        formulas.append(f"Shield = shieldUpAmountAlter({sh['shieldUpAmountAlter']})")
    if not formulas:
        formulas.append("Effect does not directly modify HP or shield.")

    formula_block = "\n".join(formulas)

    # Important details
    details = []
    if "HPAlterEffect" in comp_set:
        details.append("HPAlterEffect automatically adds `baseDmg.value`; ensure prefab `extraDmg` is configured for the intended final number.")
    if "BuryEffect" in comp_set:
        details.append("BuryEffect skips minions, neutral/Start Cards, and cards already at the bottom of `combinedDeckZone`.")
    if "StageEffect" in comp_set:
        details.append("StageEffect skips minions, neutral/Start Cards, and cards already at the top of `combinedDeckZone`.")
    if "ExileEffect" in comp_set:
        details.append("ExileEffect removes cards from `combinedDeckZone` and triggers `onFriendlyCardExiled` event if applicable.")
    if has_cost:
        details.append(f"Cost checks present: {cost_type}. Effect is skipped if cost is not met.")
    if card.get("isMinion"):
        details.append("This card is a Minion; `CombatManager.ShouldSkipEffectProcessing()` may skip its direct effect triggers.")
    if not details:
        details.append("No special implementation details detected from prefab scan.")

    # Build test case table
    case_rows = "\n".join(
        f"| {c['id']} | {c['scenario']} | {c['setup']} | {c['expected']} | {c['validation']} |"
        for c in cases
    )

    md = f"""# {name} Test Plan

## Card Overview

| Property | Value |
|----------|-------|
| **Prefab Path** | `{prefab_path}` |
| **Card Type ID** | `{card_type_id}` |
| **Is Minion** | {is_minion} |
| **Trigger Event** | {trigger} |
| **Cost Type** | {cost_type if cost_type else "none"} |

---

## Implementation Chain

{chr(10).join(str(i+1) + ". " + s for i, s in enumerate(chain_steps))}

### Effect Formula

```
{formula_block}
```

### Important Implementation Details

{chr(10).join("- " + d for d in details)}

---

## Test Strategies

### Strategy A: Programmatic Unit Test (Editor Mode) - Recommended

Use `unity-MCP` `execute_code` to simulate combat state directly without entering Play Mode or waiting for animations.

#### Steps
1. Load the prefab via `AssetDatabase.LoadAssetAtPath<GameObject>`.
2. Create or locate a `CombatManager` instance and initialize player statuses.
3. Construct a controlled `combinedDeckZone` and `revealZone`.
4. Instantiate the target card, assign ownership, and place it appropriately.
5. Call `ValueTrackerManager.me?.UpdateAllTrackers()` if the effect relies on tracked counts.
6. Invoke the effect method directly (or trigger the bound `UnityEvent`).
7. Assert the resulting state matches expectations.

#### Test Cases

| ID | Scenario | Deck / State Setup | Expected Result | Validation Point |
|----|----------|-------------------|-----------------|------------------|
{case_rows}

#### Pros
- Fast execution (seconds).
- No dependency on UI or animation timing.
- Easy to iterate and debug.

---

### Strategy B: Play Mode Integration Test

Run the actual combat scene and verify the full player-interaction flow.

#### Steps
1. Open the Combat scene (or transition from Shop to Combat).
2. Ensure the test deck contains the target card.
3. Enter Play Mode and advance until the card is revealed.
4. Record the relevant game state before and after the effect triggers.
5. Cross-reference the observed result with the expected logic.
6. Use `read_console` to capture `effectResultString` logs.
7. Optionally use `manage_camera(screenshot)` to capture UI evidence.

#### What to Verify
- Event binding: the correct `GameEvent` triggers the `CostNEffectContainer`.
- Animation: attack / effect animation plays as expected (or is skipped when `isStatusEffectDamage = true`).
- Log output: damage / heal / shield numbers match expectations.
- State consistency: HP, shield, and deck counts remain valid after execution.

---

## Recommended Execution Order

1. **Run Strategy A first.** Catch arithmetic and logic bugs immediately without scene overhead.
2. **Run Strategy B second.** Validate the full gameplay integration once core logic is confirmed.

---

## Quick Reference: Key Scripts

| Script | Path | Responsibility |
|--------|------|----------------|
| `HPAlterEffect` | `Assets/Scripts/Effects/HPAlterEffect.cs` | Damage / heal calculation and delivery |
| `ShieldAlterEffect` | `Assets/Scripts/Effects/ShieldAlterEffect.cs` | Shield calculation and delivery |
| `ValueTrackerManager` | `Assets/Scripts/Managers/ValueTrackerManager.cs` | Tracks dynamic deck counts |
| `CostNEffectContainer` | `Assets/Scripts/Card/CostNEffectContainer.cs` | Cost checking and effect invocation |
| `CombatManager` | `Assets/Scripts/Managers/CombatManager.cs` | Manages `combinedDeckZone` and `revealZone` |
| `GameEventStorage` | `Assets/Scripts/Managers/GameEventStorage.cs` | Centralized GameEvent references |
"""
    return md


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

def main():
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    scan = load_scan()
    name_map = load_name_map()

    # Build reverse lookup: basename -> chinese_name
    basename_map = {}
    for rel, cn in name_map.items():
        basename_map[os.path.basename(rel)] = cn

    generated = 0
    for card in scan:
        # Find Chinese name
        path = card["path"]
        chinese_name = None
        for rel, cn in name_map.items():
            if path.endswith(rel):
                chinese_name = cn
                break
        if not chinese_name:
            chinese_name = card["name"]

        # Gather effect info
        hp_infos = [analyse_hp_alter(h) for h in card.get("hpAlters", [])]
        shield_infos = card.get("shieldAlters", [])

        # Generate cases
        cases = generate_test_cases(card, hp_infos, shield_infos)

        # Render markdown
        md = render_plan(card, chinese_name, "", cases, hp_infos, shield_infos)

        # Sanitize filename
        safe_name = re.sub(r'[\\\\/:*?"<>|]', "_", chinese_name)
        out_path = os.path.join(OUTPUT_DIR, f"{safe_name}_TestPlan.md")
        with open(out_path, "w", encoding="utf-8") as f:
            f.write(md)
        generated += 1
        print(f"Generated: {out_path}")

    print(f"\nDone. {generated} test plans generated in {OUTPUT_DIR}/")


if __name__ == "__main__":
    main()
