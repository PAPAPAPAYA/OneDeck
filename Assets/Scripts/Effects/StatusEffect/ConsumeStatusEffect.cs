using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class ConsumeStatusEffect : EffectScript
	{
		public EnumStorage.StatusEffect statusEffectToConsume;
		// VISUAL-FIX(2026-06-12): ConsumeOwnStatusEffect missing projectile animation
		//   Cause:    ConsumeOwnStatusEffect only captured PopUp -> StatusEffectChange -> SlotIn,
		//             giving no clear visual feedback that the status effect was consumed.
		//   Affects:  ConsumeStatusEffect, AnimationRequest, RecorderAnimationPlayer, CombatUXManager
		//   Regress:  Reveal a card whose effect calls ConsumeOwnStatusEffect (e.g. OVERCHARGED_SUMMONER)
		//             Check: card pops up, projectile flies toward statusEffectConsumePos, status text updates, then slots back in.
		public void ConsumeOwnStatusEffect(int amount)
		{
			// first check if amount is met
			if (!EnumStorage.DoesListContainAmountOfStatusEffect(myCardScript.myStatusEffects, amount, statusEffectToConsume)) return;
			// Snapshot display state before mutating so card text updates are deferred until animation completes
			var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
			var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
			if (recorder != null && RecorderAnimationPlayer.me != null)
			{
				myCardScript.SnapshotDisplayState();
			}
			// then remove status effect
			var amountRemoved = 0;
			for (var i = myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
			{
				if (myCardScript.myStatusEffects[i] == statusEffectToConsume && amountRemoved < amount)
				{
					myCardScript.myStatusEffects.RemoveAt(i);
					amountRemoved++;
				}
			}
			// capture animation request for status effect consumption
			if (recorder != null)
			{
				// 1. Pop Up
				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.PopUp,
					targetCard = myCardScript.gameObject
				});

				// 2. Projectile flies from this card to statusEffectConsumePos
				Vector3 consumePos = CombatUXManager.me != null && CombatUXManager.me.statusEffectConsumePos != null
					? CombatUXManager.me.statusEffectConsumePos.position
					: myCardScript.transform.position;

				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.StatusEffectProjectile,
					attackerCard = myCard,
					targetCard = myCardScript.gameObject,
					customProjectileEndPosition = consumePos
				});

				// 3. Status Effect Change (tint + particles)
				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.StatusEffectChange,
					targetCard = myCardScript.gameObject,
					statusEffect = statusEffectToConsume,
					statusEffectAmount = -amountRemoved,
					deferDisplayCommit = true
				});

				// 4. Slot In
				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.SlotIn,
					targetCard = myCardScript.gameObject
				});
			}
			else
			{
				CapturePopUpStatusEffectChangeSlotIn(myCardScript.gameObject, statusEffectToConsume, -amountRemoved);
			}
			// lastly, refresh info display
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}

		/// <summary>
		/// Randomly consume 1 statusEffectToConsume from X enemy cards in the combined deck.
		/// </summary>
		/// <param name="amount">Number of enemy cards to target</param>
		public void ConsumeRandomEnemyCardsStatusEffect(int amount)
		{
			var eligibleCards = new List<CardScript>();
			foreach (var card in CombatManager.Me.combinedDeckZone)
			{
				if (card == null) continue;
				var cardScript = card.GetComponent<CardScript>();
				if (cardScript == null) continue;
				if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
				if (cardScript.myStatusRef == myCardScript.myStatusRef) continue; // must be enemy card
				if (!cardScript.myStatusEffects.Contains(statusEffectToConsume)) continue; // must have the status effect
				eligibleCards.Add(cardScript);
			}

			if (CombatManager.Me.revealZone != null)
			{
				var revealCardScript = CombatManager.Me.revealZone.GetComponent<CardScript>();
				if (revealCardScript != null &&
				    !CombatManager.ShouldSkipEffectProcessing(revealCardScript) &&
				    revealCardScript.myStatusRef != myCardScript.myStatusRef &&
				    revealCardScript.myStatusEffects.Contains(statusEffectToConsume) &&
				    !eligibleCards.Exists(c => c.gameObject == CombatManager.Me.revealZone))
				{
					eligibleCards.Add(revealCardScript);
				}
			}

			if (eligibleCards.Count == 0) return;

			eligibleCards = UtilityFuncManagerScript.ShuffleList(eligibleCards);
			var targetCount = Mathf.Min(amount, eligibleCards.Count);
			for (var i = 0; i < targetCount; i++)
			{
				var targetCard = eligibleCards[i];
				// Snapshot display state before mutating so card text updates are deferred until animation completes
				var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
				var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
				if (recorder != null && RecorderAnimationPlayer.me != null)
				{
					targetCard.SnapshotDisplayState();
				}
				targetCard.myStatusEffects.Remove(statusEffectToConsume);
				CapturePopUpStatusEffectChangeSlotIn(targetCard.gameObject, statusEffectToConsume, -1);
			}

			CombatInfoDisplayer.me.RefreshDeckInfo();
		}

		// caution: only used by status effect resolver to destroy self after resolving
		public void DestroySelf()
		{
			Destroy(gameObject);
		}
	}
}