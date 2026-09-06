# Card Name Horizontal Squash (replaces ellipsis overflow)

2026-09-06 · status: **done** (code + demo + docs + Unity editor verification 2026-09-06)

Change request: card names must never be ellipsized. When the name is too long for its
bottom-row box, horizontally squash the glyphs until it fits — **no lower limit, no
ellipsis fallback** (user decision 2026-09-06). Scope: Unity implementation + UIKitDemo
+ spec docs (user decision 2026-09-06).

## Current state (verified)

- Spec: `docs/demo/UIKitDemo.html` §03 — `.card-name` uses `white-space: nowrap;
  overflow: hidden; text-overflow: ellipsis` (line ~321-328); zone comment at line ~262
  says "name (left, bold, ellipsis)". `docs/UIUX_Guidelines.md` §3.2 item 5 records
  "name (left, bold, ellipsis on overflow)".
- Unity: `PhysicalCardParent.prefab` overrides on nested `PhysicalCard/CardName`
  (TMP component fileID 5323925544510727272, RectTransform 8439653587330453448):
  fontSize 15.6, `m_overflowMode = 1` (Ellipsis), NoWrap (`m_TextWrappingMode = 0`),
  autosize OFF, box 10.5 x 2.5 @ (-0.332, -1.854), pivot (0.5, 0.5), characterSpacing -5.
- Name text is assigned in `CardPhysObjScript.UpdateStatusEffectDisplay`
  (`Assets/Scripts/UXPrototype/CardPhysObjScript.cs:385-402`, both the `<b>`-wrapped
  branch and the fallback branch).

## Chosen approach: TMP `characterHorizontalScale`

`TMP_Text.characterHorizontalScale` (public API in the project's TMP, ugui 2.0 —
`Library/PackageCache/com.unity.ugui@*/Runtime/TMP/TMP_Text.cs:670`) scales BOTH glyph
rendering vertices (`TextMeshPro.cs:2840/2850`) and layout advance widths
(`TextMeshPro.cs:3853`), and TMP's own overflow math accounts for it
(`TMP_Text.cs:4467`). Squashing happens inside text layout, so the name stays
left-aligned in its box with **no pivot/position changes** and no effect on the attack
print. Advance scales linearly with the factor, so one measurement gives the exact fit
ratio.

### Rejected alternatives

- TMP auto-size + `charWidthMaxAdj`: auto-size shrinks point size uniformly (not a
  squash), and `charWidthAdjDelta` only engages on the WRAPPING path
  (`TMP_Text.cs:4473-4524` requires `textWrapMode != NoWrap`) — dead end for our NoWrap
  name print. The existing `m_charWidthMaxAdj = 50` override on the prefab is inert.
- RectTransform `localScale.x` squash: pivot is (0.5, 0.5), so the left edge would drift;
  requires a prefab pivot change to (0, y) plus anchoredPosition compensation, and scales
  the whole rect rather than just the glyphs. More fragile than the TMP property.

## Changes

### 1. `Assets/Scripts/UXPrototype/CardPhysObjScript.cs`

- Add a private helper:

```csharp
/// <summary>
/// Card names are never ellipsized: squash glyphs horizontally until the name fits
/// its box (no lower clamp — user decision 2026-09-06). characterHorizontalScale
/// scales both glyph vertices and advance widths inside TMP layout, so left
/// alignment and the attack print are unaffected.
/// </summary>
private void FitCardNamePrint()
{
	if (cardNamePrint == null) return;
	cardNamePrint.characterHorizontalScale = 1f;
	cardNamePrint.ForceMeshUpdate();
	float boxWidth = cardNamePrint.rectTransform.rect.width;
	float textWidth = cardNamePrint.preferredWidth;
	if (boxWidth <= 0f || textWidth <= boxWidth) return;
	cardNamePrint.characterHorizontalScale = boxWidth / textWidth;
}
```

- Call `FitCardNamePrint()` once after the text-assignment if/else in
  `UpdateStatusEffectDisplay` (covers both branches; also covers the
  `cardStatusEffectPrint == null` fallback path). Resetting the scale to 1 before
  measuring keeps repeated calls idempotent.
- Name print margins are 0, so `preferredWidth` needs no margin compensation.

### 2. `Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab`

