using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>
/// Data pump for a built ShopHudPage (plan-shop-hud-prefab-widgets §3.3): Collect()
/// gathers the page's HudTextBinding / HudActionBinding children once and wires every
/// action button; Refresh() pushes the same shop stats the old ShopChrome.Refresh read
/// (:316-371) through the per-key format strings baked in the prefab. Execute() carries
/// the two actions verbatim from the old chrome (:200-215): LeaveShop with the
/// PhaseTransitionDriver wrap + legacy hard-cut fallback, Options placeholder log.
/// Visual identity of the widgets is entirely prefab data — this class only pushes
/// strings and invokes actions.
/// </summary>
public class ShopHudBinder : MonoBehaviour
{
	private HudTextBinding[] _textBindings;
	private HudActionBinding[] _actionBindings;
	private HudCountBinding[] _countBindings;
	private PhaseManager _phaseManager;

	/// <summary>
	/// One-time collection after the page instance is built (inactive roots included —
	/// the page starts hidden). PhaseManager resolves the same way the old chrome Build
	/// did (warn + inert exit when missing).
	/// </summary>
	public void Collect()
	{
		_textBindings = GetComponentsInChildren<HudTextBinding>(true);
		_actionBindings = GetComponentsInChildren<HudActionBinding>(true);
		_countBindings = GetComponentsInChildren<HudCountBinding>(true);
		foreach (HudActionBinding action in _actionBindings)
		{
			if (action.button == null)
			{
				TestManager.LogWarning("[ShopHudBinder] HudActionBinding without a PhysButton — action inert");
				continue;
			}
			HudActionBinding.ActionKey key = action.key;
			action.button.SetWorldAction(() => Execute(key));
		}
		_phaseManager = FindFirstObjectByType<PhaseManager>();
		if (_phaseManager == null)
		{
			TestManager.LogWarning("[ShopChrome] PhaseManager not found in scene — exit button inert");
		}
	}

	/// <summary>Pushes every bound key's current value; labels rewrite only on change (diff guard inside the bindings).</summary>
	public void Refresh()
	{
		ShopManager shop = ShopManager.me;
		if (shop == null || _textBindings == null) return;

		string purse = shop.purse != null ? shop.purse.value.ToString() : "0";
		string payday = shop.GetCurrentPayday().ToString();
		shop.GetRarityOddsPercents(out float commonPct, out float uncommonPct, out float rarePct);
		string common = ((int)commonPct).ToString();
		string uncommon = ((int)uncommonPct).ToString();
		string rare = ((int)rarePct).ToString();
		// Username (ex-ShopPageHud.PollUsername): "???" while unnamed; the binding's diff
		// guard keeps the TMP mesh from rebuilding on unchanged ticks.
		string username = PlayerIdentity.Username;
		if (string.IsNullOrEmpty(username)) username = "???";

		foreach (HudTextBinding binding in _textBindings)
		{
			switch (binding.key)
			{
				case HudTextBinding.BindingKey.Money: binding.Apply(purse, string.Empty); break;
				case HudTextBinding.BindingKey.Income: binding.Apply(payday, string.Empty); break;
				case HudTextBinding.BindingKey.RarityCommon: binding.Apply(common, string.Empty); break;
				case HudTextBinding.BindingKey.RarityUncommon: binding.Apply(uncommon, string.Empty); break;
				case HudTextBinding.BindingKey.RarityRare: binding.Apply(rare, string.Empty); break;
				case HudTextBinding.BindingKey.Username: binding.Apply(username, string.Empty); break;
				case HudTextBinding.BindingKey.Wins:
				case HudTextBinding.BindingKey.Hearts:
					// Missing phase data keeps the label untouched — same as the old chrome,
					// which only wrote these chips when all four IntSOs existed.
					if (_phaseManager == null || _phaseManager.wins == null || _phaseManager.winCon == null
						|| _phaseManager.hearts == null || _phaseManager.heartMax == null) break;
					if (binding.key == HudTextBinding.BindingKey.Wins)
					{
						binding.Apply(_phaseManager.wins.value.ToString(), _phaseManager.winCon.value.ToString());
					}
					else
					{
						binding.Apply(_phaseManager.hearts.value.ToString(), _phaseManager.heartMax.value.ToString());
					}
					break;
				default: break; // reserved keys have no data source yet
			}
		}

		// HP pair (ex-ShopPageHud.Refresh): count-up lives inside the binding; Max(1, hpMax)
		// keeps the old divide-safe floor.
		if (_countBindings != null)
		{
			PlayerStatusSO status = CombatManager.Me != null ? CombatManager.Me.ownerPlayerStatusRef : null;
			if (status != null)
			{
				foreach (HudCountBinding count in _countBindings)
				{
					count.SetTarget(status.hp, Mathf.Max(1, status.hpMax));
				}
			}
		}
	}

	/// <summary>Release-invoked action; the LeaveShop branch is byte-equivalent to the old chrome exit (:206-215).</summary>
	public void Execute(HudActionBinding.ActionKey key)
	{
		switch (key)
		{
			case HudActionBinding.ActionKey.LeaveShop:
				if (_phaseManager == null) return;
				// Phase transition driver (plan-phase-transition-world-camera-2026-09-21): the 离开商店
				// button is THE canonical shop->combat trigger (user ruling 2026-09-21); when the driver
				// is available it wraps these calls in the camera travel, else the legacy hard cut runs.
				if (PhaseTransitionDriver.RequestShopToCombat(_phaseManager)) return;
				_phaseManager.ExitingShopPhase();
				_phaseManager.EnteringCombatPhase();
				break;
			case HudActionBinding.ActionKey.Options:
				Debug.Log("[ShopChrome] Options pressed (placeholder — no options menu yet)");
				break;
		}
	}
}
