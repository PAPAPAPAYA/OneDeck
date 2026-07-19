# Combat HP Compare Bar — Implementation Plan

Date: 2026-07-18
Status: Proposal, revised after review (no code changed yet)
Reference demo: `docs/demo/CombatHPBarDemo.html` (motion design source of truth)

## 1. Overview

Add an HP compare bar to the top of the combat UI, matching the mockup and the
validated HTML demo:

- Left segment = player HP share of (player HP + enemy HP).
- Right segment = enemy HP share of the same total.
- Pure color bar, no numbers.
- Bar size and both colors configurable.
- Motion effects (validated in the demo):
	1. Damage ghost trail (delayed collapse of the just-lost region).
	2. Hit flash + bar shake (amplitude scales with lost share).
	3. Low HP pulse (share < 25% and > 0).
	- Plus: heal flash on the gained region, heal path has no shake.

## 2. Key Integration Decision: Displayed-HP Pipeline

The bar must NOT read `PlayerStatusSO.hp` directly. `CombatInfoDisplayer`
already owns a pending-HP display queue (`SnapshotHpDisplay` / `CommitHpDisplay`,
see `Assets/Scripts/Managers/CombatInfoDisplayer.cs:60-121`): logic updates HP
immediately, but each Attack animation carries its own post-hit HP value and the
UI only commits it when the hit lands. If the bar read live HP, it would race
ahead of the attack animations.

Approach:

- Add two read-only accessors to `CombatInfoDisplayer` (additive only, ~5 lines
	each): `GetDisplayedOwnerHp()` / `GetDisplayedEnemyHp()`, returning exactly the
	values `DisplayStatusInfo()` computes (queue-frozen value when pending exist,
	live `hp` otherwise).
- The bar polls these two accessors every frame and diffs each side's value
	against its own stored displayed HP. All effect triggering derives from these
	per-side HP diffs — never from share diffs (see section 4, step 4).
- Do not duplicate the queue logic — single source of truth.
- Heal / fatigue and other non-attack paths do not use the queue and fall back
	to live HP automatically, consistent with the existing HP text.

## 3. UI Structure (uGUI, under the combat Canvas)

```
HPBarRoot (RectTransform, top-center, sizeDelta = configurable bar size)
├── PlayerSeg   Image, type=Filled, Horizontal, fillOrigin=Left
├── EnemySeg    Image, type=Filled, Horizontal, fillOrigin=Right
├── PlayerGhost Image, white a=0.8
├── EnemyGhost  Image, white a=0.8
├── PlayerFlash Image, type=Filled, Horizontal, fillOrigin=Left, white, a=0
└── EnemyFlash  Image, type=Filled, Horizontal, fillOrigin=Right, white, a=0
```

- raycastTarget = off on ALL six Images. Combat input is click-driven; nothing
	in the bar may intercept raycasts.
- Share mapping uses `Image.fillAmount`: PlayerSeg fill = player share, EnemySeg
	fill = enemy share from the right. The two segments tile the bar exactly.
	`fillAmount` only rebuilds the mesh — no layout pass, matching the demo's
	GPU-friendly `scaleX` intent.
- Filled Images REQUIRE a sprite: with `sprite == null`, `Image.type=Filled`
	silently ignores `fillAmount` and renders the full quad (this caused the
	all-red bar on first run). The four Filled images wire
	`Assets/Sprites/WhiteSquare.png`; the presenter also self-heals missing
	sprites in `Awake`.
- Flash images are Filled mirrors of their segment (same type/fillOrigin). The
	presenter syncs each Flash's `fillAmount` to its segment's every frame, so
	the flash covers exactly the segment's current region; only its alpha
	animates.
- `HPBarRoot` must not sit under a LayoutGroup: the shake tween drives its
	anchoredPosition.
- Bar length/height = `HPBarRoot.sizeDelta`; both side colors = serialized
	fields. Everything tunable in the inspector.

## 4. New Component: `CombatHPBarPresenter`

