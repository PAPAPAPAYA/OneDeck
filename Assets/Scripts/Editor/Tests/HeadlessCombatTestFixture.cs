using System;
using System.Collections.Generic;
using System.Reflection;
using DefaultNamespace;
using DefaultNamespace.Managers;
using DefaultNamespace.SOScripts;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Base fixture for headless combat unit tests.
/// Sets up a full combat logic environment with NullCombatVisuals (no animations, no physical cards).
/// All created objects are destroyed in TearDown.
/// </summary>
public abstract class HeadlessCombatTestFixture
{
	protected CombatManager CombatManager { get; private set; }
	protected NullCombatVisuals NullVisuals { get; private set; }
	protected PlayerStatusSO OwnerStatus { get; private set; }
	protected PlayerStatusSO EnemyStatus { get; private set; }
	protected GameEventStorage GameEventStorage { get; private set; }
	protected ValueTrackerManager ValueTrackerManager { get; private set; }
	protected EffectChainManager EffectChainManager { get; private set; }
	protected CombatLog CombatLog { get; private set; }
	protected CardFactory CardFactory { get; private set; }
	protected DeckTester DeckTester { get; private set; }
	protected CardIDRetriever CardIDRetriever { get; private set; }
	protected CombatInfoDisplayer InfoDisplayer { get; private set; }
	protected CombatFuncs CombatFuncs { get; private set; }

	private readonly List<GameObject> _createdObjects = new List<GameObject>();
	private readonly List<ScriptableObject> _createdScriptables = new List<ScriptableObject>();

