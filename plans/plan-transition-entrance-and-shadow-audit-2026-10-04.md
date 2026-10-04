# Transition Entrance Slide & Flip-Shadow Audit (2026-10-04)

Session findings after the animated flip landed (2c98be1a, supersedes the 2026-10-02 fix-6
instant apex swap; RegressionChecklist row 147). RULINGS RECEIVED 2026-10-04 (same day):

- **Part A ruling**: user says the disappearing shadow IS `PhysicalCardShadow` (not the price
  block, not a new back-shadow wish). Re-diagnosis below — audit geometry CONFIRMED correct;
  a rendering-mechanism cause was NOT found; one screenshot pending to close it.
- **Part B ruling**: recommended plan APPROVED — implemented same day (see Part B status).

## Part A — "the other shadow disappears after the flip" (diagnosis, ruling pending)

### Report

After the Shop→Combat flight the deck cards flip face-down; the user reports
`PhysicalCardBigShadow` still shows but "another shadow" no longer does.

### Evidence

1. **The flip path is exonerated.** Edit Mode experiment: instantiated
   `Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab` (the `physicalCardPrefab` wired on
   BOTH `ShopUXManager` and `CombatUXManager` in GameScene), invoked `BuildFlipRoot`, ran
   `SetFaceUp(false, false, true)`, dumped the FlipRoot state:
   - `PhysicalCardBigShadow` active=True localPos=(0.15, -0.15, +0.25)
   - `PhysicalCardShadow` active=True localPos=(0, 0, +0.20)
   - `CardBack` active=True localPos=(0, 0, 0); FlipRoot localScale ends at (1, 1, 1)
   The animated flip (`SetFaceUp(false, animated:true, force:true)`) reaches the identical
   end state: it only tweens `FlipRoot.localScale.x` 0→1 and `ApplyFaceVisibility` toggles
   ONLY `_faceElements` + `CardBack` — shadows are neither squashed permanently nor hidden.
2. **The rim shadow is geometrically invisible at ALL times, face-up included.**
   `PhysicalCardShadow` is Sliced 6.4×9.2 — the SAME size as face and back — at the same
   x/y, 0.2 BEHIND the front sprite (larger z = farther from camera in this project). It is
   fully covered by whichever front sprite (face or tinted back) renders. The flip cannot
   change its visibility, before or after.
3. Prefab hierarchy note: both shadows are DIRECT children of the face-parent node, so
   `BuildFlipRoot`'s `faceParent.Find(...)` reparents both under FlipRoot; neither enters
   `_faceElements`. `PhysicalCardParent` also carries an inactive
   `deprecated/old shadow/PhysicalCardShadow` (Simple draw mode) — not a factor.
4. **What DOES disappear mid-transition: the sell-price block** — most likely what was seen.
   `ShopCardView.UpdatePriceDisplay` runs every frame;
   `showPrice = shopPhase && cardImRepresenting != null && !suppressPriceDisplay
   && isFaceUp && !isFlipPlaying`. Travel start flips the phase to Combat (and any flip
   window sets `isFlipPlaying`), so the whole price `PhysButton` hides — including its child
   literally named `Shadow` (SpriteRenderer using the card-face sprite, copied at
   `EnsurePriceButton`). This is the 2026-09-30 reroll-flip rule (VISUAL-FIX block in
   `ShopCardView`) — by design, and it predates the animated flip.

### Ruling needed (pick one)

- **(a)** The seen shadow = the price block's `Shadow` → by design, no change.
- **(b)** Want the face-down card back to carry its own rim/silhouette shadow (today the
  face-down look relies on BigShadow only; the rim shadow is invisible under the back) →
  small design change (back-shadow sprite or enlarged back silhouette). NOT implemented.
- **(c)** Neither → need a screenshot or the object name to continue.

### Re-diagnosis after the ruling (2026-10-04, user named PhysicalCardShadow)

