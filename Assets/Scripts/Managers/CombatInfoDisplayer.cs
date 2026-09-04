using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;
using TMPro;

// a required component of combat manager, responsible for temporarily show combat info
public class CombatInfoDisplayer : MonoBehaviour
{
	#region Singleton
	public static CombatInfoDisplayer me;

	private void Awake()
	{
		me = this;
	}
	#endregion
	
	public GamePhaseSO gamePhase;
	public TextMeshProUGUI playerStatusDisplay;
	public TextMeshProUGUI enemyStatusDisplay;
	public TextMeshProUGUI revealZoneDisplay;
	public TextMeshProUGUI combatTipsDisplay;
	public TextMeshProUGUI effectResultDisplay;

	public TextMeshProUGUI playerDeckDisplay;
	public TextMeshProUGUI enemyDeckDisplay;

	public bool showRevealedCardName;
	
	// HP display freeze: logic updates HP immediately, but the UI must not run ahead
	// of the attack animations. Each attack snapshots (freezes) the display once, and
	// commits its OWN actual HP loss when its animation lands — order-independent by
	// design (see VISUAL-FIX below). The old absolute-value FIFO popped in playback
	// order while values were enqueued in logic order, so reactive chains (e.g.
	// bury -> onMeBuried damage) swapped damage numbers between hits.
	private int _pendingOwnerHpCount;
	private int _pendingEnemyHpCount;
	private int _displayedOwnerHp;
	private int _displayedEnemyHp;

	/// <summary>
	/// Fired after each CommitHpDisplay: (isOwner, hpLossOfThisHit, newDisplayedHp).
	/// Presentation-only consumers (DamageFloaterPresenter) spawn one floater per hit.
	/// </summary>
	public event Action<bool, int, int> onHpDisplayCommitted;

	/// <summary>
	/// Fired when pending locks are cleared (animations cancelled / combat reset) so
	/// consumers can silently resync to the live HP instead of showing a stale diff.
	/// </summary>
	public event Action onHpDisplayLocksCleared;

	private void Update()
	{
		if (gamePhase.Value() != EnumStorage.GamePhase.Combat) return;
		DisplayStatusInfo();
		effectResultDisplay.text = CombatLog.me != null ? CombatLog.me.GetRenderedText() : "";
	}

	public void ClearInfo()
	{
		playerStatusDisplay.text = "";
		enemyStatusDisplay.text = "";
		revealZoneDisplay.text = "";
		combatTipsDisplay.text = "";
		CombatLog.me?.Clear();
		effectResultDisplay.text = "";
		playerDeckDisplay.text = "";
		enemyDeckDisplay.text = "";
		ClearHpDisplayLocks();
	}
	
	/// <summary>
	/// Freeze the HP display for one pending attack hit. The first snapshot freezes the
	/// UI on preHitHp; each CommitHpDisplay subtracts that hit's own HP loss.
	/// </summary>
	/// <param name="target">Target player status</param>
	/// <param name="preHitHp">HP value to freeze the display on before the first hit lands</param>
	public void SnapshotHpDisplay(PlayerStatusSO target, int preHitHp)
	{
		if (target == null) return;
		if (target == CombatManager.Me.ownerPlayerStatusRef)
		{
			if (_pendingOwnerHpCount == 0)
			{
				_displayedOwnerHp = preHitHp;
			}
			_pendingOwnerHpCount++;
			TestManager.Log("[DamageFloater] Snapshot frame=" + Time.frameCount
				+ " side=player preHitHp=" + preHitHp + " pending=" + _pendingOwnerHpCount);
		}
		else
		{
			if (_pendingEnemyHpCount == 0)
			{
				_displayedEnemyHp = preHitHp;
			}
			_pendingEnemyHpCount++;
			TestManager.Log("[DamageFloater] Snapshot frame=" + Time.frameCount
				+ " side=enemy preHitHp=" + preHitHp + " pending=" + _pendingEnemyHpCount);
		}
	}
	
