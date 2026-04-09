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
			var cardInstance = Instantiate(cardToAdd,
				belongsToSessionOwner ? _combatManager.playerDeckParent.transform : _combatManager.enemyDeckParent.transform); // instantiate and assign corresponding parent
			cardInstance.GetComponent<CardScript>().myStatusRef = 
				belongsToSessionOwner ? _combatManager.ownerPlayerStatusRef : _combatManager.enemyPlayerStatusRef; // assign corresponding target
			cardInstance.GetComponent<CardScript>().theirStatusRef = 
				belongsToSessionOwner ? _combatManager.enemyPlayerStatusRef : _combatManager.ownerPlayerStatusRef; // assign corresponding target
			cardInstance.name = cardToAdd.name.Replace("(Clone)", "");
			_combatManager.combinedDeckZone.Insert(0, cardInstance); // add the new card to combined deck (insert at first position)
			
			// 触发友方minion添加事件（只有己方添加时才触发）
			var cardScript = cardInstance.GetComponent<CardScript>();
			if (belongsToSessionOwner && cardScript != null && cardScript.isMinion)
			{
				GameEventStorage.me.onFriendlyMinionAdded?.RaiseOwner();
			}
			
			// 创建对应的物理卡片并插入到 deck 顶部
			CombatUXManager.me.AddPhysicalCardToDeck(cardInstance);
		}

		public GameObject AddCard_TargetSpecific(GameObject cardToAdd, PlayerStatusSO targetPlayerStatus)
		{
			GameObject cardInst;
			if (targetPlayerStatus == _combatManager.ownerPlayerStatusRef) // card belongs to player
			{
				cardInst = Instantiate(cardToAdd, _combatManager.playerDeckParent.transform);
				cardInst.GetComponent<CardScript>().myStatusRef = _combatManager.ownerPlayerStatusRef;
				cardInst.GetComponent<CardScript>().theirStatusRef = _combatManager.enemyPlayerStatusRef;
				cardInst.name = cardInst.name.Replace("(Clone)", "");
				_combatManager.combinedDeckZone.Insert(0, cardInst); // add the new card to combined deck (insert at first position)
			}
			else // card belongs to enemy
			{
				cardInst = Instantiate(cardToAdd, _combatManager.enemyDeckParent.transform);
				cardInst.GetComponent<CardScript>().myStatusRef = _combatManager.enemyPlayerStatusRef;
				cardInst.GetComponent<CardScript>().theirStatusRef = _combatManager.ownerPlayerStatusRef;
				cardInst.name = cardInst.name.Replace("(Clone)", "");
				_combatManager.combinedDeckZone.Insert(0, cardInst); // add the new card to combined deck (insert at first position)
			}
			
			// 触发友方minion添加事件（只有己方添加时才触发）
			var addedCardScript = cardInst.GetComponent<CardScript>();
			bool isFriendlyCard = targetPlayerStatus == _combatManager.ownerPlayerStatusRef;
			if (isFriendlyCard && addedCardScript != null && addedCardScript.isMinion)
			{
				GameEventStorage.me.onFriendlyMinionAdded?.RaiseOwner();
			}
			
			// 创建对应的物理卡片并插入到 deck 顶部
			CombatUXManager.me.AddPhysicalCardToDeck(cardInst);
			
			return cardInst;
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
