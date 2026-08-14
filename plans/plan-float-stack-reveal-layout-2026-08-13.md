# Float Stack Reveal Layout — PRD / Implementation Plan

Date: 2026-08-13
Status: Approved design (doc only; implementation pending)
Interactive reference: `docs/demo/CardStackRevealDemo.html` (validated in browser, incl. reveal conveyor + shadow tracking)
Scope: combat-phase physical deck layout + reveal-zone presentation. Shop layout, card logic/effects, animation request pipeline are untouched.

## 1. Overview

Add a fourth combat deck layout mode, the **Float Stack Reveal** layout:

- Deck cards stack directly (no horizontal fan); each card one fixed step higher than
  the card in front (`stepY`, signed — negative = downward stack).
- The front-most card is the **revealed card**: scaled up and floated away from the
  stack by offsets measured from the **shadow anchor**, so it reads as hovering above
  the deck.
- The revealed card casts a **card-shaped big shadow** that stays at the anchor — it is
  the card's own `PhysicalCardBigShadow`, driven by the layout: on reveal it tweens from
  the card's in-deck pose to the anchor pose in sync with the reveal flight; on return
  it fades out. All other cards' big shadows are disabled while this mode is active.
- The anchor (shadow position) is `physicalCardDeckPos`. Every position derives from it.
- The mode is selected from a single Inspector enum dropdown alongside the existing
  layouts (Linear / Cascade / Arc Loop).

## 2. Confirmed Defaults

From the user's final demo tuning (screenshot 2026-08-13):

| Param | Default | Notes |
|---|---|---|
| floatStackPxToWorld | 0.01 | demo px → world |
| floatStackStepY | 14 | signed; negative = downward stack |
| floatStackRevealScale | 1.07 | revealed card scale multiplier |
| floatStackRevealFloatX | 16 | left offset from shadow anchor (px) |
| floatStackRevealUpY | −14 | up offset from shadow anchor (px; negative = below) |
| floatStackShadowOffset | (0, 30) | extra shadow offset from anchor (px, canvas y-down) |
| floatStackShadowOpacity | 0.85 | driven shadow target alpha |

Not ported: demo cardCount (live deck size), shadowBlur (CSS-only effect), rotJitter
(reuse the existing `DeckLayoutOffsetProvider` position/rotation jitter ranges).

## 3. Layout Semantics (settled in the demo)

- **Stack card** at unity index `j` (0 = deck bottom, `count−1` = next to reveal):
  canvas offset `(0, −stepY·(count−j))` → Unity offset `(0, +stepY·(count−j))·pxToWorld`.
  Slot 0 (the anchor) is the shadow's home and is never occupied by a deck card.
- **Revealed card**: canvas offset `(−floatX, −upY)` → Unity offset
  `(−floatX, +upY)·pxToWorld`; scale = `physicalCardDeckSize × revealScale`.
- **z order**: the existing formula `basePos.z − zOffset·j` works as-is — the next card
  (highest index) is front-most of the stack, deck bottom (index 0) is back-most. The
  reveal-zone z clamp in `GetRevealZonePosition` is unchanged.
- **Rotation jitter**: existing per-card jitter (`GetFinalDeckRotationForCard` +
  `_deckOffsetProvider`) applies unchanged; the revealed card's rotation is zeroed on
  entry (existing `SetRotationImmediate(Quaternion.identity)`).
- **Count seams**: in Float Stack mode the layout count is the RAW
  `physicalCardsInDeck.Count` — the `revealCardCountsAsDeckFront` +1 does NOT apply
  (the stack always starts at slot 1; re-layout on reveal/return is the intended
  conveyor slide). Peel-focus segment count still takes precedence when focused.
- **Conveyor**: reveal = top card floats out; return = card arcs to the deck-bottom slot
  (highest slot index `count`) and every stack card slides one step down. Structurally
  identical to today's reveal→bottom flow.

## 4. Mode Selector & Migration

```csharp
public enum DeckLayoutMode { Linear, Cascade, ArcLoop, FloatStack }
```

- New serialized field `public DeckLayoutMode deckLayoutMode = DeckLayoutMode.Cascade;`
  — the single Inspector dropdown for layout selection.
- Back-compat mapping (no scene data loss; both legacy bools keep serialized values):
  - `enableCascadeDeckLayout == false` → forced **Linear** (shipped off-switch wins).
  - `enableArcLoopDeckLayout == true && deckLayoutMode == Cascade` → **ArcLoop**
    (legacy arc selection wins until the enum is explicitly changed; tooltip documents this).
  - otherwise → `deckLayoutMode`.
- One helper `EffectiveDeckLayoutMode` is the single resolution point; the
  `IsArcLoopLayoutActive`-style checks are reworked to compare against it.

