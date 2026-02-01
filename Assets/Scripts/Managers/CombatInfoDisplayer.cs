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
	
	public void ShowCardInfo(CardScript cardRevealed, int deckSize, int graveSize, bool ownersCard)
	{
		revealZoneDisplay.text = "#" + (graveSize + 1) + // card num
		                         (ownersCard ? " your card: \n\n" : " their card: \n\n") + // card owner
		                         ProcessTagInfo(cardRevealed) + // tags
		                         cardRevealed.gameObject.name + // card name
		                         "\n" + cardRevealed.cardDesc; // card description
		revealZoneDisplay.color = ownersCard ? Color.blue : Color.red;
	}

	private string ProcessTagInfo(CardScript card)
	{
		var tagInfo = "";
		
		// show undead status effect
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

			tagInfo += "[" + amount + " Undead]";
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

			tagInfo += "[" + amount + " Rested]";
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

			tagInfo += "[" + infectedAmount + " Infected]";
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

			tagInfo += "[" + manaAmount + " Mana]";
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

			tagInfo += "[" + heartChangeAmount + " Heart-Changed]";
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

			tagInfo += "[" + powerAmount + " Power]";
		}

		if (card.myStatusEffects.Count > 0)
		{
			tagInfo += " ";
		}

		return tagInfo;
	}

	private void DisplayStatusInfo()
	{
		playerStatusDisplay.text =
			"Your HP: " + CombatManager.Me.ownerPlayerStatusRef.hp + "\n" +
			"Your SHIELD: " + CombatManager.Me.ownerPlayerStatusRef.shield + "\n";
		enemyStatusDisplay.text =
			"Their HP: " + CombatManager.Me.enemyPlayerStatusRef.hp + "\n" +
			"Their SHIELD: " + CombatManager.Me.enemyPlayerStatusRef.shield + "\n";
	}

	public void RefreshDeckInfo()
	{
		var playerDeckString = "";
		foreach (var cardScript in CombatFuncs.me.ReturnPlayerCardScripts())
		{
			var playerCardString = ProcessTagInfo(cardScript) + cardScript.gameObject.name + "\n";
			if (CombatManager.Me.graveZone.Contains(cardScript.gameObject))
			{
				playerCardString = "<color=grey>"+ProcessTagInfo(cardScript) + cardScript.gameObject.name + "</color>\n";
			}
			playerDeckString += playerCardString;
		}

		playerDeckDisplay.text = playerDeckString;

		var enemyDeckString = "";
		foreach (var cardScript in CombatFuncs.me.ReturnEnemyCardScripts())
		{
			var enemyCardString = ProcessTagInfo(cardScript) + cardScript.gameObject.name + "\n";
			if (CombatManager.Me.graveZone.Contains(cardScript.gameObject))
			{
				enemyCardString = "<color=grey>"+ProcessTagInfo(cardScript) + cardScript.gameObject.name + "</color>\n";
			}
			enemyDeckString += enemyCardString;
		}

		enemyDeckDisplay.text = enemyDeckString;
	}
}