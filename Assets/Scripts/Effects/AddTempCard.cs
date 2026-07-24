using System.Collections.Generic;
using System.Linq;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class AddTempCard : EffectScript
	{
		public int cardCount = 1;
		
		[Header("Curse Card Copy")]
		[Tooltip("Cursed card type ID to copy (empty string means disabled)")]
		public StringSO curseCardTypeID;

		public void AddCardToMe(GameObject cardToAdd)
		{
			for (int i = 0; i < cardCount; i++)
			{
				GameObject newCard = CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.myStatusRef);
				CapturePopUpSlotInForNewCard(newCard);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				AppendLog("// [" + GameColorPalette.Me.friendly.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.friendly.OpenTag + "你</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.friendly.OpenTag + cardToAdd.name + "</color>]");
			}
			else // if this card belong to enemy
			{
				AppendLog("// [" + GameColorPalette.Me.enemy.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.enemy.OpenTag + "敌人</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.enemy.OpenTag + cardToAdd.name + "</color>]");
			}
		}

		public void AddCardToThem(GameObject cardToAdd)
		{
			for (int i = 0; i < cardCount; i++)
			{
				GameObject newCard = CombatFuncs.me.AddCard_TargetSpecific(cardToAdd, myCardScript.theirStatusRef);
				CapturePopUpSlotInForNewCard(newCard);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				AppendLog("// [" + GameColorPalette.Me.friendly.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.enemy.OpenTag + "敌人</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.enemy.OpenTag + cardToAdd.name + "</color>]");
			}
			else // if this card belong to enemy
			{
				AppendLog("// [" + GameColorPalette.Me.enemy.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.friendly.OpenTag + "你</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.friendly.OpenTag + cardToAdd.name + "</color>]");
			}
		}

		public void AddSelfToMe()
		{
			for (int i = 0; i < cardCount; i++)
			{
				GameObject newCard = CombatFuncs.me.AddCard_TargetSpecific(myCard, myCardScript.myStatusRef);
				CapturePopUpSlotInForNewCard(newCard);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				AppendLog("// [" + GameColorPalette.Me.friendly.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.friendly.OpenTag + "你</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.friendly.OpenTag + myCard.name + "</color>]");
			}
			else // if this card belong to enemy
			{
				AppendLog("// [" + GameColorPalette.Me.enemy.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.enemy.OpenTag + "敌人</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.enemy.OpenTag + myCard.name + "</color>]");
			}
		}

		public void AddSelfToThem()
		{
			for (int i = 0; i < cardCount; i++)
			{
				GameObject newCard = CombatFuncs.me.AddCard_TargetSpecific(myCard, myCardScript.theirStatusRef);
				CapturePopUpSlotInForNewCard(newCard);
			}
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef) // if this card belongs to player
			{
				AppendLog("// [" + GameColorPalette.Me.friendly.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.enemy.OpenTag + "敌人</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.enemy.OpenTag + myCard.name + "</color>]");
			}
			else // if this card belong to enemy
			{
				AppendLog("// [" + GameColorPalette.Me.enemy.OpenTag + myCard.name + "</color>]向" + GameColorPalette.Me.friendly.OpenTag + "你</color>添加了" + GameColorPalette.Me.highlight.OpenTag + cardCount + "</color>张[" + GameColorPalette.Me.friendly.OpenTag + myCard.name + "</color>]");
			}
		}

		/// <summary>
		/// Capture PopUp + SlotIn animation requests for a newly added card.
		/// </summary>
		private void CapturePopUpSlotInForNewCard(GameObject newCard)
		{
			if (newCard == null) return;
			var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
			var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
			if (recorder == null) return;

			int deckIndex = CombatManager.Me != null ? CombatManager.Me.combinedDeckZone.IndexOf(newCard) : -1;
			if (deckIndex < 0) deckIndex = 0;

			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.MoveToPopUpPosition,
				targetCard = newCard,
				targetIndex = deckIndex
			});
			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.SlotIn,
				targetCard = newCard
			});
		}
		
		/// <summary>
		/// Copy an enemy card matching curseCardTypeID to the effect triggerer's opponent.
		/// If multiple enemy cards match, randomly select one.
		/// Copies will retain all original status effects.
		/// </summary>
		public void CopyEnemyCurseCardToThem()
		{
			// If curseCardTypeID is empty, do not execute
			if (curseCardTypeID == null || string.IsNullOrEmpty(curseCardTypeID.value))
			{
				// Debug.LogWarning($"[{myCard.name}] CopyEnemyCurseCardToThem: curseCardTypeID is empty or null");
				return;
			}
			
			// Get all enemy cards
			List<CardScript> enemyCards = CombatFuncs.me.ReturnEnemyCardScripts();
			
			// Filter cards matching curseCardTypeID
			List<CardScript> matchingCards = enemyCards
				.Where(card => card.cardTypeID == curseCardTypeID?.value)
				.ToList();
			
			// If no matching cards, do not execute
			if (matchingCards.Count == 0)
			{
				// Debug.Log($"[{myCard.name}] CopyEnemyCurseCardToThem: no enemy card with typeID '{curseCardTypeID?.value}' found");
				return;
			}
			
			// Randomly select one matching card
			CardScript selectedCard = matchingCards[Random.Range(0, matchingCards.Count)];
			GameObject cardPrefab = selectedCard.gameObject;
			
			// Save original card's status effects
			List<EnumStorage.StatusEffect> originalStatusEffects = selectedCard.myStatusEffects;
			
			// Copy selected card to the triggerer's opponent and copy status effects
			for (int i = 0; i < cardCount; i++)
			{
				GameObject newCard = CombatFuncs.me.AddCard_TargetSpecific(cardPrefab, myCardScript.theirStatusRef);
				
				// Copy status effects to new card
				if (newCard != null)
				{
					CardScript newCardScript = newCard.GetComponent<CardScript>();
					if (newCardScript != null && originalStatusEffects != null)
					{
						newCardScript.myStatusEffects = new List<EnumStorage.StatusEffect>(originalStatusEffects);
					}
				}
			}
			
			// Log result
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
			{
				AppendLog($"// [{GameColorPalette.Me.friendly.OpenTag}{myCard.name}</color>]从敌方复制了{GameColorPalette.Me.highlight.OpenTag}{cardCount}</color>张[{GameColorPalette.Me.enemy.OpenTag}{cardPrefab.name}</color>]给{GameColorPalette.Me.enemy.OpenTag}敌人</color>");
			}
			else
			{
				AppendLog($"// [{GameColorPalette.Me.enemy.OpenTag}{myCard.name}</color>]从你方复制了{GameColorPalette.Me.highlight.OpenTag}{cardCount}</color>张[{GameColorPalette.Me.friendly.OpenTag}{cardPrefab.name}</color>]给{GameColorPalette.Me.friendly.OpenTag}你</color>");
			}
		}
	}
}