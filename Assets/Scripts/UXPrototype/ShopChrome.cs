using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

// Shop top chrome (Guidelines 3.6) as WORLD page content. Since 2026-09-24
// (plan-shop-hud-prefab-widgets) the bar is an authored prefab: this file is a thin
// loader that instantiates the ShopHudPage prefab wired on ShopUXManager.hudPagePrefab
// and lets ShopHudBinder push strings into the page's HudTextBinding / HudActionBinding
// widgets. Positions, sizes, fonts, texts and colors are prefab data
// (Assets/Prefabs/ShopHud/ShopHudPage.prefab + Tpl_* variants + PaletteTint); this file
// keeps only the frozen public API surface and the band contract the shelf layout
// consumes. Root placement is the authored prefab root transform since 2026-09-29
// (page content — scrolls away with the wheel, 2026-09-21 world scroll); the loader
// no longer writes the page position at all.
//
// 2026-09-21 world scroll (plan-shop-topbar-world-scroll-2026-09-21): the bar is PAGE
// content, not camera-pinned. BandBottomWorldY is captured ONCE at build and the whole
// bar then scrolls away with the wheel like any other shop world content (since
// 2026-09-29 the root position itself is the authored prefab transform, authored to the
// scroll-0 camera spot). A scrolled-down shop shows no
// exit button on purpose (user ruling 2026-09-21): scroll back up, or press Space (the
// PhaseManager shortcut).
// VISUAL-FIX(2026-09-21): whole top bar (chips + buttons + avatar/HP) stayed pinned to
//   the viewport top while the shop page scrolled away under the wheel
//   Cause:    ShopChromeAnchor copied Camera.main into the chrome root every LateUpdate,
//             so the bar floated above the page no matter where the camera scrolled.
//   Affects:  chrome root placement (now written once at build) + BandBottomWorldY
//             (build-time captured); the world avatar/HP are prefab children of the
//             page (Tpl_Avatar / Tpl_HpPill, plan-shop-mirror-prefab).
//   Regress:  Shop at scroll 0 renders the bar pixel-identical to the anchor build; wheel
//             down takes chips, buttons, avatar and HP pill away with the cards; wheel up
//             brings them back; Space exits from a fully scrolled shop; CheckShelfClearance
//             limit unchanged; combat/result HUD (canvas) untouched.
//             (2026-10-07: the avatar + HP pill mirrors merged into the NamePlate widget —
//             plan-hp-name-plate-2026-10-07.)
public class ShopChrome : MonoBehaviour
{
	private const string ChromeName = "Shop Chrome";

	// Fallback band/root defaults (cross-file readers: ShopTopBarLayout conversion
	// helpers, ShopHudPage field initializers). After Build the authored ShopHudPage
	// fields win — the prefab owns the contract (plan §2.3).
	public const float BandHeightDefault = 2.6f;      // reserved clearance zone at the page top (no rendered band)
	public const float CameraForwardOffsetDefault = 2f; // legend: page z the retired camera formula added (no longer drives placement)
	public const float BandInsetFromTopDefault = 0.5f; // legend: root-to-band geometry the retired formula used (gizmo band line)

	// Page-first band values (legend/gizmo only since 2026-09-29 — root placement is the
	// authored prefab transform). Resolve from the authored page once built; before Build
	// the defaults apply.
	public static float BandHeight => _page != null ? _page.bandHeight : BandHeightDefault;
	public static float BandInsetFromTop => _page != null ? _page.bandInsetFromTop : BandInsetFromTopDefault;
	public static float CameraForwardOffset => _page != null ? _page.cameraForwardOffset : CameraForwardOffsetDefault;

	private static ShopChrome _instance;
	public static ShopChrome Instance => _instance;

	private static ShopHudPage _page;

	// Build-captured camera center — the canvas-side world→anchored inversion basis
	// (ShopTopBarLayout), so a scrolled camera cannot shift the parked shop anchors. Not
	// a placement basis anymore (2026-09-29: root placement is the authored prefab
	// transform); captured for the inversion only.
	private static Vector3 _rigPos;
	public static Vector3 BuildCameraCenter => _rigPos;

	// Shelf layout contract limit, captured ONCE at build (page content, not camera-
	// relative). float.MaxValue until built — the same "not ready" reading the old
	// null-cam guard gave.
	private static float _bandBottomWorldY = float.MaxValue;

	private ShopHudBinder _binder;

	/// <summary>
	/// Bottom edge of the chrome band in world Y — the shelf layout contract limit. Captured
	/// once at build (page content, not camera-relative).
	/// </summary>
	public static float BandBottomWorldY()
	{
		return _bandBottomWorldY;
	}

