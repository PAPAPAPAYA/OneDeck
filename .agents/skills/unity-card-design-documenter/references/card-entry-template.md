# Per-Card Entry Template

Use this template for each card inside a category section.

---

### {{CARD_NAME}} ({{DISPLAY_NAME}})
- **cardTypeID**: `{{CARD_TYPE_ID}}`
- **Price**: {{PRICE}}
- **Is Minion**: {{IS_MINION}}
- **Tags**: {{TAGS}}
- **Initial Status Effects**: {{STATUS_EFFECTS}}
- **Description**: {{CARD_DESC}}
- **Costs**:
	- Bury Cost: {{BURY_COST}}
	- Delay Cost: {{DELAY_COST}}
	- Expose Cost: {{EXPOSE_COST}}
	- Minion Cost: {{MINION_COST_COUNT}} (type: `{{MINION_COST_TYPE_ID}}`, owner: {{MINION_COST_OWNER}})
- **Effect Containers**:
{{EFFECT_CONTAINERS}}
- **Key Component Fields**:
{{KEY_COMPONENTS}}

---

### Effect Container Sub-Template

For each CostNEffectContainer, emit:

```
	{{INDEX}}. `{{CONTAINER_NAME}}`
		- Trigger: {{TRIGGER_EVENT}}
		- CheckCost: {{CHECK_COST_CALLS}}
		- PreEffect: {{PRE_EFFECT_CALLS}}
		- Effect: {{EFFECT_CALLS}}
```

Example:
```
	1. `deal dmg and stage friendly`
		- Trigger: onMeRevealed
		- CheckCost: (none)
		- PreEffect: (none)
		- Effect: HPAlterEffect->DecreaseTheirHp(0,) | StageEffect->StageMyCards(1,)
```

### Key Component Sub-Template

For each effect component found on children, emit a bullet:

```
	- `{{COMPONENT_TYPE}}` on `{{CHILD_NAME}}`: {{FIELD_SUMMARY}}
```

Examples:
```
	- `HPAlterEffect` on `deal dmg`: baseDmg=2, extraDmg=1, isStatusEffectDamage=False
	- `CurseEffect` on `enhance curse`: cardTypeID=JU_ON, powerCoefficient=1
	- `StageEffect` on `stage friendly`: tagToCheck=None
```
