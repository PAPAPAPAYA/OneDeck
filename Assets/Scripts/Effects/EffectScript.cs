using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class EffectScript : MonoBehaviour
{
	public StringSO effectResultString;
	
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
		return statusRef == combatManager.ownerPlayerStatusRef ? "<color=#87CEEB>Your</color> [" : "<color=orange>Enemy's</color> [";
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

		// Play particle effects
		if (particlePrefab != null)
		{
			for (int i = 0; i < amount; i++)
			{
				Vector3 spawnPosition = GetPhysicalCardWorldPosition(targetCardScript.transform) + Vector3.up * particleYOffset;
				ParticleSystem particleInstance = Instantiate(particlePrefab, spawnPosition, Quaternion.identity, targetCardScript.transform);
				particleInstance.Play();
			}
		}

		TriggerTintForStatusEffect(targetCardScript, effect);

		if (!suppressLog)
		{
			LogStatusEffectGiven(targetCardScript, effect, actualLogAmount);
		}
	}

	protected void LogStatusEffectGiven(CardScript targetCardScript, EnumStorage.StatusEffect effect, int amount)
	{
		string targetCardOwnerString = GetCardOwnerPrefix(targetCardScript.myStatusRef);
		string targetCardColor = GetCardOwnerColor(targetCardScript.myStatusRef);
		string thisCardOwnerString = GetMyCardOwnerPrefix();
		string thisCardColor = GetMyCardOwnerColor();
		effectResultString.value +=
			"// " + thisCardOwnerString +
			"<color=" + thisCardColor + ">" + myCard.name + "</color>] gave " +
			targetCardOwnerString +
			"<color=" + targetCardColor + ">" + targetCardScript.gameObject.name + "</color>] " +
			"<color=yellow>" + amount + "</color> [" + effect + "]\n";
	}
	#endregion

	#region Helper Methods - Visuals
	protected Vector3 GetPhysicalCardWorldPosition(Transform cardTransform)
	{
		if (CombatUXManager.me != null)
		{
			var cardScript = cardTransform.GetComponent<CardScript>();
			if (cardScript != null)
			{
				CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
				var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(cardScript);
				if (physicalCard != null) return physicalCard.transform.position;
			}
		}
		return cardTransform.position;
	}

	protected void TriggerTintForStatusEffect(CardScript targetCard, EnumStorage.StatusEffect effect)
	{
		if (effect != EnumStorage.StatusEffect.Infected && effect != EnumStorage.StatusEffect.Power) return;
		if (CombatUXManager.me == null) return;
		CombatUXManager.me.BuildCardScriptToPhysicalDictionary();
		var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(targetCard);
		if (physicalCard != null)
		{
			var cardPhysObj = physicalCard.GetComponent<CardPhysObjScript>();
			if (cardPhysObj != null) cardPhysObj.TriggerTintForStatusEffect(effect);
		}
	}
	#endregion
}
