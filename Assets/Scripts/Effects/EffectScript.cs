using System.Collections.Generic;
using DefaultNamespace.SOScripts;
using UnityEngine;

public class EffectScript : MonoBehaviour
{
	[Tooltip("Deprecated: use AppendLog() instead. Kept for backward compatibility with existing prefabs.")]
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
			CombatManager.Me?.visuals?.PlayStatusEffectParticle(targetCardScript, particlePrefab, particleYOffset, amount);
		}

		TriggerTintForStatusEffect(targetCardScript, effect);

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
		if (CombatLog.me != null)
		{
			CombatLog.me.Append(text);
		}
		else if (effectResultString != null)
		{
			// Fallback for backward compatibility if CombatLog is not available
			effectResultString.value += text;
		}
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
			EnumStorage.StatusEffect.Counter => "反击",
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
	#endregion
}