	// VISUAL-FIX(2026-08-04): Damage floater numbers swapped between hits (Corpse
	//   Explosion 2x2 + Eternal Ghost 1 showed as 2/1/2 instead of 2/2/1)
	//   Cause:    The old FIFO queue stored absolute post-hit HP values in LOGIC
	//             order, but CommitHpDisplay popped them in ANIMATION PLAYBACK order.
	//             A reactive chain (bury -> onMeBuried damage) resolves its damage
	//             between the parent's two hits in logic, yet plays its attack
	//             animation after the parent's — so each commit popped a snapshot
	//             belonging to a different hit. Sum and final HP stayed correct;
	//             only the per-hit numbers were misattributed.
	//   Affects:  CombatInfoDisplayer.CommitHpDisplay, HPAlterEffect damage capture,
	//             DamageFloaterPresenter (now event-driven per commit)
	//   Regress:  Combat with a card that buries Eternal Ghost between its own two
	//             hits: floaters must read 2 (reveal hit), 2 (parent anim), 1 (ghost anim)
	/// <summary>
	/// Commit one attack hit: subtract its OWN actual HP loss (captured at logic
	/// time, so playback order no longer matters) from the displayed value and
	/// notify presentation consumers via onHpDisplayCommitted.
	/// </summary>
	/// <param name="target">Target player status</param>
	/// <param name="hpLoss">Actual HP lost by this hit (shield-soaked / overkill excluded)</param>
	public void CommitHpDisplay(PlayerStatusSO target, int hpLoss)
	{
		if (target == null) return;
		if (target == CombatManager.Me.ownerPlayerStatusRef)
		{
			if (_pendingOwnerHpCount > 0)
			{
				_pendingOwnerHpCount--;
				_displayedOwnerHp -= hpLoss;
				TestManager.Log("[DamageFloater] Commit frame=" + Time.frameCount
					+ " side=player hpLoss=" + hpLoss + " displayed=" + _displayedOwnerHp
					+ " pendingLeft=" + _pendingOwnerHpCount);
				onHpDisplayCommitted?.Invoke(true, hpLoss, _displayedOwnerHp);
			}
		}
		else
		{
			if (_pendingEnemyHpCount > 0)
			{
				_pendingEnemyHpCount--;
				_displayedEnemyHp -= hpLoss;
				TestManager.Log("[DamageFloater] Commit frame=" + Time.frameCount
					+ " side=enemy hpLoss=" + hpLoss + " displayed=" + _displayedEnemyHp
					+ " pendingLeft=" + _pendingEnemyHpCount);
				onHpDisplayCommitted?.Invoke(false, hpLoss, _displayedEnemyHp);
			}
		}
	}
	
	/// <summary>
	/// Displayed owner HP exactly as shown by DisplayStatusInfo (queue-frozen value when
	/// pending values exist, live hp otherwise). Read-only accessor for presentation-only
	/// UI (e.g. CombatHPBarPresenter) that must stay in sync with the HP text.
	/// </summary>
	public int GetDisplayedOwnerHp()
	{
		return _pendingOwnerHpCount > 0
			? _displayedOwnerHp
			: CombatManager.Me.ownerPlayerStatusRef.hp;
	}

	/// <summary>
	/// Displayed enemy HP exactly as shown by DisplayStatusInfo (queue-frozen value when
	/// pending values exist, live hp otherwise). Read-only accessor for presentation-only
	/// UI (e.g. CombatHPBarPresenter) that must stay in sync with the HP text.
	/// </summary>
	public int GetDisplayedEnemyHp()
	{
		return _pendingEnemyHpCount > 0
			? _displayedEnemyHp
			: CombatManager.Me.enemyPlayerStatusRef.hp;
	}

	/// <summary>
	/// True while attack hits are pending commit for the given side (display frozen).
	/// Frame-polling consumers must not diff HP for a frozen side — per-hit losses
	/// already arrive via onHpDisplayCommitted.
	/// </summary>
	public bool HasPendingHpDisplay(bool isOwner)
	{
		return isOwner ? _pendingOwnerHpCount > 0 : _pendingEnemyHpCount > 0;
	}

	/// <summary>
	/// Clear all pending HP display locks. Used when animations are cancelled or combat ends.
	/// Notifies consumers so they can silently resync to the live HP.
	/// </summary>
	public void ClearHpDisplayLocks()
	{
		if (_pendingOwnerHpCount > 0 || _pendingEnemyHpCount > 0)
		{
			TestManager.Log("[DamageFloater] ClearHpDisplayLocks frame=" + Time.frameCount
				+ " pendingPlayer=" + _pendingOwnerHpCount + " pendingEnemy=" + _pendingEnemyHpCount);
		}
		_pendingOwnerHpCount = 0;
		_pendingEnemyHpCount = 0;
		_displayedOwnerHp = 0;
		_displayedEnemyHp = 0;
		onHpDisplayLocksCleared?.Invoke();
	}

	public string ReturnCardOwnerInfo(PlayerStatusSO statusRef)
	{
		if (statusRef == CombatManager.Me.ownerPlayerStatusRef)
		{
			return "你的";
		}
		else
		{
			return "敌方的";
		}
	}
	
	public void ShowCardInfo(CardScript cardRevealed, int cardNumber, bool ownersCard)
	{
		if (!showRevealedCardName) return;
		// Card name color: blue for player, orange for enemy
		string cardNameColor = ownersCard ? GameColorPalette.Me.friendly.Hex : GameColorPalette.Me.enemy.Hex;
		revealZoneDisplay.text = "#" + cardNumber + "\n" +// card num
		                         ProcessStatusEffectInfo(cardRevealed) + // tags
		                         "<color=" + cardNameColor + ">" + cardRevealed.GetDisplayName() + "</color>:" + // card name with color
		                         "\n" + cardRevealed.GetCardDescForDisplay(); // card description
	}