	/// <summary>
	/// Builds the chrome once (idempotent) from the authored page prefab; shows it when
	/// the game is already in Shop phase (the enter-shop UnityEvent may fire before
	/// ShopUXManager.Start).
	/// </summary>
	public static void Bootstrap(GameObject hudPagePrefab)
	{
		if (_instance != null) return;
		if (hudPagePrefab == null)
		{
			TestManager.LogWarning("[ShopChrome] hudPagePrefab not wired on ShopUXManager — world chrome skipped");
			return;
		}

		Camera cam = Camera.main;
		_rigPos = cam != null ? cam.transform.position : Vector3.zero;

		GameObject root = Instantiate(hudPagePrefab);
		root.name = ChromeName;
		root.SetActive(false);
		_instance = root.AddComponent<ShopChrome>();
		_page = root.GetComponent<ShopHudPage>();
		if (_page == null)
		{
			TestManager.LogError("[ShopChrome] hudPagePrefab lacks the ShopHudPage root component — world chrome skipped");
			Destroy(root);
			_instance = null;
			_page = null;
			return;
		}
		// Band-bottom contract capture; the root placement is the authored prefab
		// transform (2026-09-29), one-shot and page-content (2026-09-21 world scroll).
		_page.OpenPage();
		_bandBottomWorldY = _page.BandBottomWorldY;

		_instance._binder = root.GetComponent<ShopHudBinder>();
		if (_instance._binder != null) _instance._binder.Collect();

		if (ShopManager.me != null && ShopManager.me.gamePhaseRef != null
			&& ShopManager.me.gamePhaseRef.currentGamePhase == EnumStorage.GamePhase.Shop)
		{
			ShowIfActive();
		}
	}

	public static void RefreshIfActive()
	{
		if (_instance != null && _instance.gameObject.activeSelf) _instance.Refresh();
	}

	public static void ShowIfActive()
	{
		if (_instance == null) return;
		// 2026-10-02 re-audit fix 4b (docs/PhaseTransition.md): no IsTransitioning gate — the
		// bar is PAGE content, so showing it at the Result->Shop travel start (EnterShop runs
		// there) lets it scroll INTO view with the descent, symmetric with the section
		// panels. The old gate made the bar pop at landing only while the panels were already
		// visible mid-descent.
		_instance.gameObject.SetActive(true);
		_instance.Refresh();
	}

	public static void HideIfActive()
	{
		if (_instance != null) _instance.gameObject.SetActive(false);
	}

	/// <summary>
	/// Shows/hides only the world NamePlate mirror child (2026-10-02 re-audit fixes 4/7;
	/// the Avatar + HpPill pair merged into one plate per plan-hp-name-plate-2026-10-07):
	/// during a driver travel the canvas HUD world-flies as the single shared copy, so the
	/// mirror hides at travel start and returns on the shop landing (driver calls). No-op
	/// before the chrome is built (headless / bypass).
	/// </summary>
	public static void SetMirrorsActive(bool active)
	{
		if (_page == null) return;
		Transform plate = _page.transform.Find("NamePlate");
		if (plate != null) plate.gameObject.SetActive(active);
	}

	/// <summary>
	/// Layout-contract assertion (call after every shelf build): the topmost shelf card must
	/// stay below the band, or hover/deny excursions of a price button can slide under it.
	/// </summary>
	public static void CheckShelfClearance(IEnumerable<GameObject> shelfCards)
	{
		if (_instance == null || !_instance.gameObject.activeSelf || shelfCards == null) return;
		float limit = BandBottomWorldY();
		float top = float.MinValue;
		foreach (GameObject card in shelfCards)
		{
			if (card == null) continue;
			CardPhysObjScript phys = card.GetComponent<CardPhysObjScript>();
			if (phys == null) continue;
			float cardTop = phys.TargetPosition.y + ShopSectionPanels.FaceBelowHalfHeightFactor * phys.TargetScale.y; // face half-height + price button allowance
			if (cardTop > top) top = cardTop;
		}
		if (top > limit)
		{
			TestManager.LogWarning($"[ShopChrome] shelf top {top:0.00} enters the chrome band (limit {limit:0.00}) — raise the shelf start or lower the band");
		}
	}

	/// <summary>
	/// World center of the built page's NamePlate prefab child — the canvas plate parks
	/// there in the Shop phase (plan-hp-name-plate-2026-10-07 §3.4, ex plan-shop-mirror-
	/// prefab §3.4); false until the chrome is built.
	/// </summary>
	public static bool TryGetNamePlateWorldCenter(out Vector3 worldPos) => TryGetMirrorWorldCenter("NamePlate", out worldPos);

	private static bool TryGetMirrorWorldCenter(string childName, out Vector3 worldPos)
	{
		worldPos = default;
		if (_page == null) return false;
		Transform child = _page.transform.Find(childName);
		if (child == null) return false;
		worldPos = child.position;
		return true;
	}

	/// <summary>Shop scale authored on the mirror widget root (ShopMirrorScale); null until built.</summary>
	public static float? HpDisplayShopScale => GetMirrorScale("NamePlate");

	private static float? GetMirrorScale(string childName)
	{
		if (_page == null) return null;
		Transform child = _page.transform.Find(childName);
		if (child == null) return null;
		ShopMirrorScale mirrorScale = child.GetComponent<ShopMirrorScale>();
		return mirrorScale != null ? mirrorScale.canvasShopScale : (float?)null;
	}

	/// <summary>Pushes shop stats through the page's bindings (texts incl. username + HP count-up).</summary>
	private void Refresh()
	{
		if (_binder != null) _binder.Refresh();
	}
}
