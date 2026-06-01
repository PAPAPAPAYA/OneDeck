using System;
using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.SOScripts;
using UnityEngine;

// mana: positive tag that can be stacked
public class ManaAlterEffect : StatusEffectGiverEffect
{
	public void ConsumeMana(int amount)
	{
		if (!EnumStorage.DoesListContainAmountOfStatusEffect(myCardScript.myStatusEffects, amount, EnumStorage.StatusEffect.Mana)) return;
		var amountRemoved = 0;
		// Snapshot display state before mutating so card text updates are deferred until animation completes
		var recorderGo = EffectChainManager.Me != null ? EffectChainManager.Me.currentEffectRecorder : null;
		var recorder = recorderGo != null ? recorderGo.GetComponent<EffectRecorder>() : null;
		if (recorder != null && RecorderAnimationPlayer.me != null)
		{
			myCardScript.SnapshotDisplayState();
		}
		for (var i = myCardScript.myStatusEffects.Count - 1; i >= 0; i--)
		{
			if (myCardScript.myStatusEffects[i] == EnumStorage.StatusEffect.Mana && amountRemoved < amount)
			{
				myCardScript.myStatusEffects.RemoveAt(i);
				amountRemoved++;
			}
		}
		CaptureStatusEffectChangeAnimationRequest(myCardScript.gameObject, EnumStorage.StatusEffect.Mana, -amountRemoved);
	}
}
