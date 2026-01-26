using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class TagGiverEffect : EffectScript
	{
		[Header("Tag Related Refs")]
		public GameObject myTagResolver;
		public bool canTagBeStacked = false;
		[Tooltip("if this is none, then won't run give tag")]
		public EnumStorage.Tag tagToGive;
		public bool spreadEvenly = false;
		public EnumStorage.TargetType target; // whose cards the status effect will be given to

		public virtual void GiveStatusEffect(int amount)
		{
			if (tagToGive == EnumStorage.Tag.None) return;
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
			if (!canTagBeStacked)
			{
				for (var i = cardsToGiveTag.Count - 1; i >= 0; i--)
				{
					if (cardsToGiveTag[i].GetComponent<CardScript>().myTags.Contains(tagToGive))
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
				targetCardScript.myTags.Add(tagToGive);
				var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
				var thisCardOwnerString = myCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
				effectResultString.value +=
					"// " + thisCardOwnerString + // tag giver owner card
					myCardScript.cardName + "] gave " + // tag giver card name 
					targetCardOwnerString + // status effect receiver card owner
					targetCardScript.cardName + "] " + // status effect receiver card
					"1 [" + tagToGive + "]\n"; // status effect
				if (myTagResolver == null) continue;
				var tagResolver = Instantiate(myTagResolver, targetCardScript.transform);
				GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
			}
			CombatInfoDisplayer.me.RefreshDeckInfo();
		}
	}
}