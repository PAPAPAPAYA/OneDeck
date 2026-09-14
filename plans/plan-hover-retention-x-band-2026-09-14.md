# Plan: Configurable Hover Retention X Band

Date: 2026-09-14
Status: Implemented 2026-09-14. Default is a bounded band; 0 restores the legacy full-row behaviour.

## Goal

Make the horizontal extent of "the cursor is still on the hovered deck card" a tunable value.

## Verified current state — the X extent is not "too large", it is unbounded

`CardPhysObjScript.IsCursorOverCard()` (CardPhysObjScript.cs:1677) uses, for any card that is in
`physicalCardsInDeck`, a **pure-Y band test**:

```csharp
float localHalfH = _hoverCollider is BoxCollider2D box
	? box.size.y * 0.5f
	: _hoverCollider.bounds.extents.y / Mathf.Max(transform.lossyScale.y, 0.0001f);
float slotScaleY = Mathf.Max(uxMgr.GetDeckScaleAtIndex(deckIndex).y, 0.0001f);
float halfH = localHalfH * slotScaleY;
return worldPos.y >= slotPos.y - halfH && worldPos.y <= slotPos.y + halfH;
```

X is **intentionally ignored**: the comment above it states "Pure-Y band test: X intentionally
ignored (the pop-up slides out horizontally, so any X-sensitive region would re-create 'cursor
left' in the X axis)". So today the retention region is the entire screen row of that deck slot.

### Why it is X-free (the fix that must not be re-broken)

VISUAL-FIX(2026-09-09) / RegressionChecklist row 86: with `popUpXOffset = 4` the popped card
slides out horizontally. The first attempt anchored the test on the card's own live collider, so
the card vacating the cursor in X read as "cursor left" -> `EndHover` -> slot-in -> the card
behind popped and vacated the same spot -> the first card's collider swept back under the cursor
-> A/B ping-pong. A second attempt used a slot-anchored rectangle but **kept the card's own X
extent**, so drifting horizontally toward the popped card still snapped it back mid-inspection.
The shipped fix removed X from the test entirely and additionally anchored arbitration on the
slot z (`HoverArbitrationZ`, CardPhysObjScript.cs:1700) so `popUpZBoost` cannot decide ownership.

The lesson is not "X is dangerous" but "the test must not be anchored on a moving pose". The
slot anchor (`TryGetDeckSlotPosition`, CombatUXManager.cs:1547) is a **static** value computed
from the layout + per-card jitter: it does not move when a card pops up, so an X band anchored on
it cannot create a feedback loop.

## Design

Add one world-unit field next to the other pop-up tunables (`CombatUXManager`, `POP UP / SLOT IN`
header):

| Field | Type | Default | Meaning |
|-------|------|---------|---------|
| `hoverRetentionXHalfWidth` | float | 6 | Half-width, in world units, of the retention band measured from the hovered deck slot's centre. The hover survives while `slotPos.y +/- halfH` (unchanged) **and** `abs(cursorWorld.x - slotPos.x) <= hoverRetentionXHalfWidth`. `<= 0` = legacy behaviour (X ignored entirely, whole row retained). |

Test change, inside the existing `inDeck` branch:

```csharp
bool insideRow = worldPos.y >= slotPos.y - halfH && worldPos.y <= slotPos.y + halfH;
if (!insideRow) return false;
float bandHalfWidth = uxMgr != null ? uxMgr.hoverRetentionXHalfWidth : 0f;
if (bandHalfWidth <= 0f) return true;              // legacy: X ignored (byte-identical to today)
return Mathf.Abs(worldPos.x - slotPos.x) <= bandHalfWidth;
```

Properties of this shape:

- **Legacy path is preserved exactly.** With the field at 0 the code returns where the old code
	returned, so the 09-09 behaviour is still reachable for A/B comparison and for anyone who
	prefers the whole row.
- **Uses the same slot anchor as the Y band**, so it is immune to pop-up displacement, in-flight
	scale tweens and `popUpZBoost` for the same reason the Y band is.
- **It also uses the same values the spread offset feeds** (see the companion plan), so a
	neighbour's band travels with it as it parts. The hovered card's own spread offset is 0, so its
	own band never moves.
- **Single change point** (`IsCursorOverCard`), which is shared by both consumers: the
	`UpdateHover` cursor-left poll and the `UpdatePendingHover` "cursor still there?" gate. Enter
	detection still uses the real collider (`OnMouseEnter`), so the result is a normal hysteresis
	shape: narrow entry, wider configurable retention.

### Default value derivation (measured from the scene)

- `popUpXOffset = 4`, `popUpYOffset = 0`, `popUpZBoost = 0`, `popUpScaleMultiplier = 1.1`
	(GameScene.unity:9157-9164, 9209). The hover "pop" is therefore a pure +4 world-unit slide on
	X with a small scale bump; there is no vertical lift to fall back on.
- Card face: `PhysicalCardParent.prefab` BoxCollider2D `m_Size = {6.4, 9.2}`, and the instanced
	card renders at about 0.48 of those local units — cross-checked with
	`floatStackCardHalfHeightPx = 105` x `floatStackPxToWorld = 0.021` = 2.205 world half height
	=> card face is about 3.1 x 4.4 world units, half width about **1.55** at stack scale 1 and
	about **0.85** at `floatStackMinScale = 0.55`.
- The band must cover the popped card's resting spot plus its own width, so
	`|popUpXOffset| + halfWidth + margin` = 4 + 0.85..1.55 + ~0.7 => **about 5.5-6.2**. Default
	**6** therefore keeps the pop-up inspectable at every slot scale while being a finite row
	segment instead of the whole row.
- The tooltip records that derivation, so the value can be re-tuned if `popUpXOffset` or the card
	scale changes.

## Non-goals / limitations (v1)

- The band is a rectangle in world X/Y, not a rotated card shape. The deck layouts keep cards
	within a few degrees of upright, so this is not visible in practice.
- Only deck cards get the band. Cards outside `physicalCardsInDeck` (reveal zone, minions, shop)
	keep the live-collider test, which is correct for them: they are not stacked under a cursor and
	they do not slide out when popped.
- Tightening the band makes horizontal drift out of the band end the hover (slot-in), which is the
	intended effect of this change. If a value is set too small the hover will feel twitchy while
	inspecting a popped card — tune upward; the failure mode is "hover drops", never a loop.

## Verification

EditMode: no layout math is touched (the band is a hover-only test), so layout golden tests are
unaffected. Compile check via the offline csproj build.

Play Mode (manual, folded into RegressionChecklist row 98): with the default 6, park the cursor on
a face-up deck card and move it horizontally onto the popped card — the hover must survive all the
way to the popped card's far edge; move further out — the card must slot back in exactly once,
with no self re-pop and no A/B flicker between adjacent cards (row 86's scenario); set the field to
0 in the Inspector and confirm the old whole-row behaviour returns immediately.
