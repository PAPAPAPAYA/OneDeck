# Plan: Face-Down Deck Cards — Flip on Stage / Reveal

Date: 2026-07-20
Status: **Implemented** 2026-07-20 (`CardPhysObjScript.cs` + `CombatUXManager.cs`)
Interactive spec: `docs/demo/CardFlipDemo.html` (validated in browser 2026-07-20)

## Goal

While a card sits statically in the combat deck, the player must not see its face
(no name / desc / status info; the card back only carries the ownership color). The card flips face-up when:

1. It enters the reveal zone (during the deck-to-reveal flight).
2. It is staged (`MoveToTopPopUpBatch`) — and **stays face-up** on deck top after slot-in
   (confirmed by user 2026-07-20).

Additional rules confirmed via demo iteration (2026-07-20):

3. **Revealed-cards-stay-up rule (hardcoded, no toggle)**: once a card has been shown
   face-up (reveal / stage / popup / temp-card entry), it is never covered again —
   it stays face-up wherever it sits in the deck, until exiled or shuffled.
   (Decided 2026-07-20: the demo comparison toggle is NOT carried over to Unity;
   the rule is permanent behavior.)
4. **Shuffle force-cover rule (fixed)**: when a shuffle completes, every card in the
   deck flips face-down — this **overrides rule 3** — and the per-card "was revealed"
   memory is reset (shuffle = information reset). Start Card keeps its face.

## Confirmed / Assumed Decisions

| # | Decision | Status |
|---|----------|--------|
| 1 | Staged card stays face-up after landing on deck top | Confirmed by user |
| 2 | PopUp scenes (Bury / Exile / status-effect batch / AddTempCard entry) also flip face-up while at peak, flip back down when the card settles into the deck | Confirmed via demo review |
| 3 | Face-down hides texts / status display / tints; the **back shows ownership color** (`ownerCardColor` / `opponentCardColor`, tracked per frame) | Updated by user 2026-07-20 (was: neutral single design) |
| 4 | Flip animation = 2D "squash flip" (scaleX 1→0→1 with face/back visibility swap at 0) on a dedicated child `FlipRoot`. Placeholder back art reused from existing sprites | Confirmed via demo review |
| 5 | Start Card (`isPhysicalStartCard`) stays visible — it carries no hidden info | Assumed |
| 6 | Shop phase untouched (separate prefab + `ShopUXManager`; `isFaceUp` defaults to true) | Assumed |
| 7 | Revealed-cards-stay-up: hardcoded permanent rule, **no serialized toggle** | Confirmed by user (2026-07-20) |
| 8 | Shuffle force-cover: fixed rule, bypasses rule #7, flips every deck card down at shuffle landing and clears the revealed-memory | Confirmed by user |

## Current State (verified by reading code)

- No flip / card-back mechanism existed. Physical cards were face-up from spawn to destroy.
- One shared `physicalCardPrefab` (+ `startCardPhysicalPrefab`, `minionPhysicalPrefab`).
  Spawn entry points: `CombatUXManager.InstantiateAllPhysicalCards()` (CombatUXManager.cs:2315)
  and `CombatUXManager.AddPhysicalCardToDeck()` (CombatUXManager.cs:2198).
- Face content refs on `CardPhysObjScript`: `cardFace`, `cardEdge`, `cardImg` (SpriteRenderer)
  + 8 TMP prints, refreshed every frame in `Update()` (`ApplyColor`,
  `UpdateStatusEffectDisplay`, `UpdateCardDescription`, `UpdateTag/Rarity/Cost/Price`).
- Reveal entry: `CombatUXManager.MovePhysicalCardToRevealZone()` (CombatUXManager.cs:253).
- Stage playback: `RecorderAnimationPlayer` case `MoveToTopPopUpBatch`
  (RecorderAnimationPlayer.cs:860) → `visuals.MoveCardToTopPopUpBatch(...)` (:909).
