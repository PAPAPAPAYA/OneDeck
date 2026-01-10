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
        
        public void GiveTagToRandom(int amount)
        {
            if (tagToGive == EnumStorage.Tag.None) return;
            var cardsToInfect = new List<GameObject>();
            UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, cardsToInfect, true);
            UtilityFuncManagerScript.CopyGameObjectList(combatManager.graveZone, cardsToInfect, false);
            cardsToInfect = UtilityFuncManagerScript.ShuffleList(cardsToInfect);
            if (!canTagBeStacked)
            {
                for (var i = cardsToInfect.Count - 1; i >= 0; i--)
                {
                    if (cardsToInfect[i].GetComponent<CardScript>().myTags.Contains(tagToGive))
                    {
                        cardsToInfect.RemoveAt(i);
                    }
                }
            }
            if (cardsToInfect.Count <= 0) return;
            amount = Mathf.Clamp(amount, 0, cardsToInfect.Count);
            for (var i = 0; i < amount; i++)
            {
                var targetCardScript = cardsToInfect[i].GetComponent<CardScript>();
                targetCardScript.myTags.Add(tagToGive);
                var targetCardOwnerString = targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? "Your [" : "Enemy's [";
                effectResultString.value +=
                    "// [" + myCardScript.cardName + "] gave " +
                    targetCardOwnerString +
                    targetCardScript.cardName + "] " +
                    "1 [" + tagToGive + "]\n";
                if (myTagResolver == null) continue;
                Instantiate(myTagResolver, targetCardScript.transform);
            }
        }
    }
}