Fresh read-only probes (preview scenes + prefab walks, all inactive-instance safe):

1. **The audit's geometry is CONFIRMED — with one evidence correction.** The prefab has THREE
   `PhysicalCardShadow`-named nodes. Only the TOP-LEVEL one under `PhysicalCard/` is active:
   localPos (0, 0, +0.20), dead-center behind the full-alpha `PhysicalCardFace` (z=0) and the
   full-alpha runtime `CardBack` (z=0) — invisible in BOTH states; the flip cannot change
   that. (The audit's first experiment — and one session probe — misread the NESTED
   `deprecated/old shadow/PhysicalCardShadow` (offset (0.15,-0.15,0.1), under an INACTIVE
   `deprecated` node): depth-first same-name lookup hits it after the flip reparents the real
   rim into FlipRoot. Do not trust flat YAML/dump name matches on this prefab; walk with
   active flags.)
2. **No runtime path touches the rim.** Project-wide grep: `BuildFlipRoot`'s reparent is the
   ONLY code reference. `ApplyColor`/`ApplyBackColor`/`ShopCardView` never touch it.
3. **Leading candidate for what was actually seen**: FloatStack big-shadow suppression —
   `CombatUXManager.ApplyFloatStackShadowSuppression` suppresses EVERY deck card's
   `PhysicalCardBigShadow` in combat (only the revealed card keeps one, driven to the deck
   anchor), while SHOP deck cards each show their own BigShadow crescent. The suppression
   lands at the same moment as the flip (landing/deck layout), so it reads as "the flip ate
   a shadow". Under this theory "BigShadow still shows" = the single driven anchor shadow.
4. **Minor fact for later work**: `MinionPhysicalCardParent.prefab` / `PhysicalCard.prefab`
   carry a DIFFERENT `PhysicalCardShadow` — active, alpha 1.00, offset (0.15, 0, 0.10) — a
   genuinely VISIBLE solid sliver. Combat spawns isMinion cards from the Minion prefab, so
   minion cards visibly differ from PhysicalCardParent cards in shadow structure. Not the
   flight flip's doing, but a real cross-prefab inconsistency worth a future pass.
5. **Closing evidence needed**: one screenshot of the deck where the shadow is missed
   (ideally shop deck + combat deck side by side), or the object path inspected mid-play.

## Part B — enemy cards + start card entrance slide (APPROVED + IMPLEMENTED 2026-10-04)

### Why they "pop" today

- `GatherDecks` order: `combinedDeckZone = [player 0..n-1, enemy n..N-2, start card N-1]`
  (original deck order per side; start card added last = first revealed = frontmost slot).
- The flight dummies are the SHOP player deck only (n cards) landing on stack slots 0..n-1
  (the driver bakes the Float Stack step model — the shipped layout) → they cover exactly
  the player-card slots. Enemy cards and the start card have NO dummy coverage and appear
  instantly at landing (enemy backs = `OpponentCardColor` tint via `ApplyBackColor`; the
  start card spawns face-up per the InstantiateAllPhysicalCards face-down rule).
- `EnemyDeck` / `enemyDeckParent` (scene object at the world origin) is a LOGICAL container
  only — there is NO separate enemy visual stack; the physical deck is ONE merged stack.

### Recommended plan: ride the existing start-card shuffle animation

1. New driver static gate (e.g. `CombatEntrancePending`): set true at ToCombat travel
   start; headless/bypass (`Available == false`) and direct phase flips never set it.
2. In `InstantiateAllPhysicalCards`' layout loop, when the gate is on, enemy-owned cards
   (`cardImRepresenting.myStatusRef != CombatManager.Me.ownerPlayerStatusRef`) and the
   start card (`isStartCard`) spawn at `finalPos + (0, enemySlideWorld, 0)` instead of
   `finalPos`. `enemySlideWorld = PhaseFlightPlanner.PxToWorld(cfg.enemySlideDemoPx, pageH)`
   = 2.293 world units at the shipping ortho — the same distance/direction as the enemy HUD
   slide-in (fix 2).
