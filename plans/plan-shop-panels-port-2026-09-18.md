# Shop Page Panels Port Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Port UIKitDemo §08 (v0.9/v1.1) shop-page layout into the engine: three world-space translucent panels (Shop / Deck / Upgrades) with headers + deck slot counter, reroll relocated into the Shop panel header, and an enriched top chrome bar (avatar+username, rarity-odds chips, wins/hearts chips, options placeholder) — per the user-annotated 2026-09-18 layout.

**Architecture:** All new visuals are runtime-built world objects (the 2026-09-18 world-entity direction — no uGUI, no new canvas). A new `ShopSectionPanels` component mirrors the `ShopChrome` Bootstrap pattern and owns panel backgrounds, headers, the deck counter, and the reroll button. `ShopUXManager` keeps ownership of all card lists (adds `_spawnedUtilityCards` for the Upgrades row) and notifies panels to re-fit after every relayout. Pure math (bounds, odds, formatting) lives in static/testable helpers.

**Tech Stack:** Unity 2022.x MonoBehaviour, TextMeshPro, DOTween, NUnit EditMode tests (`Assets/Scripts/Editor/Tests/`).

**Spec:** `docs/demo/UIKitDemo.html` §08 (shop page) + `docs/UIUX_Guidelines.md` §3.6; user-annotated layout screenshot 2026-09-18 (top bar: 离开商店/头像+用户名/HP/$/稀有度概率/胜场·心数/收入/选项; reroll inside Shop panel header; deck counter `03/05`; three panels; upgrades panel = owned utility passives, sell at half price).

## Global Constraints

- Line endings CRLF, tab indentation, English comments only (`AGENTS.md`).
- Colors ONLY via `GameColorPalette.<Name>Color` statics — never hardcode hex.
- Shop is world-only: no uGUI; one physics input pipeline via `ShopInputGate`; interactive elements are `PhysButton`; read-only elements are flat `HudChip`s (R2 converse).
- New shop visuals are runtime-built (pattern of `ShopChrome.Bootstrap`) — no new scene objects for visuals; the only scene edits are two serialized tuning values and deletion of two superseded label objects (Task 6).
- UI copy stays English per engine convention: `Shop`, `Deck`, `Upgrades`, `Reroll: $N`, `Exit`, options glyph `❚❚`.
- The `✦` glyph resolves via the NotoSansSymbols2 fallback wired on the RobotoCondensed SDF assets (see `CardPhysObjScript.cs:488-490`) — chips using `chromeFont` can print `✦` directly. `▦` (U+25A6) / `♥` (U+2665) need an in-editor glyph check; if tofu, use `W`/`H` prefix labels instead (verification step in Task 3).
- Existing refresh triggers: `ShopManager` calls `ShopChrome.RefreshIfActive()` in `EnterShop`/`BuyFunc`/`SellFunc`/`Reroll` and `ShowIfActive`/`HideIfActive` in `EnterShop`/`ExitShop` — panels get sibling calls at the same sites.
- Repo has uncommitted user changes (incl. `GameScene.unity`) — commit ONLY paths listed per task; never `git add -A`.

## Established facts (executor context — verified 2026-09-18)

