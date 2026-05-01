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

		public void AddCardInTheMiddleOfCombat(GameObject cardToAdd, bool belongsToSessionOwner)
		{
			var factory = CardFactory.me;
			if (factory == null)
			{
				Debug.LogError("[CombatFuncs] CardFactory is not available!");
				return;
			}

			var targetStatus = belongsToSessionOwner ? _combatManager.ownerPlayerStatusRef : _combatManager.enemyPlayerStatusRef;
			factory.SpawnCardForPlayer(cardToAdd, targetStatus, deckIndex: 0, triggerMinionEvent: belongsToSessionOwner);
		}

		public GameObject AddCard_TargetSpecific(GameObject cardToAdd, PlayerStatusSO targetPlayerStatus)
		{
			var factory = CardFactory.me;
			if (factory == null)
			{
				Debug.LogError("[CombatFuncs] CardFactory is not available!");
				return null;
			}

			bool isFriendlyCard = targetPlayerStatus == _combatManager.ownerPlayerStatusRef;
			return factory.SpawnCardForPlayer(cardToAdd, targetPlayerStatus, deckIndex: 0, triggerMinionEvent: isFriendlyCard);
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