## 5. Unity Integration Design

### 5.1 New file `Assets/Scripts/UXPrototype/DeckFloatStackLayout.cs`

Pure static, unit-testable (same pattern as `DeckCascadeLayout` / `DeckArcLoopLayout`):

```csharp
public static class DeckFloatStackLayout
{
	[System.Serializable]
	public struct Params
	{
		public float stepY;         // stack step per card (demo px, signed)
		public float revealScale;   // revealed card scale multiplier
		public float revealFloatX;  // revealed card left offset from anchor (demo px)
		public float revealUpY;     // revealed card up offset from anchor (demo px, signed)
	}

	// Unity-units offset of deck index j in a count-card stack (anchor-relative).
	public static Vector2 ComputeSlotOffset(int unityIndex, int deckCount, Params p, float pxToWorld);
	// Unity-units float pose of the revealed card (anchor-relative) + scale multiplier.
	public static Vector2 ComputeRevealOffset(Params p, float pxToWorld);
}
```

### 5.2 `Assets/Scripts/UXPrototype/DeckPositionCalculator.cs`

- New carrier `FloatStackConfig { bool enabled; DeckFloatStackLayout.Params layoutParams; float pxToWorld; }`.
- `CalculatePositionAtIndex(...)` gains optional trailing `FloatStackConfig floatStack = null`;
  branch order: FloatStack → ArcLoop → Cascade → Linear. FloatStack branch:
  `offset = ComputeSlotOffset(index, deckCount, ...)`; `z = basePos.z − zOffset·index`.

### 5.3 `Assets/Scripts/UXPrototype/CombatUXManager.cs`

- New `[Header("FLOAT STACK REVEAL LAYOUT")]` fields per section 2 + the mode enum.
- Config builders (`BuildFloatStackLayoutParams` / `BuildFloatStackConfig`); pass the
  config at the three calculator call sites (as with Arc Loop).
- Count seams: `GetLayoutDeckCount()` returns the raw physical count when
  `EffectiveDeckLayoutMode == FloatStack` (focus segment count still wins);
  `CalculateAnimationPositionAtIndex` same treatment.
- `MoveRevealedCardToBottom`: `effectiveCount = physicalCardsInDeck.Count` when Float
  Stack is active (the returned card lands at slot `count` = stack back).
- `GetDeckScaleAtIndex` / `GetCascadeJitterScale`: FloatStack branch → uniform
  `physicalCardDeckSize` / jitter scale 1 (no depth scale in this mode).
- Reveal pose: new `GetRevealZoneScale()` helper — FloatStack →
  `physicalCardDeckSize × floatStackRevealScale`, else `physicalCardRevealSize`;
  replaces the direct `physicalCardRevealSize` reads (4 sites).
  `GetRevealZonePosition()`: FloatStack → anchor + `ComputeRevealOffset(...)`; the
  existing front-z min-clamp logic applies unchanged.
- Arc midpoint (return flight keeps the existing arc tween): `TryGetArcMidpointPosition`
  gains a FloatStack branch — midpoint = `anchor + (0, stepY·(count+1))·pxToWorld +
  arcMidpointOffset` (above the stack back); `IsDynamicArcMidpointActive` reworks to
  `useDynamicArcMidpoint && EffectiveDeckLayoutMode != Linear`. Gizmo branch likewise.
- Peel focus: flows through the count seam; the focus card lands on stack slot 1
  (front of stack) — the stack-mode equivalent of today's front-slot behavior.

### 5.4 Shadow system — `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` + `CombatUXManager.cs`

Prefab facts (`Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab`):
`PhysicalCardBigShadow` = child SpriteRenderer, localPos (0.15, −0.15, 0.25),
localScale (0.5, 0.5, 2); re-parented into `FlipRoot` by `BuildFlipRoot`
(`CardPhysObjScript.cs:660`), found by name today.

`CardPhysObjScript` additions:
- `public SpriteRenderer bigShadowRenderer;` — **new serialized reference**; auto-wired
  by name in `BuildFlipRoot` when null (zero prefab edits required).
- `public bool IsBigShadowDriven { get; }` + `SetBigShadowSuppressed(bool)` —
  guarded `SetActive` on the shadow object (no-op while driven).

`CombatUXManager` choreography (FloatStack mode only):
- `ApplyFloatStackShadowSuppression()`: for every physical deck card,
  `suppressed = (mode == FloatStack)`; called from `UpdateAllPhysicalCardTargets`
  (idempotent) and on mode switch. Cards without a big shadow no-op; the revealed
  card is not in the deck list so the driven shadow is never suppressed.
