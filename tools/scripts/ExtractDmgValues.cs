{
	string[] paths = new string[] {
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/0_Common/GRAVE_PUNCH.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/1_Uncommon/CORPSE_CANON.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/1_Uncommon/GRAVE_INVITATION.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/Bury/2_Rare/BODY_CANON.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/0_Common/SOLDIER_SKELETON.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/AVENGER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/CURSED_CORPSE.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/SCAPEGOAT.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/1_Uncommon/SPIKE_SKELETON.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/2_Rare/GRAVE_KEEPER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Bury and buried/DeathRattle/2_Rare/SLIME.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/0_Common/RIFT_INSECT.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/1_Uncommon/RIFT_MONSTER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Conjure/2_Rare/RIFT_DEVOURER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Curse/0_Common/POISONER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/Curse/1_Uncommon/CURSE_THIRST_BEAST.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/0_Common/BLACKSMITH.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/0_Common/COFFIN_MAKER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/BONE_COMBINATION.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/GOBLIN_ASSASSIN_TEAM.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/GOBLIN_CHARGE_TEAM.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/POWER_CRAVER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/POWER_SURGE.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/SNATCHER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/1_Uncommon/TACTICAL_BREACHER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/2_Rare/ALMIGHTY.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/2_Rare/ETERNAL_GHOST.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/2_Rare/POWER_SIPHONER.prefab",
		"Assets/Prefabs/Cards/3.0 no cost (current)/General/2_Rare/UNFINISHED_ROBOT.prefab"
	};

	System.Text.StringBuilder jsonBuilder = new System.Text.StringBuilder();
	jsonBuilder.Append("[");
	int successCount = 0;

	for (int pi = 0; pi < paths.Length; pi++)
	{
		string path = paths[pi];
		var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.GameObject>(path);
		if (prefab == null)
		{
			UnityEngine.Debug.LogError("NOT_FOUND: " + path);
			continue;
		}
		var cardScript = prefab.GetComponent<CardScript>();
		if (cardScript == null)
		{
			UnityEngine.Debug.LogError("NO_CARDSCRIPT: " + path);
			continue;
		}

		var hpAlter = prefab.GetComponentInChildren<HPAlterEffect>();
		int baseDmg = 0;
		int extraDmg = 0;
		if (hpAlter != null && hpAlter.baseDmg != null)
		{
			baseDmg = hpAlter.baseDmg.value;
			extraDmg = hpAlter.extraDmg;
		}
		else
		{
			UnityEngine.Debug.LogWarning("NO_HPALTER: " + path);
		}

		int powerCount = 0;
		if (cardScript.myStatusEffects != null)
		{
			for (int i = 0; i < cardScript.myStatusEffects.Count; i++)
			{
				if (cardScript.myStatusEffects[i] == EnumStorage.StatusEffect.Power)
					powerCount++;
			}
		}

		int totalDmg = baseDmg + extraDmg + powerCount;

		if (successCount > 0) jsonBuilder.Append(",");
		jsonBuilder.Append("\n  {");
		jsonBuilder.Append("\n    \"prefabName\": \"" + prefab.name + "\",");
		jsonBuilder.Append("\n    \"cardTypeID\": \"" + (cardScript.cardTypeID != null ? cardScript.cardTypeID : "") + "\",");
		jsonBuilder.Append("\n    \"baseDmg\": " + baseDmg + ",");
		jsonBuilder.Append("\n    \"extraDmg\": " + extraDmg + ",");
		jsonBuilder.Append("\n    \"powerCount\": " + powerCount + ",");
		jsonBuilder.Append("\n    \"totalDmg\": " + totalDmg);
		jsonBuilder.Append("\n  }");
		successCount++;
	}

	jsonBuilder.Append("\n]");

	string projectRoot = System.IO.Path.GetDirectoryName(UnityEngine.Application.dataPath);
	string outPath = System.IO.Path.Combine(projectRoot, "tools", "outputs", "OneDeckDmgValues.json");
	string outDir = System.IO.Path.GetDirectoryName(outPath);
	if (!System.IO.Directory.Exists(outDir))
	{
		System.IO.Directory.CreateDirectory(outDir);
	}
	System.IO.File.WriteAllText(outPath, jsonBuilder.ToString());
	UnityEngine.Debug.Log("Wrote damage values JSON to: " + outPath + " (count=" + successCount + ")");
	return 0;
}
