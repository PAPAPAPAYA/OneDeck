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

	public void ShowCardInfo(CardScript cardRevealed, int deckSize, int graveSize, bool ownersCard)
	{
		revealZoneDisplay.text = "#" + (graveSize + 1) + // card num
		                         (ownersCard ? " your card: \n\n" : " their card: \n\n") + // card owner
		                         ProcessTagInfo(cardRevealed) + // tags
		                         cardRevealed.cardName + // card name
		                         "\n" + cardRevealed.cardDesc; // card description
		revealZoneDisplay.color = ownersCard ? Color.blue : Color.red;
	}

	private string ProcessTagInfo(CardScript card)
	{
		var tagInfo = "";

		// show infected tag
		if (card.myTags.Contains(EnumStorage.Tag.Infected))
		{
			var infectedAmount = 0;
			foreach (var myTag in card.myTags)
			{
				if (myTag == EnumStorage.Tag.Infected)
				{
					infectedAmount++;
				}
			}

			tagInfo += "[" + infectedAmount + " Infected]";
		}

		// show mana tag
		if (card.myTags.Contains(EnumStorage.Tag.Mana))
		{
			var manaAmount = 0;
			foreach (var myTag in card.myTags)
			{
				if (myTag == EnumStorage.Tag.Mana)
				{
					manaAmount++;
				}
			}

			tagInfo += "[" + manaAmount + " Mana]";
		}

		// show heart changed tag
		if (card.myTags.Contains(EnumStorage.Tag.HeartChanged))
		{
			var heartChangeAmount = 0;
			foreach (var myTag in card.myTags)
			{
				if (myTag == EnumStorage.Tag.HeartChanged)
				{
					heartChangeAmount++;
				}
			}

			tagInfo += "[" + heartChangeAmount + " Heart-Changed]";
		}

		// show power tag
		if (card.myTags.Contains(EnumStorage.Tag.Power))
		{
			var powerAmount = 0;
			foreach (var myTag in card.myTags)
			{
				if (myTag == EnumStorage.Tag.Power)
				{
					powerAmount++;
				}
			}

			tagInfo += "[" + powerAmount + " Power]";
		}

		if (card.myTags.Count > 0)
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
			var playerCardString = ProcessTagInfo(cardScript) + cardScript.cardName + "\n";
			if (CombatManager.Me.graveZone.Contains(cardScript.gameObject))
			{
				playerCardString = "<color=grey>"+ProcessTagInfo(cardScript) + cardScript.cardName + "</color>\n";
			}
			playerDeckString += playerCardString;
		}

		playerDeckDisplay.text = playerDeckString;

		var enemyDeckString = "";
		foreach (var cardScript in CombatFuncs.me.ReturnEnemyCardScripts())
		{
			var enemyCardString = ProcessTagInfo(cardScript) + cardScript.cardName + "\n";
			if (CombatManager.Me.graveZone.Contains(cardScript.gameObject))
			{
				enemyCardString = "<color=grey>"+ProcessTagInfo(cardScript) + cardScript.cardName + "</color>\n";
			}
			enemyDeckString += enemyCardString;
		}

		enemyDeckDisplay.text = enemyDeckString;
	}
}