- Remove the inert `m_charWidthMaxAdj = 50` override on the CardName TMP component.
- Keep `m_overflowMode = Ellipsis` as a theoretical safety net; with unlimited squash
  it is never hit. No other prefab changes (pivot, position, size all untouched).

### 3. `docs/demo/UIKitDemo.html`

- `.card-name`: drop `overflow: hidden; text-overflow: ellipsis;`; keep
  `white-space: nowrap` and `min-width: 0`; add `transform-origin: left center;`.
- Add a small `fitCardNames()` JS helper: for each `.card-name`, reset
  `style.transform`, compute `clientWidth / scrollWidth`, and when < 1 set
  `transform: scaleX(ratio)`. Call it after demo render / card text updates.
- Update the §03 zone comment: "name (left, bold, ellipsis)" -> "name (left, bold,
  squashed horizontally to fit — never ellipsized)".

### 4. `docs/UIUX_Guidelines.md`

- §3.2 item 5: replace "name (left, bold, ellipsis on overflow)" with "name (left,
  bold, horizontally squashed to fit on overflow — never ellipsized; no squash floor)".
- The desc print's 2-line clamp + `…` (item 3) is unchanged — ellipsis stays for desc.

### 5. `plans/plan-card-template-v1.1-port-2026-09-05.md`

- Append a dated follow-up entry: name overflow switched from Ellipsis to unlimited
  horizontal squash via `characterHorizontalScale`; reference this plan.

No `AGENTS.md` change needed (its v1.1 template line does not mention name ellipsis).

## Verification (editor, prefab stage; play mode only on explicit request)

- Short name (<= 4 chars): scale stays 1, pixel-identical to current.
- Long name (8-10+ chars): fully visible, visibly squashed, left-aligned, no overlap
  with the attack print, no `…`.
- `<b>` bold path and the no-`cardStatusEffectPrint` fallback path both measured
  correctly (rich-text tags excluded from width).
- Repeat `UpdateStatusEffectDisplay` calls stay idempotent (scale reset before measure).
- Console: zero errors/warnings.
- `docs/RegressionChecklist.md`: NOT applicable (behavior change by request, not a
  visual bug fix) — no row added.

## Risks

- Extremely long names become unreadable slivers — accepted by user (no squash floor).
- If the name box is ever resized, the fit adapts automatically (box width is read at
  runtime).

## Handoff: Unity-side verification (for a session WITH Unity MCP)

Code + demo + docs are done (2026-09-06). What remains is in-editor verification. The
authoring session had no Unity MCP, so none of the steps below have run yet.

### What to verify

1. Compile: domain reload finishes with zero console errors.
2. Prefab-stage fit check on `Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab`
   (nested `PhysicalCard/CardName` TMP print; box width 10.5 local units):
   long name squashes (`characterHorizontalScale` < 1), renders fully (no U+2026 `…`),
   stays left-aligned, does not collide with `AttackPrint`.
3. Idempotency: invoking the fit twice yields the same scale (it resets to 1 before
   measuring).
4. Bold path: text is assigned as `"<b>" + name + "</b>"` — measure with the tags on.
5. Longest-name audit: find the longest real `displayName` under
   `Assets/Prefabs/Cards/` and confirm it still fits at an acceptable squash (user
   accepted unlimited squash, but report the worst ratio).
6. Play Mode: only if the user explicitly asks (project rule).

### Non-destructive verification recipe (Unity MCP `execute_code`, compiler auto)

Use `LoadPrefabContents` / `UnloadPrefabContents` — never save the prefab after the
test, and never leave test text in `m_text`. `FitCardNamePrint` is private; invoke by
reflection. If `CardPhysObjScript` does not resolve, use
`System.Type.GetType("CardPhysObjScript, Assembly-CSharp")`.

