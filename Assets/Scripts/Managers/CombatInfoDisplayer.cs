using System;
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
	
	public StringSO effectResultString;
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
		effectResultDisplay.text = effectResultString.value;
	}

	public void ClearInfo()
	{
		playerStatusDisplay.text = "";
		enemyStatusDisplay.text = "";
		revealZoneDisplay.text = "";
		combatTipsDisplay.text = "";
		effectResultString.value = "";
		effectResultDisplay.text = "";
		playerDeckDisplay.text = "";
		enemyDeckDisplay.text = "";
	}

	public string ReturnCardOwnerInfo(PlayerStatusSO statusRef)
	{
		if (statusRef == CombatManager.Me.ownerPlayerStatusRef)
		{
			return "Your";
		}
		else
		{
			return "Their";
		}
	}
	
	public void ShowCardInfo(CardScript cardRevealed, int cardNumber, bool ownersCard)
	{
		if (!showRevealedCardName) return;
		// Card name color: blue for player, orange for enemy
		string cardNameColor = ownersCard ? "#87CEEB" : "orange";
		revealZoneDisplay.text = "#" + cardNumber + "\n" +// card num
		                         ProcessStatusEffectInfo(cardRevealed) + // tags
		                         "<color=" + cardNameColor + ">" + cardRevealed.gameObject.name + "</color>:" + // card name with color
		                         "\n" + cardRevealed.cardDesc; // card description
	}

	public void ShowStartCardInfo(int cardNumber)
	{
		if (!showRevealedCardName) return;
		revealZoneDisplay.text = "#" + cardNumber + "\n<color=grey>--- Start Card ---</color>";
	}

	public string ProcessStatusEffectInfo(CardScript card)
	{
		var statusEffectInfo = "";
		
		// show revive status effect
		if (card.myStatusEffects.Contains(EnumStorage.StatusEffect.Revive))
		{
			var amount = 0;
			foreach (var myTag in card.myStatusEffects)
			{
				if (myTag == EnumStorage.StatusEffect.Revive)
				{
					amount++;
				}
			}

			statusEffectInfo += "[" + amount + " Revive]";
		}
		
		// show rest status effect
		if (card.myStatusEffects.Contains(EnumStorage.StatusEffect.Rest))
		{
			var amount = 0;
			foreach (var myTag in card.myStatusEffects)
			{
				if (myTag == EnumStorage.StatusEffect.Rest)
				{
					amount++;
				}
			}

			statusEffectInfo += "[" + amount + " Rest Needed]";
		}

		// show infected status effect
		if (card.myStatusEffects.Contains(EnumStorage.StatusEffect.Infected))
		{
			var infectedAmount = 0;
			foreach (var myTag in card.myStatusEffects)
			{
				if (myTag == EnumStorage.StatusEffect.Infected)
				{
					infectedAmount++;
				}
			}

			statusEffectInfo += "[" + infectedAmount + " Infected]";
		}

		// show mana status effect
		if (card.myStatusEffects.Contains(EnumStorage.StatusEffect.Mana))
		{
			var manaAmount = 0;
			foreach (var myTag in card.myStatusEffects)
			{
				if (myTag == EnumStorage.StatusEffect.Mana)
				{
					manaAmount++;
				}
			}

			statusEffectInfo += "[" + manaAmount + " Mana]";
		}

		// show heart changed status effect
		if (card.myStatusEffects.Contains(EnumStorage.StatusEffect.HeartChanged))
		{
			var heartChangeAmount = 0;
			foreach (var myTag in card.myStatusEffects)
			{
				if (myTag == EnumStorage.StatusEffect.HeartChanged)
				{
					heartChangeAmount++;
				}
			}

			statusEffectInfo += "[" + heartChangeAmount + " Heart-Changed]";
		}

		// show power status effect
		if (card.myStatusEffects.Contains(EnumStorage.StatusEffect.Power))
		{
			var powerAmount = 0;
			foreach (var myTag in card.myStatusEffects)
			{
				if (myTag == EnumStorage.StatusEffect.Power)
				{
					powerAmount++;
				}
			}

			statusEffectInfo += "[" + powerAmount + " Power]";
		}

		if (card.myStatusEffects.Count > 0)
		{
			statusEffectInfo += " ";
		}

		return statusEffectInfo;
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
			// 跳过 Start Card
			if (cardScript.isStartCard) continue;
			
			var playerCardString = ProcessStatusEffectInfo(cardScript) + cardScript.gameObject.name + "\n";
			if (CombatManager.Me.graveZone.Contains(cardScript.gameObject))
			{
				playerCardString = "<color=grey>"+ProcessStatusEffectInfo(cardScript) + cardScript.gameObject.name + "</color>\n";
			}
			playerDeckString += playerCardString;
		}

		playerDeckDisplay.text = playerDeckString;

		var enemyDeckString = "";
		foreach (var cardScript in CombatFuncs.me.ReturnEnemyCardScripts())
		{
			// 跳过 Start Card
			if (cardScript.isStartCard) continue;
			
			var enemyCardString = ProcessStatusEffectInfo(cardScript) + cardScript.gameObject.name + "\n";
			if (CombatManager.Me.graveZone.Contains(cardScript.gameObject))
			{
				enemyCardString = "<color=grey>"+ProcessStatusEffectInfo(cardScript) + cardScript.gameObject.name + "</color>\n";
			}
			enemyDeckString += enemyCardString;
		}

		enemyDeckDisplay.text = enemyDeckString;
	}
}