using System;
using DefaultNamespace;
using DefaultNamespace.Effects;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Tests for StatusEffectGiverEffect.onlyTargetEnemyDamagingCards and
/// UtilityFuncManagerScript.HasDecreaseTheirHpEffect.
/// </summary>
public class EnemyDamagingTargetFilterTests : HeadlessCombatTestFixture
{
	#region Helpers

	private int CountStatusEffect(GameObject card, EnumStorage.StatusEffect effect)
	{
		int count = 0;
		foreach (var e in card.GetComponent<CardScript>().myStatusEffects)
		{
			if (e == effect) count++;
		}
		return count;
	}

	/// <summary>
	/// Append a persistent void call to a UnityEvent field via the SerializedObject API.
	/// (UnityEventTools.AddPersistentListener is internal and cannot be used here.)
	/// </summary>
	private void AddPersistentVoidCall(Component host, string eventPropertyPath, UnityEngine.Object callTarget, string methodName)
	{
		var so = new SerializedObject(host);
		var calls = so.FindProperty(eventPropertyPath + ".m_PersistentCalls.m_Calls");
		Assert.IsNotNull(calls, "Persistent calls property should exist on " + eventPropertyPath);
		calls.arraySize++;
		var element = calls.GetArrayElementAtIndex(calls.arraySize - 1);
		element.FindPropertyRelative("m_Target").objectReferenceValue = callTarget;
		element.FindPropertyRelative("m_MethodName").stringValue = methodName;
		element.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue = callTarget.GetType().AssemblyQualifiedName;
		element.FindPropertyRelative("m_CallState").intValue = 1; // UnityEventCallState.EditorAndRuntime
		element.FindPropertyRelative("m_Mode").intValue = 1; // PersistentListenerMode.Void
		so.ApplyModifiedPropertiesWithoutUndo();
	}

	/// <summary>
	/// Add an HPAlterEffect to the card and bind one of its methods persistently
	/// via a child CostNEffectContainer.effectEvent (mirrors prefab wiring).
	/// </summary>
	private HPAlterEffect AddBoundHpAlterEffect(GameObject card, string methodName)
	{
		var effectObj = CreateGameObject("HPAlterEffect");
		effectObj.transform.SetParent(card.transform);
		var hpAlter = effectObj.AddComponent<HPAlterEffect>();

		var containerObj = CreateGameObject("CostNEffectContainer");
		containerObj.transform.SetParent(card.transform);
		var container = containerObj.AddComponent<CostNEffectContainer>();
		if (container.effectEvent == null) container.effectEvent = new UnityEvent();
		AddPersistentVoidCall(container, "effectEvent", hpAlter, methodName);
		return hpAlter;
	}

	private void AddDecreaseTheirHpBinding(GameObject card)
	{
		AddBoundHpAlterEffect(card, "DecreaseTheirHp");
	}

	#endregion

	#region HasDecreaseTheirHpEffect Detection

	[Test]
	public void HasDecreaseTheirHpEffect_BoundViaEffectEvent_ReturnsTrue()
	{
		var card = CreateCard(true, "BoundCard");
		AddDecreaseTheirHpBinding(card);

		Assert.IsTrue(UtilityFuncManagerScript.HasDecreaseTheirHpEffect(card),
			"Card with DecreaseTheirHp bound via effectEvent should be detected");
	}

	[Test]
	public void HasDecreaseTheirHpEffect_BoundViaGameEventListener_ReturnsTrue()
	{
		var card = CreateCard(true, "ListenerCard");
		var effectObj = CreateGameObject("HPAlterEffect");
		effectObj.transform.SetParent(card.transform);
		var hpAlter = effectObj.AddComponent<HPAlterEffect>();

		var listenerObj = CreateGameObject("GameEventListener");
		listenerObj.transform.SetParent(card.transform);
		var listener = listenerObj.AddComponent<GameEventListener>();
		AddPersistentVoidCall(listener, "response", hpAlter, "DecreaseTheirHp");

		Assert.IsTrue(UtilityFuncManagerScript.HasDecreaseTheirHpEffect(card),
			"Card with DecreaseTheirHp bound via GameEventListener.response should be detected");
	}

	[Test]
	public void HasDecreaseTheirHpEffect_UnboundHpAlterEffect_ReturnsFalse()
	{
		var card = CreateCard(true, "UnboundCard");
		var effectObj = CreateGameObject("HPAlterEffect");
		effectObj.transform.SetParent(card.transform);
		effectObj.AddComponent<HPAlterEffect>();

		Assert.IsFalse(UtilityFuncManagerScript.HasDecreaseTheirHpEffect(card),
			"Card with an unbound HPAlterEffect should not be detected");
	}

	[Test]
	public void HasDecreaseTheirHpEffect_BoundDecreaseMyHp_ReturnsFalse()
	{
		var card = CreateCard(true, "SelfDamageCard");
		AddBoundHpAlterEffect(card, "DecreaseMyHp");

		Assert.IsFalse(UtilityFuncManagerScript.HasDecreaseTheirHpEffect(card),
			"Card bound to DecreaseMyHp should not count as enemy-damaging");
	}

	#endregion

	#region GiveStatusEffectToLastXCards Filter

	[Test]
	public void GiveStatusEffectToLastXCards_FilterOn_OnlyDamagingCardsReceivePower()
	{
		var giverCard = CreateCard(true, "Giver");
		var nonDamaging = CreateCard(true, "NonDamaging");
		var damaging = CreateCard(true, "Damaging");
		AddDecreaseTheirHpBinding(damaging);
		CombatManager.combinedDeckZone.Add(nonDamaging);
		CombatManager.combinedDeckZone.Add(damaging);
		CombatManager.combinedDeckZone.Add(giverCard);

		var giver = CreateEffect<StatusEffectGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.onlyTargetEnemyDamagingCards = true;
		giver.lastXCardsCount = 5;
		giver.statusEffectLayerCount = 1;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveStatusEffectToLastXCards();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CountStatusEffect(damaging, EnumStorage.StatusEffect.Power),
			"Damaging card should receive Power");
		Assert.AreEqual(0, CountStatusEffect(nonDamaging, EnumStorage.StatusEffect.Power),
			"Non-damaging card should be skipped when the filter is on");
	}

	[Test]
	public void GiveStatusEffectToLastXCards_FilterOff_AllCardsReceivePower()
	{
		var giverCard = CreateCard(true, "Giver");
		var nonDamaging = CreateCard(true, "NonDamaging");
		var damaging = CreateCard(true, "Damaging");
		AddDecreaseTheirHpBinding(damaging);
		CombatManager.combinedDeckZone.Add(nonDamaging);
		CombatManager.combinedDeckZone.Add(damaging);
		CombatManager.combinedDeckZone.Add(giverCard);

		var giver = CreateEffect<StatusEffectGiverEffect>(giverCard);
		giver.statusEffectToGive = EnumStorage.StatusEffect.Power;
		giver.onlyTargetEnemyDamagingCards = false;
		giver.lastXCardsCount = 5;
		giver.statusEffectLayerCount = 1;

		EffectChainManager.MakeANewEffectRecorder(giverCard, giver.gameObject);
		giver.GiveStatusEffectToLastXCards();
		EffectChainManager.Me.CloseOpenedChain();

		Assert.AreEqual(1, CountStatusEffect(damaging, EnumStorage.StatusEffect.Power),
			"Damaging card should receive Power");
		Assert.AreEqual(1, CountStatusEffect(nonDamaging, EnumStorage.StatusEffect.Power),
			"Non-damaging card should also receive Power when the filter is off");
	}

	#endregion
}
