using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Effects
{
	public class HeartChangeEffect : TagGiverEffect
	{
		public override void GiveTagToRandom(int amount)
		{
			base.GiveTagToRandom(amount);
			ProcessCardsWithHeartChangeTag();
		}

		private void ProcessCardsWithHeartChangeTag()
		{
			var heartChangedCards = new List<GameObject>();
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.combinedDeckZone, heartChangedCards, true);
			UtilityFuncManagerScript.CopyGameObjectList(combatManager.graveZone, heartChangedCards, false);
			foreach (var card in heartChangedCards)
			{
				var cardScript = card.GetComponent<CardScript>();
				if (!cardScript.myTags.Contains(EnumStorage.Tag.HeartChanged)) continue;
				foreach (var cardTag in cardScript.myTags)
				{
					if (cardTag == EnumStorage.Tag.HeartChanged)
					{
						ChangeCardTarget(cardScript);
					}
				}
			}
		}

		private void ChangeCardTarget(CardScript targetCardScript)
		{
			targetCardScript.myStatusRef = 
				targetCardScript.myStatusRef == combatManager.ownerPlayerStatusRef ? 
					combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
			targetCardScript.theirStatusRef = 
				targetCardScript.theirStatusRef == combatManager.ownerPlayerStatusRef ? 
					combatManager.enemyPlayerStatusRef : combatManager.ownerPlayerStatusRef;
		}
	}
}