- `ShopChrome` (`Assets/Scripts/UXPrototype/ShopChrome.cs`, 323 lines): static `Bootstrap(Sprite, TMP_FontAsset)` called from `ShopUXManager.Start()` (`ShopUXManager.cs:606`). Builds 4 chips + exit/reroll buttons from `RoundedCorner.png` (9-sliced, guid `ee4c724560d27684d969ba363b913448`) + `chromeFont` (RobotoCondensed-Regular SDF). `BandHeight = 1.0f` const; `BandBottomWorldY()` = cam.y + orthoSize − BandHeight. `Refresh()` (:261-300) reads `ShopManager.me.purse.value`, `GetCurrentPayday()`, `CombatManager.Me.ownerPlayerStatusRef.hp/hpMax`, `playerDeckRef.deck.Count`, `deckSize.value`; `RefreshRerollState()` (:302-322) handles reroll label/disabled; `SetRerollRolling(bool)` called from `ShopUXManager.OnReroll()` (:964) and `SpawnNewShopCardsAfterDelay()` (:1030). No band background sprite is rendered today.
- `HudChip` (`Assets/Scripts/UXPrototype/HudChip.cs`): `Bind(SpriteRenderer panel, TMP_Text label)`, `SetText(string)`, `SetVisible(bool)`. Styling = `GameColorPalette.TooltipBgColor` (opaque navy) panel, `TooltipTextColor`/`HighlightColor` text.
- `PhysButton` (`Assets/Scripts/UXPrototype/PhysButton.cs`): `SetWorldAction(Action)`, `SetDisabled(bool)`, `ConfigureWorldFaceSize(Vector2, Vector2)`, `SetWorldFace(SpriteRenderer)`, `SetShadowTransform(Transform)`; hover label swap via public `label` + `hoverText`.
- `ShopManager` (`Assets/Scripts/Managers/ShopManager.cs`): `public static ShopManager me`; `IntSO purse`, `payCheck`, `deckSize`, `maxDeckSize`, `RerollPriceRef`; `GetCurrentPayday()` (:553); `FreeRerollsLeft` (:493); `RerollPrice` (:496); `GetCardPrice(CardScript)` (:177); private `GetActiveRarityWeightRef()` (:125-143); `rarityWeightRef` + `sessionRarityWeights` (table `0→_0 (90/9/1)`, `2→_1 (75/22/3)`, `4→_2 (60/35/5)`); `SellFunc` half price at :359 (`purse.value += GetCardPrice(cardScript) / 2`); `public GameObject sectionIdentifier` toggled in `EnterShop` (:401) / `ExitShop` (:431).
- Wins/hearts state EXISTS: `PhaseManager` (`Assets/Scripts/Managers/PhaseManager.cs:16-19`) `public IntSO wins, winCon, hearts, heartMax` — assets `Assets/SORefs/PlayerRefs/attributes/WinAmount.asset` (0), `WinCondition.asset` (6), `HeartAmount.asset` (1), `HeartMax.asset` (3). Updated only in combat-result branch; no shop UI reads them yet.
- Username: `PlayerIdentity.Username` (static, `Assets/Scripts/Net/PlayerIdentity.cs:45`; `???` when unset — mirror `CombatIconPresenter.RefreshNameLabels`).
- `ShopUXManager` (`Assets/Scripts/UXPrototype/ShopUXManager.cs`, 1148 lines): singleton `Instance`; serialized `xOffset 3.2`, `yOffset 4.5`, `objPerRow 3`, `physCardSize 0.8`, `shopItemPos (0,2.2,0)`, `playerDeckPos (0,-2.5,0)`, `emptySlotZOffset 0.1`, `chromeSprite`, `chromeFont`. Layout formulas: `GetPlayerDeckSlotPosition(i)` (:250), `GetShopItemSlotPosition(i)` (:286), `CurrentDeckPos()` (:298, shifts by shelf rows), `GetUtilityZoneBaseSlot(occupyingTrackEndSlot)` (:263, today returns `deckSize.value`), `StackSlotAssigner` (:314-354, routes by `occupiesDeckSlot`), `RelayoutPlayerDeckCards()` (:363), `RelayoutAll()` (:382), `RelayoutDeckBand()` (:402), `Update()` consumes `_layoutDirty` (:625-633), `HandleCameraScroll()` (:699-716, dynamic min-Y via `ComputeDynamicMinY` :648-677 reading `_spawnedPlayerCards`/`_spawnedEmptySlots` targets), `SpawnEmptySlots(from,count,animate)` (:919-950), `InstantiateShopPhysCards()` (:94-174), `InstantiatePlayerDeckPhysCards()` (:513-601, skips only `!physicalDeckCard` at :552), `OnCardPurchased` (:737-800), `OnCardSold` (:811-872), `PulsePlayerCard` (:1133-1147), `OnReroll` (:957-970). Scene labels `Shop:` / `YourDeck:` are world TMPs under `ShopManager/section identifiers` (toggled by `ShopManager` only).
- `ShopRarityWeightSO` (`Assets/Scripts/SOScripts/ShopRarityWeightSO.cs`): `public List<RarityWeightEntry> entries`, `public float GetWeight(Rarity)` (1f default), nested `public class RarityWeightEntry { public EnumStorage.Rarity rarity; public float weight = 1f; }`. Namespace `DefaultNamespace.Managers`.
- `EnumStorage.Rarity { Common, Uncommon, Rare }` (:74-79); `EnumStorage.UtilityKind` (:83-99); `CardScript.IsUtilityPassive => isPassive && utilityKind != None` (`CardScript.cs:180`), `occupiesDeckSlot` (:33), `physicalDeckCard` (:31).
- Palette: `Assets/Resources/GameColorPalette.asset`; accessor pattern `public static Color TooltipBgColor => Me != null && Me.tooltipBg != null ? Me.tooltipBg.value : Color.white;` (`GameColorPalette.cs`). Orphan `Assets/SORefs/Colors/TooltipBg.asset` (black, a=0.85) is the reference YAML shape for a new ColorSO.
- Tests: NUnit in `Assets/Scripts/Editor/Tests/`, no asmdef, fixture style see `ShopBoardPipelineTests.cs`; `GameColorPaletteWiringTests.AllColorFields_Wired` reflects every `ColorSO` field on the palette asset and requires wiring under `Assets/SORefs/Colors/`. No EditMode test touches ShopUXManager/ShopChrome today; there is NO Unity MCP in the authoring session — tests are written per TDD and executed via Unity Test Runner (EditMode) by the user or a desktop-driven run.
- Camera: ortho size 6.06; chrome pinned via `ShopChromeAnchor` at cam z + `CameraForwardOffset` (2f in front of the camera). Panels live in the shop world at z 0.5 (behind cards at z 0; larger z = farther from camera at z −100).

---

### Task 1: `ShopPanelBg` palette color

**Files:**
- Create: `Assets/SORefs/Colors/ShopPanelBg.asset`
- Modify: `Assets/Scripts/SOScripts/GameColorPalette.cs` (add field + accessor under the `Overlay Panels` header group)
- Modify: `Assets/Resources/GameColorPalette.asset` (wire the new field)

**Interfaces:**
- Produces: `GameColorPalette.ShopPanelBgColor` (static `Color`, translucent black a=0.85) consumed by Tasks 3 (dark chips) and 4 (panel backgrounds).

- [ ] **Step 1: Create the ColorSO asset.** Read `Assets/SORefs/Colors/TooltipBg.asset` and write `Assets/SORefs/Colors/ShopPanelBg.asset` with the same YAML shape but: new `guid` (generate any unique 32-hex, e.g. `shop`+random — must differ from all existing guids; check none conflicts with `grep -r "guid" Assets/SORefs/Colors/TooltipBg.asset`), `m_Name: ShopPanelBg`, and the color value `rgba: {r: 0, g: 0, b: 0, a: 0.85}`.

- [ ] **Step 2: Add field + accessor.** In `GameColorPalette.cs`, inside the `[Header("Overlay Panels")]` group after `resultPanelText`:
```csharp
	public ColorSO shopPanelBg;
```
and with the other static accessors:
```csharp
	public static Color ShopPanelBgColor => Me != null && Me.shopPanelBg != null ? Me.shopPanelBg.value : new Color(0f, 0f, 0f, 0.85f);
```

- [ ] **Step 3: Wire the palette asset.** In `Assets/Resources/GameColorPalette.asset`, add `shopPanelBg: {fileID: <fileID>, guid: <new guid from Step 1>, type: 2}` next to the other `ColorSO` field entries (match the existing entry format exactly; the fileID of a ColorSO main object is `11400000`).

- [ ] **Step 4: Run the wiring test.** Unity Test Runner → EditMode → `GameColorPaletteWiringTests.AllColorFields_Wired` must pass (it reflects the new field and requires a non-null wired ColorSO under `Assets/SORefs/Colors/`). If Unity MCP `run_tests` is unavailable, run via the Test Runner window and paste the result into the commit message.

- [ ] **Step 5: Commit**
```bash
git add Assets/SORefs/Colors/ShopPanelBg.asset Assets/Scripts/SOScripts/GameColorPalette.cs Assets/Resources/GameColorPalette.asset
git commit -m "feat(shop): ShopPanelBg palette color for translucent world panels"
```

---

### Task 2: Rarity-odds percentage API

**Files:**
- Modify: `Assets/Scripts/SOScripts/ShopRarityWeightSO.cs` (add `GetOddsPercents`)
- Modify: `Assets/Scripts/Managers/ShopManager.cs` (public wrapper near `GetActiveRarityWeightRef` :125-143)
- Test: `Assets/Scripts/Editor/Tests/ShopRarityWeightSOTests.cs` (new)

