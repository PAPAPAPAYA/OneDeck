using DefaultNamespace.Managers;
using UnityEngine;
using UnityEngine.UI;

// Shop top HUD bar (Guidelines 3.6): a horizontal strip of HudChips. Pure view —
// values are pulled on Refresh() from ShopManager (logic owner) at transaction
// points, with change-cached labels so repeated calls are cheap. Runtime-built
// under Shop Canvas with no scene edit (same precedent as the PhysButton UI-mode
// attach); the legacy debug display texts it replaces are hidden at runtime.
public class ShopHudBar : MonoBehaviour
{
	private const string BarName = "Shop Hud Bar";

	// Legacy Shop Canvas texts whose writers were retired with the HudBar port.
	private static readonly string[] LegacyDisplayNames =
	{
		"Player Stats Display",
		"Deck Display",
		"Shop Display",
		"General Info Display",
	};

	private static ShopHudBar _instance;

	public static ShopHudBar Instance => _instance;

	private HudChip _moneyChip;
	private HudChip _incomeChip;
	private HudChip _hpChip;
	private HudChip _deckChip;

	private int _lastPurse = int.MinValue;
	private int _lastPayday = int.MinValue;
	private int _lastHp = int.MinValue;
	private int _lastHpMax = int.MinValue;
	private int _lastDeck = int.MinValue;
	private int _lastMaxDeck = int.MinValue;

	/// <summary>
	/// Builds the bar under "Shop Canvas" once (idempotent) and hides the legacy
	/// debug display texts. Called from ShopUXManager.Start.
	/// </summary>
	public static void BootstrapForShop()
	{
		GameObject canvasGo = GameObject.Find("Shop Canvas");
		if (canvasGo == null) return;

		if (_instance == null)
		{
			Transform existing = canvasGo.transform.Find(BarName);
			_instance = existing != null
				? existing.GetComponent<ShopHudBar>()
				: BuildBar(canvasGo);
		}

		foreach (string legacyName in LegacyDisplayNames)
		{
			Transform legacy = canvasGo.transform.Find(legacyName);
			if (legacy != null) legacy.gameObject.SetActive(false);
		}

		// The enter-shop UnityEvent can fire before this bootstrap (scene-boot order is
		// unspecified), making EnterShop's ShowIfActive a no-op on a null instance. If
		// the shop phase is already live, show + fill right here; otherwise the bar
		// stays hidden until the next EnterShop.
		if (ShopManager.me != null && ShopManager.me.gamePhaseRef != null
			&& ShopManager.me.gamePhaseRef.currentGamePhase == EnumStorage.GamePhase.Shop)
		{
			ShowIfActive();
		}
	}

	public static void RefreshIfActive()
	{
		if (_instance != null) _instance.Refresh();
	}

	public static void ShowIfActive()
	{
		if (_instance == null) return;
		_instance.gameObject.SetActive(true);
		_instance.Refresh();
	}

	public static void HideIfActive()
	{
		if (_instance != null) _instance.gameObject.SetActive(false);
	}

	private static ShopHudBar BuildBar(GameObject canvasGo)
	{
		HudChip chipPrefab = Resources.Load<HudChip>("HudChip");
		if (chipPrefab == null)
		{
			TestManager.LogWarning("[ShopHudBar] HudChip prefab missing under Assets/Resources — top bar skipped");
			return null;
		}

		GameObject barGo = new GameObject(BarName, typeof(RectTransform), typeof(HorizontalLayoutGroup));
		RectTransform barRect = (RectTransform)barGo.transform;
		barRect.SetParent(canvasGo.transform, false);
		barRect.anchorMin = new Vector2(0f, 1f);
		barRect.anchorMax = new Vector2(1f, 1f);
		barRect.pivot = new Vector2(0.5f, 1f);
		barRect.anchoredPosition = Vector2.zero;
		barRect.sizeDelta = new Vector2(0f, 56f);

		HorizontalLayoutGroup layout = barGo.GetComponent<HorizontalLayoutGroup>();
		layout.spacing = 10f;
		layout.padding = new RectOffset(12, 12, 6, 6);
		layout.childAlignment = TextAnchor.UpperLeft;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		layout.childControlWidth = false;
		layout.childControlHeight = false;

		ShopHudBar bar = barGo.AddComponent<ShopHudBar>();
		bar._moneyChip = bar.CreateChip(chipPrefab, "ChipMoney", 130f, "$0", true);
		bar._incomeChip = bar.CreateChip(chipPrefab, "ChipIncome", 160f, "+$0/combat", false);
		bar._hpChip = bar.CreateChip(chipPrefab, "ChipHP", 110f, "0/0", false);
		bar._deckChip = bar.CreateChip(chipPrefab, "ChipDeck", 110f, "0/0", false);
		bar.Refresh();
		// Hidden until the first shop entry (ShopManager.EnterShop -> ShowIfActive),
		// so the bar never shows placeholder values outside the shop phase.
		barGo.SetActive(false);
		return bar;
	}

	private HudChip CreateChip(HudChip prefab, string chipName, float width, string initialText, bool accent)
	{
		HudChip chip = Object.Instantiate(prefab, transform, false);
		chip.name = chipName;
		chip.Setup(initialText, accent);
		LayoutElement element = chip.GetComponent<LayoutElement>();
		if (element != null)
		{
			element.preferredWidth = width;
			element.minHeight = 44f;
		}
		return chip;
	}

	/// <summary>
	/// Pulls the shop stats into the chips; each label rewrites only when its value
	/// changed, so frequent calls (entry / buy / sell / reroll) stay cheap.
	/// </summary>
	public void Refresh()
	{
		ShopManager shop = ShopManager.me;
		if (shop == null) return;

		int purse = shop.purse != null ? shop.purse.value : 0;
		if (purse != _lastPurse)
		{
			_lastPurse = purse;
			if (_moneyChip != null) _moneyChip.SetText("$" + purse);
		}

		int payday = shop.GetCurrentPayday();
		if (payday != _lastPayday)
		{
			_lastPayday = payday;
			if (_incomeChip != null) _incomeChip.SetText("+$" + payday + "/combat");
		}

		PlayerStatusSO status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;
		int hp = status != null ? status.hp : 0;
		int hpMax = status != null ? status.hpMax : 0;
		if (hp != _lastHp || hpMax != _lastHpMax)
		{
			_lastHp = hp;
			_lastHpMax = hpMax;
			if (_hpChip != null) _hpChip.SetText(hp + "/" + hpMax);
		}

		int deck = shop.deckSize != null ? shop.deckSize.value : 0;
		int maxDeck = shop.maxDeckSize != null ? shop.maxDeckSize.value : 0;
		if (deck != _lastDeck || maxDeck != _lastMaxDeck)
		{
			_lastDeck = deck;
			_lastMaxDeck = maxDeck;
			if (_deckChip != null) _deckChip.SetText(deck + "/" + maxDeck);
		}
	}
}
