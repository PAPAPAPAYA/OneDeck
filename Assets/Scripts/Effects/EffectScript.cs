using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.SOScripts;
using UnityEngine;
using DefaultNamespace.Managers;

public class EffectScript : MonoBehaviour
{

	protected CombatManager combatManager;
	protected GameObject myCard;
	protected CardScript myCardScript;
	protected virtual void OnEnable()
	{
		combatManager = CombatManager.Me;
		myCard = transform.parent.gameObject;
		myCardScript = myCard.GetComponent<CardScript>();
	}

	#region Helper Methods - Card Ownership & Colors
	protected string GetCardOwnerPrefix(PlayerStatusSO statusRef)
	{
		return statusRef == combatManager.ownerPlayerStatusRef ? "<color=#87CEEB>你的</color>[" : "<color=orange>敌方的</color>[";
	}

	protected string GetCardOwnerColor(PlayerStatusSO statusRef)
	{
		return statusRef == combatManager.ownerPlayerStatusRef ? "#87CEEB" : "orange";
	}

	protected string GetMyCardOwnerPrefix()
	{
		return GetCardOwnerPrefix(myCardScript.myStatusRef);
	}

	protected string GetMyCardOwnerColor()
	{
		return GetCardOwnerColor(myCardScript.myStatusRef);
	}

	/// <summary>
	/// Selects the IntSO that matches this card's owner faction.
	/// Returns ownerIntSO if the card belongs to the owner/player, otherwise enemyIntSO.
	/// </summary>
	protected IntSO GetIntSOForOwner(IntSO ownerIntSO, IntSO enemyIntSO)
	{
		if (myCardScript == null || combatManager == null) return null;
		return myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef
			? ownerIntSO
			: enemyIntSO;
	}
	#endregion

	#region Helper Methods - Status Effect Application Core
	protected void ApplyStatusEffectCore(
		CardScript targetCardScript,
		EnumStorage.StatusEffect effect,
		int amount,
		GameObject resolverPrefab,
		ParticleSystem particlePrefab,
		float particleYOffset,
		int resolverCount,
		int? logAmount = null,
		bool suppressLog = false)
	{
		if (effect == EnumStorage.StatusEffect.None) return;
		int actualLogAmount = logAmount ?? amount;

		// Update last applied status effect tracking
		if (ValueTrackerManager.me != null)
		{
			if (ValueTrackerManager.me.lastAppliedStatusEffectRef != null)
				ValueTrackerManager.me.lastAppliedStatusEffectRef.value = effect;
			if (ValueTrackerManager.me.lastAppliedStatusEffectAmountRef != null)
				ValueTrackerManager.me.lastAppliedStatusEffectAmountRef.value = actualLogAmount;
		}

		// Raise status effect events
		combatManager.lastCardGotStatusEffect = targetCardScript;
		GameEventStorage.me?.onMeGotStatusEffect?.RaiseSpecific(targetCardScript.gameObject);

		// Raise Power-related events when a card gains Power
		if (effect == EnumStorage.StatusEffect.Power)
		{
			combatManager.lastCardGotPower = targetCardScript;
			GameEventStorage.me?.onAnyCardGotPower?.Raise();
			GameEventStorage.me?.onMeGotPower?.RaiseSpecific(targetCardScript.gameObject);
			if (targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef)
			{
				GameEventStorage.me?.onFriendlyCardGotPower?.RaiseOwner();
				GameEventStorage.me?.onEnemyCardGotPower?.RaiseOpponent();
			}
			else
			{
				GameEventStorage.me?.onFriendlyCardGotPower?.RaiseOpponent();
				GameEventStorage.me?.onEnemyCardGotPower?.RaiseOwner();
			}
		}

		// Snapshot display state before mutating so card text updates are deferred until animation completes
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder != null && RecorderAnimationPlayer.me != null)
		{
			targetCardScript.SnapshotDisplayState();
		}