- PopUp / SlotIn: `CombatUXManager.PopUpCard()` (:2908) / `SlotInCard()` (:2962);
  batch cases in `RecorderAnimationPlayer` funnel through them per card (verified).
- Reveal → bottom: `CombatUXManager.MoveRevealedCardToBottom()` (:351).
- Key constraint: the root transform's **scale and rotation are owned by deck-layout /
  move tweens** (`SetTargetScale`, `SetTargetRotation`, cascade per-index scale). A flip
  tween on the root transform would fight them.

## Design (as implemented)

### 1. `FlipRoot` child + placeholder card back (built at runtime, no prefab edits)

In `CardPhysObjScript.Awake()` (`BuildFlipRoot()`):

- Creates child `FlipRoot` **under the face elements' own parent** (the shaker child),
  so card shakes still apply to face content. Reparents `cardFace`, `cardEdge`,
  `cardImg` + all `*Print` transforms under it (`worldPositionStays: true`, pose-exact).
  `PhysicalCardBigShadow` / `PhysicalCardShadow` are reparented under it too (they
  squash with the flip) but are NOT in the visibility toggle — the card back keeps its
  silhouette/drop shadow when face-down. Stickers and deprecated branches are untouched.
- Placeholder back: new `CardBack` SpriteRenderer under `FlipRoot`, reusing
  `cardFace.sprite` with matching `drawMode` / `size` / `sharedMaterial` (same sorting
  layer/order). Tinted per ownership: `ApplyBackColor()` runs every frame while face-down
  and applies `ownerCardColor` / `opponentCardColor` (mirrors `ApplyColor`'s ownership
  check, so HeartChanged shows on the back). Real back art can replace it later without
  code changes.
- All flip tweens act on `FlipRoot.localScale.x` only — never on the root transform, so
  deck layout / cascade scale / move tweens are untouched.
- Skipped when `cardFace == null` (start card prefab: flip disabled).

### 2. Face state API on `CardPhysObjScript`

```csharp
public bool isFaceUp { get; private set; } = true;     // default up: shop/temp unaffected
public bool everRevealed { get; private set; }          // rule #7 memory: was ever shown face-up
public void SetFaceUp(bool faceUp, bool animated, bool force = false, Action onComplete = null)
public void ClearRevealedMemory()                        // rule #8 only
```

