using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	/// <summary>
	/// Attack-times granting counterpart of AttackGiverEffect: changes the attack SEGMENT
	/// count (attack +N times) instead of the attack value. This-round grants live in
	/// CardScript.attackTimesModThisRound (cleared at round start); creatures additionally
	/// read the faction-level per-round aura (BATTLE_HORN, E7) so creatures generated later
	/// in the round are covered too. The curse grant (RELIC_CURSE_HASTE) is permanent on the
	/// curse instance via CardScript.ModifyAttackTimes.
	/// Every grant captures AttackChange requests flagged attackTimesChange so the particle
	/// plays while the attack PRINT stays put — only the xN badge steps (RecorderAnimationPlayer
	/// skips CommitAttackDisplayDelta for flagged requests). No attack-gain events are raised
	/// here: the value did not change, so 被强化 reactions must not fire.
	/// </summary>
	public class AttackTimesGiverEffect : AttackGiverEffect
	{
		/// <summary>
		/// Give +N attack times to this card itself for this round
		/// (COMBO_STARTER 被强化反应 on onMeGainedAttack; EXILE_BERSERKER on onCardExiledByOwnSide).
		/// </summary>
		public virtual void GiveSelfAttackTimes(int times)
		{
			if (times <= 0) return;
			GrantAttackTimes(myCardScript, times, permanent: false);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { myCardScript }, times);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Give +N attack times to 1 random friendly creature for this round (COMBO_GRANTER
		/// "本回合1友方生物攻击次数+1"). Self is eligible (文案无排除); ties of the random
		/// pool follow the shuffled order.
		/// </summary>
		public virtual void GiveRandomFriendlyCreatureAttackTimes(int times)
		{
			if (times <= 0) return;
			var creatures = CollectFriendlyCreatures();
			if (creatures.Count <= 0) return;
			var target = creatures[Random.Range(0, creatures.Count)];
			GrantAttackTimes(target, times, permanent: false);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { target }, times);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Bump the faction's per-round creature attack-times aura (BATTLE_HORN
		/// "本回合友方生物攻击次数+1", 4.0 E7). Aura lives on ValueTrackerManager, is reset at
		/// every round start, and is read by CardScript.GetAttackTimes for Creature-type cards —
		/// creatures generated later in the same round are covered automatically. The batch
		/// animation runs over the creatures present at grant time (current beneficiaries).
		/// </summary>
		public virtual void BumpFriendlyCreatureAttackTimesAura(int times)
		{
			if (times <= 0) return;
			var tracker = ValueTrackerManager.me;
			if (tracker == null) return;
			IntSO aura = GetIntSOForOwner(
				tracker.creatureAttackTimesAuraOwnerThisRoundRef,
				tracker.creatureAttackTimesAuraEnemyThisRoundRef);
			if (aura == null) return;
			aura.value += times;

			var creatures = CollectFriendlyCreatures();
			if (creatures.Count > 0)
			{
				CaptureBatchStatusEffectAnimation(creatures, times);
			}
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		/// <summary>
		/// Permanently give +N attack times to the currently revealed enemy curse card
		/// (RELIC_CURSE_HASTE 被动, bound to onEnemyCurseCardRevealed — the revealed card is in
		/// the reveal zone when the event fires). The grant rides the curse card instance, which
		/// persists across rounds; the passive carrier never leaves play, so no revoke is needed.
		/// </summary>
		public virtual void GiveRevealedCurseAttackTimes(int times)
		{
			if (times <= 0) return;
			var storage = GameEventStorage.me;
			var combatManagerRef = combatManager;
			if (storage == null || combatManagerRef == null || combatManagerRef.revealZone == null) return;

			var curseScript = combatManagerRef.revealZone.GetComponent<CardScript>();
			if (curseScript == null || ShouldSkipCard(curseScript)) return;
			if (storage.curseCardTypeID == null || string.IsNullOrEmpty(storage.curseCardTypeID.value)) return;
			if (curseScript.cardTypeID != storage.curseCardTypeID.value) return;
			// The curse is hostile to the passive's owner by construction (RaiseOwner fires for
			// enemy-faction curses); keep the guard anyway so a mirrored setup stays correct.
			if (curseScript.myStatusRef == myCardScript.myStatusRef) return;

			GrantAttackTimes(curseScript, times, permanent: true);
			CaptureBatchStatusEffectAnimation(new List<CardScript> { curseScript }, times);
			CombatInfoDisplayer.me?.RefreshDeckInfo();
		}

		private List<CardScript> CollectFriendlyCreatures()
		{
			// CollectFriendlyCards applies ShouldSkipCard + faction + PassesDamageFilter and
			// covers deck + reveal zone; narrow to creatures here.
			var cards = CollectFriendlyCards(filterCanReceive: false, includeSelf: true);
			cards.RemoveAll(c => c == null || !c.IsCreature);
			return cards;
		}

		private void GrantAttackTimes(CardScript target, int times, bool permanent)
		{
			if (target == null || times <= 0) return;
			if (permanent)
			{
				target.ModifyAttackTimes(times);
			}
			else
			{
				target.ModifyAttackTimesThisRound(times);
			}

			// Mirror ApplyAttackCore's capture, flagged as a times change: particle plays,
			// attack print stays put, xN badge refreshes via RefreshCardAttackDisplay.
			var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
			var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
			if (recorder != null)
			{
				recorder.animationRequests.Add(new AnimationRequest
				{
					type = AnimationRequestType.AttackChange,
					targetCard = target.gameObject,
					statusEffectAmount = times,
					statusEffectParticlePrefab = statusEffectParticlePrefab,
					statusEffectParticleYOffset = particleYOffset,
					attackTimesChange = true
				});
			}
		}
	}
}
