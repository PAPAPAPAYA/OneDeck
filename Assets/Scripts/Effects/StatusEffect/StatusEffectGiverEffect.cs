using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class StatusEffectGiverEffect : EffectScript
	{
		[Header("Status Effect Related Refs")]
		public GameObject myStatusEffectResolverScript;
		public bool canStatusEffectBeStacked = false;
		[Tooltip("if this is none, then won't run give status effect")]
		public EnumStorage.StatusEffect statusEffectToGive;
		public bool spreadEvenly = false;
		[Tooltip("only applies to GiveStatusEffect(): whose cards the status effect will be given to")]
		public EnumStorage.TargetType target; // whose cards the status effect will be given to

		public virtual void GiveSelfStatusEffect(int amount) // give self status effect, default to stack
		{
			for (int i = 0; i < amount; i++)
			{
				// give status effect
				myCardScript.myStatusEffects.Add(statusEffectToGive);
				// display effect info
				var thisCardOwnerString = CombatInfoDisplayer.me.ReturnCardOwnerInfo(myCardScript.myStatusRef);
				effectResultString.value +=
					"// " + thisCardOwnerString + // tag giver owner card
					" [" + myCard.name + "] gave" + // tag giver card name 
					" it" + // status effect receiver card
					" 1 [" + statusEffectToGive + "]\n"; // status effect
				// make statue effect resolver
				if (myStatusEffectResolverScript == null) continue;
				var tagResolver = Instantiate(myStatusEffectResolverScript, myCard.transform);
				GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
			}
		}
		
		public virtual void GiveStatusEffect(int amount) // give card owner's or opponent's or full random cards status effect
		{
			if (statusEffectToGive == EnumStorage.StatusEffect.None) return;
			var cardsToGiveTag = new List<GameObject>();
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToGiveTag, true);
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.graveZone, cardsToGiveTag, false);
			cardsToGiveTag = UtilityFuncManagerScript.ShuffleList(cardsToGiveTag);
			for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
			{
				if (cardsToGiveTag[i].GetComponent<CardScript>().myStatusRef != myCardScript.myStatusRef)
				{
					if (target == EnumStorage.TargetType.Me) cardsToGiveTag.RemoveAt(i);
				}
				else
				{
					if (target == EnumStorage.TargetType.Them) cardsToGiveTag.RemoveAt(i);
				}
			}
			if (!canStatusEffectBeStacked)
			{
				for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
				{
					if (cardsToGiveTag[i].GetComponent<CardScript>().myStatusEffects.Contains(statusEffectToGive))
					{
						cardsToGiveTag.RemoveAt(i);
					}
				}
			}
			if (cardsToGiveTag.Count <= 0) return;
			if (spreadEvenly)
			{
				amount = Mathf.Clamp(amount, 0, cardsToGiveTag.Count);
			}

			for (var i = 0; i < amount; i++)
			{
				CardScript targetCardScript;
				if (spreadEvenly)
				{
					targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
				}
				else
				{
					targetCardScript = cardsToGiveTag[Random.Range(0, cardsToGiveTag.Count)].GetComponent<CardScript>();
				}
				targetCardScript.myStatusEffects.Add(statusEffectToGive);
				var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
				var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
				effectResultString.value +=
					"// " + thisCardOwnerString + // tag giver owner card
					myCard.name + "] gave " + // tag giver card name 
					targetCardOwnerString + // status effect receiver card owner
					targetCardScript.gameObject.name + "] " + // status effect receiver card
					"1 [" + statusEffectToGive + "]\n"; // status effect
				if (myStatusEffectResolverScript == null) continue;
				var tagResolver = Instantiate(myStatusEffectResolverScript, targetCardScript.transform);
				GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
			}
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}
	}
}