- Same-state call → no-op (+ invoke callback).
- **Stay-up guard (rule #7, hardcoded)**: a cover-down call is skipped when
  `everRevealed` is set and `force` is false. `force: true` bypasses the guard —
  used only by the shuffle rule (#8). Flipping up sets `everRevealed = true`.
- `animated: true` → DOTween squash on `FlipRoot`: scaleX 1→0 (swap visibility at 0)
  →1, `Ease.InQuad`/`OutQuad`, `SetUpdate(UpdateType.Normal, true)`. Duration via
  `GetCombatScaledDuration(flipDuration * 0.5f)` per half; serialized `flipDuration`
  (default 0.3s).
- `Update()` gates every face-content writer behind `isFaceUp` (`ApplyColor`,
  status/desc/tag/rarity/cost/price), so nothing leaks onto the back.
- The flip tween is deliberately NOT part of `KillTweens()` — `CombatCardView` calls
  `KillTweens()` every frame during special animations, which would freeze a flip
  mid-squash. Killed in `OnDestroy()`.

### 3. Trigger points (all in `CombatUXManager`; no EffectRecorder / logic changes)

| # | Where | Action |
|---|-------|--------|
| 1 | `InstantiateAllPhysicalCards` | Spawn covered instantly, except the Start Card |
| 2 | `AddPhysicalCardToDeck` | Spawn covered instantly; its `MoveToPopUpPosition` + `SlotIn` requests flip it up then down via #4/#9 |
| 3 | `MovePhysicalCardToRevealZone` | `SetFaceUp(true, animated)` at entry (covers both normal and `pendingRevealZoneMove` branches) |
| 4 | `PopUpCard` / `SlotInCard` | PopUp → flip up; SlotIn → flip down (guard #7 applies). Batch cases in `RecorderAnimationPlayer` funnel through these per card — no changes needed there |
| 5 | `MoveCardToTopPopUpBatch` (stage) | Flip each staged card up at arc start; stays up on deck top (decision #1) |
| 6 | `MoveRevealedCardToBottom` | Flip down at arc start (guard #7 applies) |
| 7 | `MoveCardWithAnimation` ToBottom / ToIndex / ToTop | Flip down at move start (guard #7 applies); ToTop heading to the reveal zone stays up |
| 8 | `PlayStartCardShuffleAnimation` | Per-card landing: `SetFaceUp(false, animated, force:true)` + `ClearRevealedMemory()`. Start Card skipped |
| 9 | `MoveCardToPopUpPosition` (temp-card entry) | Flip up at popup start |

Net state rule: **static in deck = face-down; at peak / in reveal zone = face-up;
staged-on-top = face-up (persistent); any once-revealed card stays face-up anywhere in
the deck; shuffle always ends with every deck card face-down and memory wiped.**

### 4. Edge cases

- `SyncPhysicalCardsWithCombinedDeck`: reorders existing objects only; `isFaceUp` and
  `everRevealed` ride on the phys script, survive sync.
- Staged face-up card later buried/moved: covered by #7 / #4 (guard may keep it up).
- Shuffle with face-up cards in the deck: rule #8 covers them with `force:true` and
  wipes memory — the player loses track of them.
- Temp cards spawned after a shuffle start with `everRevealed == false` (fresh memory).
- Headless tests (`NullCombatVisuals`): flip is visual-only; no `ICombatVisuals`
  interface change required.
- Cascade on/off, `combatAnimationSpeedScale`: flip duration scaled via
  `GetCombatScaledDuration`; layout paths untouched.

### 5. Files touched

- `Assets/Scripts/UXPrototype/CardPhysObjScript.cs` — `FlipRoot` build, placeholder back,
  `SetFaceUp` (+ `force`, `everRevealed`), `ClearRevealedMemory`, `ApplyBackColor`,
  `Update()` guards, tween cleanup. Serialized: `flipDuration`.
- `Assets/Scripts/UXPrototype/CombatUXManager.cs` — hooks #1–#9 above (10 call sites).
- No changes required in `RecorderAnimationPlayer.cs` (batch requests funnel through the
  hooked per-card methods).
- Optional later: real card-back sprite asset.

### 6. Verification

- EditMode instantiation check via MCP `execute_code` (2026-07-20): FlipRoot built with
  10 face elements + CardBack; cover/flip-up/guard/force-cover/memory-clear transitions
  all behave per rules #7/#8.
- 3 pre-existing EditMode test failures confirmed unrelated (identical failures on
  pre-change HEAD code): `AfterShuffleTimingTests` ×2,
  `RecorderAnimationPlayerTests.PlayRecordersCoroutine_SameSourceMultipleRecorders_PopsUpOnce`.
- Manual PlayMode checklist:
  1. Combat start: whole deck face-down (Start Card visible), cascade layout intact.
  2. Reveal: card squash-flips face-up during flight to reveal zone.
  3. Trigger effect → resolved card returns to bottom and STAYS face-up (rule #7).
  4. Stage: flips up on the arc, stays up on deck top; revealing it next does not re-flip.
  5. Bury: popup flips up; the card stays up after landing (rule #7).
  6. AddTempCard (e.g. RIFT_INSECT): enters covered, pop-up flips up, stays up.
  7. Start Card shuffle: every deck card lands face-down, memory wiped; Start Card keeps face.
  8. `CombatManager.combatAnimationSpeedScale` scales flip duration; Shop phase unaffected.