	[SetUp]
	public virtual void SetUp()
	{
		CleanupSingletons();

		// Player statuses
		OwnerStatus = CreateScriptableObject<PlayerStatusSO>();
		OwnerStatus.hp = 100;
		OwnerStatus.hpMax = 100;
		OwnerStatus.shield = 0;

		EnemyStatus = CreateScriptableObject<PlayerStatusSO>();
		EnemyStatus.hp = 100;
		EnemyStatus.hpMax = 100;
		EnemyStatus.shield = 0;

		// CombatManager
		var cmObj = CreateGameObject("TestCombatManager");
		CombatManager = cmObj.AddComponent<CombatManager>();
		CombatManager.Me = CombatManager;
		CombatManager.ownerPlayerStatusRef = OwnerStatus;
		CombatManager.enemyPlayerStatusRef = EnemyStatus;
		CombatManager.combinedDeckZone = new List<GameObject>();

		var pdp = CreateGameObject("PlayerDeckParent");
		CombatManager.playerDeckParent = pdp;
		var edp = CreateGameObject("EnemyDeckParent");
		CombatManager.enemyDeckParent = edp;

		// Inject null visuals
		NullVisuals = new NullCombatVisuals();
		CombatManager.SetVisualsOverride(NullVisuals);

		// CardIDRetriever
		var cirObj = CreateGameObject("TestCardIDRetriever");
		CardIDRetriever = cirObj.AddComponent<CardIDRetriever>();
		CardIDRetriever.Me = CardIDRetriever;

		// ValueTrackerManager
		var vtmObj = CreateGameObject("TestValueTrackerManager");
		ValueTrackerManager = vtmObj.AddComponent<ValueTrackerManager>();
		global::ValueTrackerManager.me = ValueTrackerManager;
		ValueTrackerManager.ownerCardCountInDeckRef = CreateScriptableObject<IntSO>();
		ValueTrackerManager.enemyCardCountInDeckRef = CreateScriptableObject<IntSO>();
		ValueTrackerManager.ownerCardsBuriedCountRef = CreateScriptableObject<IntSO>();
		ValueTrackerManager.enemyCardsBuriedCountRef = CreateScriptableObject<IntSO>();
		ValueTrackerManager.stagedOwnerRef = CreateScriptableObject<IntSO>();
		ValueTrackerManager.stagedEnemyRef = CreateScriptableObject<IntSO>();

		// GameEventStorage
		var gesObj = CreateGameObject("TestGameEventStorage");
		GameEventStorage = gesObj.AddComponent<GameEventStorage>();
		global::GameEventStorage.me = GameEventStorage;
		GameEventStorage.onTheirPlayerTookDmg = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMyPlayerTookDmg = CreateScriptableObject<GameEvent>();
		GameEventStorage.onTheirPlayerHealed = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMyPlayerHealed = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMyPlayerShieldUpped = CreateScriptableObject<GameEvent>();
		GameEventStorage.onTheirPlayerShieldUpped = CreateScriptableObject<GameEvent>();
		GameEventStorage.onAnyCardRevealed = CreateScriptableObject<GameEvent>();
		GameEventStorage.onHostileCardRevealed = CreateScriptableObject<GameEvent>();
		GameEventStorage.afterShuffle = CreateScriptableObject<GameEvent>();
		GameEventStorage.beforeRoundStart = CreateScriptableObject<GameEvent>();
		GameEventStorage.onAnyCardGotPower = CreateScriptableObject<GameEvent>();
		GameEventStorage.onFriendlyCardGotPower = CreateScriptableObject<GameEvent>();
		GameEventStorage.onEnemyCardGotPower = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMeGotStatusEffect = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMeGotPower = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMeRevealed = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMeStaged = CreateScriptableObject<GameEvent>();
		GameEventStorage.onMeBuried = CreateScriptableObject<GameEvent>();
		GameEventStorage.onAnyCardBuried = CreateScriptableObject<GameEvent>();
		GameEventStorage.onFriendlyCardBuried = CreateScriptableObject<GameEvent>();
		GameEventStorage.onFriendlyCardExiled = CreateScriptableObject<GameEvent>();
		GameEventStorage.onFriendlyFlyExiled = CreateScriptableObject<GameEvent>();
		GameEventStorage.onFriendlyMinionAdded = CreateScriptableObject<GameEvent>();
		GameEventStorage.onEnemyCurseCardRevealed = CreateScriptableObject<GameEvent>();
		GameEventStorage.onEnemyCurseCardGotPower = CreateScriptableObject<GameEvent>();
		GameEventStorage.onThisTagResolverAttached = CreateScriptableObject<GameEvent>();
		GameEventStorage.curseCardTypeID = CreateScriptableObject<StringSO>();

		// EffectChainManager
		var ecmObj = CreateGameObject("TestEffectChainManager");
		EffectChainManager = ecmObj.AddComponent<EffectChainManager>();
		global::EffectChainManager.Me = EffectChainManager;
		EffectChainManager.openedEffectRecorders = new List<GameObject>();
		EffectChainManager.closedEffectRecorders = new List<GameObject>();
		var recPrefab = CreateGameObject("EffectRecorderPrefab");
		recPrefab.AddComponent<EffectRecorder>();
		EffectChainManager.effectRecorderPrefab = recPrefab;
		EffectChainManager.sessionNumberRef = CreateScriptableObject<IntSO>();

		// DeckTester
		var dtObj = CreateGameObject("TestDeckTester");
		DeckTester = dtObj.AddComponent<DeckTester>();
		DefaultNamespace.Managers.DeckTester.me = DeckTester;
		DeckTester.deckADmgOutputs_ToOpp = new List<float>();
		DeckTester.deckADmgOutputs_ToSelf = new List<float>();
		DeckTester.deckBDmgOutputs_ToOpp = new List<float>();
		DeckTester.deckBDmgOutputs_ToSelf = new List<float>();

		// Components auto-added by CombatManager's [RequireComponent]
		CombatLog = CombatManager.GetComponent<CombatLog>();
		global::CombatLog.me = CombatLog;

		CardFactory = CombatManager.GetComponent<CardFactory>();
		CardFactory.me = CardFactory;
		CardFactory.combatManager = CombatManager;

		CombatFuncs = CombatManager.GetComponent<CombatFuncs>();
		CombatFuncs.me = CombatFuncs;

		InfoDisplayer = CombatManager.GetComponent<CombatInfoDisplayer>();
		CombatInfoDisplayer.me = InfoDisplayer;

		// Force-set private fields on CombatManager in case OnEnable didn't fire reliably in Edit Mode
		var infoField = typeof(CombatManager).GetField("_infoDisplayer", BindingFlags.NonPublic | BindingFlags.Instance);
		if (infoField != null) infoField.SetValue(CombatManager, InfoDisplayer);
		var funcsField = typeof(CombatManager).GetField("_combatFuncs", BindingFlags.NonPublic | BindingFlags.Instance);
		if (funcsField != null) funcsField.SetValue(CombatManager, CombatFuncs);

		// Force-set CombatFuncs._combatManager so ReturnPlayerCardScripts() doesn't NRE
		var cfCmField = typeof(CombatFuncs).GetField("_combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		if (cfCmField != null) cfCmField.SetValue(CombatFuncs, CombatManager);

		// Dummy UI to prevent NullReferenceException in RefreshDeckInfo / ShowCardInfo
		SetupDummyUI();

		// Prevent CombatManager.Update and CombatInfoDisplayer.Update from running combat logic
		var gamePhaseSo = CreateScriptableObject<GamePhaseSO>();
		gamePhaseSo.currentGamePhase = EnumStorage.GamePhase.Shop;
		CombatManager.currentGamePhaseRef = gamePhaseSo;
		InfoDisplayer.gamePhase = gamePhaseSo;

		// Round tracking
		CombatManager.roundNumRef = CreateScriptableObject<IntSO>();
		CombatManager.combatFinished = CreateScriptableObject<BoolSO>();
	}

	[TearDown]
	public virtual void TearDown()
	{
		foreach (var obj in _createdObjects)
		{
			if (obj != null)
				UnityEngine.Object.DestroyImmediate(obj);
		}
		_createdObjects.Clear();

		foreach (var so in _createdScriptables)
		{
			if (so != null)
				UnityEngine.Object.DestroyImmediate(so);
		}
		_createdScriptables.Clear();

		CleanupSingletons();
	}

	private void CleanupSingletons()
	{
		if (CombatManager.Me != null)
		{
			var cm = CombatManager.Me;
			CombatManager.Me = null;
			if (cm != null) UnityEngine.Object.DestroyImmediate(cm.gameObject);
		}
		if (global::ValueTrackerManager.me != null)
		{
			var vtm = global::ValueTrackerManager.me;
			global::ValueTrackerManager.me = null;
			if (vtm != null) UnityEngine.Object.DestroyImmediate(vtm.gameObject);
		}
		if (global::GameEventStorage.me != null)
		{
			var ges = global::GameEventStorage.me;
			global::GameEventStorage.me = null;
			if (ges != null) UnityEngine.Object.DestroyImmediate(ges.gameObject);
		}
		if (DefaultNamespace.Managers.DeckTester.me != null)
		{
			var dt = DefaultNamespace.Managers.DeckTester.me;
			DefaultNamespace.Managers.DeckTester.me = null;
			if (dt != null) UnityEngine.Object.DestroyImmediate(dt.gameObject);
		}
		if (DefaultNamespace.Managers.CardIDRetriever.Me != null)
		{
			var cir = DefaultNamespace.Managers.CardIDRetriever.Me;
			DefaultNamespace.Managers.CardIDRetriever.Me = null;
			if (cir != null) UnityEngine.Object.DestroyImmediate(cir.gameObject);
		}
		if (global::EffectChainManager.Me != null)
		{
			var ecm = global::EffectChainManager.Me;
			global::EffectChainManager.Me = null;
			if (ecm != null) UnityEngine.Object.DestroyImmediate(ecm.gameObject);
		}
		if (CombatInfoDisplayer.me != null)
		{
			var cid = CombatInfoDisplayer.me;
			CombatInfoDisplayer.me = null;
			if (cid != null && cid.gameObject != null) UnityEngine.Object.DestroyImmediate(cid.gameObject);
		}
		if (global::CombatLog.me != null)
		{
			var cl = global::CombatLog.me;
			global::CombatLog.me = null;
			if (cl != null && cl.gameObject != null) UnityEngine.Object.DestroyImmediate(cl.gameObject);
		}
		if (CombatFuncs.me != null) CombatFuncs.me = null;
		if (CardFactory.me != null) CardFactory.me = null;
	}

	#region Object Creation Helpers

	protected GameObject CreateGameObject(string name)
	{
		var obj = new GameObject(name);
		_createdObjects.Add(obj);
		return obj;
	}

	protected T CreateScriptableObject<T>() where T : ScriptableObject
	{
		var so = ScriptableObject.CreateInstance<T>();
		_createdScriptables.Add(so);
		return so;
	}

	protected DeckSO CreateDeckSO(List<GameObject> cards)
	{
		var so = ScriptableObject.CreateInstance<DeckSO>();
		_createdScriptables.Add(so);
		so.deck = cards ?? new List<GameObject>();
		return so;
	}

	private void SetupDummyUI()
	{
		var dummyTextObj = CreateGameObject("DummyText");
		var dummyText = dummyTextObj.AddComponent<TMPro.TextMeshProUGUI>();

		InfoDisplayer.playerStatusDisplay = dummyText;
		InfoDisplayer.enemyStatusDisplay = dummyText;
		InfoDisplayer.revealZoneDisplay = dummyText;
		InfoDisplayer.combatTipsDisplay = dummyText;
		InfoDisplayer.effectResultDisplay = dummyText;
		InfoDisplayer.playerDeckDisplay = dummyText;
		InfoDisplayer.enemyDeckDisplay = dummyText;
	}

	#endregion

	#region Combat Helpers

	/// <summary>
	/// Create a logical card with ownership setup.
	/// </summary>
	protected GameObject CreateCard(bool isOwner, string name = null, string cardTypeID = null)
	{
		var cardName = name ?? (isOwner ? "FriendlyCard" : "EnemyCard");
		var card = CreateGameObject(cardName);
		var cs = card.AddComponent<CardScript>();
		cs.myStatusRef = isOwner ? OwnerStatus : EnemyStatus;
		cs.theirStatusRef = isOwner ? EnemyStatus : OwnerStatus;
		cs.myStatusEffects = new List<EnumStorage.StatusEffect>();
		cs.myTags = new List<EnumStorage.Tag>();
		if (cardTypeID != null)
			cs.cardTypeID = cardTypeID;
		return card;
	}

	/// <summary>
	/// Create a minion card.
	/// </summary>
	protected GameObject CreateMinion(bool isOwner, string name = null, string cardTypeID = null)
	{
		var card = CreateCard(isOwner, name, cardTypeID);
		card.GetComponent<CardScript>().isMinion = true;
		return card;
	}

	/// <summary>
	/// Create a Start Card (neutral).
	/// </summary>
	protected GameObject CreateStartCard()
	{
		var card = CreateGameObject("StartCard");
		var cs = card.AddComponent<CardScript>();
		cs.isStartCard = true;
		cs.myStatusEffects = new List<EnumStorage.StatusEffect>();
		cs.myTags = new List<EnumStorage.Tag>();
		return card;
	}

	/// <summary>
	/// Create an effect component under a card and wire protected fields via reflection.
	/// </summary>
	protected T CreateEffect<T>(GameObject parentCard) where T : EffectScript
	{
		var effectObj = CreateGameObject(typeof(T).Name);
		effectObj.transform.SetParent(parentCard.transform);
		var effect = effectObj.AddComponent<T>();

		var myCardField = typeof(EffectScript).GetField("myCard", BindingFlags.NonPublic | BindingFlags.Instance);
		myCardField.SetValue(effect, parentCard);

		var myCardScriptField = typeof(EffectScript).GetField("myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
		myCardScriptField.SetValue(effect, parentCard.GetComponent<CardScript>());

		var combatManagerField = typeof(EffectScript).GetField("combatManager", BindingFlags.NonPublic | BindingFlags.Instance);
		combatManagerField.SetValue(effect, CombatManager);

		return effect;
	}

	/// <summary>
	/// Create a CostNEffectContainer under a card.
	/// </summary>
	protected CostNEffectContainer CreateCostContainer(GameObject parentCard)
	{
		var containerObj = CreateGameObject("CostNEffectContainer");
		containerObj.SetActive(false);
		containerObj.transform.SetParent(parentCard.transform);
		var cnt = containerObj.AddComponent<CostNEffectContainer>();

		var cs = parentCard.GetComponent<CardScript>();
		Assert.IsNotNull(cs, "Parent card must have CardScript for CostNEffectContainer");

		var myCardScriptField = typeof(CostNEffectContainer).GetField("_myCardScript", BindingFlags.NonPublic | BindingFlags.Instance);
		Assert.IsNotNull(myCardScriptField, "Reflection should find _myCardScript field");
		myCardScriptField.SetValue(cnt, cs);

		// Verify it was set
		var verify = myCardScriptField.GetValue(cnt) as CardScript;
		Assert.IsNotNull(verify, "_myCardScript must be set after reflection injection");

		// Initialize UnityEvents (normally populated via Inspector)
		cnt.checkCostEvent = new UnityEngine.Events.UnityEvent();
		cnt.preEffectEvent = new UnityEngine.Events.UnityEvent();
		cnt.effectEvent = new UnityEngine.Events.UnityEvent();

		containerObj.SetActive(true);
		return cnt;
	}

	/// <summary>
	/// Move the top card of combinedDeckZone into revealZone.
	/// </summary>
	protected CardScript RevealTopCard()
	{
		if (CombatManager.combinedDeckZone.Count == 0)
			return null;

		var cardObj = CombatManager.combinedDeckZone[CombatManager.combinedDeckZone.Count - 1];
		CombatManager.combinedDeckZone.RemoveAt(CombatManager.combinedDeckZone.Count - 1);
		CombatManager.revealZone = cardObj;
		CombatManager.cardsRevealedThisRound++;
		CombatManager.totalCardsRevealed++;
		return cardObj.GetComponent<CardScript>();
	}

	/// <summary>
	/// Move the revealed card back to bottom of deck.
	/// </summary>
	protected void PutRevealedCardToBottom()
	{
		if (CombatManager.revealZone == null) return;

		var cardToBottom = CombatManager.revealZone;
		CombatManager.revealZone = null;
		CombatManager.combinedDeckZone.Insert(0, cardToBottom);
	}

	/// <summary>
	/// Trigger reveal events for the card currently in revealZone.
	/// Simulates the second click (effect trigger phase).
	/// </summary>
	protected void TriggerRevealedCard()
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

	#endregion

	#region Event Helpers

	/// <summary>
	/// Register a temporary callback to a GameEvent for test assertion.
	/// Adds a dummy CardScript so RaiseOwner/RaiseOpponent don't NRE.
	/// </summary>
	protected void RegisterEventCallback(GameEvent gameEvent, System.Action callback, PlayerStatusSO listenerOwner = null)
	{
		var obj = CreateGameObject("EventListener");
		var listener = obj.AddComponent<GameEventListener>();
		listener.@event = gameEvent;
		listener.response.AddListener(() => callback());

		var cs = obj.AddComponent<CardScript>();
		cs.myStatusRef = listenerOwner ?? OwnerStatus;
		cs.myStatusEffects = new List<EnumStorage.StatusEffect>();
		cs.myTags = new List<EnumStorage.Tag>();

		gameEvent.RegisterListener(listener);
	}

	#endregion
}
