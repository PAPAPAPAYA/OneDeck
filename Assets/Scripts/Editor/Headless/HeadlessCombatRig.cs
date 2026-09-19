using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using UnityEngine;

/// <summary>
/// Non-test twin of HeadlessCombatTestFixture, for the infinity plan's core abstraction
/// (plans/plan-infinity-detection-2026-09-17.md §2 / §10 P1 "RunBudgetSim headless 化").
/// It lives in the Editor assembly deliberately: the trigger bridge needs SerializedObject
/// (flipping UnityEvent persistent calls from RuntimeOnly to EditorAndRuntime) and
/// AssetDatabase (prefab loading), both editor-only APIs — and every intended consumer
/// (P2 attribution, P5 batch scan, these tests) runs in the editor or in batch mode.
/// The point of the extraction is that NON-TEST code can run a headless combat: the test
/// fixture sits under Editor/Tests and is therefore unusable from a batch tool.
/// TRIGGER FIDELITY (2026-09-19, plan §15): the bridge maps each listener from the REAL event
/// asset it was serialized against to this rig's event instance, and sets curseCardTypeID.
/// The legacy "point everything at onMeRevealed" shortcut rewrote cross-card triggers and made
/// whole decks measure a different loop than production — never reintroduce it here.
/// Determinism: GatherDecks seeds Rng from TestManager.overrideCombatSeed when non-zero, so the
/// rig creates a TestManager and injects the seed through that production path. Edit Mode never
/// runs Awake, so every singleton is assigned manually.
/// </summary>
public class HeadlessCombatRig : System.IDisposable
{
	private static TMPro.TextMeshProUGUI _sharedDummyText;

	private readonly List<GameObject> _createdObjects = new List<GameObject>();
	private readonly List<ScriptableObject> _createdScriptables = new List<ScriptableObject>();
	private readonly HashSet<GameObject> _bridged = new HashSet<GameObject>();

	public CombatManager CombatManager { get; private set; }
	public TestManager TestManager { get; private set; }
	public GameEventStorage GameEventStorage { get; private set; }
	public ValueTrackerManager ValueTrackerManager { get; private set; }
	public EffectChainManager EffectChainManager { get; private set; }
	public CombatBudgetGuard Guard { get; private set; }
	public CombatArrangementCycleDetector Detector { get; private set; }

	// ---- lifecycle ----

