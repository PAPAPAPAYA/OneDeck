// Batch Card Prefab Inspector for Card Design Documentation
//
// Usage:
// 1. Populate the `paths` array with all target prefab asset paths.
// 2. Run via execute_code (compiler: "codedom").
// 3. Output is written to `docs/CardDesign_GenerationLog.txt` (overwrites old file).
// 4. Read the log file and parse lines starting with "CARD|".
//
// Constraint: codedom (C# 6) - no $"", no ?., no file-level using, must return value.

{
	string[] paths = new string[] {
		// Example paths - replace with actual discovered prefabs
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/ALMIGHTY.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Curse/JU_ON.prefab"
	};

	var logLines = new System.Collections.Generic.List<string>();

	foreach (var path in paths)
	{
		var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
		if (prefab == null)
		{
			logLines.Add("ERROR|NOT_FOUND|" + path);
			continue;
		}

		var cardScript = prefab.GetComponent<CardScript>();
		if (cardScript == null)
		{
			logLines.Add("ERROR|NO_CARDSCRIPT|" + path);
			continue;
		}

		// Start log with fixed prefix for easy parsing
		string output = "CARD|" + prefab.name + "|" + path + "|";

		// CardScript fields
		output += "cardTypeID=" + cardScript.cardTypeID + ";";
		output += "displayName=" + cardScript.displayName + ";";
		output += "cardDesc=" + cardScript.cardDesc.Replace("\n", "\\n") + ";";
		output += "isMinion=" + cardScript.isMinion + ";";
		output += "buryCost=" + cardScript.buryCost + ";";
		output += "delayCost=" + cardScript.delayCost + ";";
		output += "exposeCost=" + cardScript.exposeCost + ";";
		output += "minionCostCount=" + cardScript.minionCostCount + ";";
		output += "minionCostCardTypeID=" + cardScript.minionCostCardTypeID + ";";
		output += "minionCostOwner=" + cardScript.minionCostOwner + ";";

		// Status Effects list
		string statusEffects = "";
		if (cardScript.myStatusEffects != null)
		{
			for (int i = 0; i < cardScript.myStatusEffects.Count; i++)
			{
				if (i > 0) statusEffects += ",";
				statusEffects += cardScript.myStatusEffects[i].ToString();
			}
		}
		output += "statusEffects=" + statusEffects + ";";

		// Tags list
		string tags = "";
		if (cardScript.myTags != null)
		{
			for (int i = 0; i < cardScript.myTags.Count; i++)
			{
				if (i > 0) tags += ",";
				tags += cardScript.myTags[i].ToString();
			}
		}
		output += "tags=" + tags + ";";

		// Build listener map: CostNEffectContainer -> trigger event name
		var listenerMap = new System.Collections.Generic.Dictionary<CostNEffectContainer, string>();
		var listeners = prefab.GetComponents<DefaultNamespace.GameEventListener>();
		for (int l = 0; l < listeners.Length; l++)
		{
			var listener = listeners[l];
			if (listener == null) continue;
			var listenerSo = new UnityEditor.SerializedObject(listener);
			var eventProp = listenerSo.FindProperty("event");
			string eventName = "";
			if (eventProp != null && eventProp.objectReferenceValue != null)
			{
				eventName = eventProp.objectReferenceValue.name;
			}

			var responseProp = listenerSo.FindProperty("response");
			if (responseProp != null)
			{
				var calls = responseProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
				if (calls != null)
				{
					for (int i = 0; i < calls.arraySize; i++)
					{
						var call = calls.GetArrayElementAtIndex(i);
						var targetProp = call.FindPropertyRelative("m_Target");
						if (targetProp != null && targetProp.objectReferenceValue != null)
						{
							var targetContainer = targetProp.objectReferenceValue as CostNEffectContainer;
							if (targetContainer != null)
							{
								listenerMap[targetContainer] = eventName;
							}
						}
					}
				}
			}
		}

		// CostNEffectContainers
		var containers = prefab.GetComponentsInChildren<CostNEffectContainer>();
		output += "containers=" + containers.Length + ";";

		for (int c = 0; c < containers.Length; c++)
		{
			var container = containers[c];
			output += "[CONTAINER_" + c + " name=" + container.name + "]";

			// Trigger event from GameEventListener mapping (e.g. [TRIGGER_onMeRevealed])
			if (listenerMap.ContainsKey(container))
			{
				output += "[TRIGGER_" + listenerMap[container] + "]";
			}
			else
			{
				output += "[TRIGGER_NONE]";
			}

			var so = new UnityEditor.SerializedObject(container);

			// checkCostEvent
			var checkCostProp = so.FindProperty("checkCostEvent");
			if (checkCostProp != null)
			{
				var calls = checkCostProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
				if (calls != null)
				{
					for (int i = 0; i < calls.arraySize; i++)
					{
						var call = calls.GetArrayElementAtIndex(i);
						string targetType = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
						string methodName = call.FindPropertyRelative("m_MethodName").stringValue;
						var args = call.FindPropertyRelative("m_Arguments");
						int intArg = args.FindPropertyRelative("m_IntArgument").intValue;
						string stringArg = args.FindPropertyRelative("m_StringArgument").stringValue;
						output += "[CHECK_" + targetType + "->" + methodName + "(" + intArg + "," + stringArg + ")]";
					}
				}
			}

			// preEffectEvent
			var preEffectProp = so.FindProperty("preEffectEvent");
			if (preEffectProp != null)
			{
				var calls = preEffectProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
				if (calls != null)
				{
					for (int i = 0; i < calls.arraySize; i++)
					{
						var call = calls.GetArrayElementAtIndex(i);
						string targetType = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
						string methodName = call.FindPropertyRelative("m_MethodName").stringValue;
						var args = call.FindPropertyRelative("m_Arguments");
						int intArg = args.FindPropertyRelative("m_IntArgument").intValue;
						string stringArg = args.FindPropertyRelative("m_StringArgument").stringValue;
						output += "[PRE_" + targetType + "->" + methodName + "(" + intArg + "," + stringArg + ")]";
					}
				}
			}

			// effectEvent
			var effectEventProp = so.FindProperty("effectEvent");
			if (effectEventProp != null)
			{
				var calls = effectEventProp.FindPropertyRelative("m_PersistentCalls.m_Calls");
				if (calls != null)
				{
					for (int i = 0; i < calls.arraySize; i++)
					{
						var call = calls.GetArrayElementAtIndex(i);
						string targetType = call.FindPropertyRelative("m_TargetAssemblyTypeName").stringValue;
						string methodName = call.FindPropertyRelative("m_MethodName").stringValue;
						var args = call.FindPropertyRelative("m_Arguments");
						int intArg = args.FindPropertyRelative("m_IntArgument").intValue;
						string stringArg = args.FindPropertyRelative("m_StringArgument").stringValue;
						output += "[EFFECT_" + targetType + "->" + methodName + "(" + intArg + "," + stringArg + ")]";
					}
				}
			}
		}

		// Effect component fields on children
		foreach (UnityEngine.Transform child in prefab.transform)
		{
			string safeName = safeName.Replace("[", "(").Replace("]", ")");
			var hpAlter = child.GetComponent<HPAlterEffect>();
			if (hpAlter != null)
			{
				output += "[HPALTER_" + safeName + " baseDmg=" + (hpAlter.baseDmg != null ? hpAlter.baseDmg.value.ToString() : "0") + " isStatusEffectDamage=" + hpAlter.isStatusEffectDamage + " extraDmg=" + hpAlter.extraDmg + " statusEffectToCheck=" + hpAlter.statusEffectToCheck + "]";
			}

			var shieldAlter = child.GetComponent<DefaultNamespace.Effects.ShieldAlterEffect>();
			if (shieldAlter != null)
			{
				output += "[SHIELD_" + safeName + "]";
			}

			var addTemp = child.GetComponent<DefaultNamespace.Effects.AddTempCard>();
			if (addTemp != null)
			{
				output += "[ADDTEMP_" + safeName + " cardCount=" + addTemp.cardCount + " curseCardTypeID=" + (addTemp.curseCardTypeID != null ? addTemp.curseCardTypeID.value : "") + "]";
			}

			var curse = child.GetComponent<DefaultNamespace.Effects.CurseEffect>();
			if (curse != null)
			{
				output += "[CURSE_" + safeName + " cardTypeID=" + (curse.cardTypeID != null ? curse.cardTypeID.value : "") + " cardPrefab=" + (curse.cardPrefab != null ? curse.cardPrefab.name : "") + " powerCoefficient=" + curse.powerCoefficient + "]";
			}

			var exile = child.GetComponent<ExileEffect>();
			if (exile != null)
			{
				output += "[EXILE_" + safeName + " tagToCheck=" + exile.tagToCheck + "]";
			}

			var bury = child.GetComponent<BuryEffect>();
			if (bury != null)
			{
				output += "[BURY_" + safeName + " tagToCheck=" + bury.tagToCheck + "]";
			}

			var stage = child.GetComponent<StageEffect>();
			if (stage != null)
			{
				output += "[STAGE_" + safeName + " tagToCheck=" + stage.tagToCheck + " targetFriendly=" + stage.targetFriendly + " statusEffectToCheck=" + stage.statusEffectToCheck + "]";
			}

			var cardManip = child.GetComponent<CardManipulationEffect>();
			if (cardManip != null)
			{
				output += "[MANIP_" + safeName + " tagToCheck=" + cardManip.tagToCheck + "]";
			}

			var transfer = child.GetComponent<DefaultNamespace.Effects.TransferStatusEffectEffect>();
			if (transfer != null)
			{
				output += "[TRANSFER_" + safeName + " isFromFriendly=" + transfer.isFromFriendly + " statusEffectToTransfer=" + transfer.statusEffectToTransfer + " curseCardTypeID=" + (transfer.curseCardTypeID != null ? transfer.curseCardTypeID.value : "") + "]";
			}

			var changeTarget = child.GetComponent<DefaultNamespace.Effects.ChangeCardTarget>();
			if (changeTarget != null)
			{
				output += "[CHANGETARGET_" + safeName + "]";
			}

			var changeHpAlter = child.GetComponent<DefaultNamespace.Effects.ChangeHpAlterAmountEffect>();
			if (changeHpAlter != null)
			{
				output += "[CHANGEHPALTER_" + safeName + "]";
			}

			var hpMaxAlter = child.GetComponent<HPMaxAlterEffect>();
			if (hpMaxAlter != null)
			{
				output += "[HPMAXALTER_" + safeName + "]";
			}

			// StatusEffectAmplifierEffect (must be checked before StatusEffectGiverEffect because it inherits from it)
			var amplifier = child.GetComponent<DefaultNamespace.Effects.StatusEffectAmplifierEffect>();
			if (amplifier != null)
			{
				output += "[AMPLIFIER_" + safeName + " statusEffectToGive=" + amplifier.statusEffectToGive + " statusEffectToCount=" + amplifier.statusEffectToCount + " statusEffectMultiplier=" + amplifier.statusEffectMultiplier + " target=" + amplifier.target + " includeSelf=" + amplifier.includeSelf + " lastXCardsCount=" + amplifier.lastXCardsCount + " xFriendlyCount=" + amplifier.xFriendlyCount + " statusEffectLayerCount=" + amplifier.statusEffectLayerCount + " yFriendlyLayerCount=" + amplifier.yFriendlyLayerCount + "]";
			}
			else
			{
				// PowerReactionEffect (must be checked before StatusEffectGiverEffect because it inherits from it)
				var powerReaction = child.GetComponent<DefaultNamespace.Effects.PowerReactionEffect>();
				if (powerReaction != null)
				{
					output += "[POWERREACTION_" + safeName + " powerAmount=" + powerReaction.powerAmount + " excludeSelf=" + powerReaction.excludeSelf + " statusEffectToGive=" + powerReaction.statusEffectToGive + " statusEffectToCount=" + powerReaction.statusEffectToCount + " target=" + powerReaction.target + " includeSelf=" + powerReaction.includeSelf + " lastXCardsCount=" + powerReaction.lastXCardsCount + " xFriendlyCount=" + powerReaction.xFriendlyCount + " statusEffectLayerCount=" + powerReaction.statusEffectLayerCount + " yFriendlyLayerCount=" + powerReaction.yFriendlyLayerCount + "]";
				}
				else
				{
					// StatusEffectGiverEffect (only if not an amplifier or power reaction)
					var statusGiver = child.GetComponent<DefaultNamespace.Effects.StatusEffectGiverEffect>();
					if (statusGiver != null)
					{
						output += "[GIVER_" + safeName + " statusEffectToGive=" + statusGiver.statusEffectToGive + " statusEffectToCount=" + statusGiver.statusEffectToCount + " target=" + statusGiver.target + " includeSelf=" + statusGiver.includeSelf + " lastXCardsCount=" + statusGiver.lastXCardsCount + " xFriendlyCount=" + statusGiver.xFriendlyCount + " statusEffectLayerCount=" + statusGiver.statusEffectLayerCount + " yFriendlyLayerCount=" + statusGiver.yFriendlyLayerCount + "]";
					}
				}
			}

			// ConsumeStatusEffect
			var consumer = child.GetComponent<DefaultNamespace.Effects.ConsumeStatusEffect>();
			if (consumer != null)
			{
				output += "[CONSUMER_" + safeName + " statusEffectToConsume=" + consumer.statusEffectToConsume + "]";
			}
		}

		logLines.Add(output);
	}

	// Write log file to docs/CardDesign_GenerationLog.txt (overwrites old file)
	string projectRoot = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
	string logDir = System.IO.Path.Combine(projectRoot, "docs");
	if (!System.IO.Directory.Exists(logDir))
	{
		System.IO.Directory.CreateDirectory(logDir);
	}
	string logPath = System.IO.Path.Combine(logDir, "CardDesign_GenerationLog.txt");
	System.IO.File.WriteAllLines(logPath, logLines.ToArray());
	UnityEngine.Debug.Log("Card design log written to: " + logPath + " (lines=" + logLines.Count + ")");

	return 0;
}
