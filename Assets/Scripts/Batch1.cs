using System;
using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using DefaultNamespace.Effects;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

public static class B1
{
	public static void Run()
	{
		if (CombatManager.Me != null) UnityEngine.Object.DestroyImmediate(CombatManager.Me.gameObject);
		CombatManager.Me = null;
		if (ValueTrackerManager.me != null) UnityEngine.Object.DestroyImmediate(ValueTrackerManager.me.gameObject);
		ValueTrackerManager.me = null;
		if (GameEventStorage.me != null) UnityEngine.Object.DestroyImmediate(GameEventStorage.me.gameObject);
		GameEventStorage.me = null;
		if (DeckTester.me != null) UnityEngine.Object.DestroyImmediate(DeckTester.me.gameObject);
		DeckTester.me = null;
		if (CardIDRetriever.Me != null) UnityEngine.Object.DestroyImmediate(CardIDRetriever.Me.gameObject);
		CardIDRetriever.Me = null;
		if (CombatUXManager.me != null) UnityEngine.Object.DestroyImmediate(CombatUXManager.me.gameObject);
		CombatUXManager.me = null;
		if (CombatFuncs.me != null) UnityEngine.Object.DestroyImmediate(CombatFuncs.me.gameObject);
		CombatFuncs.me = null;
		if (EffectChainManager.Me != null) UnityEngine.Object.DestroyImmediate(EffectChainManager.Me.gameObject);
		EffectChainManager.Me = null;

		PlayerStatusSO os = (PlayerStatusSO)ScriptableObject.CreateInstance(typeof(PlayerStatusSO));
		os.hp = 100; os.hpMax = 100; os.shield = 0;
		PlayerStatusSO es = (PlayerStatusSO)ScriptableObject.CreateInstance(typeof(PlayerStatusSO));
		es.hp = 100; es.hpMax = 100; es.shield = 0;

		GameObject cmObj = new GameObject("TestCM");
		CombatManager cm = cmObj.AddComponent<CombatManager>();
		CombatManager.Me = cm;
		cm.ownerPlayerStatusRef = os;
		cm.enemyPlayerStatusRef = es;
		cm.combinedDeckZone = new List<GameObject>();
		GameObject pdp = new GameObject("PDP"); cm.playerDeckParent = pdp;
		GameObject edp = new GameObject("EDP"); cm.enemyDeckParent = edp;

		GameObject vtmObj = new GameObject("TestVTM");
		ValueTrackerManager vtm = vtmObj.AddComponent<ValueTrackerManager>();
		ValueTrackerManager.me = vtm;
		vtm.ownerCardCountInDeckRef = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));
		vtm.enemyCardCountInDeckRef = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));
		vtm.ownerCardsBuriedCountRef = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));
		vtm.enemyCardsBuriedCountRef = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));

		GameObject cirObj = new GameObject("TestCIR");
		CardIDRetriever cir = cirObj.AddComponent<CardIDRetriever>();
		CardIDRetriever.Me = cir;

		GameObject gesObj = new GameObject("TestGES");
		GameEventStorage ges = gesObj.AddComponent<GameEventStorage>();
		GameEventStorage.me = ges;
		ges.onTheirPlayerTookDmg = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.onAnyCardGotPower = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.onFriendlyCardExiled = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.onFriendlyFlyExiled = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.onFriendlyMinionAdded = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.onEnemyCurseCardGotPower = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.onAnyCardBuried = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.onFriendlyCardBuried = (GameEvent)ScriptableObject.CreateInstance(typeof(GameEvent));
		ges.curseCardTypeID = (StringSO)ScriptableObject.CreateInstance(typeof(StringSO));
		ges.curseCardTypeID.value = "JU_ON";

		GameObject dtObj = new GameObject("TestDT");
		DeckTester dt = dtObj.AddComponent<DeckTester>();
		DeckTester.me = dt;

		GameObject physPrefab = new GameObject("PhysPrefab");
		CardPhysObjScript cpos = physPrefab.AddComponent<CardPhysObjScript>();
		cpos.cardNamePrint = physPrefab.AddComponent<TMPro.TextMeshPro>();
		cpos.cardDescPrint = physPrefab.AddComponent<TMPro.TextMeshPro>();
		cpos.cardFace = physPrefab.AddComponent<SpriteRenderer>();
		cpos.cardEdge = physPrefab.AddComponent<SpriteRenderer>();
		GameObject cuxmObj = new GameObject("TestCUXM");
		CombatUXManager cuxm = cuxmObj.AddComponent<CombatUXManager>();
		CombatUXManager.me = cuxm;
		cuxm.physicalCardPrefab = physPrefab;
		cuxm.minionPhysicalPrefab = physPrefab;
		cuxm.startCardPhysicalPrefab = physPrefab;
		cuxm.physicalCardDeckSize = Vector3.one;
		GameObject tempPos = new GameObject("TempPos"); cuxm.physicalCardNewTempCardPos = tempPos.transform;
		cuxm.physicalCardsInDeck = new List<GameObject>();
		FieldInfo cuxmCmField = typeof(CombatUXManager).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		cuxmCmField.SetValue(cuxm, cm);

		CombatFuncs cf = cmObj.AddComponent<CombatFuncs>();
		CombatFuncs.me = cf;
		FieldInfo cfCmField = typeof(CombatFuncs).GetField("_combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		cfCmField.SetValue(cf, cm);

		GameObject ecmObj = new GameObject("TestECM");
		EffectChainManager ecm = ecmObj.AddComponent<EffectChainManager>();
		EffectChainManager.Me = ecm;
		ecm.openedEffectRecorders = new List<GameObject>();
		ecm.closedEffectRecorders = new List<GameObject>();
		GameObject recPrefab = new GameObject("RecPrefab");
		recPrefab.AddComponent<EffectRecorder>();
		ecm.effectRecorderPrefab = recPrefab;
		ecm.sessionNumberRef = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));

		StringSO effectResultStr = (StringSO)ScriptableObject.CreateInstance(typeof(StringSO));

		Func<bool, GameObject> MC = (isOwner) =>
		{
			GameObject card = new GameObject(isOwner ? "F" : "E");
			CardScript cs = card.AddComponent<CardScript>();
			cs.myStatusRef = isOwner ? os : es;
			cs.theirStatusRef = isOwner ? es : os;
			cs.myStatusEffects = new List<EnumStorage.StatusEffect>();
			cs.myTags = new List<EnumStorage.Tag>();
			return card;
		};
		Func<bool, GameObject> MM = (isOwner) =>
		{
			GameObject card = MC(isOwner);
			card.GetComponent<CardScript>().isMinion = true;
			return card;
		};
		Func<GameObject> MS = () =>
		{
			GameObject card = new GameObject("SC");
			CardScript cs = card.AddComponent<CardScript>();
			cs.isStartCard = true;
			cs.myStatusEffects = new List<EnumStorage.StatusEffect>();
			cs.myTags = new List<EnumStorage.Tag>();
			return card;
		};
		Action<GameObject, EffectScript> W = (parent, eff) =>
		{
			FieldInfo f1 = typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance);
			f1.SetValue(eff, parent);
			FieldInfo f2 = typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
			f2.SetValue(eff, parent.GetComponent<CardScript>());
			FieldInfo f3 = typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
			f3.SetValue(eff, cm);
		};
		Action C = () =>
		{
			foreach (GameObject c in new List<GameObject>(cm.combinedDeckZone))
				if (c != null) UnityEngine.Object.DestroyImmediate(c);
			cm.combinedDeckZone.Clear();
			if (cm.revealZone != null) { UnityEngine.Object.DestroyImmediate(cm.revealZone); cm.revealZone = null; }
			if (CombatUXManager.me != null)
			{
				foreach (GameObject p in new List<GameObject>(CombatUXManager.me.physicalCardsInDeck))
					if (p != null) UnityEngine.Object.DestroyImmediate(p);
				CombatUXManager.me.physicalCardsInDeck.Clear();
			}
		};
		Func<GameObject, CostNEffectContainer> MKC = (parent) =>
		{
			GameObject co = new GameObject("CNT");
			co.transform.SetParent(parent.transform);
			CostNEffectContainer cnt = co.AddComponent<CostNEffectContainer>();
			cnt.effectResultString = effectResultStr;
			FieldInfo mcs = typeof(CostNEffectContainer).GetField("_myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
			mcs.SetValue(cnt, parent.GetComponent<CardScript>());
			FieldInfo cff = typeof(CostNEffectContainer).GetField("_costNotMetFlag", BindingFlags.NonPublic | BindingFlags.Instance);
			cff.SetValue(cnt, 0);
			return cnt;
		};
		Func<GameObject, HPAlterEffect> MHP = (parent) =>
		{
			GameObject eo = new GameObject("HAE");
			eo.transform.SetParent(parent.transform);
			HPAlterEffect hae = eo.AddComponent<HPAlterEffect>();
			W(parent, hae);
			IntSO bd = (IntSO)ScriptableObject.CreateInstance(typeof(IntSO));
			bd.value = 2;
			hae.baseDmg = bd;
			hae.extraDmg = 0;
			hae.isStatusEffectDamage = true;
			hae.effectResultString = effectResultStr;
			return hae;
		};

		Debug.Log("===== Conjure Strategy A - Batch 1 =====");
		int pass = 0, fail = 0;
		Action<string, bool> A = (id, ok) =>
		{
			if (ok) { pass++; Debug.Log("[TEST PASS] " + id); }
			else { fail++; Debug.Log("[TEST FAIL] " + id); }
		};

		// DEATHBED_CURSE A-1
		{
			C();
			GameObject sc = MS(); cm.combinedDeckZone.Add(sc);
			GameObject db = MC(true); db.name = "DEATHBED_CURSE";
			CardScript dbcs = db.GetComponent<CardScript>(); dbcs.cardTypeID = "DEATHBED_CURSE";
			cm.combinedDeckZone.Insert(0, db);
			GameObject juon = MC(false); juon.name = "JU_ON";
			CardScript juoncs = juon.GetComponent<CardScript>(); juoncs.cardTypeID = "JU_ON";
			cm.combinedDeckZone.Add(juon);
			CostNEffectContainer cnt = MKC(db);
			cnt.CheckCost_IndexBeforeStartCard();
			int cf1 = (int)typeof(CostNEffectContainer).GetField("_costNotMetFlag", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cnt);
			GameObject ceObj = new GameObject("CE"); ceObj.transform.SetParent(db.transform);
			CurseEffect ce = ceObj.AddComponent<CurseEffect>();
			W(db, ce);
			StringSO jtid = (StringSO)ScriptableObject.CreateInstance(typeof(StringSO)); jtid.value = "JU_ON";
			ce.cardTypeID = jtid; ce.effectResultString = effectResultStr;
			int pb = 0; foreach (var se in juoncs.myStatusEffects) if (se == EnumStorage.StatusEffect.Power) pb++;
			ce.EnhanceCurse(1);
			int pa = 0; foreach (var se in juoncs.myStatusEffects) if (se == EnumStorage.StatusEffect.Power) pa++;
			A("DEATHBED_CURSE A-1", cf1 == 0 && (pa - pb) == 1);
			C(); UnityEngine.Object.DestroyImmediate(db); UnityEngine.Object.DestroyImmediate(juon);
			UnityEngine.Object.DestroyImmediate(sc); UnityEngine.Object.DestroyImmediate(ceObj);
			UnityEngine.Object.DestroyImmediate(cnt.gameObject);
		}
		// A-2
		{
			C();
			GameObject sc = MS(); cm.combinedDeckZone.Add(sc);
			GameObject db = MC(true); db.name = "DEATHBED_CURSE";
			CardScript dbcs = db.GetComponent<CardScript>(); dbcs.cardTypeID = "DEATHBED_CURSE";
			cm.combinedDeckZone.Add(db);
			CostNEffectContainer cnt = MKC(db);
			cnt.CheckCost_IndexBeforeStartCard();
			int cf2 = (int)typeof(CostNEffectContainer).GetField("_costNotMetFlag", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(cnt);
			A("DEATHBED_CURSE A-2", cf2 > 0);
			C(); UnityEngine.Object.DestroyImmediate(db); UnityEngine.Object.DestroyImmediate(sc);
			UnityEngine.Object.DestroyImmediate(cnt.gameObject);
		}
		// A-3
		{
			C();
			GameObject sc = MS(); cm.combinedDeckZone.Add(sc);
			GameObject db = MC(true); db.name = "DEATHBED_CURSE";
			cm.combinedDeckZone.Insert(0, db);
			GameObject ceObj = new GameObject("CE"); ceObj.transform.SetParent(db.transform);
			CurseEffect ce = ceObj.AddComponent<CurseEffect>();
			W(db, ce);
			StringSO jtid = (StringSO)ScriptableObject.CreateInstance(typeof(StringSO)); jtid.value = "JU_ON";
			ce.cardTypeID = jtid; ce.effectResultString = effectResultStr;
			GameObject juonPrefab = MC(false); juonPrefab.name = "JU_ON";
			juonPrefab.GetComponent<CardScript>().cardTypeID = "JU_ON";
			ce.cardPrefab = juonPrefab;
			ce.EnhanceCurse(1);
			bool found = false; int pc = 0;
			foreach (GameObject c in cm.combinedDeckZone)
			{
				CardScript s = c.GetComponent<CardScript>();
				if (s != null && s.cardTypeID == "JU_ON" && s.myStatusRef == es)
				{
					found = true;
					foreach (var se in s.myStatusEffects) if (se == EnumStorage.StatusEffect.Power) pc++;
				}
			}
			A("DEATHBED_CURSE A-3", found && pc == 1);
			C(); UnityEngine.Object.DestroyImmediate(db); UnityEngine.Object.DestroyImmediate(ceObj);
			UnityEngine.Object.DestroyImmediate(juonPrefab); UnityEngine.Object.DestroyImmediate(sc);
		}
		// A-4
		{
			C();
			GameObject sc = MS(); cm.combinedDeckZone.Add(sc);
			GameObject db = MC(true); db.name = "DEATHBED_CURSE";
			cm.combinedDeckZone.Insert(0, db);
			GameObject juon = MC(false); juon.name = "JU_ON";
			CardScript juoncs = juon.GetComponent<CardScript>(); juoncs.cardTypeID = "JU_ON";
			cm.combinedDeckZone.Add(juon);
			GameObject ceObj = new GameObject("CE"); ceObj.transform.SetParent(db.transform);
			CurseEffect ce = ceObj.AddComponent<CurseEffect>();
			W(db, ce);
			StringSO jtid = (StringSO)ScriptableObject.CreateInstance(typeof(StringSO)); jtid.value = "JU_ON";
			ce.cardTypeID = jtid; ce.effectResultString = effectResultStr;
			int pb = 0; foreach (var se in juoncs.myStatusEffects) if (se == EnumStorage.StatusEffect.Power) pb++;
			ce.EnhanceCurse(1);
			int pa = 0; foreach (var se in juoncs.myStatusEffects) if (se == EnumStorage.StatusEffect.Power) pa++;
			A("DEATHBED_CURSE A-4", (pa - pb) == 1);
			C(); UnityEngine.Object.DestroyImmediate(db); UnityEngine.Object.DestroyImmediate(juon);
			UnityEngine.Object.DestroyImmediate(sc); UnityEngine.Object.DestroyImmediate(ceObj);
		}

		// FALL_INTO_RIFT A-1
		{
			C();
			GameObject fitr = MC(true); fitr.name = "FALL_INTO_RIFT";
			CardScript fitrcs = fitr.GetComponent<CardScript>();
			fitrcs.minionCostCount = 1; fitrcs.minionCostOwner = EnumStorage.TargetType.Me;
			GameObject minion = MM(true); cm.combinedDeckZone.Add(minion);
			GameObject enemy = MC(false); cm.combinedDeckZone.Add(enemy);
			GameObject meObj = new GameObject("MCE"); meObj.transform.SetParent(fitr.transform);
			MinionCostEffect mce = meObj.AddComponent<MinionCostEffect>();
			W(fitr, mce); mce.effectResultString = effectResultStr;
			mce.ExecuteMinionCost();
			bool mgone = true;
			foreach (GameObject c in cm.combinedDeckZone) if (c == minion) mgone = false;
			GameObject beObj = new GameObject("BE"); beObj.transform.SetParent(fitr.transform);
			BuryEffect be = beObj.AddComponent<BuryEffect>();
			W(fitr, be); be.effectResultString = effectResultStr;
			int dbf = cm.combinedDeckZone.Count;
			be.BuryTheirCards(1);
			A("FALL_INTO_RIFT A-1", mgone && cm.combinedDeckZone.Count == dbf);
			C(); UnityEngine.Object.DestroyImmediate(fitr); UnityEngine.Object.DestroyImmediate(meObj);
			UnityEngine.Object.DestroyImmediate(beObj);
		}
		// A-2
		{
			C();
			GameObject fitr = MC(true); fitr.name = "FALL_INTO_RIFT";
			CardScript fitrcs = fitr.GetComponent<CardScript>();
			fitrcs.minionCostCount = 1; fitrcs.minionCostOwner = EnumStorage.TargetType.Me;
			GameObject enemy = MC(false); cm.combinedDeckZone.Add(enemy);
			GameObject meObj = new GameObject("MCE"); meObj.transform.SetParent(fitr.transform);
			MinionCostEffect mce = meObj.AddComponent<MinionCostEffect>();
			W(fitr, mce); mce.effectResultString = effectResultStr;
			mce.ExecuteMinionCost();
			A("FALL_INTO_RIFT A-2", cm.combinedDeckZone.Count == 1);
			C(); UnityEngine.Object.DestroyImmediate(fitr); UnityEngine.Object.DestroyImmediate(meObj);
		}
		// A-3
		{
			C();
			GameObject fitr = MC(true); fitr.name = "FALL_INTO_RIFT";
			CardScript fitrcs = fitr.GetComponent<CardScript>();
			fitrcs.minionCostCount = 1; fitrcs.minionCostOwner = EnumStorage.TargetType.Me;
			GameObject minion = MM(true); cm.combinedDeckZone.Add(minion);
			GameObject enemy = MC(false); cm.combinedDeckZone.Insert(0, enemy);
			GameObject meObj = new GameObject("MCE"); meObj.transform.SetParent(fitr.transform);
			MinionCostEffect mce = meObj.AddComponent<MinionCostEffect>();
			W(fitr, mce); mce.effectResultString = effectResultStr;
			mce.ExecuteMinionCost();
			bool mgone = true;
			foreach (GameObject c in cm.combinedDeckZone) if (c == minion) mgone = false;
			GameObject beObj = new GameObject("BE"); beObj.transform.SetParent(fitr.transform);
			BuryEffect be = beObj.AddComponent<BuryEffect>();
			W(fitr, be); be.effectResultString = effectResultStr;
			int dbf = cm.combinedDeckZone.Count;
			be.BuryTheirCards(1);
			A("FALL_INTO_RIFT A-3", mgone && cm.combinedDeckZone.Count == dbf);
			C(); UnityEngine.Object.DestroyImmediate(fitr); UnityEngine.Object.DestroyImmediate(meObj);
			UnityEngine.Object.DestroyImmediate(beObj);
		}

		// RIFT A-1
		{
			C();
			GameObject rift = MC(true); rift.name = "RIFT";
			GameObject friendly = MC(true); cm.combinedDeckZone.Add(friendly);
			GameObject seObj = new GameObject("SE"); seObj.transform.SetParent(rift.transform);
			StageEffect se = seObj.AddComponent<StageEffect>();
			W(rift, se); se.effectResultString = effectResultStr;
			GameObject eeObj = new GameObject("EE"); eeObj.transform.SetParent(rift.transform);
			ExileEffect ee = eeObj.AddComponent<ExileEffect>();
			W(rift, ee); ee.effectResultString = effectResultStr;
			cm.revealZone = rift;
			se.StageMyCards(1);
			bool staged = (cm.combinedDeckZone.IndexOf(friendly) == cm.combinedDeckZone.Count - 1);
			cm.combinedDeckZone.Add(rift);
			ee.ExileSelf();
			bool exiled = !cm.combinedDeckZone.Contains(rift);
			A("RIFT A-1", staged && exiled);
			C(); UnityEngine.Object.DestroyImmediate(rift); UnityEngine.Object.DestroyImmediate(friendly);
			UnityEngine.Object.DestroyImmediate(seObj); UnityEngine.Object.DestroyImmediate(eeObj);
		}
		// A-2
		{
			C();
			GameObject rift = MC(true); rift.name = "RIFT";
			GameObject friendly = MC(true); cm.combinedDeckZone.Add(friendly);
			GameObject seObj = new GameObject("SE"); seObj.transform.SetParent(rift.transform);
			StageEffect se = seObj.AddComponent<StageEffect>();
			W(rift, se); se.effectResultString = effectResultStr;
			GameObject eeObj = new GameObject("EE"); eeObj.transform.SetParent(rift.transform);
			ExileEffect ee = eeObj.AddComponent<ExileEffect>();
			W(rift, ee); ee.effectResultString = effectResultStr;
			int dbf = cm.combinedDeckZone.Count;
			se.StageMyCards(1);
			cm.combinedDeckZone.Add(rift);
			ee.ExileSelf();
			A("RIFT A-2", cm.combinedDeckZone.Count == dbf);
			C(); UnityEngine.Object.DestroyImmediate(rift); UnityEngine.Object.DestroyImmediate(friendly);
			UnityEngine.Object.DestroyImmediate(seObj); UnityEngine.Object.DestroyImmediate(eeObj);
		}
		// A-3
		{
			C();
			GameObject rift = MC(true); rift.name = "RIFT";
			cm.revealZone = rift;
			GameObject eeObj = new GameObject("EE"); eeObj.transform.SetParent(rift.transform);
			ExileEffect ee = eeObj.AddComponent<ExileEffect>();
			W(rift, ee); ee.effectResultString = effectResultStr;
			ee.ExileSelf();
			bool notInDeck = !cm.combinedDeckZone.Contains(rift);
			A("RIFT A-3", notInDeck && cm.revealZone == null);
			C(); UnityEngine.Object.DestroyImmediate(rift); UnityEngine.Object.DestroyImmediate(eeObj);
		}

		Debug.Log("===== Batch 1 Results: " + pass + " PASS, " + fail + " FAIL =====");
	}
}