	public static HeadlessCombatRig Create(int guardPerRound = 200, int guardTotal = 1500, int guardRounds = 60)
	{
		CleanupSingletons();
		var rig = new HeadlessCombatRig();

		// Player statuses.
		rig.OwnerStatus = rig.CreateScriptableObject<PlayerStatusSO>();
		rig.OwnerStatus.hp = 30;
		rig.OwnerStatus.hpMax = 30;
		rig.EnemyStatus = rig.CreateScriptableObject<PlayerStatusSO>();
		rig.EnemyStatus.hp = 30;
		rig.EnemyStatus.hpMax = 30;

		// CombatManager (+ [RequireComponent] children).
		var cmObj = rig.CreateGameObject("HeadlessCombatManager");
		var cm = cmObj.AddComponent<CombatManager>();
		rig.CombatManager = cm;
		CombatManager.Me = cm;
		cm.ownerPlayerStatusRef = rig.OwnerStatus;
		cm.enemyPlayerStatusRef = rig.EnemyStatus;
		cm.combinedDeckZone = new List<GameObject>();
		cm.playerDeckParent = rig.CreateGameObject("PlayerDeckParent");
		cm.enemyDeckParent = rig.CreateGameObject("EnemyDeckParent");
		cm.SetVisualsOverride(new NullCombatVisuals());

		// TestManager: created purely to carry overrideCombatSeed into GatherDecks' production
		// seeding path (CombatManager :319-322). Awake is not called in Edit Mode, and neither is
		// Start, so this is inert apart from the log category switches (IsEnabled reads bool
		// fields when Me != null, which also silences the noisier categories by default).
		var tm = rig.CreateGameObject("HeadlessTestManager").AddComponent<TestManager>();
		rig.TestManager = tm;
		TestManager.Me = tm;

		var cir = rig.CreateGameObject("HeadlessCardIDRetriever").AddComponent<CardIDRetriever>();
		CardIDRetriever.Me = cir;

		rig.ValueTrackerManager = rig.BuildValueTrackerManager();
		rig.GameEventStorage = rig.BuildGameEventStorage();
		rig.EffectChainManager = rig.BuildEffectChainManager();

		var dt = rig.CreateGameObject("HeadlessDeckTester").AddComponent<DeckTester>();
		DeckTester.me = dt;
		dt.deckADmgOutputs_ToOpp = new List<float>();
		dt.deckADmgOutputs_ToSelf = new List<float>();
		dt.deckBDmgOutputs_ToOpp = new List<float>();
		dt.deckBDmgOutputs_ToSelf = new List<float>();

		// RequireComponent-created singletons on the CombatManager object.
		global::CombatLog.me = cm.GetComponent<CombatLog>();
		var cardFactory = cm.GetComponent<CardFactory>();
		CardFactory.me = cardFactory;
		cardFactory.combatManager = cm;
		CombatFuncs.me = cm.GetComponent<CombatFuncs>();
		CombatInfoDisplayer.me = cm.GetComponent<CombatInfoDisplayer>();

		// OnEnable does not fire reliably in Edit Mode; pin the private back-references.
		var infoField = typeof(CombatManager).GetField("_infoDisplayer", BindingFlags.NonPublic | BindingFlags.Instance);
		if (infoField != null) infoField.SetValue(cm, CombatInfoDisplayer.me);
		var funcsField = typeof(CombatManager).GetField("_combatFuncs", BindingFlags.NonPublic | BindingFlags.Instance);
		if (funcsField != null) funcsField.SetValue(cm, CombatFuncs.me);
		var cfCmField = typeof(CombatFuncs).GetField("_combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		if (cfCmField != null) cfCmField.SetValue(CombatFuncs.me, cm);

		rig.SetupDummyUI();

		// Combat phase so playback coroutines behave; Update is not driven in Edit Mode.
		var gamePhaseSo = rig.CreateScriptableObject<GamePhaseSO>();
		gamePhaseSo.currentGamePhase = EnumStorage.GamePhase.Combat;
		cm.currentGamePhaseRef = gamePhaseSo;
		CombatInfoDisplayer.me.gamePhase = gamePhaseSo;

		cm.roundNumRef = rig.CreateScriptableObject<IntSO>();
		cm.combatFinished = rig.CreateScriptableObject<BoolSO>();

		// L0 guard + L1 detector, mirroring CombatManager.Awake (which does not run here).
		rig.Guard = cmObj.AddComponent<CombatBudgetGuard>();
		rig.Guard.maxRevealsPerRound = guardPerRound;
		rig.Guard.maxTotalReveals = guardTotal;
		rig.Guard.maxRounds = guardRounds;
		typeof(CombatBudgetGuard)
			.GetProperty("Me", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?.SetValue(null, rig.Guard);

		rig.Detector = cmObj.AddComponent<CombatArrangementCycleDetector>();
		typeof(CombatArrangementCycleDetector)
			.GetProperty("Me", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
			?.SetValue(null, rig.Detector);

		return rig;
	}

	public PlayerStatusSO OwnerStatus { get; private set; }
	public PlayerStatusSO EnemyStatus { get; private set; }

	public void Dispose()
	{
		foreach (var obj in _createdObjects)
		{
			if (obj != null) Object.DestroyImmediate(obj);
		}
		_createdObjects.Clear();

		foreach (var so in _createdScriptables)
		{
			if (so != null) Object.DestroyImmediate(so);
		}
		_createdScriptables.Clear();

		CleanupSingletons();
	}

	/// <summary>
	/// Clears every singleton the rig assigns, so consecutive runs cannot leak state into each
	/// other. Mirrors HeadlessCombatTestFixture.CleanupSingletons.
	/// </summary>
	private static void CleanupSingletons()
	{
		if (CombatManager.Me != null)
		{
			var cm = CombatManager.Me;
			CombatManager.Me = null;
			if (cm != null) Object.DestroyImmediate(cm.gameObject);
		}
		if (RecorderAnimationPlayer.me != null)
		{
			var rap = RecorderAnimationPlayer.me;
			RecorderAnimationPlayer.me = null;
			if (rap != null && rap.gameObject != null) Object.DestroyImmediate(rap.gameObject);
		}
		if (ValueTrackerManager.me != null)
		{
			var vtm = ValueTrackerManager.me;
			ValueTrackerManager.me = null;
			if (vtm != null) Object.DestroyImmediate(vtm.gameObject);
		}
		if (GameEventStorage.me != null)
		{
			var ges = GameEventStorage.me;
			GameEventStorage.me = null;
			if (ges != null) Object.DestroyImmediate(ges.gameObject);
		}
		if (DeckTester.me != null)
		{
			var dt = DeckTester.me;
			DeckTester.me = null;
			if (dt != null) Object.DestroyImmediate(dt.gameObject);
		}
		if (CardIDRetriever.Me != null)
		{
			var cir = CardIDRetriever.Me;
			CardIDRetriever.Me = null;
			if (cir != null) Object.DestroyImmediate(cir.gameObject);
		}
		if (EffectChainManager.Me != null)
		{
			var ecm = EffectChainManager.Me;
			EffectChainManager.Me = null;
			if (ecm != null) Object.DestroyImmediate(ecm.gameObject);
		}
		if (CombatInfoDisplayer.me != null)
		{
			var cid = CombatInfoDisplayer.me;
			CombatInfoDisplayer.me = null;
			if (cid != null && cid.gameObject != null) Object.DestroyImmediate(cid.gameObject);
		}
		if (global::CombatLog.me != null)
		{
			var cl = global::CombatLog.me;
			global::CombatLog.me = null;
			if (cl != null && cl.gameObject != null) Object.DestroyImmediate(cl.gameObject);
		}
		if (CombatFuncs.me != null) CombatFuncs.me = null;
		if (CardFactory.me != null) CardFactory.me = null;
		if (TestManager.Me != null) TestManager.Me = null;
	}

	// ---- environment builders (ordered to satisfy the fixture's dependency graph) ----

	private ValueTrackerManager BuildValueTrackerManager()
	{
		var vtm = CreateGameObject("HeadlessValueTrackerManager").AddComponent<ValueTrackerManager>();
		global::ValueTrackerManager.me = vtm;
		vtm.ownerCardCountInDeckRef = CreateScriptableObject<IntSO>();
		vtm.enemyCardCountInDeckRef = CreateScriptableObject<IntSO>();
		vtm.ownerCardsBuriedCountRef = CreateScriptableObject<IntSO>();
		vtm.enemyCardsBuriedCountRef = CreateScriptableObject<IntSO>();
		vtm.stagedOwnerRef = CreateScriptableObject<IntSO>();
		vtm.stagedEnemyRef = CreateScriptableObject<IntSO>();
		vtm.ownerRevivedCountRef = CreateScriptableObject<IntSO>();
		vtm.enemyRevivedCountRef = CreateScriptableObject<IntSO>();
		vtm.ownerRevivedCountThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.enemyRevivedCountThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.creatureAttackTimesAuraOwnerThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.creatureAttackTimesAuraEnemyThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.creaturesBuriedByOwnerThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.creaturesBuriedByEnemyThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.graveCreatureAuraOwnerThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.graveCreatureAuraEnemyThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.friendlyExiledByOwnerThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.friendlyExiledByEnemyThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.curseAttackOverrideOwnerThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.curseAttackOverrideEnemyThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.bloodPactOwnerThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.bloodPactEnemyThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.riftOverrideOwnerThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.riftOverrideEnemyThisRoundRef = CreateScriptableObject<IntSO>();
		vtm.lastAppliedStatusEffectRef = CreateScriptableObject<StatusEffectSO>();
		vtm.lastAppliedStatusEffectAmountRef = CreateScriptableObject<IntSO>();
		return vtm;
	}

	private GameEventStorage BuildGameEventStorage()
	{
		var ges = CreateGameObject("HeadlessGameEventStorage").AddComponent<GameEventStorage>();
		global::GameEventStorage.me = ges;
		ges.onTheirPlayerTookDmg = CreateScriptableObject<GameEvent>();
		ges.onMyPlayerTookDmg = CreateScriptableObject<GameEvent>();
		ges.onTheirPlayerHealed = CreateScriptableObject<GameEvent>();
		ges.onMyPlayerHealed = CreateScriptableObject<GameEvent>();
		ges.onMyPlayerShieldUpped = CreateScriptableObject<GameEvent>();
		ges.onTheirPlayerShieldUpped = CreateScriptableObject<GameEvent>();
		ges.onAnyCardRevealed = CreateScriptableObject<GameEvent>();
		ges.onHostileCardRevealed = CreateScriptableObject<GameEvent>();
		ges.afterShuffle = CreateScriptableObject<GameEvent>();
		ges.beforeRoundStart = CreateScriptableObject<GameEvent>();
		ges.onRoundEnd = CreateScriptableObject<GameEvent>();
		ges.onAnyCardAttacked = CreateScriptableObject<GameEvent>();
		ges.onAnyFriendlyCardAttacked = CreateScriptableObject<GameEvent>();
		ges.onAnyCardGotPower = CreateScriptableObject<GameEvent>();
		ges.onFriendlyCardGotPower = CreateScriptableObject<GameEvent>();
		ges.onEnemyCardGotPower = CreateScriptableObject<GameEvent>();
		ges.onMeGotStatusEffect = CreateScriptableObject<GameEvent>();
		ges.onMeGotPower = CreateScriptableObject<GameEvent>();
		ges.onMeRevealed = CreateScriptableObject<GameEvent>();
		ges.onMeStaged = CreateScriptableObject<GameEvent>();
		ges.onMeBuried = CreateScriptableObject<GameEvent>();
		ges.onAnyCardBuried = CreateScriptableObject<GameEvent>();
		ges.onFriendlyCardBuried = CreateScriptableObject<GameEvent>();
		ges.onMeRevived = CreateScriptableObject<GameEvent>();
		ges.onAnyCardRevived = CreateScriptableObject<GameEvent>();
		ges.onFriendlyCardRevived = CreateScriptableObject<GameEvent>();
		ges.onEnemyCardRevived = CreateScriptableObject<GameEvent>();
		ges.onFriendlyCardExiled = CreateScriptableObject<GameEvent>();
		ges.onFriendlyFlyExiled = CreateScriptableObject<GameEvent>();
		ges.onFriendlyMinionAdded = CreateScriptableObject<GameEvent>();
		ges.onEnemyCurseCardRevealed = CreateScriptableObject<GameEvent>();
		ges.onEnemyCurseCardGotPower = CreateScriptableObject<GameEvent>();
		ges.onEnemyCurseCardGainedAttack = CreateScriptableObject<GameEvent>();
		ges.onAnyCardGainedAttack = CreateScriptableObject<GameEvent>();
		ges.onMeGainedAttack = CreateScriptableObject<GameEvent>();
		ges.onFriendlyCardGainedAttack = CreateScriptableObject<GameEvent>();
		ges.onEnemyCardGainedAttack = CreateScriptableObject<GameEvent>();
		ges.onThisTagResolverAttached = CreateScriptableObject<GameEvent>();
		ges.curseCardTypeID = CreateScriptableObject<StringSO>();
		return ges;
	}

	private EffectChainManager BuildEffectChainManager()
	{
		var ecm = CreateGameObject("HeadlessEffectChainManager").AddComponent<EffectChainManager>();
		global::EffectChainManager.Me = ecm;
		ecm.openedEffectRecorders = new List<GameObject>();
		ecm.closedEffectRecorders = new List<GameObject>();
		var recPrefab = CreateGameObject("EffectRecorderPrefab");
		recPrefab.AddComponent<EffectRecorder>();
		ecm.effectRecorderPrefab = recPrefab;
		ecm.sessionNumberRef = CreateScriptableObject<IntSO>();
		return ecm;
	}

	private void SetupDummyUI()
	{
		if (_sharedDummyText == null)
		{
			// Shared across rigs: creating a TextMeshProUGUI per run triggers TLS allocator
			// warnings (same reason HeadlessCombatTestFixture shares one).
			var dummyTextObj = new GameObject("HeadlessSharedDummyText");
			_sharedDummyText = dummyTextObj.AddComponent<TMPro.TextMeshProUGUI>();
		}

		var info = CombatInfoDisplayer.me;
		info.playerStatusDisplay = _sharedDummyText;
		info.enemyStatusDisplay = _sharedDummyText;
		info.revealZoneDisplay = _sharedDummyText;
		info.combatTipsDisplay = _sharedDummyText;
		info.effectResultDisplay = _sharedDummyText;
		info.playerDeckDisplay = _sharedDummyText;
		info.enemyDeckDisplay = _sharedDummyText;
	}

	// ---- factories ----

	public GameObject CreateGameObject(string name)
	{
		var obj = new GameObject(name);
		_createdObjects.Add(obj);
		return obj;
	}

	public T CreateScriptableObject<T>() where T : ScriptableObject
	{
		var so = ScriptableObject.CreateInstance<T>();
		_createdScriptables.Add(so);
		return so;
	}

	public DeckSO CreateDeckSO(List<GameObject> cards)
	{
		var so = CreateScriptableObject<DeckSO>();
		so.deck = cards ?? new List<GameObject>();
		return so;
	}

	/// <summary>Logical card with ownership set (no effects unless the caller adds them).</summary>
	public GameObject CreateCard(bool isOwner, string name, string cardTypeID)
	{
		var card = CreateGameObject(name);
		var cs = card.AddComponent<CardScript>();
		cs.myStatusRef = isOwner ? OwnerStatus : EnemyStatus;
		cs.theirStatusRef = isOwner ? EnemyStatus : OwnerStatus;
		cs.myStatusEffects = new List<EnumStorage.StatusEffect>();
		cs.myTags = new List<EnumStorage.Tag>();
		if (cardTypeID != null) cs.cardTypeID = cardTypeID;
		return card;
	}

	/// <summary>
	/// Wires the deck for a run. Must be called BEFORE GatherDecks so the production seeding path
	/// (CombatManager.GatherDecks -> Rng.InitCombat) picks up the seed.
	/// </summary>
	public void SetCombatSeed(int seed)
	{
		TestManager.overrideCombatSeed = seed;
	}

	/// <summary>
	/// Mirrors the production wiring of GameScene.unity's GameEventStorage: the curse type id
	/// gates the onEnemyCurseCardRevealed broadcast. Without it cross-card curse triggers
	/// (e.g. RELIC_CURSE_REVIVAL) silently never fire. Must run BEFORE GatherDecks.
	/// </summary>
	public void EnableProductionTriggerWiring()
	{
		GameEventStorage.curseCardTypeID.value = "JU_ON";
	}

	public void BridgeAllCards()
	{
		foreach (var card in CombatManager.combinedDeckZone) BridgeCard(card);
	}

	// ---- reveal primitives (mirror HeadlessCombatTestFixture) ----

	public CardScript RevealTopCard()
	{
		if (CombatManager.combinedDeckZone.Count == 0) return null;
		var cardObj = CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1];
		CombatManager.combinedDeckZone.RemoveAt(CombatManager.combinedDeckZone.Count - 1);
		CombatManager.revealZone = cardObj;
		CombatManager.cardsRevealedThisRound++;
		CombatManager.totalCardsRevealed++;
		return cardObj.GetComponent<CardScript>();
	}

	public void PutRevealedCardToBottom()
	{
		if (CombatManager.revealZone == null) return;
		var cardToBottom = CombatManager.revealZone;
		CombatManager.revealZone = null;
		CombatManager.combinedDeckZone.Insert(0, cardToBottom);
	}

	public void TriggerRevealedCard()
	{
		if (CombatManager.revealZone == null) return;
		var cardScript = CombatManager.revealZone.GetComponent<CardScript>();
		if (cardScript == null) return;

		GameEventStorage.onAnyCardRevealed.Raise();
		GameEventStorage.onMeRevealed.RaiseSpecific(CombatManager.revealZone);

		if (GameEventStorage.curseCardTypeID != null &&
		    !string.IsNullOrEmpty(GameEventStorage.curseCardTypeID.value) &&
		    cardScript.cardTypeID == GameEventStorage.curseCardTypeID.value)
		{
			if (cardScript.myStatusRef == EnemyStatus)
				GameEventStorage.onEnemyCurseCardRevealed.RaiseOwner();
			else
				GameEventStorage.onEnemyCurseCardRevealed.RaiseOpponent();
		}

		if (cardScript.myStatusRef == EnemyStatus)
			GameEventStorage.onHostileCardRevealed.RaiseOwner();
		else
			GameEventStorage.onHostileCardRevealed.RaiseOpponent();
	}

	/// <summary>CombatManager's private reveal-count fatigue check, which the fixture
	/// primitives bypass (production fires it inside RevealNextCardCore).</summary>
	public void CheckFatigueByRevealCount()
	{
		typeof(CombatManager)
			.GetMethod("CheckFatigueByRevealCount", BindingFlags.NonPublic | BindingFlags.Instance)
			?.Invoke(CombatManager.Me, null);
	}

	// ---- trigger bridge ----

	/// <summary>
	/// Edit Mode never runs Awake/OnEnable and skips RuntimeOnly persistent UnityEvent calls
	/// (callState=2), and CardFactory.CreateLogicalCard wires only the status refs — the
	/// EffectScript/container back-references the game resolves at spawn are null. This is the
	/// CurseSummonerPrefabSmokeTests bridge (runtime-ref injection + callState flip + listener
	/// registration) with the production-faithful event mapping. Idempotent per card instance so
	/// a driver can re-bridge freshly spawned cards (fatigue cards, curse tokens) before triggering.
	/// </summary>
	public void BridgeCard(GameObject card)
	{
		if (card == null || !_bridged.Add(card)) return;

		var cardScript = card.GetComponent<CardScript>();
		var myCardField = typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance);
		var myCardScriptField = typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
		var combatManagerField = typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		var containerField = typeof(CostNEffectContainer).GetField("_myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);

		foreach (var effect in card.GetComponentsInChildren<EffectScript>(true))
		{
			myCardField?.SetValue(effect, card);
			myCardScriptField?.SetValue(effect, cardScript);
			combatManagerField?.SetValue(effect, CombatManager);
		}
		foreach (var container in card.GetComponentsInChildren<CostNEffectContainer>(true))
		{
			containerField?.SetValue(container, cardScript);
			ForceEditorCallState(container, "effectEvent");
			ForceEditorCallState(container, "checkCostEvent");
		}
		foreach (var listener in card.GetComponentsInChildren<GameEventListener>(true))
		{
			ForceEditorCallState(listener, "response");
			GameEvent target = MapRealEventToFixtureEvent(listener.@event);
			listener.@event = target;
			target.RegisterListener(listener);
		}
	}

	public void ResetBridgeCache()
	{
		_bridged.Clear();
	}

	/// <summary>
	/// Destroys the shared dummy UI object. Editor tests MUST call this from OneTimeTearDown:
	/// the dummy lives in the ACTIVE SCENE (not under any rig-owned parent), so leaving it behind
	/// marks GameScene dirty — and the next Test Runner run then trips the
	/// "Scene(s) Have Been Modified" modal, which blocks the main thread and looks like a broken
	/// runner. A short-lived batch process can skip it (the process exit takes it away).
	/// </summary>
	public static void DestroySharedResources()
	{
		if (_sharedDummyText != null)
		{
			Object.DestroyImmediate(_sharedDummyText.gameObject);
			_sharedDummyText = null;
		}
	}

	/// <summary>
	/// Re-points a listener from the REAL event asset it was serialized against to this rig's
	/// equivalent instance, so cross-card triggers keep their production semantics (the rig
	/// builds its own GameEvent objects, so identity matching is impossible and asset-name
	/// matching is the contract). Unknown events fall back to onMeRevealed, which keeps cards
	/// outside the audited combos working as they did before the mapping existed.
	/// </summary>
	public GameEvent MapRealEventToFixtureEvent(GameEvent real)
	{
		if (real == null) return GameEventStorage.onMeRevealed;
		switch (real.name)
		{
			case "OnMeRevealed": return GameEventStorage.onMeRevealed;
			case "OnHostileCurseRevealed": return GameEventStorage.onEnemyCurseCardRevealed;
			case "OnAnyCardRevealed": return GameEventStorage.onAnyCardRevealed;
			case "OnHostileCardRevealed": return GameEventStorage.onHostileCardRevealed;
			case "OnMeRevived": return GameEventStorage.onMeRevived;
			case "OnAnyCardRevived": return GameEventStorage.onAnyCardRevived;
			case "OnFriendlyCardRevived": return GameEventStorage.onFriendlyCardRevived;
			case "OnEnemyCardRevived": return GameEventStorage.onEnemyCardRevived;
			case "OnMeBuried": return GameEventStorage.onMeBuried;
			case "OnAnyCardBuried": return GameEventStorage.onAnyCardBuried;
			case "OnFriendlyCardBuried": return GameEventStorage.onFriendlyCardBuried;
			default: return GameEventStorage.onMeRevealed;
		}
	}

	/// <summary>Flips one UnityEvent field's persistent calls from RuntimeOnly to EditorAndRuntime (instance copy only).</summary>
	public void ForceEditorCallState(Object owner, string fieldPath)
	{
		var so = new UnityEditor.SerializedObject(owner);
		var calls = so.FindProperty(fieldPath + ".m_PersistentCalls.m_Calls");
		if (calls == null) return;
		for (int i = 0; i < calls.arraySize; i++)
		{
			var state = calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_CallState");
			if (state != null) state.intValue = 1;
		}
		so.ApplyModifiedPropertiesWithoutUndo();
	}
}
