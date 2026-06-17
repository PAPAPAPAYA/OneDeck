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
	
	// HP display pending queue: logic updates HP immediately, but each Attack animation
	// carries its own post-hit HP value. The UI shows the oldest pending value until
	// the corresponding animation hits and calls CommitHpDisplay.
	private Queue<int> _pendingOwnerHp = new Queue<int>();
	private Queue<int> _pendingEnemyHp = new Queue<int>();
	private int _displayedOwnerHp;
	private int _displayedEnemyHp;

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
	/// Queue a post-hit HP value for display. The first queued value freezes the UI on
	/// preHitHp; each subsequent CommitHpDisplay pops the oldest value and shows it.
	/// </summary>
	/// <param name="target">Target player status</param>
	/// <param name="preHitHp">HP value to freeze the display on before the first hit lands</param>
	/// <param name="postHitHp">HP value to display when this attack hits</param>
	public void SnapshotHpDisplay(PlayerStatusSO target, int preHitHp, int postHitHp)
	{
		if (target == null) return;
		if (target == CombatManager.Me.ownerPlayerStatusRef)
		{
			if (_pendingOwnerHp.Count == 0)
			{
				_displayedOwnerHp = preHitHp;
			}
			_pendingOwnerHp.Enqueue(postHitHp);
		}
		else
		{
			if (_pendingEnemyHp.Count == 0)
			{
				_displayedEnemyHp = preHitHp;
			}
			_pendingEnemyHp.Enqueue(postHitHp);
		}
	}
	
	/// <summary>
	/// Pop the oldest pending HP display value for the target. This makes the UI update
	/// to the HP value that corresponds to the current hitting attack.
	/// </summary>
	/// <param name="target">Target player status</param>
	public void CommitHpDisplay(PlayerStatusSO target)
	{
		if (target == null) return;
		if (target == CombatManager.Me.ownerPlayerStatusRef)
		{
			if (_pendingOwnerHp.Count > 0)
			{
				_displayedOwnerHp = _pendingOwnerHp.Dequeue();
			}
		}
		else
		{
			if (_pendingEnemyHp.Count > 0)
			{
				_displayedEnemyHp = _pendingEnemyHp.Dequeue();
			}
		}
	}
	
	/// <summary>
	/// Clear all pending HP display values. Used when animations are cancelled or combat ends.
	/// </summary>
	public void ClearHpDisplayLocks()
	{
		_pendingOwnerHp.Clear();
		_pendingEnemyHp.Clear();
		_displayedOwnerHp = 0;
		_displayedEnemyHp = 0;
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
		string cardNameColor = ownersCard ? "#87CEEB" : "orange";
		revealZoneDisplay.text = "#" + cardNumber + "\n" +// card num
		                         ProcessStatusEffectInfo(cardRevealed) + // tags
		                         "<color=" + cardNameColor + ">" + cardRevealed.GetDisplayName() + "</color>:" + // card name with color
		                         "\n" + cardRevealed.cardDesc; // card description
	}

	public void ShowStartCardInfo(int cardNumber)
	{
		if (!showRevealedCardName) return;
		revealZoneDisplay.text = "#" + cardNumber + "\n<color=grey>--- Start Card ---</color>";
	}

	public string ProcessStatusEffectInfo(CardScript card)
	{
		return ProcessStatusEffectInfo(card.myStatusEffects);
	}

	public string ProcessStatusEffectInfo(List<EnumStorage.StatusEffect> statusEffects)
	{
		var lines = new System.Collections.Generic.List<string>();

		// show revive status effect
		if (statusEffects.Contains(EnumStorage.StatusEffect.Revive))
		{
			var amount = 0;
			foreach (var effect in statusEffects)
			{
				if (effect == EnumStorage.StatusEffect.Revive)
				{
					amount++;
				}
			}

			lines.Add("[" + amount + " Revive]");
		}

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
		int playerHp = _pendingOwnerHp.Count > 0
			? _displayedOwnerHp
			: CombatManager.Me.ownerPlayerStatusRef.hp;
		int enemyHp = _pendingEnemyHp.Count > 0
			? _displayedEnemyHp
			: CombatManager.Me.enemyPlayerStatusRef.hp;
		
		playerStatusDisplay.text =
			"Your HP: <color=#90EE90>" + playerHp + "</color>\n";
		enemyStatusDisplay.text =
			"Their HP: <color=#90EE90>" + enemyHp + "</color>\n";
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