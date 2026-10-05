# Transition Entrance Slide & Flip-Shadow Audit (2026-10-04)

Session findings after the animated flip landed (2c98be1a, supersedes the 2026-10-02 fix-6
instant apex swap; RegressionChecklist row 147). RULINGS RECEIVED 2026-10-04 (same day):

- **Part A ruling**: user says the disappearing shadow IS `PhysicalCardShadow` (not the price
  block, not a new back-shadow wish). RESOLVED same day via the user's paused-scene probe:
  root cause = flight-dummy z-step gap (see re-diagnosis point 3); fixed in
  `FlyDummiesToCombatStack` (VISUAL-FIX 2026-10-04, RegressionChecklist row 149).
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
3. **RESOLVED (user paused-scene probe, 2026-10-04 evening): the flight-dummy z-step gap.**
   The user paused mid-transition (flight dummies still on screen,
   `physicalCardsInDeck` still 0) and inspected: every dummy's `PhysicalCardShadow`
   z-landed BEHIND the other friendly cards. Numbers: `FlyDummiesToCombatStack` bakes the
   demo's hardcoded **-0.01/card** landing z-step while the shipped deck layout steps
   **`zOffset` = 0.5/card** (CombatUXManager scene value; every `DeckPositionCalculator`
   mode uses `basePos.z - zOffset*index`). A card's rim (local z +0.2, dead-center) only
   renders in the inter-card gap when the stack step exceeds 0.2 — at 0.01 every rim was
   buried behind ~20 neighbour faces, so the flying deck showed no per-card shadows and
   the landing swap teleported the stack onto the 0.5-step model (shadows popped in one
   frame). That pop was the original "a shadow disappeared after the flip" sighting.
   (The earlier FloatStack-suppression suspicion is WITHDRAWN — suppression only touches
   BigShadow and is the shipped combat design.) Fix: dummy landing z = `-zOffset*i`
   (VISUAL-FIX(2026-10-04) in `FlyDummiesToCombatStack`; row 149).
4. **Minor fact for later work**: `MinionPhysicalCardParent.prefab` / `PhysicalCard.prefab`
   carry a DIFFERENT `PhysicalCardShadow` — active, alpha 1.00, offset (0.15, 0, 0.10) — a
   genuinely VISIBLE solid sliver. Combat spawns isMinion cards from the Minion prefab, so
   minion cards visibly differ from PhysicalCardParent cards in shadow structure. Not the
   flight flip's doing, but a real cross-prefab inconsistency worth a future pass.
5. ~~Closing evidence needed~~ Closed by the paused-scene probe (point 3); the fix awaits
   the user's next Play run (row 149 ⚠️).

## Part B — enemy cards + start card entrance slide (implemented 93f2b921, SUPERSEDED same day by Part C)

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

> **SUPERSEDED (2026-10-04 evening, user request "中立卡和敌方卡出现的时机和友方卡一样"):**
> the spawn-offset path was removed in the same day — with full dummy coverage (Part C below)
> every slot has a flying dummy, so a spawn offset on the REAL cards would visibly teleport
> them at the landing swap. The two tuning fields it proposed survived as
> `enemyCardSlideDemoPx` / `startCardSlideDemoPx` (now controlling the clone spawn heights).

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

## Part C — full dummy coverage: enemy + Start Card on the shared flight schedule (2026-10-04, approved + implemented)

User request: the neutral (Start) card and enemy cards should APPEAR at the same timing as
the friendly cards — today (pre-Part-C) they popped in at the landing while friendly slots
were flown in by the borrowed shop dummies.

Feasibility fact (verified): `GatherDecks` runs in `CombatManager.Update`'s state machine
(`CombatState.GatherDeckLists`), i.e. 1-2 frames AFTER the driver's phase flip, and
`RevealCards` stays input-blocked until landing — so the driver can read the fully-populated
`combinedDeckZone` at travel start without touching combat logic.

Implementation (all in `PhaseTransitionDriver`):

- `FlyDummiesToCombatStack` refactored into `BuildFlightGeometry` (struct with anchor /
  deck scale / step / arc / z-step) + `FlyDummyToSlot(dummy, slotIndex, fanCount, ..., flip)`;
  the per-card arc/scale/fan/flip code is unchanged (VISUAL-FIX blocks moved with it).
  `flip=false` skips the mid-arc cover: enemy clones spawn ALREADY face-down (hidden info —
  their faces must never flash) and the Start Card clone stays face-up (its spawn rule).
- `CoverUncoveredSlotsWithDummies` coroutine: waits (≤0.5s guard) for `combinedDeckZone`,
  then for every index >= the borrowed player dummies instantiates one presentation clone
  with the same prefab selection as `InstantiateAllPhysicalCards` (physical / minion /
  start-card prefab), wires `cardImRepresenting` (enemy orange back + opponent art come free
  per frame), spawns it at `slot + (0, slide, 0)` and flies it on the shared schedule.
  `slide` = `enemyCardSlideDemoPx` / `startCardSlideDemoPx` (new config fields, default 140
  = the shipped look). `combinedDeckZone[i]` IS the card `InstantiateAllPhysicalCards` puts
  on slot i, so clone slots match the real landing slots by construction.