```csharp
var path = "Assets/Prefabs/UXPrototype/PhysicalCardParent.prefab";
var root = UnityEditor.PrefabUtility.LoadPrefabContents(path);
var phys = root.GetComponentInChildren<CardPhysObjScript>(true);
var t = typeof(CardPhysObjScript);
var tmp = (TextMeshPro)t.GetField("cardNamePrint").GetValue(phys);
var fit = t.GetMethod("FitCardNamePrint",
	System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

tmp.text = "<b>名字很长的深海灯笼鱼</b>"; // 9-char stress case, same as demo
fit.Invoke(phys, null);
float scale = tmp.characterHorizontalScale;      // expect < 1
float boxW = tmp.rectTransform.rect.width;       // 10.5
bool hasEllipsis = tmp.text.IndexOf('…') >= 0; // expect false
int visible = tmp.textInfo.characterCount;       // expect full length after squash

fit.Invoke(phys, null);                          // idempotency: scale unchanged
float scale2 = tmp.characterHorizontalScale;

tmp.text = "<b>蓝色的鱼</b>";                     // short name
fit.Invoke(phys, null);
float shortScale = tmp.characterHorizontalScale; // expect 1

UnityEditor.PrefabUtility.UnloadPrefabContents(root); // discard, DO NOT save
// assert: scale < 1, scale == scale2, !hasEllipsis, shortScale == 1
```

### Acceptance criteria

- All 6 verification items pass; console clean.
- If `characterHorizontalScale` fails to compile (API missing), the TMP version drifted
  — fallback: keep `overflowMode = Ellipsis` and squash via
  `cardNamePrint.rectTransform.localScale.x` with a left-edge position compensation
  (pivot is (0.5, 0.5)); record the deviation in this plan before implementing.

### Notes for the next session

- `StartCard.prefab` and `EmptyCardSpace.prefab` have their own `CardName` prints but
  share `CardPhysObjScript`, so they inherit the squash from code — no prefab edits
  needed there; spot-check one if convenient.
- Ellipsis char setup (U+2026 via NotoSansSymbols2 fallback) is untouched — desc print
  still 2-line clamps with `…`.
- Editing conventions: CRLF + tabs, English comments; this plan file is the single
  source of truth — record verification results here (flip status to fully done).

## Verification results (2026-09-06, Unity 6000.3.9f1, Unity MCP)

All 6 handoff items pass; console clean (only the pre-existing Input Manager
deprecation warning and a transient MCP websocket warning).

1. **Compile**: forced recompile + domain reload, zero errors.
2. **Prefab-stage fit check** (`LoadPrefabContents`, discarded without saving): box
   width 10.5 confirmed. 10-char bold stress name `<b>名字很长的深海灯笼鱼</b>` →
   `characterHorizontalScale` 0.6608, all 10 chars visible, no U+2026 in the rendered
   `textInfo`, no ellipsis fallback hit.
3. **Idempotency**: second fit invocation yields the identical scale (0.6608).
4. **Bold path**: measured with `<b>` tags on; rich-text tags correctly excluded from
   the width (10 visible chars).
5. **No collision with AttackPrint**: verified on a live (unsaved) scene instance via
   world-space renderer bounds — squashed name ink ends 0.14 world units (~5.5% card
   width) before the `12` attack ink; both sit inside the card face with symmetric
   outer padding. Name stays left-aligned (ink starts at the box's left edge).
   (Positioned game-view screenshots don't show the print layer — the TMP prints sit
   inside the face mesh and need the in-game render setup — so the bounds data was
   used as ground truth; test instance destroyed, screenshots deleted.)
6. **Longest-name audit** (288 unique names across `Assets/Prefabs/Cards`): worst real
   (CJK) names are 9 chars — `少了一页的住户名单`, `多出来的第十三张床` → scale **0.735**,
   well readable. 8-char names sit at 0.827. Only test cards go lower (52-char
   English test name → 0.281); no gameplay card comes close. Unlimited squash is
   therefore never exercised by real content in any meaningful way.

No fallback needed — `characterHorizontalScale` compiled and behaved exactly as
analyzed. Plan fully done.

## Follow-up (2026-09-06): unbound placeholder also squashes

The scene's idle `PhysicalCardParent` instance (`cardImRepresenting == null`) kept
showing its serialized placeholder name (`瞥见一扇奇怪的窗`) ellipsized, because
`UpdateStatusEffectDisplay` early-returned before the fit ran. Fix (user-approved):
the null-card path now calls `FitCardNamePrint()` before returning, so any face-up
instance squashes whatever name text it holds. Verified in prefab stage: placeholder
squashes to 0.827 (8 chars, matches the CJK audit table), all chars visible, no
ellipsis, idempotent across repeated calls. Edit-mode prefab-stage rendering still
ellipsizes (no `Update()` runs there) — cosmetic only, out of scope.