**Interfaces:**
- Consumes: nothing.
- Produces: `ShopRarityWeightSO.GetOddsPercents(out float commonPct, out float uncommonPct, out float rarePct)`; `ShopManager.GetRarityOddsPercents(out float commonPct, out float uncommonPct, out float rarePct)` — consumed by Task 3 rarity chips.

- [ ] **Step 1: Write the failing test** `Assets/Scripts/Editor/Tests/ShopRarityWeightSOTests.cs`:
```csharp
using DefaultNamespace.Managers;
using NUnit.Framework;

public class ShopRarityWeightSOTests
{
	private static ShopRarityWeightSO MakeWeights(float common, float uncommon, float rare)
	{
		ShopRarityWeightSO so = ScriptableObject.CreateInstance<ShopRarityWeightSO>();
		so.entries.Add(new ShopRarityWeightSO.RarityWeightEntry { rarity = EnumStorage.Rarity.Common, weight = common });
		so.entries.Add(new ShopRarityWeightSO.RarityWeightEntry { rarity = EnumStorage.Rarity.Uncommon, weight = uncommon });
		so.entries.Add(new ShopRarityWeightSO.RarityWeightEntry { rarity = EnumStorage.Rarity.Rare, weight = rare });
		return so;
	}

	[Test]
	public void GetOddsPercents_9091_NormalizesToHundred()
	{
		ShopRarityWeightSO so = MakeWeights(90f, 9f, 1f);
		so.GetOddsPercents(out float c, out float u, out float r);
		Assert.AreEqual(90f, c, 0.01f);
		Assert.AreEqual(9f, u, 0.01f);
		Assert.AreEqual(1f, r, 0.01f);
	}

	[Test]
	public void GetOddsPercents_MissingTier_CountsAsWeightOne()
	{
		ShopRarityWeightSO so = MakeWeights(1f, 1f, 0f); // no Rare entry -> GetWeight returns 1f
		so.entries.RemoveAll(e => e.rarity == EnumStorage.Rarity.Rare);
		so.GetOddsPercents(out float c, out float u, out float r);
		Assert.AreEqual(33.3333f, c, 0.01f);
		Assert.AreEqual(33.3333f, u, 0.01f);
		Assert.AreEqual(33.3333f, r, 0.01f);
	}

	[Test]
	public void GetOddsPercents_ZeroTotal_ReturnsZeros()
	{
		ShopRarityWeightSO so = MakeWeights(0f, 0f, 0f);
		so.GetOddsPercents(out float c, out float u, out float r);
		Assert.AreEqual(0f, c);
		Assert.AreEqual(0f, u);
		Assert.AreEqual(0f, r);
	}
}
```
(If the file uses tabs/CRLF per `AGENTS.md`; add `using UnityEngine;` — omitted above only if implicit usings are off in this project; check a sibling test e.g. `UtilityShopBonusTests.cs` and match.)

- [ ] **Step 2: Run test → verify FAIL** (`GetOddsPercents` undefined).

- [ ] **Step 3: Implement** in `ShopRarityWeightSO.cs`:
```csharp
	/// <summary>
	/// Normalized roll odds per rarity tier (percent, 0-100) for HUD display.
	/// Tiers missing from the table count as weight 1 (matches GetWeight).
	/// All-zero total yields 0/0/0.
	/// </summary>
	public void GetOddsPercents(out float commonPct, out float uncommonPct, out float rarePct)
	{
		float common = GetWeight(EnumStorage.Rarity.Common);
		float uncommon = GetWeight(EnumStorage.Rarity.Uncommon);
		float rare = GetWeight(EnumStorage.Rarity.Rare);
		float total = common + uncommon + rare;
		if (total <= 0f)
		{
			commonPct = uncommonPct = rarePct = 0f;
			return;
		}
		commonPct = common / total * 100f;
		uncommonPct = uncommon / total * 100f;
		rarePct = rare / total * 100f;
	}
```

- [ ] **Step 4: Add the ShopManager wrapper** (public surface for the chrome; keep `GetActiveRarityWeightRef` private):
```csharp
	/// <summary>Roll odds (percent) of the active rarity weight table, for shop HUD chips.</summary>
	public void GetRarityOddsPercents(out float commonPct, out float uncommonPct, out float rarePct)
	{
		ShopRarityWeightSO active = GetActiveRarityWeightRef();
		if (active != null)
		{
			active.GetOddsPercents(out commonPct, out uncommonPct, out rarePct);
			return;
		}
		commonPct = uncommonPct = rarePct = 0f;
	}
```

- [ ] **Step 5: Run tests → verify PASS** (`ShopRarityWeightSOTests`, 3 tests).

- [ ] **Step 6: Commit**
```bash
git add Assets/Scripts/SOScripts/ShopRarityWeightSO.cs Assets/Scripts/Managers/ShopManager.cs Assets/Scripts/Editor/Tests/ShopRarityWeightSOTests.cs
git commit -m "feat(shop): rarity-odds percentage API for HUD chips"
```

---

### Task 3: Chrome top-bar enrichment (`ShopChrome.cs`)

**Files:**
- Modify: `Assets/Scripts/UXPrototype/ShopChrome.cs` (Build layout, Refresh, remove deck chip, remove reroll button + RefreshRerollState + SetRerollRolling)

**Interfaces:**
- Consumes: Task 1 `GameColorPalette.ShopPanelBgColor`; Task 2 `ShopManager.GetRarityOddsPercents`; `PhaseManager.wins/winCon/hearts/heartMax`; `PlayerIdentity.Username`.
- Produces: unchanged public surface EXCEPT removal — `SetRerollRolling` and reroll refresh move to `ShopSectionPanels` (Task 4); `ShopUXManager` call sites updated in Task 4. New chips are private; `BandHeight` grows 1.0f → 2.6f.

- [ ] **Step 1: Update constants.** `BandHeight` 1.0f → 2.6f (three HUD rows must clear the shelf). Add row layout constants:
```csharp
	private const float RowPitch = 0.72f;        // vertical distance between HUD chip rows
	private const float RowOneYOffset = 0.55f;   // first row center below viewport top edge
	private const float SmallChipWidth = 1.9f;   // rarity / wins / hearts chips
	private const float SmallChipFontSize = 1.9f;
	private const float AvatarSquareSize = 0.42f;
```
Change `BandInsetFromTop` usage: rows are laid from the top edge down (see Step 2), keep `BandInsetFromTop` for the anchor but rows use `RowOneYOffset`.

