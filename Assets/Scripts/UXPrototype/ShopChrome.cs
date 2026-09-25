using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

// Shop top chrome (Guidelines 3.6) as WORLD page content. Since 2026-09-24
// (plan-shop-hud-prefab-widgets) the bar is an authored prefab: this file is a thin
// loader that instantiates the ShopHudPage prefab wired on ShopUXManager.hudPagePrefab,
// applies the one-shot root placement (page content — scrolls away with the wheel,
// 2026-09-21 world scroll) and lets ShopHudBinder push strings into the page's
// HudTextBinding / HudActionBinding widgets. Positions, sizes, fonts, texts and colors
// are prefab data (Assets/Prefabs/ShopHud/ShopHudPage.prefab + Tpl_* variants +
// PaletteTint); this file keeps only the frozen public API surface and the band contract
// the shelf layout consumes.
//
// 2026-09-21 world scroll (plan-shop-topbar-world-scroll-2026-09-21): the bar is PAGE
// content, not camera-pinned. The root position and BandBottomWorldY are written ONCE at
// build (the spot the old anchor computed at scroll 0) and the whole bar then scrolls
// away with the wheel like any other shop world content. A scrolled-down shop shows no
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
public class ShopChrome : MonoBehaviour
{
	private const string ChromeName = "Shop Chrome";

	// Fallback band/root defaults (cross-file readers: ShopTopBarLayout conversion
	// helpers, ShopHudPage field initializers). After Build the authored ShopHudPage
	// fields win — the prefab owns the contract (plan §2.3).
	public const float BandHeightDefault = 2.6f;      // reserved clearance zone at the page top (no rendered band)
	public const float CameraForwardOffsetDefault = 2f; // page z distance in front of the camera
	public const float BandInsetFromTopDefault = 0.5f; // page root center below the viewport top edge

	// Page-first band values. BandInsetFromTop feeds ShopTopBarLayout.ViewportToChromeLocalY,
	// which the KEPT mirror path consumes (ShopPageHud avatar/HP placement) — so it must
	// resolve from the authored page once built (plan §7.9); before Build the defaults apply.
	public static float BandHeight => _page != null ? _page.bandHeight : BandHeightDefault;
	public static float BandInsetFromTop => _page != null ? _page.bandInsetFromTop : BandInsetFromTopDefault;
	public static float CameraForwardOffset => _page != null ? _page.cameraForwardOffset : CameraForwardOffsetDefault;

	private static ShopChrome _instance;
	public static ShopChrome Instance => _instance;

	private static ShopHudPage _page;

	// Build-captured camera center (the page-content placement basis, 2026-09-21 world-
	// scroll semantics). The canvas-side world→anchored inversion (ShopTopBarLayout) reads
	// it, so a scrolled camera cannot shift the parked shop anchors.
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
		float orthoSize = cam != null ? cam.orthographicSize : 5f;
		Vector3 rigPos = cam != null ? cam.transform.position : Vector3.zero;
		_rigPos = rigPos;

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
		// Page-content placement, written once (2026-09-21 world scroll semantics).
		_page.OpenPage(rigPos, orthoSize);
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
		// Showing the bar mid-travel would float it over the departing/arriving page. The
		// driver calls ShowIfActive again on landing (ResultToShopRoutine).
		if (PhaseTransitionDriver.IsTransitioning) return;
		_instance.gameObject.SetActive(true);
		_instance.Refresh();
	}

	public static void HideIfActive()
	{
		if (_instance != null) _instance.gameObject.SetActive(false);
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
	/// World center of the built page's Avatar / HpPill prefab children — the canvas
	/// avatar/HP park there in the Shop phase (plan-shop-mirror-prefab §3.4); false until
	/// the chrome is built.
	/// </summary>
	public static bool TryGetAvatarWorldCenter(out Vector3 worldPos) => TryGetMirrorWorldCenter("Avatar", out worldPos);

	public static bool TryGetHpPillWorldCenter(out Vector3 worldPos) => TryGetMirrorWorldCenter("HpPill", out worldPos);

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
	public static float? AvatarShopScale => GetMirrorScale("Avatar");

	public static float? HpDisplayShopScale => GetMirrorScale("HpPill");

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
