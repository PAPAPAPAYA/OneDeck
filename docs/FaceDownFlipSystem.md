# Face-Down / Flip System

Deck cards are face-down by default (card back; name/desc/status/ownership info hidden). State lives on `CardPhysObjScript`:

- `isFaceUp` (default `true`; combat deck spawn paths cover cards), `everRevealed` (set on any face-up flip).
- `SetFaceUp(bool faceUp, bool animated, bool force = false, Action onComplete = null)` — 2D squash flip (scaleX 1→0→1) on the runtime-built `FlipRoot` child (built in `Awake()`, reparenting face elements, prints, shadows); never tweens the root transform, so layout/move tweens are unaffected. `CardBack` reuses the face sprite, tinted per ownership (`ownerCardColor`/`opponentCardColor`) every frame. Duration via `flipDuration` (combat-speed scaled).
- **Never-cover rule (hardcoded, no toggle)**: cover calls are skipped when `everRevealed` — a card once shown stays face-up until exiled or shuffled. `force: true` bypasses the guard (shuffle only).
- `ClearRevealedMemory()` — resets `everRevealed` (shuffle only).
- Face-down skips all face writers in `Update()` (`ApplyColor`, status/desc/tag/rarity/cost/price), so nothing leaks onto the back.
- Flip triggers (all in `CombatUXManager`): `InstantiateAllPhysicalCards` / `AddPhysicalCardToDeck` (covered on entry), `MovePhysicalCardToRevealZone` (up), `PopUpCard` (up), `SlotInCard` (down), `MoveRevealedCardToBottom` (down), `MoveCardWithAnimation` ToBottom/ToIndex/ToTop (down; reveal-zone-bound ToTop excluded), `MoveCardToTopPopUpBatch` (up; staged cards stay up on deck top), `MoveCardToPopUpPosition` (up).
- **Shuffle force-cover rule**: `PlayStartCardShuffleAnimation` covers every deck card mid-flight at the arc midpoint (`force: true`) and clears `everRevealed` — overrides the never-cover rule; cards land already face-down. Start Card keeps its face.
- **Passive exception (VISUAL-FIX 2026-09-10)**: 4.0 passive cards (`CardScript.isPassive`, pinned below the Start Card, never revealed) take the opposite branch at the same arc midpoint — flip UP and KEEP `everRevealed`. The flip plays once per combat; later shuffles early-return on the already-face-up state, and every non-forced cover path (`SlotInCard`, `MoveCardWithAnimation`, `AddPhysicalCardToDeck`) is blocked by the kept never-cover rule. This shuffle callback is the project's only `force` cover and only `ClearRevealedMemory` call site, so passives stay face-up for the rest of the combat.
- The flip tween is deliberately NOT killed by `KillTweens()` (CombatCardView calls it every frame during special animations, which would freeze a flip mid-squash).