- [ ] **Step 2: Rewrite `Build()`.** Layout (camera half-width `halfW`, top edge `topY = cam.transform.position.y + cam.orthographicSize`; all chips are flat read-only `HudChip`s except Exit/Options which are `PhysButton`s):
  - `exitButton` at `(leftX, topY - RowOneYOffset)` (reuse existing `CreateButton`).
  - `optionsButton` = `CreateButton("OptionsButton", "❚❚", rightX, out _)` at the same row; `SetWorldAction(() => Debug.Log("[ShopChrome] Options pressed (placeholder — no options menu yet)"))`.
  - Avatar chip at `(leftX, topY - RowOneYOffset - RowPitch)`: flat chip of width `ChipWidth`, label = `PlayerIdentity.Username` (fallback `"???"`), plus a child square `SpriteRenderer` (`SetupSliced`, `GameColorPalette.ShopPanelBgColor`) of size `(AvatarSquareSize, AvatarSquareSize)` positioned at the chip's left inside edge (`localPosition = new Vector3(-ChipWidth / 2f + AvatarSquareSize / 2f + 0.1f, 0f, -0.01f)`); store the label TMP as `_avatarLabel`.
  - Row 1 (y = `topY - RowOneYOffset`, centered as a group around x=0, lg chips): HP (`_hpChip`), Money (`_moneyChip`, accent). Group width = `2*ChipWidth + ChipSpacing`; start x = `-groupWidth/2 + ChipWidth/2`.
  - Row 2 (y = `topY - RowOneYOffset - RowPitch`, sm dark chips, `CreateChipVariant(width, fontSize, usePanelBg: true)`): rarity chips `_rarityCommonChip`, `_rarityUncommonChip`, `_rarityRareChip` labeled `"✦ 0%"`, `"✦✦ 0%"`, `"✦✦✦ 0%"` (the `✦` resolves via the RobotoCondensed SDF fallback — see Constraints).
  - Row 3 (y = `topY - RowOneYOffset - 2*RowPitch`, sm dark chips): `_winsChip` (`"▦ 0/6"`), `_heartsChip` (`"♥ 0/3"`), `_incomeChip` (existing, `"+$0/combat"`).
  - DELETE: `_deckChip` (slot counter moves to the Deck panel header, Task 4) and `_rerollButton`/`_rerollLabel` + their `CreateButton` (reroll moves to the Shop panel header, Task 4).
  - Factor `CreateChip` into `CreateChip(name, x, y, initialText, accent, float width = ChipWidth, float fontSize = ChipFontSize, bool darkPanel = false)`; when `darkPanel` is true the panel color is `GameColorPalette.ShopPanelBgColor` instead of `TooltipBgColor`.

- [ ] **Step 3: Extend `Refresh()`** (keep existing diff-guard pattern; add guards `_lastCommonOdds/_lastUncommonOdds/_lastRareOdds` as `int` (rounded), `_lastWins/_lastWinCon/_lastHearts/_lastHeartMax`, `_lastUsername`):
```csharp
	if (ShopManager.me != null)
	{
		ShopManager.me.GetRarityOddsPercents(out float c, out float u, out float r);
		if ((int)c != _lastCommonOdds) { _lastCommonOdds = (int)c; _rarityCommonChip.SetText("✦ " + (int)c + "%"); }
		if ((int)u != _lastUncommonOdds) { _lastUncommonOdds = (int)u; _rarityUncommonChip.SetText("✦✦ " + (int)u + "%"); }
		if ((int)r != _lastRareOdds) { _lastRareOdds = (int)r; _rarityRareChip.SetText("✦✦✦ " + (int)r + "%"); }
	}
	PhaseManager phaseManager = _phaseManager; // cached in Build (already FindObjectOfType'd there)
	if (phaseManager != null && phaseManager.wins != null && phaseManager.winCon != null
		&& phaseManager.hearts != null && phaseManager.heartMax != null)
	{
		if (phaseManager.wins.value != _lastWins || phaseManager.winCon.value != _lastWinCon)
		{ _lastWins = phaseManager.wins.value; _lastWinCon = phaseManager.winCon.value; _winsChip.SetText("▦ " + _lastWins + "/" + _lastWinCon); }
		if (phaseManager.hearts.value != _lastHearts || phaseManager.heartMax.value != _lastHeartMax)
		{ _lastHearts = phaseManager.hearts.value; _lastHeartMax = phaseManager.heartMax.value; _heartsChip.SetText("♥ " + _lastHearts + "/" + _lastHeartMax); }
	}
	string username = PlayerIdentity.Username;
	if (string.IsNullOrEmpty(username)) username = "???";
	if (username != _lastUsername) { _lastUsername = username; _avatarLabel.text = username; }
```
Also ensure `ShowIfActive()` runs `Refresh()` (it does today — keep). DELETE the `RefreshRerollState()` method body + its call in `Refresh()` and the `SetRerollRolling` method (Task 4 re-implements them on `ShopSectionPanels`).

- [ ] **Step 4: Glyph check.** Enter Play Mode → Shop: rarity chips show `✦`, wins shows `▦`, hearts shows `♥`, options button shows `❚❚`. If `▦`/`♥`/`❚❚` render as tofu, replace with text prefixes: `_winsChip.SetText("W " + ...)`, `_heartsChip.SetText("H " + ...)`, options label `"||"` — note the substitution in the commit message.

- [ ] **Step 5: Commit**
```bash
git add Assets/Scripts/UXPrototype/ShopChrome.cs
git commit -m "feat(shop): chrome top bar v2 — avatar, rarity odds, wins/hearts, options; drop deck chip"
```

---

### Task 4: `ShopSectionPanels` — three panels + headers + counter + reroll relocation

**Files:**
- Create: `Assets/Scripts/UXPrototype/ShopSectionPanels.cs`
- Modify: `Assets/Scripts/UXPrototype/ShopUXManager.cs` (`Start` Bootstrap, list accessors, layout hooks)
- Modify: `Assets/Scripts/Managers/ShopManager.cs` (panels refresh/show/hide call sites; stop toggling `sectionIdentifier`)
- Modify: `Assets/Scripts/UXPrototype/ShopChrome.cs` (remove reroll leftovers if any remain)
- Test: `Assets/Scripts/Editor/Tests/ShopSectionPanelsTests.cs` (new — pure helpers only)