Location: `Assets/Scripts/UXPrototype/CombatHPBarPresenter.cs` (presentation
layer, next to `CombatUXManager`). Pure visuals; no game-logic changes.

Serialized references: the 6 Images, `barRoot`, `gamePhaseRef` (`GamePhaseSO`,
same asset wired on `CombatInfoDisplayer.gamePhase`), `canvas` (root combat
Canvas, for screen-pixel conversion; auto-resolve via `GetComponentInParent`
when unset), both colors, and all tuning constants (defaults from the demo, see
section 6).

Internal state: `_displayedPlayerHp` / `_displayedEnemyHp` (last frame's
displayed values), `_wasInCombat` (phase edge detection), `_ghostEdgePlayer` /
`_ghostEdgeEnemy` (nullable float, ghost trail accumulation).

`Update()` flow:

1. Phase edge handling (compare `gamePhaseRef.Value()` against `_wasInCombat`):
	- Entering Combat: silently sync `_displayedPlayerHp` / `_displayedEnemyHp`
		and both fillAmounts to the current displayed values — no tweens, no
		effects. Without this, the first-frame diff from the default state would
		play a phantom damage/heal effect. Show the bar.
	- Leaving Combat: run cleanup (4.2), hide the bar
		(`barRoot.gameObject.SetActive(false)`), return.
	- Outside Combat: return.
2. Read displayed HPs via the two accessors, compute `playerPct`
	(both zero -> 0.5 fallback, same as demo).
3. Tween both fillAmounts to the new shares when changed (restart-safe: kill
	the segment's previous fill tween first; hits can land mid-transition).
4. Classify effects PER SIDE from each side's displayed-HP delta — NOT from
	share diffs. Shares are complementary (playerPct + enemyPct = 1), so one
	side's change always flips both shares: share-diff classification would play
	a phantom damage ghost + shake on the enemy whenever the player heals, and a
	phantom heal flash on the enemy whenever the player takes damage. The demo
	classifies by the changed side's HP delta sign (`changeHP`), and per-side
	displayed-HP diffing is its exact equivalent:
	- Side's displayed HP dropped and |share delta| >= ghostMinDelta ->
		`PlayDamage(side)`.
	- Side's displayed HP rose and |share delta| >= ghostMinDelta ->
		`PlayHeal(side)`.
	- Both sides can change in the same frame; classify each side independently.
5. Store the new displayed HPs; refresh low-HP pulse state for both sides.

### 4.1 Demo-to-DOTween Mapping

| Demo effect | Unity implementation (DOTween) |
|---|---|
| Share transition 0.25s | `playerSeg.DOFillAmount(p, 0.25f).SetEase(Ease.OutQuad)` (enemy likewise) |
| Damage ghost trail | Instantly set ghost anchors to the lost region `[newPct, oldPct]`; pivot.x on the new-boundary side (player=0, enemy=1); then `DOScaleX(0, 0.43f).SetEase(Ease.InQuad).SetDelay(0.35f)` + `DOFade(0, 0.43f).SetEase(Ease.InQuad).SetDelay(0.35f)` |
| Trail accumulation on rapid hits | Same `ghostEdge` logic as the demo: keep the farthest old boundary while a ghost tween is still active; a new hit kills the side's running ghost tweens and restarts hold + collapse; reset `ghostEdge` on tween complete |
| Hit flash | Side Flash image (Filled mirror of the segment): `DOFade(0.85f, 0.10f)` then `DOFade(0, 0.12f)` |
| Bar shake | `barRoot.DOShakePosition(0.32f, new Vector3(strength, 0, 0), vibrato: 10, randomness: 0, fadeOut: true)` — pure horizontal, no directional randomness. `strength` uses the demo formula in screen pixels, then converts to anchored units: `strengthPx = clamp(deltaShare * barWidthPx * 0.12f, 2f, 14f)` with `barWidthPx = barRoot.rect.width * canvas.scaleFactor`, then `strength = strengthPx / canvas.scaleFactor` |
| Heal flash | Kill the side's running damage-ghost tweens and clear its `ghostEdge` (the demo cancels the side's animation on heal); ghost covers the gained region `[min(old,new), max(old,new)]`; set a=0.75 instantly, then `DOFade(0, 0.45f).SetEase(Ease.OutQuad)`; restore the ghost's base a=0.8 on complete |
| Low HP pulse | `DOFade(0.55f, 0.45f).SetLoops(-1, LoopType.Yoyo)` on the low side's segment. Intentional deviation from the demo's `brightness(1.7)` pulse — uGUI has no brightness filter, alpha pulse is the cheap equivalent. On stop (out of range or at zero) `Kill` the tween AND restore the segment's alpha to 1 |

