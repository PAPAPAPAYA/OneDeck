# Plan: Centralized Color System via ScriptableObject

Date: 2026-07-22
Status: Implemented 2026-07-22 (all 4 phases; unified minion/empty/start card prefab colors to main card colors per user decision)

## Goal

Create a `ColorSO` ScriptableObject and route all project colors through it, so colors are defined once, consistent everywhere, and editable in a single place.

## Current State

Two distinct color usage patterns exist today, both scattered:

### 1. Rich-text log colors (hardcoded hex strings)

Used in `AppendLog` / TMP text across many effect files:

| Semantic | Current value | Example locations |
|----------|---------------|-------------------|
| Friendly / You | `#87CEEB` | `HPAlterEffect`, `AddTempCard`, `CurseEffect`, `ShieldAlterEffect`, `EffectScript`, `CombatInfoDisplayer` |
| Enemy | `orange` | same set |
| Damage | `red` | `HPAlterEffect:593-622`, `CombatManager:305,320` |
| Heal | `#90EE90` | `HPAlterEffect:637-663`, `CombatInfoDisplayer:303-305` |
| Shield | `grey` | `ShieldAlterEffect`, `CombatInfoDisplayer:173` |
| Numbers / price | `yellow` | `AddTempCard`, `ShopCardView:64`, `CurseEffect` |

The ternary `statusRef == ownerPlayerStatusRef ? "#87CEEB" : "orange"` is duplicated in at least 4 files (`EffectScript:23`, `AddTextEffect:34`, `CardManipulationEffect:25-36`, `CurseEffect:242-273,476-478`, `CombatInfoDisplayer:163`).

### 2. Inspector Color fields (per-component values)

| Field | Component |
|-------|-----------|
| `ownerCardColor`, `opponentCardColor`, `ownerTextColor`, `opponentTextColor` | `CardPhysObjScript` |
| `infectedTintColor`, `powerTintColor` | `CardPhysObjScript:57,64` |
| `playerColor`, `enemyColor`, `shadowColor` | `CombatHPBarPresenter:29-55` |
| `normalColor`, `lowHpColor`, `zeroGrayColor` | `HPNumericDisplay:52-54` |

AGENTS.md also defines canonical color tags (Damage `red`, Heal `#90EE90`, Shield `grey`, Friendly `#87CEEB`, Enemy `orange`) — the SO palette formalizes exactly this table.

## Design

### 1. `ColorSO`

New file `Assets/Scripts/SOScripts/ColorSO.cs`, following the existing `IntSO` pattern:

```csharp
[CreateAssetMenu(fileName = "ColorSO", menuName = "SORefs/ColorSO")]
public class ColorSO : ScriptableObject
{
	public Color value;
	[TextArea]
	public string description;

	// Rich-text hex without '#', cached; used for TMP <color=...> tags.
	public string Hex => ColorUtility.ToHtmlStringRGBA(value);
	public string OpenTag => "<color=#" + Hex + ">";
}
```

- Assets live under `Assets/SORefs/Colors/` (mirrors existing SORefs folder convention).
- `Hex` / `OpenTag` properties make log-string usage a drop-in replacement: `"<color=#87CEEB>"` becomes `palette.friendly.OpenTag`.

### 2. `GameColorPalette` SO

One palette SO holding named `ColorSO` references, following the `GameEventStorage` / `EnumStorage` aggregation pattern:

```csharp
[CreateAssetMenu(fileName = "GameColorPalette", menuName = "SORefs/GameColorPalette")]
public class GameColorPalette : ScriptableObject
{
	[Header("Log / Rich Text")]
	public ColorSO friendly;   // #87CEEB
	public ColorSO enemy;      // orange
	public ColorSO damage;     // red
	public ColorSO heal;       // #90EE90
	public ColorSO shield;     // grey
	public ColorSO highlight;  // yellow (numbers, price)

	[Header("Physical Card")]
	public ColorSO ownerCardColor;
	public ColorSO opponentCardColor;
	public ColorSO ownerTextColor;
	public ColorSO opponentTextColor;
	public ColorSO infectedTint;
	public ColorSO powerTint;

	[Header("HP Bar / Numeric")]
	public ColorSO hpBarPlayer;
	public ColorSO hpBarEnemy;
	public ColorSO hpBarShadow;
	public ColorSO hpNormal;
	public ColorSO hpLow;
	public ColorSO hpZeroGray;
}
```