**Interfaces:**
- Consumes: Task 1 `ShopPanelBgColor`; existing `ShopChrome` build recipes; `ShopManager` (`deckSize`, `FreeRerollsLeft`, `RerollPrice`, `purse`, `Reroll()`); `ShopUXManager` spawned lists + `physCardSize`.
- Produces (later tasks rely on these EXACT names):
  - `public static ShopSectionPanels Instance`;
  - `public static void Bootstrap(Sprite sprite, TMP_FontAsset font)`;
  - `public static void SetRerollRolling(bool rolling)`;
  - `public void RefreshLayout()` — refits all three panels from current content;
  - `public void RefreshCounter()` — updates the Deck panel `03/05` counter;
  - `public static float PanelZ = 0.5f` conceptually (const);
  - `ShopUXManager` additions: `public IReadOnlyList<GameObject> SpawnedShopCards/SpawnedPlayerCards/SpawnedUtilityCards/SpawnedEmptySlots` (Task 5 populates the utility list; expose all four now).
  - `public static Bounds ComputeContentBounds(IList<Vector3> centers, float halfWidth, float aboveHalfHeight, float belowHalfHeight)` (pure).

- [ ] **Step 1: Write the failing test** `Assets/Scripts/Editor/Tests/ShopSectionPanelsTests.cs`:
```csharp
using NUnit.Framework;
using UnityEngine;

public class ShopSectionPanelsTests
{
	[Test]
	public void ComputeContentBounds_SingleCenter_ReturnsCenteredBounds()
	{
		var centers = new System.Collections.Generic.List<Vector3> { new Vector3(1f, 2f, 0f) };
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		Assert.AreEqual(new Vector3(1f, 2f, 0f), b.center);
		Assert.AreEqual(2.8f, b.size.x, 0.001f);
		Assert.AreEqual(4.5f, b.size.y, 0.001f);
	}

	[Test]
	public void ComputeContentBounds_MultiRow_UsesExtremes()
	{
		var centers = new System.Collections.Generic.List<Vector3>
		{
			new Vector3(-3.2f, 0f, 0f), new Vector3(3.2f, 0f, 0f), new Vector3(0f, -4.5f, 0f)
		};
		Bounds b = ShopSectionPanels.ComputeContentBounds(centers, 1.4f, 2.0f, 2.5f);
		Assert.AreEqual((-3.2f - 1.4f), b.min.x, 0.001f);
		Assert.AreEqual((3.2f + 1.4f), b.max.x, 0.001f);
		Assert.AreEqual((0f + 2.0f), b.max.y, 0.001f);
		Assert.AreEqual((-4.5f - 2.5f), b.min.y, 0.001f);
	}

	[Test]
	public void FormatSlotCount_PadsToTwoDigits()
	{
		Assert.AreEqual("03/05", ShopSectionPanels.FormatSlotCount(3, 5));
		Assert.AreEqual("12/12", ShopSectionPanels.FormatSlotCount(12, 12));
	}
}
```

- [ ] **Step 2: Run test → verify FAIL** (class undefined).