### 4.2 Implementation Requirements

- Animation speed: every tween gets `tween.timeScale = CombatAnimationSpeed.SpeedScale`.
	Rationale over the `ScaleDuration` convention used in `CardPhysObjScript`:
	mathematically equivalent for durations, and it also scales `SetDelay`, so
	the ghost hold (0.35s) accelerates together with everything else.
- Combat-entry init: the first Combat frame syncs displayed HPs/share silently
	(Update step 1), so the bar never plays a phantom effect from default state.
- Cleanup: on the Combat->other-phase edge and in `OnDisable`, `DOTween.Kill`
	all owned tweens and reset overlays: ghost/flash alpha 0, `ghostEdge`
	cleared, segment alpha 1 — nothing leaks into the next combat.
- All thresholds, durations and amplitudes are serialized fields with demo
	defaults.

## 5. Change List (Estimated)

1. New `Assets/Scripts/UXPrototype/CombatHPBarPresenter.cs` (~200 lines, pure
	presentation).
2. `CombatInfoDisplayer`: add 2 read-only accessors (~10 lines, additive only) —
	the single touch point on existing code.
3. Combat scene: add the UI hierarchy from section 3 under the combat Canvas
	and wire references (including `gamePhaseRef`). Keep the existing HP text
	display in place (run both in parallel for observation; decide removal later).
4. No changes to `HPAlterEffect`, the two-phase animation system, or any effect
	logic.

## 6. Constants (from the demo)

| Constant | Value |
|---|---|
| Share transition duration | 0.25s, ease-out |
| Ghost hold / collapse | hold 0.35s, collapse 0.43s (total 780ms), ease-in |
| Ghost alpha | 0.8 white |
| Hit flash | 220ms total |
| Shake | 320ms, pure horizontal (X only), strength = clamp(deltaShare x barWidth x 0.12, 2, 14) screen px |
| Heal flash | fade 0.75 -> 0 over 450ms, ease-out |
| Low HP threshold | share < 0.25 and > 0 |
| Low HP pulse | 0.9s period yoyo (implemented as alpha pulse — intentional deviation, see 4.1) |
| Ghost min delta | 0.4% of bar (below this, skip effects) |
| Both-zero fallback | 50/50 split |

## 7. Verification Plan

Use the project's Play Mode test strategy (`unity-card-playmode-test` skill)
with `autoReveal` enabled:

1. Single attack -> bar changes exactly on the hit moment, never earlier
	(proves the queue integration).
2. Multi-hit chain -> ghost trail accumulates continuously; the bar matches the
	HP text frame-by-frame (both read the same displayed values — strongest
	regression oracle).
3. Drive one side below 25% -> pulse starts/stops correctly and segment alpha
	returns to 1; at 0 -> no pulse, share correct.
4. Heal / fatigue damage (non-attack paths) -> bar still updates.
5. Combat -> Shop -> Combat transition -> no leftover tweens or ghost overlays.
6. Entering combat -> bar snaps to the correct share on the first frame with NO
	damage/heal effects (proves the silent init).
7. Heal on one side only -> that side plays the heal flash; the other side
	shows NO ghost, NO flash, NO shake (proves per-side HP-delta classification).

## 8. Out of Scope

- Removing or restyling the existing HP text display.
- KO / zero-HP finishing animation and boundary sweep light (demo options not
	selected).
- Shop UI.