3. The start-card shuffle animation (`PlayStartCardShuffleAnimation` — plays at EVERY
   combat start and flies ALL cards simultaneously to their shuffled positions) then
   delivers the downward motion for free: enemy/start cards swoop down from above onto the
   deck while player cards barely move. Zero new tween systems, zero sequencing risk. The
   start card drops from above straight into the reveal-zone flight, then the shuffle
   settles it into the stack.
4. Placement (clean-as-you-code): logic in `PhaseTransitionDriver` (static helper + gate);
   `CombatUXManager.InstantiateAllPhysicalCards` gets a one-line call in its layout loop.

### Properties

- Driver unavailable (config off / batch / `-odseed N` / NullVisuals) → gate never set →
  spawn positions byte-for-byte legacy.
- Input is blocked through landing + the shuffle (`BlockInput`), so nothing can hover or
  grab a mid-drop card.
- Offset touches spawn position only; shuffle targets, `DeckPositionCalculator` math and
  layout modes are untouched (works for any layout mode).

### Trade-off / open decision

- The drop duration/ease ride the shuffle animation's own move tween (combat-scaled), NOT
  the transition `transDur`/`ApplyEase`. If a slower, more deliberate entrance is wanted,
  the driver would have to own the tween and extend the post-landing BlockInput hold — a
  larger change (not recommended first).
- Rejected alternative: spawning enemy cards at travel start — instantiate is owned by
  `RevealCards` (combat logic); the driver must stay presentation-only.

### Implementation (2026-10-04, approved)

- `PhaseTransitionDriver.CombatEntrancePending` (static gate): set at ToCombat travel start
  (before the phase calls), cleared after the landing wait loop confirms the physicals
  spawned; also cleared defensively in `Awake`/`OnDestroy` so a scene teardown mid-transition
  cannot leak it into the next scene. Headless/bypass (`Available == false`) and direct
  phase flips never set it.
- `PhaseTransitionDriver.GetEntranceSpawnOffset(CardScript)`: zero while the gate is off;
  otherwise enemy-owned (`myStatusRef != ownerPlayerStatusRef`, null-safe) or `isStartCard`
  cards get `+PxToWorld(cfg.enemySlideDemoPx, PageHeightWorld(Camera.main.orthographicSize))`
  on Y (= 2.293 world units at the shipping ortho, same as the enemy HUD slide-in).
- `CombatUXManager.InstantiateAllPhysicalCards` layout loop: one `pos += ...` line (the
  plan-blessed single-line touch on the god file). Spawn positions, rotations, scales and
  `DeckPositionCalculator` math untouched; gate-off path is byte-for-byte legacy.
- Play verification still pending (needs a user Play run of one Shop→Combat transition).

## Session evidence

- Experiments: preview-scene prefab tree dumps (PhysicalCard / StartCard / their Parent
  variants), post-flip state dump, sprite bounds/colors (face & both shadows Sliced
  6.4×9.2, shadow alpha 0.5). Preview scenes closed; no active-scene writes.
- Code read: `CardPhysObjScript` (BuildFlipRoot / SetFaceUp / ApplyFaceVisibility /
  ApplyBackColor / KillFlipTween), `ShopCardView` (UpdatePriceDisplay / EnsurePriceButton),
  `CombatUXManager` (InstantiateAllPhysicalCards / PlayStartCardShuffleAnimation /
  GetFinalDeckPositionForCard), `CombatManager` (GatherDecks / InstantiateDeckSide),
  `PhaseTransitionDriver`, `PhaseFlightPlanner`, `CombatAnimationSpeed`.
- Working-tree note: `Assets/Scenes/GameScene.unity` carries an uncommitted PrefabInstance
  (z≈8.03) NOT from this session — left untouched, excluded from any commit.