- [ ] **Step 3: Implement** `Assets/Scripts/UXPrototype/ShopSectionPanels.cs` (~380 lines; CRLF + tabs; English comments):
```csharp
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// Runtime-built world-space section panels for the shop page (UIKitDemo §08 port):
/// translucent dark rounded rectangles behind the Shop / Deck / Upgrades rows, with
/// header labels, the Deck slot counter (03/05), and the reroll button (relocated
/// from the chrome band into the Shop panel header, 2026-09-18 annotated layout).
/// Built once by Bootstrap (mirrors ShopChrome); refitted by RefreshLayout, which
/// ShopUXManager calls after every relayout. No colliders — panels never intercept
/// physics input. Pure helpers (bounds/format) are static for EditMode tests.
/// </summary>
public class ShopSectionPanels : MonoBehaviour
{
	public static ShopSectionPanels Instance { get; private set; }

	private const float PanelZ = 0.5f;            // behind cards (z 0) and empty slots (z 0.1)
	private const float HeaderZ = 0.4f;           // in front of the panel background
	private const float HeaderHeight = 1.2f;      // header band above content
	private const float SidePadding = 0.55f;
	private const float TopPadding = 0.35f;
	private const float BottomPadding = 0.5f;
	private const float PanelGap = 0.7f;          // vertical gap between stacked panels
	private const float FitTweenDuration = 0.3f;
	// Card face half-extents at scale 1, from the EmptyCardSpace bake (3.3 x 4.7 units).
	// belowHalfHeight includes the price-button allowance (matches ShopChrome.CheckShelfClearance).
	private const float FaceHalfWidthFactor = 1.65f;
	private const float FaceAboveHalfHeightFactor = 2.35f;
	private const float FaceBelowHalfHeightFactor = 2.5f;

	private Sprite _sprite;
	private TMP_FontAsset _font;
	private SpriteRenderer _shopPanel;
	private SpriteRenderer _deckPanel;
	private SpriteRenderer _upgradesPanel;
	private TextMeshPro _shopHeader;
	private TextMeshPro _deckHeader;
	private TextMeshPro _upgradesHeader;
	private TextMeshPro _deckCounter;
	private PhysButton _rerollButton;
	private TMP_Text _rerollLabel;
	private bool _rerollRolling;

	private static ShopSectionPanels _instance; // kept private; use Instance
```
(Continue the full implementation:)
  - `public static void Bootstrap(Sprite sprite, TMP_FontAsset font)` — idempotent; creates scene-root `GameObject("Shop Section Panels")` (inactive), adds component + sets `Instance`, stores sprite/font, `Build()`, then `if (ShopManager.me != null && ShopManager.me.gamePhaseRef.currentGamePhase == EnumStorage.GamePhase.Shop) Show();`
  - `Build()` — three panels: each = child GameObject with `SpriteRenderer` (`sprite = _sprite`, `drawMode = Sliced`, `color = GameColorPalette.ShopPanelBgColor`, `sortingOrder = 0`), z = PanelZ. Headers: `CreateHeader(parent, "Shop"/"Deck"/"Upgrades")` — world TextMeshPro (`font = _font`, `fontSize = 3.2f`, `color = GameColorPalette.TooltipTextColor`, `alignment = Left`, `enableWordWrapping = false`), z = HeaderZ. Deck counter: right-aligned TextMeshPro (`alignment = Right`, `fontSize = 3.2f`, `color = GameColorPalette.HighlightColor`). Reroll button: replicate `ShopChrome.CreateButton` (Face SpriteRenderer `SetupSliced`-style with `GameColorPalette.OwnerCardColor` + Shadow `CardShadowColor`, label `ButtonFontSize 2.4f`, `OwnerTextColor`, `ConfigureWorldFaceSize(new Vector2(2.0f, 0.56f), Vector2.zero)`, `SetWorldAction(() => { if (ShopManager.me != null) ShopManager.me.Reroll(); })`).
  - `public static Bounds ComputeContentBounds(IList<Vector3> centers, float halfWidth, float aboveHalfHeight, float belowHalfHeight)` — pure; over `centers` min/max: `min = (minX - halfWidth, minY - belowHalfHeight)`, `max = (maxX + halfWidth, maxY + aboveHalfHeight)` in the XY plane, z ignored (return `new Bounds()` centered at content center with `size` from extents; implement exactly as: center = (min+max)/2, size = max-min; guard empty list → zero bounds at origin).
  - `public static string FormatSlotCount(int used, int total) => used.ToString("00") + "/" + total.ToString("00");`
  - `private static List<Vector3> TargetsOf(IReadOnlyList<GameObject> cards)` — collects `CardPhysObjScript.TargetPosition` (skip null).
  - `public void RefreshLayout()` — compute `s = ShopUXManager.Instance != null ? ShopUXManager.Instance.physCardSize : 0.8f`; halfW/aboveH/belowH from factors × s; fit each panel via `FitPanel(SpriteRenderer panel, TextMeshPro header, TMP_Text counterOrNull, IList<Vector3> centers, bool hasCounter)`:
    - `Bounds content = ComputeContentBounds(centers, halfW, aboveH, belowH)`; if `content.size == Vector3.zero` → panel + header `SetVisible(false)` (upgrades panel hides when no owned utilities).
    - panel size = `(content.size.x + 2*SidePadding, content.size.y + TopPadding + BottomPadding + HeaderHeight)`; center = `(content.center.x, content.center.y - HeaderHeight/2f + (TopPadding - BottomPadding)/2f, PanelZ)`.
    - Tween: `DOTween.To(() => panel.size, v => panel.size = v, targetSize, FitTweenDuration).SetEase(Ease.OutQuad)` and `panel.transform.DOMove(targetCenter, FitTweenDuration).SetEase(Ease.OutQuad)`.
    - header position = `(panel left inside edge + 0.25f, panel top - HeaderHeight/2f, HeaderZ)`; header text anchored left: set `rectTransform.pivot = new Vector2(0f, 0.5f)` and position accordingly; counter (Deck panel) at right inside edge − 0.25f with pivot `(1f, 0.5f)`.
    - reroll button position = `(panel right inside edge - ButtonWidth/2f - 0.15f, header Y, 0f)` — only the Shop panel.
    - Stack order note: Deck panel top = Shop panel bottom − PanelGap; compute Shop panel first, then Deck content bounds are already absolute world positions (grid-derived), so panels never overlap as long as row origins keep gaps — enforcement is `ShopChrome.CheckShelfClearance`-style only (no runtime assertion needed beyond existing log).
  - `public void RefreshCounter()` — `int total = ShopManager.me != null && ShopManager.me.deckSize != null ? ShopManager.me.deckSize.value : 0; int used = total - (ShopUXManager.Instance != null ? ShopUXManager.Instance.SpawnedEmptySlots.Count : 0); _deckCounter.text = FormatSlotCount(used, total);` (empty slots are spawned for every free deck slot, so `total - emptyCount` = occupied slots).
  - Reroll state — port from `ShopChrome.RefreshRerollState()` verbatim semantics: label `freeLeft > 0 ? "Reroll: $0" : "Reroll: $" + ShopManager.me.RerollPrice`, disabled when rolling or unaffordable, label color `CardTextSoftColor` vs `OwnerTextColor`; `public static void SetRerollRolling(bool rolling)` → sets instance flag + refresh. Called from `Refresh()` and after layout refits (reroll button Y depends on shop panel header Y).
  - `public void Refresh()` → `RefreshCounter(); RefreshRerollState();`
  - `public void Show()` / `Hide()` → `gameObject.SetActive(true/false)` + `Refresh()` on show.
  - `private void OnDestroy()` → clear `Instance`.

- [ ] **Step 4: Wire `ShopUXManager`.**
  - In `Start()` after `ShopChrome.Bootstrap(chromeSprite, chromeFont);` add: `ShopSectionPanels.Bootstrap(chromeSprite, chromeFont);`
  - Add public accessors next to the spawned-list declarations:
```csharp
	public IReadOnlyList<GameObject> SpawnedShopCards => _spawnedShopCards;
	public IReadOnlyList<GameObject> SpawnedPlayerCards => _spawnedPlayerCards;
	public IReadOnlyList<GameObject> SpawnedUtilityCards => _spawnedUtilityCards; // populated by Task 5
	public IReadOnlyList<GameObject> SpawnedEmptySlots => _spawnedEmptySlots;
```
  - Add `private readonly List<GameObject> _spawnedUtilityCards = new List<GameObject>();` (Task 5 uses it).
  - Hook panel refits at the end of `RelayoutDeckBand()` (:402-426), `RelayoutAll()` (:382-395), and `SpawnEmptySlots()` (:919-950): `ShopSectionPanels.Instance?.RefreshLayout();` — and in `SpawnEmptySlots` also `ShopSectionPanels.Instance?.RefreshCounter();`.
  - Switch reroll rolling call sites: `ShopChrome.SetRerollRolling(true/false)` in `OnReroll()` (:964) and `SpawnNewShopCardsAfterDelay()` (:1030) → `ShopSectionPanels.SetRerollRolling(true/false)`.

- [ ] **Step 5: Wire `ShopManager` refresh/show/hide.** At each existing `ShopChrome.RefreshIfActive()` call site (`EnterShop` :399 area, `BuyFunc` :340, `SellFunc` :370, `Reroll` :519) append `ShopSectionPanels.Instance?.RefreshIfActive();` — implement `RefreshIfActive()` on `ShopSectionPanels` (`if (Instance != null && Instance.gameObject.activeSelf) Instance.Refresh();`). At `EnterShop` (`ShowIfActive`) append `ShopSectionPanels.Instance?.ShowIfActive();`, at `ExitShop` (`HideIfActive`) append `...HideIfActive();` with the same phase guard. In `EnterShop` (:401) REMOVE `sectionIdentifier.SetActive(true);` and in `ExitShop` (:431) remove `sectionIdentifier.SetActive(false);` (labels superseded by panel headers; objects deleted in Task 6).

