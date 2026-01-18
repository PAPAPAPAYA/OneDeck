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
        
        public virtual void GiveTagToRandom(int amount)
        {
            if (tagToGive == EnumStorage.Tag.None) return;
            var cardsToGiveTag = new List<GameObject>();
            UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToGiveTag, true);
            UtilityFuncManagerScript.CopyGameObjectList(combatManager.graveZone, cardsToGiveTag, false);
            cardsToGiveTag = UtilityFuncManagerScript.ShuffleList(cardsToGiveTag);
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
            amount = Mathf.Clamp(amount, 0, cardsToGiveTag.Count);
            for (var i = 0; i < amount; i++)
            {
                var targetCardScript = cardsToGiveTag[i].GetComponent<CardScript>();
                targetCardScript.myTags.Add(tagToGive);
                var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
                effectResultString.value +=
                    "// [" + myCardScript.cardName + "] gave " +
                    targetCardOwnerString +
                    targetCardScript.cardName + "] " +
                    "1 [" + tagToGive + "]\n";
                if (myTagResolver == null) continue;
                var tagResolver = Instantiate(myTagResolver, targetCardScript.transform);
                GameEventStorage.me.onThisTagResolverAttached.RaiseSpecific(tagResolver);
            }
            CombatInfoDisplayer.me.RefreshDeckInfo();
        }
    }
}