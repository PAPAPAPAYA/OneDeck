using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	/// <summary>
	/// Attack-giver counterpart of StatusEffectGiverEffect (attack-attribute redesign).
	/// Grants permanent attack (CardScript.ModifyAttack) instead of status effect layers;
	/// the attack-gain event family, per-card stats and AttackChange animation capture all
	/// live in EffectScript.ApplyAttackCore. Target-selection helpers are inherited.
	/// </summary>
	public class AttackGiverEffect : StatusEffectGiverEffect
	{
		/// <summary>
		/// Attack granting has no status-effect receive restrictions (no stacking limit and no
		/// statusEffectToGive gate): any card may receive permanent attack. The 强化 target pool
		/// is shaped by PassesDamageFilter (IsCreature || HasAttackAttribute) / the target predicate, not by
		/// CanReceiveStatusEffect — which would otherwise always reject cards when
		/// statusEffectToGive is None (the field is meaningless for attack granting).
		/// </summary>
		protected override bool CanReceiveStatusEffect(CardScript cardScript, EnumStorage.StatusEffect effect)
		{
			return true;
		}

		/// <summary>
		/// Give permanent attack to this card itself.
		/// </summary>
		public virtual void GiveSelfAttack(int amount)
		{
			if (amount <= 0) return;
			ApplyAttackCore(myCardScript, amount, statusEffectParticlePrefab, particleYOffset);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { myCardScript }, amount);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Double this card's own attack (e.g. UNFINISHED_ROBOT "翻倍自身攻击力").
		/// Gains equal to its current attack, so later changes scale up too.
		/// </summary>
		public virtual void DoubleOwnAttack()
		{
			int currentAttack = myCardScript.GetAttack();
			if (currentAttack <= 0) return;
			ApplyAttackCore(myCardScript, currentAttack, statusEffectParticlePrefab, particleYOffset);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { myCardScript }, currentAttack);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Give permanent attack to random friendly cards — same selection semantics as
		/// GiveStatusEffect: the friendly pool is shuffled, then one random card is picked
		/// per point of attack (the same card may be picked multiple times).
		/// </summary>
		public virtual void GiveAttack(int amount)
		{
			if (amount <= 0) return;

			var cardsToGiveTag = new List<GameObject>();
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToGiveTag, true);
			if (includeSelf) cardsToGiveTag.Add(myCard);
			if (combatManager.revealZone != null && !cardsToGiveTag.Contains(combatManager.revealZone))
			{
				if (combatManager.revealZone != myCard || includeSelf)
					cardsToGiveTag.Add(combatManager.revealZone);
			}
			cardsToGiveTag = UtilityFuncManagerScript.ShuffleList(cardsToGiveTag);
			for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
			{
				var targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
				if (ShouldSkipCard(targetCardScript) || !MatchesTargetFilter(targetCardScript, target) ||
				    !PassesDamageFilter(targetCardScript))
					cardsToGiveTag.RemoveAt(i);
			}
			if (cardsToGiveTag.Count <= 0) return;
			if (spreadEvenly) amount = Mathf.Clamp(amount, 0, cardsToGiveTag.Count);

			var targetCards = new List<CardScript>();
			for (var i = 0; i < amount; i++)
			{
				CardScript targetCardScript = spreadEvenly
					? cardsToGiveTag[i].GetComponent<CardScript>()
					: cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
				targetCards.Add(targetCardScript);
			}

			foreach (var t in targetCards)
			{
				ApplyAttackCore(t, 1, statusEffectParticlePrefab, particleYOffset, 1);
			}

			CombatInfoDisplayer.me?.RefreshDeckInfo();
			CaptureBatchStatusEffectAnimation(targetCards, 1);
		}

		/// <summary>
		/// Give permanent attack to all friendly cards (MARTYR "被埋葬:所有友方卡 +1 攻击力").
		/// </summary>
		public virtual void GiveAllFriendlyAttack(int amount)
		{
			if (amount <= 0) return;

			var targetCards = CollectFriendlyCards(filterCanReceive: true, includeSelf: includeSelf);
			if (targetCards.Count <= 0) return;

			foreach (var card in targetCards)
			{
				ApplyAttackCore(card, amount, statusEffectParticlePrefab, particleYOffset);
			}

			CombatInfoDisplayer.me?.RefreshDeckInfo();
			CaptureBatchStatusEffectAnimation(targetCards, amount);
		}

		/// <summary>
		/// Give permanent attack to the friendly card with the lowest attack
		/// (POWER_TRANSFER "给予 1 张友方[攻击者](最低攻击力)1 攻击力").
		/// Only friendly cards with positive attack are eligible; ties are broken randomly.
		/// </summary>
		public virtual void GiveFriendlyCardWithMinAttack(int amount)
		{
			if (amount <= 0) return;

			var target = CardScript.FindCardWithMinAttack(
				combatManager.combinedDeckZone,
				combatManager.revealZone,
				c => !ShouldSkipCard(c) &&
				     MatchesTargetFilter(c, this.target) &&
				     PassesDamageFilter(c) &&
				     c.myStatusRef == myCardScript.myStatusRef &&
				     c.GetAttack() > 0 &&
				     (includeSelf || c != myCardScript));
			if (target == null) return;

			ApplyAttackCore(target, amount, statusEffectParticlePrefab, particleYOffset);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { target }, amount);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Give permanent attack to the last X cards in the combined deck (MAD_SCIENTIST,
		/// CURSE_THIRST_ARCH_SUMMONER). Reads lastXCardsCount / statusEffectLayerCount.
		/// </summary>
		public virtual void GiveAttackToLastXCards()
		{
			if (lastXCardsCount <= 0 || statusEffectLayerCount <= 0) return;
			var combinedDeck = combatManager.combinedDeckZone;
			int startIndex;
			if (combatManager.revealZone != null && combatManager.revealZone == myCard)
			{
				startIndex = combinedDeck.Count - 1;
			}
			else
			{
				int currentIndex = -1;
				for (int i = 0; i < combinedDeck.Count; i++)
				{
					if (combinedDeck[i] == myCard)
					{
						currentIndex = i;
						break;
					}
				}
				if (currentIndex < 0) return;
				startIndex = currentIndex - 1;
			}
			var targetCards = new List<CardScript>();
			int cardsGiven = 0;
			for (int i = startIndex; i >= 0 && cardsGiven < lastXCardsCount; i--)
			{
				var targetCard = combinedDeck[i];
				var targetCardScript = targetCard.GetComponent<CardScript>();
				if (ShouldSkipCard(targetCardScript)) continue;
				if (!PassesDamageFilter(targetCardScript)) continue;
				targetCards.Add(targetCardScript);
				cardsGiven++;
			}
			if (targetCards.Count <= 0) return;

			foreach (var t in targetCards)
			{
				ApplyAttackCore(t, statusEffectLayerCount, statusEffectParticlePrefab, particleYOffset);
			}

			CombatInfoDisplayer.me?.RefreshDeckInfo();
			CaptureBatchStatusEffectAnimation(targetCards, statusEffectLayerCount);
		}

		/// <summary>
		/// Give permanent attack to X random friendly cards (BLACKSMITH, POWER_SURGE,
		/// SACRIFICIAL_SWORD). Reads xFriendlyCount / yFriendlyLayerCount.
		/// </summary>
		public virtual void GiveAttackToXFriendly()
		{
			if (xFriendlyCount <= 0 || yFriendlyLayerCount <= 0) return;

			var friendlyCards = CollectFriendlyCards(filterCanReceive: true, includeSelf: includeSelf);
			if (friendlyCards.Count <= 0) return;

			friendlyCards = UtilityFuncManagerScript.ShuffleList(friendlyCards);
			var targetCards = new List<CardScript>();
			int actualCount = Mathf.Min(xFriendlyCount, friendlyCards.Count);
			for (int i = 0; i < actualCount; i++) targetCards.Add(friendlyCards[i]);
			if (targetCards.Count <= 0) return;

			foreach (var t in targetCards)
			{
				ApplyAttackCore(t, yFriendlyLayerCount, statusEffectParticlePrefab, particleYOffset);
			}

			CombatInfoDisplayer.me?.RefreshDeckInfo();
			CaptureBatchStatusEffectAnimation(targetCards, yFriendlyLayerCount);
		}

		/// <summary>
		/// Based on ownerIntSO/enemyIntSO, repeat N times: give 1 attack to one random
		/// friendly card (CURSE_THIRST_SHAMAN — reads the enemy curse attack aggregate).
		/// </summary>
		public virtual void GiveAttackToXFriendly_BasedOnIntSO()
		{
			IntSO intSO = GetIntSOForOwner(ownerIntSO, enemyIntSO);
			if (intSO == null) return;
			if (intSO.value <= 0) return;

			var targetCards = new List<CardScript>();

			for (int i = 0; i < intSO.value; i++)
			{
				var friendlyCards = CollectFriendlyCards(filterCanReceive: true, includeSelf: includeSelf);
				if (friendlyCards.Count <= 0) break;

				int randomIndex = Random.Range(0, friendlyCards.Count);
				var targetCard = friendlyCards[randomIndex];

				ApplyAttackCore(targetCard, 1, statusEffectParticlePrefab, particleYOffset);
				targetCards.Add(targetCard);
			}

			if (targetCards.Count <= 0) return;

			CombatInfoDisplayer.me?.RefreshDeckInfo();

			var uniqueTargets = new List<CardScript>();
			var projectileCountsPerTarget = new List<int>();
			foreach (var card in targetCards)
			{
				int existingIndex = uniqueTargets.IndexOf(card);
				if (existingIndex < 0)
				{
					uniqueTargets.Add(card);
					projectileCountsPerTarget.Add(1);
				}
				else
				{
					projectileCountsPerTarget[existingIndex]++;
				}
			}

			CaptureBatchStatusEffectAnimation(uniqueTargets, 1, projectileCountsPerTarget);
		}

		/// <summary>
		/// Give permanent attack to the card that most recently gained attack (WEAPON_SPIRIT
		/// "被动：友方生物被强化时：强化1该生物" — the enhanced creature gets amplified).
		/// Creatures only; the effect-chain loop guard blocks amplification re-triggers within
		/// the same chain, so each external enhancement reaction fires exactly once.
		/// </summary>
		public virtual void GiveAttackToLastGainedAttack(int amount)
		{
			if (amount <= 0) return;
			var cm = combatManager;
			if (cm == null || cm.lastCardGainedAttack == null) return;
			var target = cm.lastCardGainedAttack;
			if (!target.IsCreature) return;
			if (target.myStatusRef != myCardScript.myStatusRef) return;
			if (CombatManager.ShouldSkipEffectProcessing(target)) return;
			GiveAttackToXFriendlyWithTarget(amount, target);
		}

		private void GiveAttackToXFriendlyWithTarget(int amount, CardScript target)
		{
			ApplyAttackCore(target, amount, statusEffectParticlePrefab, particleYOffset);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { target }, amount);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Give attack to X random friendly cards based on ValueTrackerManager staged values
		/// (ELDER_SORCERER — "本回合每置顶 1 张友方卡:给予 1 张友方卡 1 攻击力").
		/// </summary>
		public virtual void GiveAttackToXFriendly_BasedOnStaged(int layerCount)		{
			if (layerCount <= 0) return;
			if (ValueTrackerManager.me == null) return;

			int xCount = 0;
			if (myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
			{
				if (ValueTrackerManager.me.stagedOwnerRef != null)
					xCount = ValueTrackerManager.me.stagedOwnerRef.value;
			}
			else
			{
				if (ValueTrackerManager.me.stagedEnemyRef != null)
					xCount = ValueTrackerManager.me.stagedEnemyRef.value;
			}

			if (xCount <= 0) return;

			int originalXFriendlyCount = xFriendlyCount;
			int originalYFriendlyLayerCount = yFriendlyLayerCount;
			xFriendlyCount = xCount;
			yFriendlyLayerCount = layerCount;
			GiveAttackToXFriendly();
			xFriendlyCount = originalXFriendlyCount;
			yFriendlyLayerCount = originalYFriendlyLayerCount;
		}

		/// <summary>
		/// Apply a THIS-ROUND attack modifier to every creature on both sides (WEAKENING_FIELD
		/// "所有生物本回合攻击力-1"). Status-type curse cards are skipped naturally — they are not
		/// creatures (2026-09-02 type split replaced the former curse-typeID exclusion). Uses
		/// CardScript.ModifyAttackThisRound — this is not a 强化 grant, so no attack-gain events
		/// fire (the round reset clears it). Method name kept: WEAKENING_FIELD.prefab binds it by
		/// name via a serialized UnityEvent call.
		/// </summary>
		public virtual void ModifyAllCreatureAttackThisRoundExceptCurse(int delta)
		{
			if (delta == 0) return;
			var deck = combatManager.combinedDeckZone;
			if (deck != null)
			{
				foreach (var cardObj in deck)
				{
					if (cardObj == null) continue;
					var cardScript = cardObj.GetComponent<CardScript>();
					if (cardScript == null || !cardScript.IsCreature) continue;
					if (CombatManager.ShouldSkipEffectProcessing(cardScript)) continue;
					cardScript.ModifyAttackThisRound(delta);
				}
			}
			if (combatManager.revealZone != null)
			{
				var revealScript = combatManager.revealZone.GetComponent<CardScript>();
				if (revealScript != null && revealScript.IsCreature &&
				    !CombatManager.ShouldSkipEffectProcessing(revealScript))
				{
					revealScript.ModifyAttackThisRound(delta);
				}
			}
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}
	}
}