- [ ] **Step 6: Run tests → verify PASS** (3 new EditMode tests). Manual verify in editor: enter Shop — three translucent panels behind the rows, headers Shop/Deck/Upgrades, `03/05`-style counter in Deck header, reroll button in Shop header right; scroll still works; buy/sell/reroll update chips + counter.

- [ ] **Step 7: Commit**
```bash
git add Assets/Scripts/UXPrototype/ShopSectionPanels.cs Assets/Scripts/UXPrototype/ShopUXManager.cs Assets/Scripts/Managers/ShopManager.cs Assets/Scripts/UXPrototype/ShopChrome.cs Assets/Scripts/Editor/Tests/ShopSectionPanelsTests.cs
git commit -m "feat(shop): world-space Shop/Deck/Upgrades section panels; reroll moves into Shop panel header"
```

---

### Task 5: Upgrades row — owned utility passives split into the third panel

**Files:**
- Modify: `Assets/Scripts/UXPrototype/ShopUXManager.cs` (utility list, grid base, relayout, buy/sell/pulse/scroll)

**Interfaces:**
- Consumes: Task 4 accessors + `ShopSectionPanels.RefreshLayout()`.
- Produces: `_spawnedUtilityCards` populated; upgrades visual row below the Deck band inside the Upgrades panel. Semantics: `occupiesDeckSlot == false` ⇔ utility/passive track (matches `StackSlotAssigner`); spec name `IsUtilityPassive` — the two coincide for all shipped utility cards.

- [ ] **Step 1: Retarget the utility zone.** Change `GetUtilityZoneBaseSlot` (:263-268) so utility cards start on the row AFTER the deck band instead of interleaving behind empty slots:
```csharp
	private int GetUtilityZoneBaseSlot(int occupyingTrackEndSlot)
	{
		if (ShopManager.me != null && ShopManager.me.deckSize != null)
		{
			int perRow = Mathf.Max(1, objPerRow);
			return Mathf.CeilToInt((float)ShopManager.me.deckSize.value / perRow) * perRow;
		}
		return occupyingTrackEndSlot;
	}
```
Update the method doc comment: utility passives now render in the Upgrades panel below the Deck panel (2026-09-18 port).

- [ ] **Step 2: Split instantiation.** In `InstantiatePlayerDeckPhysCards()` (:513-601): when `!cardScript.occupiesDeckSlot`, add the instantiated card to `_spawnedUtilityCards` instead of `_spawnedPlayerCards` (everything else — spawn at `playerDeckStartPos`, `SetShopCardDescription`, price button setup — stays identical; price buttons on utility cards already show sell `$N` via the existing path). End of method: after `RelayoutDeckBand();` add `ShopSectionPanels.Instance?.RefreshLayout();`.

- [ ] **Step 3: Split relayout.** In `RelayoutPlayerDeckCards()` (:363-374): keep ONE `StackSlotAssigner`, iterate `_spawnedPlayerCards` then `_spawnedUtilityCards` (utility cards call `Assign(typeID, false, ...)` exactly as today — the assigner routes them to the retargeted utility zone). `ClearSpawnedPlayerCards()` (:210-231) destroys + clears both lists. `OnCardPurchased()` (:737-800): route the bought card to `_spawnedUtilityCards` when `!cardScript.occupiesDeckSlot`, else `_spawnedPlayerCards` as today. `PulsePlayerCard()` (:1133-1147): search `_spawnedUtilityCards` after `_spawnedPlayerCards`.

- [ ] **Step 4: Sell flow.** `OnCardSold(GameObject, int)` (:811-872): before the existing removal logic, drop the instance from whichever list contains it (`_spawnedPlayerCards.Remove(instance)` / `_spawnedUtilityCards.Remove(instance)`); end of method add `ShopSectionPanels.Instance?.RefreshLayout();` so the Upgrades panel shrinks when its last card is sold.

- [ ] **Step 5: Scroll bound.** In `ComputeDynamicMinY()` (:648-677) include `_spawnedUtilityCards` targets in the lowest-content calculation (same null-safe `TargetPosition` pattern as `_spawnedPlayerCards`).

- [ ] **Step 6: Manual verify.** Own ≥1 utility passive (e.g. add to `PlayerDeckRef` for the test): it renders in the Upgrades panel below the Deck panel with header `Upgrades`, no empty-slot recesses, sell button at half price; buying a utility from a utility board shelf adds it to the Upgrades row (with `PulsePlayerCard` pulse); selling it removes it and the panel refits; wheel-scroll reaches the panel; reroll/buy/sell of normal cards unchanged.

- [ ] **Step 7: Commit**
```bash
git add Assets/Scripts/UXPrototype/ShopUXManager.cs
git commit -m "feat(shop): owned utility passives move from deck band into Upgrades panel row"
```

---

### Task 6: Scene cleanup + docs + final gate

**Files:**
- Modify: `Assets/Scenes/GameScene.unity` (two value tweaks; delete 3 leaf objects)
- Modify: `docs/UIUX_Guidelines.md` (§3.6 port note + Version History v0.12)
- Modify: `docs/RegressionChecklist.md` (new row)
- Modify: `AGENTS.md` (shop bullet: panels + reroll location)
- Delete (scene objects, via YAML edit): `ShopManager/section identifiers` + children `'Shop:'`, `'YourDeck:'`

- [ ] **Step 1: Scene tuning.** In `GameScene.unity` under the `ShopUXManager` component (fileID `423783303`): `shopItemPos` y `2.2` → `1.5` (clears the 2.6 chrome band: card top = 1.5 + 2.5×0.8 = 3.5 < 6.06 − 2.6 = 3.46 → then tune to `1.4` if `CheckShelfClearance` warns); `playerDeckPos` y `-2.5` → `-3.4` (keeps the shelf→deck gap with panels inserted). Live-tune via Inspector (`OnValidate` → `RelayoutAll` refreshes in real time) until: chrome rows don't overlap the Shop panel header; panels don't overlap each other (gap ≥ 0.7); no `[ShopChrome] shelf top` warning on reroll. Write the final values back into the scene YAML.

