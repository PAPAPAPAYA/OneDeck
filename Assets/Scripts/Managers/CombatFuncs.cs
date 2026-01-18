using System;
using System.Collections.Generic;
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

		public void AddCard_TargetSpecific(GameObject cardToAdd, PlayerStatusSO targetPlayerStatus)
		{
			if (targetPlayerStatus == _combatManager.ownerPlayerStatusRef) // card belongs to player
			{
				var cardInst = Instantiate(cardToAdd, _combatManager.playerDeckParent.transform);
				cardInst.GetComponent<CardScript>().myStatusRef = _combatManager.ownerPlayerStatusRef;
				cardInst.GetComponent<CardScript>().theirStatusRef = _combatManager.enemyPlayerStatusRef;
				_combatManager.combinedDeckZone.Add(cardInst); // add the new card to combined deck
				_combatManager.UpdateTrackingVariables();
			}
			else // card belongs to enemy
			{
				var cardInst = Instantiate(cardToAdd, _combatManager.enemyDeckParent.transform);
				cardInst.GetComponent<CardScript>().myStatusRef = _combatManager.enemyPlayerStatusRef;
				cardInst.GetComponent<CardScript>().theirStatusRef = _combatManager.ownerPlayerStatusRef;
				_combatManager.combinedDeckZone.Add(cardInst); // add the new card to combined deck
				_combatManager.UpdateTrackingVariables();
			}
			
		}
		
		public List<CardScript> ReturnPlayerCardScripts()
		{
			var returnedCardInstances = new List<CardScript>();
			foreach (var cardInst in _combatManager.playerDeckParent.GetComponentsInChildren<CardScript>())
			{
				returnedCardInstances.Add(cardInst);
			}
			return returnedCardInstances;
		}

		public List<CardScript> ReturnEnemyCardScripts()
		{
			var returnedCardInstances = new List<CardScript>();
			foreach (var cardInst in _combatManager.enemyDeckParent.GetComponentsInChildren<CardScript>())
			{
				returnedCardInstances.Add(cardInst);
			}
			return returnedCardInstances;
		}
	}
}