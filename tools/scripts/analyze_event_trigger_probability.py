#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Analyze GameEvent trigger probabilities for OneDeck 3.0 card pool.
Parses docs/CardDesign_GenerationLog.txt.
"""
import re
from pathlib import Path
from collections import defaultdict

ROOT = Path("d:/Unity Projects/OneDeck")
LOG_PATH = ROOT / "docs/CardDesign_GenerationLog.txt"

# Map serialized trigger names to GameEvent semantic names
TRIGGER_MAP = {
    "OnMeRevealed": "onMeRevealed",
    "OnMeBought": "onMeBought",
    "OnMeStaged": "onMeStaged",
    "OnMeBuried": "onMeBuried",
    "OnMeGotPower": "onMeGotPower",
    "OnMeGotStatusEffect": "onMeGotStatusEffect",
    "OnThisTagResolverAttached": "onThisTagResolverAttached",
    "OnAnyCardRevealed": "onAnyCardRevealed",
    "OnHostileCardRevealed": "onHostileCardRevealed",
    "OnHostileRevealed": "onHostileCardRevealed",  # log uses short form
    "OnTheirPlayerTookDmg": "onTheirPlayerTookDmg",
    "OnMyPlayerTookDmg": "onMyPlayerTookDmg",
    "OnTheirPlayerHealed": "onTheirPlayerHealed",
    "OnMyPlayerHealed": "onMyPlayerHealed",
    "OnMyPlayerShieldUpped": "onMyPlayerShieldUpped",
    "OnTheirPlayerShieldUpped": "onTheirPlayerShieldUpped",
    "AfterShuffle": "afterShuffle",
    "BeforeRoundStart": "beforeRoundStart",
    "OnFriendlyMinionAdded": "onFriendlyMinionAdded",
    "OnFriendlyCardExiled": "onFriendlyCardExiled",
    "OnFriendlyFlyExiled": "onFriendlyFlyExiled",
    "OnAnyCardBuried": "onAnyCardBuried",
    "OnFriendlyCardBuried": "onFriendlyCardBuried",
    "OnEnemyCurseCardRevealed": "onEnemyCurseCardRevealed",
    "OnHostileCurseRevealed": "onEnemyCurseCardRevealed",  # log uses short form
    "OnEnemyCurseCardGotPower": "onEnemyCurseCardGotPower",
    "OnAnyCardGotPower": "onAnyCardGotPower",
    "OnFriendlyCardGotPower": "onFriendlyCardGotPower",
    "OnEnemyCardGotPower": "onEnemyCardGotPower",
    "NONE": None,
}

STATUS_EFFECT_GIVER_METHODS = {
    "GiveSelfStatusEffect",
    "GiveStatusEffect",
    "GiveAllFriendlyStatusEffect",
    "GiveStatusEffectToLastXCards",
    "GiveStatusEffectToXFriendly",
    "GiveStatusEffectToXFriendly_BasedOnIntSO",
    "GiveStatusEffectToXFriendly_BasedOnStaged",
    "GiveStatusEffectBasedOnStatusEffectCount",
    "GiveSelfStatusEffectBasedOnStatusEffectCount",
}

POWER_REACTION_METHODS = {
    "GivePowerToCardThatGotPower",
}

STATUS_AMPLIFIER_METHODS = {
    "AmplifyStatusEffectGain",
}

CURSE_METHODS = {
    "EnhanceCurse",
    "EnhanceCurse_BasedOnIntSO",
    "EnhanceCurseWithCoefficient",
    "EnhanceFriendlyCurse",
    "ConsumeHostileCursePower",
}

TRANSFER_STATUS_METHODS = {
    "TransferAllStatusEffectToHostileCurse",
    "TransferOneStatusEffectToSelf",
}

CONSUME_STATUS_METHODS = {
    "ConsumeOwnStatusEffect",
    "ConsumeRandomEnemyCardsStatusEffect",
}


def parse_log():
    text = LOG_PATH.read_text(encoding="utf-8")
    cards = []
    for line in text.strip().splitlines():
        line = line.strip()
        if not line.startswith("CARD|"):
            continue
        # CARD|cardTypeID|path|kv;...;[CONTAINER_0 ...]
        parts = line.split("|", 3)
        if len(parts) < 4:
            continue
        card_type_id = parts[1]
        card_path = parts[2]
        rest = parts[3]

        # Split kv part and container part
        # Containers start with [CONTAINER_
        container_split = re.split(r"(?=\[CONTAINER_)", rest)
        kv_part = container_split[0]
        container_parts = container_split[1:]

        kv = {}
        for m in re.finditer(r"([^=;]+)=([^;]*)", kv_part):
            kv[m.group(1)] = m.group(2)

        containers = []
        for cp in container_parts:
            container = {"raw": cp}
            # [CONTAINER_0 name=...][TRIGGER_...]...
            name_match = re.search(r"\[CONTAINER_\d+ name=([^\]]+)\]", cp)
            container["name"] = name_match.group(1) if name_match else ""
            trigger_match = re.search(r"\[TRIGGER_([^\]]+)\]", cp)
            container["trigger"] = TRIGGER_MAP.get(
                trigger_match.group(1) if trigger_match else "NONE", None
            )

            # Effects
            effects = []
            for em in re.finditer(
                r"\[EFFECT_([^,\]]+)(?:, Assembly-CSharp)?->([^\(]+)\(([^)]*)\)\]", cp
            ):
                effects.append(
                    {
                        "class": em.group(1).strip(),
                        "method": em.group(2).strip(),
                        "args": em.group(3).strip(),
                    }
                )
            container["effects"] = effects

            # Giver fields
            giver_match = re.search(
                r"\[GIVER_[^\]]+ statusEffectToGive=([^\s;]+).*?target=([^\s;]+) includeSelf=([^\s;]+) lastXCardsCount=([^\s;]+) xFriendlyCount=([^\s;]+) statusEffectLayerCount=([^\s;]+) yFriendlyLayerCount=([^\s;]+)\]",
                cp,
            )
            if giver_match:
                container["giver"] = {
                    "statusEffectToGive": giver_match.group(1),
                    "target": giver_match.group(2),
                    "includeSelf": giver_match.group(3),
                    "lastXCardsCount": int(giver_match.group(4) or 0),
                    "xFriendlyCount": int(giver_match.group(5) or 0),
                    "statusEffectLayerCount": int(giver_match.group(6) or 0),
                    "yFriendlyLayerCount": int(giver_match.group(7) or 0),
                }

            # Curse fields
            curse_match = re.search(
                r"\[CURSE_[^\]]+ cardTypeID=([^\s;]+).*?powerCoefficient=([^\s\]]+)\]", cp
            )
            if curse_match:
                container["curse"] = {
                    "cardTypeID": curse_match.group(1),
                    "powerCoefficient": int(curse_match.group(2) or 1),
                }

            # Check cost presence
            container["has_cost_check"] = "[CHECK_" in cp
            container["is_linger"] = kv.get("tags", "") == "Linger"

            containers.append(container)
        cards.append(
            {
                "cardTypeID": card_type_id,
                "path": card_path,
                "displayName": kv.get("displayName", ""),
                "tags": kv.get("tags", ""),
                "containers": containers,
            }
        )
    return cards


def analyze(cards):
    # 1. Listener counts per event
    listener_counts = defaultdict(list)
    for c in cards:
        for cont in c["containers"]:
            ev = cont["trigger"]
            if ev:
                listener_counts[ev].append({
                    "card": c["cardTypeID"],
                    "name": c["displayName"],
                    "container": cont["name"],
                    "has_cost_check": cont["has_cost_check"],
                    "is_linger": cont["is_linger"],
                })

    # 2. Status effect givers (sources)
    status_givers = []
    for c in cards:
        for cont in c["containers"]:
            for eff in cont["effects"]:
                if eff["method"] in STATUS_EFFECT_GIVER_METHODS:
                    giver_info = cont.get("giver", {})
                    status_givers.append(
                        {
                            "card": c["cardTypeID"],
                            "name": c["displayName"],
                            "container": cont["name"],
                            "method": eff["method"],
                            "trigger": cont["trigger"],
                            "effect": giver_info.get("statusEffectToGive", "?"),
                            "target": giver_info.get("target", "?"),
                            "includeSelf": giver_info.get("includeSelf", "?"),
                            "lastX": giver_info.get("lastXCardsCount", 0),
                            "xFriendly": giver_info.get("xFriendlyCount", 0),
                            "layerCount": giver_info.get("statusEffectLayerCount", 0),
                            "yLayerCount": giver_info.get("yFriendlyLayerCount", 0),
                        }
                    )

    # 3. Power-specific sources (curse enhance, transfer, etc.)
    power_sources = []
    for c in cards:
        for cont in c["containers"]:
            for eff in cont["effects"]:
                if eff["method"] in CURSE_METHODS:
                    curse_info = cont.get("curse", {})
                    power_sources.append(
                        {
                            "card": c["cardTypeID"],
                            "name": c["displayName"],
                            "method": eff["method"],
                            "trigger": cont["trigger"],
                            "target_card_type": curse_info.get("cardTypeID", "?"),
                            "coefficient": curse_info.get("powerCoefficient", 1),
                            "type": "curse_enhance",
                        }
                    )
                if eff["method"] in TRANSFER_STATUS_METHODS:
                    power_sources.append(
                        {
                            "card": c["cardTypeID"],
                            "name": c["displayName"],
                            "method": eff["method"],
                            "trigger": cont["trigger"],
                            "type": "transfer",
                        }
                    )

    return listener_counts, status_givers, power_sources


def main():
    cards = parse_log()
    listener_counts, status_givers, power_sources = analyze(cards)

    print("=" * 80)
    print(f"OneDeck 3.0 GameEvent Trigger Probability Analysis ({len(cards)} cards parsed)")
    print("=" * 80)

    print("\n## 1. Listener counts per GameEvent")
    print("-" * 60)
    for ev in sorted(listener_counts.keys()):
        listeners = listener_counts[ev]
        linger_count = sum(1 for l in listeners if l["is_linger"])
        cost_check_count = sum(1 for l in listeners if l["has_cost_check"])
        print(f"{ev:40s} : {len(listeners):2d} listeners (Linger={linger_count}, CostCheck={cost_check_count})")

    print("\n## 2. Cards listening to status/power events")
    print("-" * 60)
    for ev in ["onMeGotStatusEffect", "onMeGotPower", "onAnyCardGotPower", "onFriendlyCardGotPower", "onEnemyCardGotPower"]:
        listeners = listener_counts.get(ev, [])
        print(f"\n{ev} ({len(listeners)} cards):")
        for l in listeners:
            flags = []
            if l["is_linger"]:
                flags.append("Linger")
            if l["has_cost_check"]:
                flags.append("CostCheck")
            flag_str = f" [{', '.join(flags)}]" if flags else ""
            print(f"  - {l['card']} ({l['name']}) -> container '{l['container']}'{flag_str}")

    print("\n## 3. Status effect givers (sources that can trigger onMeGotStatusEffect)")
    print("-" * 60)
    for g in status_givers:
        targeting = []
        if g["method"] == "GiveSelfStatusEffect":
            targeting.append("self")
        elif g["method"] == "GiveStatusEffect":
            targeting.append(f"random {g['target']}")
        elif g["method"] == "GiveAllFriendlyStatusEffect":
            targeting.append("all friendly")
        elif g["method"] == "GiveStatusEffectToLastXCards":
            targeting.append(f"last {g['lastX']} cards")
        elif g["method"] == "GiveStatusEffectToXFriendly":
            targeting.append(f"{g['xFriendly']} random friendly")
        elif g["method"] == "GiveStatusEffectToXFriendly_BasedOnIntSO":
            targeting.append(f"IntSO random friendly")
        elif g["method"] == "GiveStatusEffectToXFriendly_BasedOnStaged":
            targeting.append(f"staged-count random friendly")
        elif g["method"] == "GiveStatusEffectBasedOnStatusEffectCount":
            targeting.append(f"based on self status count -> random {g['target']}")
        elif g["method"] == "GiveSelfStatusEffectBasedOnStatusEffectCount":
            targeting.append("self based on self status count")
        print(
            f"  - {g['card']} ({g['name']}) [{g['effect']}] via {g['method']} on {g['trigger']} -> {', '.join(targeting)}"
        )

    print("\n## 4. Power-specific sources (trigger onMeGotPower / onAnyCardGotPower / faction variants)")
    print("-" * 60)
    for s in power_sources:
        if s["type"] == "curse_enhance":
            print(f"  - {s['card']} ({s['name']}) -> {s['method']} on {s['trigger']} (target={s['target_card_type']}, coeff={s['coefficient']})")
        else:
            print(f"  - {s['card']} ({s['name']}) -> {s['method']} on {s['trigger']} ({s['type']})")

    print("\n## 5. Summary: probability difficulty for onMeGotStatusEffect")
    print("-" * 60)
    on_me_status_listeners = listener_counts.get("onMeGotStatusEffect", [])
    print(f"Cards listening to onMeGotStatusEffect: {len(on_me_status_listeners)}")
    for l in on_me_status_listeners:
        print(f"  - {l['card']} ({l['name']})")
    power_listeners = listener_counts.get("onMeGotPower", []) + listener_counts.get("onAnyCardGotPower", []) + listener_counts.get("onFriendlyCardGotPower", []) + listener_counts.get("onEnemyCardGotPower", [])
    print(f"\nCards listening to any Power event: {len(power_listeners)}")
    for l in power_listeners:
        print(f"  - {l['card']} ({l['name']}) -> {l['container']} [{('Linger' if l['is_linger'] else '')}]")


if __name__ == "__main__":
    main()