- Reveal entry (`MovePhysicalCardToRevealZone`, after the flight starts):
  1. record the shadow's local pose (its in-deck follow pose);
  2. re-parent the shadow to `physicalCardDeckPos` with `worldPositionStays = true`
     (so it starts exactly at the card's in-deck pose);
  3. DOTween local pose → anchor pose over the SAME duration/ease as the reveal flight:
     position = shadowOffset converted + z just in front of the deck front card;
     scale = prefab shadow scale × revealScale; rotation = 0; alpha 0 →
     `floatStackShadowOpacity` over the first ~40% of the flight.
- Return (`MoveRevealedCardToBottom`): alpha → 0 over ~0.25s, then re-parent back to the
  card's `FlipRoot`, restore the recorded local pose, `SetActive(false)` (it is now a
  suppressed deck card).
- `ReleaseDrivenBigShadow()` helper also called from the destroy/exile path
  (`DestroyCardWithAnimation`) so an exiled-while-revealed card's shadow fades and
  restores instead of leaking.

Edge cases: start/minion prefabs (no shadow found → no-op); shop cards (never touched —
suppression only runs in the combat manager); face-down rule (revealed card stays
face-up by the never-cover rule; deck cards losing per-card big shadows in this mode is
the requested look); runtime mode switch (release any driven shadow, re-apply
suppression, full re-layout).

## 6. Test Plan

### 6.1 EditMode — new `Assets/Scripts/Editor/Tests/DeckFloatStackLayoutTests.cs`

Template: `DeckArcLoopLayoutTests.cs`. Goldens generated from
`docs/demo/CardStackRevealDemo.html` with the section-2 defaults (same browser-eval
recipe as the arc loop). Cases:

1. Golden slot offsets for 8 cards (index → anchor-relative offset; guards the
   `count − j` mapping and the y sign conversion).
2. Reveal pose: `(−floatX, +upY)·pxToWorld` with upY = −14 (negative stays negative
   after conversion: Unity y = −0.14).
3. Negative stepY (`−14`): slot offsets flip sign (downward stack).
4. Edge counts 0/1/2: no throw, no NaN.
5. Calculator branch: x/y from slot offset, z passthrough
   `basePos.z − zOffset·index`; disabled config falls through to legacy linear.

### 6.2 Regression

- `DeckCascadeLayoutTests`, `DeckArcLoopLayoutTests`, and the full EditMode suite stay
  green. Runnable in-editor via the Unity MCP `run_tests` tool (HTTP bridge on 8080) —
  proven working 2026-08-13.
- Known pre-existing failure unrelated to this feature:
  `RecorderAnimationPlayerTests.PlayRecordersCoroutine_SameSourceMultipleRecorders_PopsUpOnce`.

### 6.3 Manual Play-Mode checklist (Combat scene)

- Enum dropdown switches between all four layouts live; legacy bools map as documented.
- Reveal: top card floats to the layout-driven pose (scale ×1.07), shadow tracks from
  the in-deck pose to the anchor and holds; deck does NOT reserve a front slot
  (conveyor slide).
- Return: existing arc tween to the stack back; shadow fades out; next reveal repeats.
- Bury/Stage batch moves and peel focus behave; exile-while-revealed cleans the shadow.
- Cascade/ArcLoop/Linear look byte-identical to today (mode off-path unchanged).

## 7. Implementation Milestones (for the later code step)

1. `DeckFloatStackLayout.cs` + golden generation from the demo.
2. `DeckPositionCalculator`: `FloatStackConfig` + branch.
3. `CombatUXManager`: enum + params + all mode branches (count, reveal pose/scale,
   effectiveCount, arc midpoint, gizmo, suppression pass).
4. `CardPhysObjScript`: `bigShadowRenderer` + suppression/drive API.
5. Shadow choreography (reveal entry / return / exile) in `CombatUXManager`.
6. `DeckFloatStackLayoutTests.cs`; run the full EditMode suite via Unity MCP.
7. Manual checklist (6.3).
8. `AGENTS.md`: extend the Physical Deck Layout section with the Float Stack bullet +
   Key Files entry; run `wc -c AGENTS.md` afterwards (32 KB limit, keep ≥ 1 KB headroom).

## 8. Notes

- New feature, not a visual bug fix: no `VISUAL-FIX` block / `docs/RegressionChecklist.md`
  row required by `docs/VisualBugPrevention_Guide.md`.
- The legacy `physicalCardRevealPos` / `physicalCardRevealSize` scene objects stay in
  use for all other modes; Float Stack ignores them (its pose is layout-driven).
- Demo file `docs/demo/CardStackRevealDemo.html` remains the interactive tuning
  reference; any future default retune should be re-mirrored into section 2.