	public void ShowStartCardInfo(int cardNumber)
	{
		if (!showRevealedCardName) return;
		revealZoneDisplay.text = "#" + cardNumber + "\n" + GameColorPalette.Me.shield.OpenTag + "--- Start Card ---</color>";
	}

	public string ProcessStatusEffectInfo(CardScript card)
	{
		return ProcessStatusEffectInfo(card.myStatusEffects);
	}

	public string ProcessStatusEffectInfo(List<EnumStorage.StatusEffect> statusEffects)
	{
		var lines = new System.Collections.Generic.List<string>();

		// show rest status effect
		if (statusEffects.Contains(EnumStorage.StatusEffect.Rest))
		{
			var amount = 0;
			foreach (var effect in statusEffects)
			{
				if (effect == EnumStorage.StatusEffect.Rest)
				{
					amount++;
				}
			}

			lines.Add("[" + amount + " Rest Needed]");
		}

		// show infected status effect
		if (statusEffects.Contains(EnumStorage.StatusEffect.Infected))
		{
			var infectedAmount = 0;
			foreach (var effect in statusEffects)
			{
				if (effect == EnumStorage.StatusEffect.Infected)
				{
					infectedAmount++;
				}
			}

			lines.Add("[" + infectedAmount + " Infected]");
		}

		// show mana status effect
		if (statusEffects.Contains(EnumStorage.StatusEffect.Mana))
		{
			var manaAmount = 0;
			foreach (var effect in statusEffects)
			{
				if (effect == EnumStorage.StatusEffect.Mana)
				{
					manaAmount++;
				}
			}

			lines.Add("[" + manaAmount + " Mana]");
		}

		// show heart changed status effect
		if (statusEffects.Contains(EnumStorage.StatusEffect.HeartChanged))
		{
			var heartChangeAmount = 0;
			foreach (var effect in statusEffects)
			{
				if (effect == EnumStorage.StatusEffect.HeartChanged)
				{
					heartChangeAmount++;
				}
			}

			lines.Add("[" + heartChangeAmount + " Heart-Changed]");
		}

		// show power status effect
		if (statusEffects.Contains(EnumStorage.StatusEffect.Power))
		{
			var powerAmount = 0;
			foreach (var effect in statusEffects)
			{
				if (effect == EnumStorage.StatusEffect.Power)
				{
					powerAmount++;
				}
			}

			lines.Add("[" + powerAmount + " Power]");
		}

		// show counter status effect
		if (statusEffects.Contains(EnumStorage.StatusEffect.Counter))
		{
			var counterAmount = 0;
			foreach (var effect in statusEffects)
			{
				if (effect == EnumStorage.StatusEffect.Counter)
				{
					counterAmount++;
				}
			}

			lines.Add("[" + counterAmount + " Counter]");
		}

		return string.Join("\n", lines);
	}

	private void DisplayStatusInfo()
	{
		int playerHp = _pendingOwnerHpCount > 0
			? _displayedOwnerHp
			: CombatManager.Me.ownerPlayerStatusRef.hp;
		int enemyHp = _pendingEnemyHpCount > 0
			? _displayedEnemyHp
			: CombatManager.Me.enemyPlayerStatusRef.hp;
		
		playerStatusDisplay.text =
			"Your HP: " + GameColorPalette.Me.heal.OpenTag + playerHp + "</color>\n";
		// Async-PvP: show the ghost owner when a server deck supplied the enemy side (plan §2.5)
		string vsLine = "";
		if (OpponentDeckCache.Current != null)
		{
			vsLine = "VS " + OpponentDeckCache.Current.username;
		}
		enemyStatusDisplay.text =
			"Their HP: " + GameColorPalette.Me.heal.OpenTag + enemyHp + "</color>\n" + vsLine;
	}

	public void RefreshDeckInfo()
	{
		var playerDeckString = "";
		foreach (var cardScript in CombatFuncs.me.ReturnPlayerCardScripts())
		{
			// Skip Start Card
			if (cardScript.isStartCard) continue;
			
			var statusEffectText = ProcessStatusEffectInfo(cardScript).Replace("\n", " ");
			var playerCardString = statusEffectText + cardScript.GetDisplayName() + "\n";
			playerDeckString += playerCardString;
		}

		playerDeckDisplay.text = playerDeckString;

		var enemyDeckString = "";
		foreach (var cardScript in CombatFuncs.me.ReturnEnemyCardScripts())
		{
			// Skip Start Card
			if (cardScript.isStartCard) continue;
			
			var statusEffectText = ProcessStatusEffectInfo(cardScript).Replace("\n", " ");
			var enemyCardString = statusEffectText + cardScript.GetDisplayName() + "\n";
			enemyDeckString += enemyCardString;
		}

		enemyDeckDisplay.text = enemyDeckString;
	}
}