		for (int i = 0; i < amount; i++)
		{
			targetCardScript.myStatusEffects.Add(effect);
		}

		// Create status effect resolvers
		if (resolverPrefab != null)
		{
			for (int i = 0; i < resolverCount; i++)
			{
				var tagResolver = Instantiate(resolverPrefab, targetCardScript.transform);
				GameEventStorage.me?.onThisTagResolverAttached?.RaiseSpecific(tagResolver);
			}
		}

		// Capture status effect change into AnimationRequest when recorder system is available
		recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder != null)
		{
			TestManager.Log("[ApplyStatusEffectCore] Capturing StatusEffectChange for " + targetCardScript?.name + ". Recorder has " + recorder.animationRequests.Count + " requests before add.");
			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.StatusEffectChange,
				targetCard = targetCardScript.gameObject,
				statusEffect = effect,
				statusEffectAmount = amount,
				statusEffectParticlePrefab = particlePrefab,
				statusEffectParticleYOffset = particleYOffset
			});
		}

		if (!suppressLog)
		{
			LogStatusEffectGiven(targetCardScript, effect, actualLogAmount);
		}
	}

	/// <summary>
	/// Append a line to the combat log. Logic layer should use this instead of writing to effectResultString directly.
	/// </summary>
	protected void AppendLog(string text)
	{
		CombatLog.me?.Append(text);
	}

	protected void LogStatusEffectGiven(CardScript targetCardScript, EnumStorage.StatusEffect effect, int amount)
	{
		string targetCardOwnerString = GetCardOwnerPrefix(targetCardScript.myStatusRef);
		string targetCardColor = GetCardOwnerColor(targetCardScript.myStatusRef);
		string thisCardOwnerString = GetMyCardOwnerPrefix();
		string thisCardColor = GetMyCardOwnerColor();
		string effectNameCN = effect switch
		{
			EnumStorage.StatusEffect.Infected => "感染",
			EnumStorage.StatusEffect.Mana => "法力",
			EnumStorage.StatusEffect.HeartChanged => "心变",
			EnumStorage.StatusEffect.Power => "力量",
			EnumStorage.StatusEffect.Rest => "休息",
			EnumStorage.StatusEffect.Revive => "复活",
			EnumStorage.StatusEffect.Counter => "计数",
			_ => effect.ToString()
		};
		AppendLog(
			"// " + thisCardOwnerString +
			"<color=" + thisCardColor + ">" + myCard.name + "</color>]给予" +
			targetCardOwnerString +
			"<color=" + targetCardColor + ">" + targetCardScript.gameObject.name + "</color>]" +
			"<color=yellow>" + amount + "</color>层[" + effectNameCN + "]");
	}
	#endregion

	#region Helper Methods - Visuals
	protected void TriggerTintForStatusEffect(CardScript targetCard, EnumStorage.StatusEffect effect)
	{
		if (effect != EnumStorage.StatusEffect.Infected && effect != EnumStorage.StatusEffect.Power) return;
		CombatManager.Me?.visuals?.ApplyStatusTint(targetCard, effect);
	}

	/// <summary>
	/// Capture a status effect change (give or consume) into the current EffectRecorder as an AnimationRequest.
	/// </summary>
	protected void CaptureStatusEffectChangeAnimationRequest(GameObject targetCard, EnumStorage.StatusEffect effect, int amount)
	{
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder != null && RecorderAnimationPlayer.me != null)
		{
			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.StatusEffectChange,
				targetCard = targetCard,
				statusEffect = effect,
				statusEffectAmount = amount
			});
		}
		else
		{
			var targetCardScript = targetCard.GetComponent<CardScript>();
			if (targetCardScript != null)
			{
				TriggerTintForStatusEffect(targetCardScript, effect);
			}
		}
	}

	/// <summary>
	/// Capture PopUp -> StatusEffectChange -> SlotIn sequence for status effect consumption.
	/// Used when the target card is in the deck and should visibly pop up during the effect.
	/// </summary>
	protected void CapturePopUpStatusEffectChangeSlotIn(GameObject targetCard, EnumStorage.StatusEffect effect, int amount)
	{
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder == null) return;

		// 1. Pop Up
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.PopUp,
			targetCard = targetCard
		});

		// 2. Status Effect Change (tint + particles)
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.StatusEffectChange,
			targetCard = targetCard,
			statusEffect = effect,
			statusEffectAmount = amount
		});

		// 3. Slot In
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.SlotIn,
			targetCard = targetCard
		});
	}
	#endregion


	// VISUAL-FIX(2026-06-13): ConsumeRandomEnemyCardsStatusEffect has no projectile and queues PopUp/SlotIn per target
	//   Cause:    ConsumeRandomEnemyCardsStatusEffect called CapturePopUpStatusEffectChangeSlotIn once per target,
	//             so multiple targets played PopUp/SlotIn sequentially with no StatusEffectProjectile,
	//             and projectiles flew source->target instead of target->source for an absorb effect.
	//   Affects:  ConsumeStatusEffect, EffectScript, RecorderAnimationPlayer, CombatUXManager
	//   Regress:  Reveal POWER_TRANSFER with multiple enemy cards carrying Power
	//             Check: all target cards pop up together, projectiles fly from each target back to source in parallel,
	//             status text updates after projectiles land, then all targets slot back in together.
	protected void CaptureBatchStatusEffectConsumeAnimation(
		GameObject sourceCard,
		List<CardScript> targetCards,
		EnumStorage.StatusEffect effect,
		int amountPerTarget)
	{
		var amounts = new List<int>();
		if (targetCards != null)
		{
			for (int i = 0; i < targetCards.Count; i++) amounts.Add(amountPerTarget);
		}
		CaptureBatchStatusEffectConsumeAnimation(sourceCard, targetCards, effect, amounts, null);
	}

	// VISUAL-FIX(2026-06-13): ConsumeHostileCursePower needs batch consume animation with per-target amounts
	//   Cause:    ConsumeHostileCursePower can consume a variable number of Power layers from each
	//             enemy curse card and the absorbed power should fly toward statusEffectConsumePos,
	//             but no batch animation helper supported per-target counts or a custom end position.
	//   Affects:  CurseEffect, EffectScript, RecorderAnimationPlayer, CombatUXManager, AnimationRequest
	//   Regress:  Reveal CURSE_SUMMONER or PREMATURE when multiple enemy curse cards carry Power
	//             Check: all target curse cards pop up together, projectiles fly from each target
	//             toward statusEffectConsumePos with one projectile per consumed layer, status text
	//             updates after projectiles land, then all targets slot back in together.
	protected void CaptureBatchStatusEffectConsumeAnimation(
		GameObject sourceCard,
		List<CardScript> targetCards,
		EnumStorage.StatusEffect effect,
		List<int> amountsPerTarget,
		Vector3? customProjectileEndPosition = null)
	{
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder == null || targetCards == null || targetCards.Count == 0) return;

		// Build filtered lists so targets with zero consumed amount do not animate.
		var targetGameObjects = new List<GameObject>();
		var filteredAmounts = new List<int>();
		for (int i = 0; i < targetCards.Count; i++)
		{
			var target = targetCards[i];
			if (target == null) continue;
			int amount = (amountsPerTarget != null && i < amountsPerTarget.Count) ? amountsPerTarget[i] : 0;
			if (amount <= 0) continue;
			targetGameObjects.Add(target.gameObject);
			filteredAmounts.Add(amount);
		}
		if (targetGameObjects.Count == 0) return;

		// 1. Status Effect Change (RecorderAnimationPlayer will defer commit until projectile lands)
		for (int i = 0; i < targetGameObjects.Count; i++)
		{
			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.StatusEffectChange,
				targetCard = targetGameObjects[i],
				statusEffect = effect,
				statusEffectAmount = -filteredAmounts[i]
			});
		}

		// 2. Pop Up all targets in parallel
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.PopUpBatch,
			targetCards = targetGameObjects
		});

		// 3. Projectile flies from all targets back to source/custom position in parallel (absorb)
		int maxProjectileCount = 1;
		foreach (var amount in filteredAmounts)
		{
			if (amount > maxProjectileCount) maxProjectileCount = amount;
		}

		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.StatusEffectProjectile,
			attackerCard = sourceCard,
			targetCards = targetGameObjects,
			projectileCount = maxProjectileCount,
			projectileCountsPerTarget = filteredAmounts,
			reverseProjectile = true,
			customProjectileEndPosition = customProjectileEndPosition
		});

		// 4. Slot In all targets in parallel
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.SlotInBatch,
			targetCards = targetGameObjects
		});
	}

	// VISUAL-FIX(2026-06-13): TransferStatusEffectEffect has no PopUp/SlotIn/Projectile animation
	//   Cause:    TransferAllStatusEffectToHostileCurse and TransferOneStatusEffectToSelf
	//             only captured StatusEffectChange, giving no visible feedback that status
	//             effects were moved from source cards to a target card.
	//   Affects:  TransferStatusEffectEffect, EffectScript, RecorderAnimationPlayer, CombatUXManager
	//   Regress:  Reveal CROW_CROWD (transfer all Power to hostile curse) or POWER_SIPHONER
	//             (transfer 1 Power from each source to self). Check: source cards pop up together,
	//             projectiles fly from each source to the target card, status text updates after
	//             projectiles land, then all cards slot back in together.
	protected void CaptureBatchStatusEffectTransferAnimation(
		List<CardScript> sourceCards,
		CardScript targetCard,
		EnumStorage.StatusEffect effect,
		List<int> amountsPerSource)
	{
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder == null || sourceCards == null || sourceCards.Count == 0 || targetCard == null) return;

		// Build filtered lists so sources with zero transferred amount do not animate.
		var sourceGameObjects = new List<GameObject>();
		var filteredAmounts = new List<int>();
		for (int i = 0; i < sourceCards.Count; i++)
		{
			var source = sourceCards[i];
			if (source == null) continue;
			int amount = (amountsPerSource != null && i < amountsPerSource.Count) ? amountsPerSource[i] : 0;
			if (amount <= 0) continue;
			sourceGameObjects.Add(source.gameObject);
			filteredAmounts.Add(amount);
		}
		if (sourceGameObjects.Count == 0) return;

		// 1. Source cards pop up in parallel
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.PopUpBatch,
			targetCards = sourceGameObjects
		});

		// 2. Target card pops up so the player sees where the status effects are going
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.PopUp,
			targetCard = targetCard.gameObject
		});

		// 3. Projectiles fly from all source cards to the target card in parallel.
		//    We use reverseProjectile semantics: giverCard = target, targetCards = sources,
		//    so projectiles fly from each source back to the target (the actual receiver).
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.StatusEffectProjectile,
			attackerCards = sourceGameObjects,
			targetCard = targetCard.gameObject,
			projectileCountsPerTarget = filteredAmounts,
			reverseProjectile = true
		});

		// 4. Source cards slot back in in parallel
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.SlotInBatch,
			targetCards = sourceGameObjects
		});

		// 5. Target card slots back in
		recorder.animationRequests.Add(new AnimationRequest
		{
			type = AnimationRequestType.SlotIn,
			targetCard = targetCard.gameObject
		});

		// 6. Status effect change on source cards (executed after projectiles/slot-in so text
		//    updates only when the visual transfer completes).
		for (int i = 0; i < sourceGameObjects.Count; i++)
		{
			recorder.animationRequests.Add(new AnimationRequest
			{
				type = AnimationRequestType.StatusEffectChange,
				targetCard = sourceGameObjects[i],
				statusEffect = effect,
				statusEffectAmount = -filteredAmounts[i]
			});
		}
	}

}