- The clones join the shared dummy list → destroyed by the existing landing swap; the
  landing wait uses the full count and compensates the 1-2-frame schedule lag by waiting on
  elapsed wall time (`travelStart` captured before the camera tween).
- `enemySlideDemoPx` is back to pure enemy-HUD semantics (its card-entrance reuse ended).
- Headless/bypass/driver-off: the cover coroutine never runs — everything lands the legacy
  way. Edge: a slot without a wired prefab stays landing-pop.

## Part D — entrance clones v2: real-layout landing + spawn-at-flight-start (2026-10-04 diagnosis; fix PENDING the user's 修改代码)

### What the user saw (GIF, frozen right after the camera landing)

The Part C clones sat as a tight stack ~2.3–3.8 units ABOVE the deck anchor — top-center of
the combat page, overlapping the enemy HUD zone, visually detached from the deck. ("检查一下
敌方/起始卡生成的位置，现在效果比较奇怪")

### Root causes (code-verified, three defects)

1. **Landing targets use a linear bake that does not match the FloatStack layout** — and it
   never did (latent since the 09-21 port, inherited by Part C):
   - `FlyDummyToSlot` flies every card to `anchor + stepWorld * i` (the 10-02 comment
     "dummy i lands on stack slot i" is wrong for FloatStack).
   - The real FloatStack slot (`DeckFloatStackLayout.ComputeSlotOffset`) is CENTERED around
     the anchor: `y = effStep * (N − 2j − 1) / 2 + lift` — deck bottom (j=0, LAST revealed)
     is the visual TOP of the stack; deck top (j=N−1, FIRST revealed = Start Card) is the
     LOWEST card on screen. For a 6-card deck the Start Card's real slot is ≈ anchor−1.47
     while the clone target is anchor+1.76 — 3.2 units off, vertical order inverted.
   - The player dummies carried the same error all along (≤0.6 units for 2-3 card decks);
     the Start Card shuffle re-flies every card right after landing, which masked it.
2. **Hover window**: clones spawn at t≈0.05s but fly at `delay = i * stagger` — a motionless
   column above the deck (up to ~1s for a 15-card deck). The friendly dummies never hover:
   their flight overlaps the camera travel end to end.
3. **Landing after the camera**: clone landing = delay + dur > transDur — later than the
   friendly cards, the opposite of the approved "出现的时机和友方卡一样" reading.

(GIF static from frame 1 = editor-unfocus freeze during recording — the
`runInBackground=false` trap — not a new stall in the transition itself.)

### v2 fix plan

1. **Real-layout landing**: flight target = `CombatUXManager.GetFinalDeckPositionForCard(
   phys, i)` (public; exact for every layout mode — FloatStack centering/lift/compress
   included). Applies to BOTH the clones and the player dummies, clearing the latent 09-21
   error. Known residual: `GetPositionOffset` jitter for a phys that never got `AssignOffset`
   may differ from the real card's — Cascade-only and masked by the landing swap + shuffle.
2. **No hover**: per-clone spawn AT flight start — `DOVirtual.DelayedCall(delay)` instantiates
   and flies immediately; no resting column exists at any time.
3. **Spawn height retune**: `enemyCardSlideDemoPx` / `startCardSlideDemoPx` default
   140 → 60 (≈0.98 world units, just above the stack front); still Inspector-tunable per
   card class.
4. **Schedule unchanged**: clones continue after the player section on the shared stagger
   (the approved same-timing reading); the landing swap destroys them as before; headless/
   bypass keeps the legacy path.

Status: IMPLEMENTED 2026-10-05 (EditMode suite green; Play verification pending —
RegressionChecklist row 151 ⚠️). One deviation from the letter of item 1, same math:
the flight target routes through the NEW count-parameterized seam
`CombatUXManager.GetLayoutSlotBasePosition(i, futureCount)` (+ the now-public
`GetDeckScaleAtIndex(i, futureCount)`) instead of `GetFinalDeckPositionForCard(phys, i)`
directly — that method's jitter term calls `DeckLayoutOffsetProvider.GetPositionOffset`,
which AUTO-ASSIGNS a random offset for a phys it has never seen, so routing a dummy/clone
phys through it would bake a random jitter into the flight target (the real card's fresh
landing jitter is unmatched anyway; the landing swap + opening shuffle mask the residual,
exactly as the note above accepts). Item 1's count reality is also handled here: the
future count N (= combinedDeckZone.Count) only exists after GatherDecks (1-2 frames after
the phase flip), so the WHOLE flight schedule — player dummies included — is created after
the zone wait, and the landing wait is measured from the scheduling moment (the old
wall-time back-subtraction from travelStart is gone). The degenerate zone-timeout path
flies the player dummies on the legacy linear bake so they never strand at shop positions.

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