- A single `GameColorPalette` asset is referenced by consumers (`CardPhysObjScript`, `CombatHPBarPresenter`, `HPNumericDisplay`, `EffectScript` base, `CombatInfoDisplayer`).
- Access for log strings in effects: add a `palette` reference on `EffectScript` (base class of most effects) so all effects inherit it.

### 3. Centralized ownership-color helper

Replace the duplicated `cond ? "#87CEEB" : "orange"` ternaries with one helper on `EffectScript` (or a small static `LogColorHelper`):

```csharp
protected string OwnerTag(PlayerStatusSO statusRef)
	=> statusRef == combatManager.ownerPlayerStatusRef
		? palette.friendly.OpenTag
		: palette.enemy.OpenTag;
```

All `GetCardColorTag` / `GetMyCardColorTag` copies delegate to it.

### 4. Inspector Color fields → ColorSO references

For the four presenter/view components, replace `public Color xxx` with `public ColorSO xxx`, reading `.value` at use sites. Keep the SO reference serialized per-component (assigned in prefab/inspector) rather than a static singleton lookup — consistent with how the project already wires `GameEvent`/`PlayerStatusSO` references.

## Migration Phases

1. **Phase 1 — Infrastructure**: create `ColorSO.cs`, `GameColorPalette.cs`, author the SO assets under `Assets/SORefs/Colors/` with current values (no behavior change).
2. **Phase 2 — Log strings**: swap hardcoded hex strings in `Effects/` and `Managers/` for palette references; dedupe the ownership ternaries. Verify log output unchanged (string diff on a sample combat log).
3. **Phase 3 — Inspector fields**: convert the four components' `Color` fields to `ColorSO`, re-wire in prefabs/scenes, verify tints/HP bar colors visually unchanged.
4. **Phase 4 — Docs**: update the "Color Tags" table in `AGENTS.md` to point at the palette as the single source of truth.

Each phase is independently shippable; Phases 2 and 3 can be split per-file to keep diffs reviewable.

## Trade-offs / Notes

- TMP rich text needs a hex string, not `Color` — hence `Hex`/`OpenTag` on `ColorSO`; log code stays string-building, no runtime cost change.
- SO indirection means changing a color at runtime (e.g. a colorblind theme) is now possible by swapping palette asset — a future option, not built now.
- Do NOT convert `Color.white` / `Color.Lerp` math internals (`CardPhysObjScript:855-862`) — those are computation, not palette definitions.
- Prefab rewiring in Phase 3 is the main manual cost; values must be copy-checked against current prefab overrides (some cards may override `ownerCardColor` per-prefab — audit before replacing).
- No tests exist for log strings; verification is a manual combat-log sample diff.

## Implementation Files (estimate)

| Change | File |
|--------|------|
| New SO types | `Assets/Scripts/SOScripts/ColorSO.cs`, `GameColorPalette.cs` |
| New assets | `Assets/SORefs/Colors/*.asset`, `GameColorPalette.asset` |
| Helper + palette field | `Assets/Scripts/Effects/EffectScript.cs` |
| Log string replacement | `HPAlterEffect`, `AddTempCard`, `CurseEffect`, `ShieldAlterEffect`, `AddTextEffect`, `CardManipulationEffect`, `BuryEffect`, `ExileEffect`, `StageEffect`, `CombatManager`, `CombatInfoDisplayer`, `ShopCardView` |
| Field conversion | `CardPhysObjScript`, `CombatHPBarPresenter`, `HPNumericDisplay` (+ prefab rewire) |
| Docs | `AGENTS.md` Color Tags section |

## Out of Scope

- Theme/skin switching UI.
- Non-`Assets/Scripts` colors (materials, shaders, DOTween tweens using hardcoded colors — audit separately if desired).