- [ ] **Step 2: Delete superseded labels.** Remove GameObjects `section identifiers` (GO `59376747`, Transform `59376748`) and children `'YourDeck:'` (`591335255`), `'Shop:'` (`175289196`): delete their `GameObject`/`Transform`/`MeshRenderer`/`TextMeshPro`/`MonoBehaviour` blocks and the child entries in the parent `Transform` (`152842585`) `m_Children` list. `ShopManager.sectionIdentifier` field already unused (Task 4) — leave the field (null-safe) with a `// superseded by ShopSectionPanels headers (2026-09-18)` comment, or remove the field AND its scene reference line together.

- [ ] **Step 3: Docs.** `docs/UIUX_Guidelines.md`: extend the §3.6 Unity port note (three world panels `ShopSectionPanels`, counter format `00/00`, reroll relocated per annotated layout, upgrades panel = owned utilities at `!occupiesDeckSlot`, chrome v2 rows) + Version History entry `v0.12 · 2026-09-18`. `docs/RegressionChecklist.md`: append a row (three panels behind cards; utility split; reroll in Shop header; chrome rows). `AGENTS.md` Shop bullet: update the 2026-09-18 chrome sentence — reroll lives in the Shop panel header, sections paneled by `ShopSectionPanels`. Keep `wc -c AGENTS.md` ≤ 32 KB after edit.

- [ ] **Step 4: Final gate.** Re-run all EditMode tests (full suite green); enter Shop and verify the annotated layout end-to-end: chrome rows (avatar/HP/$ | rarity | wins/hearts/income) + Exit/❚❚; Shop panel (header + reroll + shelf + buy buttons); Deck panel (header + counter + cards + sell buttons + recessed 空卡位); Upgrades panel (owned utilities, sell only). Then:
```bash
git add Assets/Scenes/GameScene.unity docs/UIUX_Guidelines.md docs/RegressionChecklist.md AGENTS.md
git commit -m "feat(shop): scene tuning + label cleanup + docs for panel port"
```

---

## Self-Review

- **Spec coverage:** top bar (avatar+用户名 ✓ Task 3, HP/$ ✓ existing chips repositioned Task 3, rarity odds ✓ Tasks 2+3, 胜场·心数 ✓ Task 3 via PhaseManager IntSOs, 收入 ✓ Task 3, 离开商店 ✓ existing, 选项 ✓ Task 3 placeholder) · Shop panel + reroll in header ✓ Task 4 · Deck panel + 03/05 counter ✓ Task 4 · recessed 空卡位 ✓ existing (unchanged, now inside Deck panel bounds) · Upgrades panel (owned utilities, sell half price, no counter/recesses) ✓ Task 5 · panel translucency ✓ Task 1 color. Annotated-layout gaps: none identified.
- **Placeholder scan:** all code steps contain concrete code or exact fileIDs/paths; verification steps name the exact test/class or manual scenario. Glyph fallbacks are explicit with concrete substitute strings.
- **Type consistency:** `ShopSectionPanels` surface (Bootstrap/Instance/SetRerollRolling/RefreshLayout/RefreshCounter/RefreshIfActive/ShowIfActive/HideIfActive/ComputeContentBounds/FormatSlotCount) is identical across Tasks 3-6; `ShopUXManager` accessors (`Spawned*` × 4, `physCardSize`) match between Tasks 4-5; `ShopRarityWeightSO.GetOddsPercents` / `ShopManager.GetRarityOddsPercents` signatures match Task 2 definition ↔ Task 3 consumption; `GetUtilityZoneBaseSlot` return semantics change is confined to Task 5 and its only callers are `StackSlotAssigner.Assign` (:335).

---

## Execution Record (2026-09-18)

All 6 tasks executed inline (superpowers:executing-plans); 7 commits on `main`:

| Commit | Content |
|---|---|
| `1a1de9e` | Task 1 — ShopPanelBg palette color |
| `ba3363e` | Task 2 — rarity-odds API + 3 tests |
| `ac2d33a` | Task 3 — chrome top bar v2 |
| `bffc4b2` | Task 4 — ShopSectionPanels + reroll relocation |
| `8dd0eff` | Task 5 — Upgrades row utility split |
| `c523d39` | Task 6 — scene tuning + label cleanup + docs |
| `5944616` | Unity-generated .meta files for the new scripts |

### Deviations from the plan (all deliberate)

1. **Glyph substitution decided pre-emptively (Task 3).** The plan said ship spec glyphs (`▦`/`♥`/`❚❚`) and substitute only if an in-editor check showed tofu. Instead the bundled static font atlases were grepped directly: `NotoSansSymbols2 SDF` contains ONLY `✦` (U+2726, codepoint 10022) and RobotoCondensed lacks `♥` (9829) — so the shipped labels are full-word `Wins`/`Hearts` (fit the 1.9u chips at fontSize 1.9) and the options button shows `||`. Rarity chips keep `✦` (atlas-verified).
2. **Reroll removal split across Tasks 3-4 (compilable commits).** Task 3 removed the reroll *button* from `ShopChrome.Build` but kept `SetRerollRolling`/`RefreshRerollState` as null-guarded no-ops (the two `ShopUXManager` call sites still referenced them); Task 4 removed the methods and switched the call sites to `ShopSectionPanels.SetRerollRolling`. Every commit compiles.
3. **Panel-refit hook consolidated (Task 5).** Task 4 placed `RefreshLayout()` calls at the end of `RelayoutDeckBand` and `SpawnEmptySlots`; Task 5 moved the single hook into `RelayoutPlayerDeckCards` — every flow that moves a deck/utility card or empty slot ends there, so the other two calls were removed (avoids double-refits and covers the `SpawnAdditionalEmptySpaces` path, which calls `RelayoutPlayerDeckCards` directly).
4. **`ShopManager.sectionIdentifier` fully removed (Task 6).** The plan offered "leave the field with a deprecation comment, or remove"; removal was chosen (scene reference line deleted with the GameObject blocks).
5. **Scene commit isolation.** `GameScene.unity` carried unrelated uncommitted user edits (TestManager log toggles, a component add). Only the 6 port-related diff hunks were staged via a filtered patch (`git apply --cached`); the user's hunks remain unstaged in the working tree.

### Verification status (open)

- EditMode tests written but NOT executed in the authoring session (no Unity MCP available): `ShopRarityWeightSOTests` (3), `ShopSectionPanelsTests` (4), `GameColorPaletteWiringTests` (auto-covers the new `shopPanelBg` field). Run via Test Runner.
- Play-mode checklist: RegressionChecklist row 106. If `[ShopChrome] shelf top` warns, drop `shopItemPos.y` 1.5 → 1.4 (Inspector live-tunes via OnValidate → RelayoutAll).
