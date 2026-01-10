using System;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	public class CombatFuncs : MonoBehaviour
	{
		#region Singleton
		public static CombatFuncs me;

		private void Awake()
		{
			me = this;
		}
		#endregion

		private CombatManager _combatManager;

		private void OnEnable()
		{
			_combatManager = GetComponent<CombatManager>();
		}

		public void MoveCard_FromDeckToGrave(GameObject targetCard)
		{
			_combatManager.combinedDeckZone.Remove(targetCard);
			_combatManager.graveZone.Add(targetCard);
			GameEventStorage.me.onAnyCardSentToGrave.Raise(); // timepoint
			GameEventStorage.me.onMeSentToGrave.RaiseSpecific(targetCard); // timepoint
			_combatManager.UpdateTrackingVariables();
		}

		public void MoveCard_FromGraveToDeck(GameObject targetCard)
		{
			_combatManager.graveZone.Remove(targetCard);
			_combatManager.combinedDeckZone.Add(targetCard);
			_combatManager.Shuffle();
		}

		public void AddCardInTheMiddleOfCombat(GameObject cardToAdd, bool belongsToSessionOwner)
		{
			var cardInstance = Instantiate(cardToAdd,
				belongsToSessionOwner ? _combatManager.playerDeckParent.transform : _combatManager.enemyDeckParent.transform); // instantiate and assign corresponding parent
			cardInstance.GetComponent<CardScript>().myStatusRef = 
				belongsToSessionOwner ? _combatManager.ownerPlayerStatusRef : _combatManager.enemyPlayerStatusRef; // assign corresponding target
			cardInstance.GetComponent<CardScript>().theirStatusRef = 
				belongsToSessionOwner ? _combatManager.enemyPlayerStatusRef : _combatManager.ownerPlayerStatusRef; // assign corresponding target
			_combatManager.combinedDeckZone.Add(cardInstance); // add the new card to combined deck
			_combatManager.UpdateTrackingVariables();
		}
	}
}