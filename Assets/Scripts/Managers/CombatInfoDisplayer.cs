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
		playerStatusDisplay.text =
			"Your HP: <color=#90EE90>" + CombatManager.Me.ownerPlayerStatusRef.hp + "</color>\n" +
			"Your SHIELD: <color=grey>" + CombatManager.Me.ownerPlayerStatusRef.shield + "</color>\n";
		enemyStatusDisplay.text =
			"Their HP: <color=#90EE90>" + CombatManager.Me.enemyPlayerStatusRef.hp + "</color>\n" +
			"Their SHIELD: <color=grey>" + CombatManager.Me.enemyPlayerStatusRef.shield + "</color>\n";
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