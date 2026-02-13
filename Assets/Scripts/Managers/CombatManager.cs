using System;
using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Managers;
using UnityEngine;

[RequireComponent(typeof(CombatInfoDisplayer))]
[RequireComponent(typeof(CombatFuncs))]
// this script functions as a variable storage in combat
public class CombatManager : MonoBehaviour
{
	#region SINGLETON

	public static CombatManager Me;

	private void Awake()
	{
		Me = this;
	}

	#endregion

	[Header("PHASE AND STATE REFS")]
	public GamePhaseSO currentGamePhaseRef;
	public EnumStorage.CombatState currentCombatState;

	[Header("PLAYER STATUS REFS")]
	public PlayerStatusSO ownerPlayerStatusRef;
	public PlayerStatusSO enemyPlayerStatusRef;

	[Header("DECK REFS")]
	public DeckSO playerDeck;
	public DeckSO enemyDeck;
	public GameObject playerDeckParent;
	public GameObject enemyDeckParent;
	public List<GameObject> playerCardInstances = new List<GameObject>();
	public List<GameObject> enemyCardInstances = new List<GameObject>();

	[Header("ZONES")]
	public List<GameObject> combinedDeckZone;
	public int deckSize;
	public GameObject revealZone;
	public List<GameObject> graveZone;

	[Header("FLOW")]
	public bool awaitingRevealConfirm = true;
	public int cardNum;
	public BoolSO combatFinished; // identify if this session of combat is finished
	public int cardsRevealedThisRound; // tracks how many cards have been revealed this round (for display numbering)

	[Header("SUPPLEMENT COMPONENTS")]
	private CombatInfoDisplayer _infoDisplayer;
	private CombatFuncs _combatFuncs;

	[Header("OVERTIME")]
	public IntSO roundNumRef;
	public int overtimeRoundThreshold;
	public GameObject cardToAddWhenOvertime;
	[Tooltip("add this amount of fatigue to both player")]
	public int fatigueAmount;

	#region Enter and exit funcs

	public void EnterCombat()
	{
		currentCombatState = EnumStorage.CombatState.GatherDeckLists;
		combatFinished.value = false;
	}

	public void ExitCombat()
	{
		// clean up ui
		_infoDisplayer.ClearInfo();
		// clean up combined deck
		foreach (var cardInstance in combinedDeckZone)
		{
			Destroy(cardInstance);
		}

		combinedDeckZone.Clear();
		// clean up grave
		foreach (var cardInstance in graveZone)
		{
			Destroy(cardInstance);
		}

		graveZone.Clear();
		// clean up tracking stats
		roundNumRef.value = 0;
		cardNum = 0;
		deckSize = 0;
		cardsRevealedThisRound = 0;
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.chainNumber = 0;
	}

	#endregion

	private void OnEnable()
	{
		_infoDisplayer = GetComponent<CombatInfoDisplayer>();
		_combatFuncs = GetComponent<CombatFuncs>();
	}

	private void Update()
	{
		if (currentGamePhaseRef.Value() != EnumStorage.GamePhase.Combat) return;
		switch (currentCombatState)
		{
			case EnumStorage.CombatState.GatherDeckLists:
				GatherDecks();
				break;
			case EnumStorage.CombatState.ShuffleDeck:
				ResetGrave();
				CheckFatigueNAddFatigue(); // process fatigue
				Shuffle();
				break;
			case EnumStorage.CombatState.Reveal:
				RevealCards();
				break;
		}
	}

	public void GatherDecks() // collect player and enemy decks and instantiate cards
	{
		combinedDeckZone.Clear();
		foreach (var card in playerDeck.deck)
		{
			var cardInstance = Instantiate(card, playerDeckParent.transform);
			cardInstance.name = cardInstance.name.Replace("(Clone)", "");
			var cardInstanceScript = cardInstance.GetComponent<CardScript>();
			// assign cards' targets
			cardInstanceScript.myStatusRef = ownerPlayerStatusRef;
			cardInstanceScript.theirStatusRef = enemyPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		foreach (var card in enemyDeck.deck)
		{
			var cardInstance = Instantiate(card, enemyDeckParent.transform);
			cardInstance.name = cardInstance.name.Replace("(Clone)", "");
			var cardInstanceScript = cardInstance.GetComponent<CardScript>();
			// assign cards' targets
			cardInstanceScript.myStatusRef = enemyPlayerStatusRef;
			cardInstanceScript.theirStatusRef = ownerPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		deckSize = combinedDeckZone.Count;
		_infoDisplayer.RefreshDeckInfo();
		GameEventStorage.me.beforeRoundStart.Raise(); // timepoint

		// 记录玩家卡组快照（用于胜率统计）- 直接从playerDeck查询，无需等待实例化
		TestWriteRead.CardWinRateTracker.Me?.RecordPlayerDeckSnapshot(playerDeck.deck);

		currentCombatState = EnumStorage.CombatState.ShuffleDeck;
	}

	private void CheckFatigueNAddFatigue()
	{
		if (roundNumRef.value <= overtimeRoundThreshold) return; // check if overtime
		print("fatigue kicked in");
		// add fatigue to owner side
		for (var i = 0; i < fatigueAmount; i++)
		{
			_combatFuncs.AddCardInTheMiddleOfCombat(cardToAddWhenOvertime, true);
		}

		// add fatigue to enemy side
		for (var i = 0; i < fatigueAmount; i++)
		{
			_combatFuncs.AddCardInTheMiddleOfCombat(cardToAddWhenOvertime, false);
		}
	}

	private void ResetGrave()
	{
		if (graveZone.Count <= 0) return; // if grave empty, return
		UtilityFuncManagerScript.CopyGameObjectList(graveZone, combinedDeckZone, false); // copy from grave to combined deck
		graveZone.Clear(); // empty the grave
	}

	public void Shuffle()
	{
		combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone); // shuffle deck
		_infoDisplayer.RefreshDeckInfo();
		GameEventStorage.me.afterShuffle.Raise(); // TIMEPOINT: after shuffle
		UpdateTrackingVariables();

		CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
		CombatUXManager.me.UpdateAllPhysicalCardTargets();

		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	public void UpdateTrackingVariables()
	{
		deckSize = combinedDeckZone.Count; // refresh deck size
		cardNum = combinedDeckZone.Count - 1; // reveal from last to first cause we remove the revealed card from list
	}

	public void ResetCardsRevealedCount()
	{
		cardsRevealedThisRound = 0;
	}

	private void RevealCards()
	{
		if (awaitingRevealConfirm)
		{
			CombatInfoDisplayer.me.RefreshDeckInfo();
			// there's only one card in combined deck zone
			if (revealZone && combinedDeckZone.Count == 0)
			{
				_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to send last card to grave";
				if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
				// FIX: 先保存卡片引用，清空revealZone，再把卡片加入墓地，最后触发事件
				// 这样可以防止Undead等复活效果在事件触发时干扰当前状态
				var cardToGrave = revealZone;
				revealZone = null; // 先清空引用，确保复活效果执行时状态正确
				graveZone.Add(cardToGrave);
				CombatUXManager.me.MovePhysicalCardFromDeckToGrave(cardToGrave);
				GameEventStorage.me.onAnyCardSentToGrave.Raise(); // timepoint
				GameEventStorage.me.onMeSentToGrave.RaiseSpecific(cardToGrave); // timepoint
				
				// FIX: 处理复活效果可能导致的状态不一致
				// 如果复活把卡片加回了combined deck，更新追踪变量以正确反映新状态
				if (combinedDeckZone.Count > 0)
				{
					UpdateTrackingVariables();
				}
			}	
			// combat finished
			else if (ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0)
			{
				if (combatFinished.value) return;
				_infoDisplayer.combatTipsDisplay.text = "COMBAT FINISHED\nTAP / SPACE to continue";
				if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
				combatFinished.value = true;
				CombatUXManager.me.ClearAllPhysicalCards();
			}
			// round finished
			else if (cardNum < 0)
			{
				_infoDisplayer.combatTipsDisplay.text = "ROUND FINISHED\nTAP / SPACE to shuffle";
				_infoDisplayer.revealZoneDisplay.text = "";
				_infoDisplayer.effectResultString.value = "";
				if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
				GameEventStorage.me.beforeRoundStart.Raise(); // timepoint
				roundNumRef.value++;
				cardsRevealedThisRound = 0; // reset counter for new round
				_infoDisplayer.ClearInfo();
				CombatUXManager.me.ReviveAllPhysicalCards();
				currentCombatState = EnumStorage.CombatState.ShuffleDeck;
			}
			// need to reveal next card
			else
			{
				_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to reveal";
				CombatUXManager.me.InstantiateAllPhysicalCards();
				if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
				
				awaitingRevealConfirm = false;
				_infoDisplayer.effectResultString.value = "";
			}
			EffectChainManager.Me.CloseOpenedChain();
		}
		else
		{
			CombatUXManager.me.MovePhysicalCardFromDeckToGrave(CombatUXManager.me.physicalCardsInDeck[^1]);
			if (revealZone)
			{
				// FIX: 同样先清空revealZone再触发事件，防止复活效果干扰状态
				var cardToGrave = revealZone;
				revealZone = null; // 先清空引用
				graveZone.Add(cardToGrave);
				GameEventStorage.me.onAnyCardSentToGrave.Raise(); // timepoint
				GameEventStorage.me.onMeSentToGrave.RaiseSpecific(cardToGrave); // timepoint
				
				// FIX: 复活效果可能改变了combined deck，更新cardNum以反映正确状态
				cardNum = combinedDeckZone.Count - 1;
			}
			// reveal next card
			var cardRevealed = combinedDeckZone[cardNum].GetComponent<CardScript>();
			revealZone = combinedDeckZone[cardNum];
			combinedDeckZone.RemoveAt(cardNum);
			deckSize = combinedDeckZone.Count; // refresh deck size after card removal
			cardsRevealedThisRound++; // increment revealed count
			_infoDisplayer.ShowCardInfo(
				cardRevealed,
				cardsRevealedThisRound,
				cardRevealed.myStatusRef == ownerPlayerStatusRef);
			cardNum--;
			GameEventStorage.me.onAnyCardRevealed.Raise(); // timepoint
			GameEventStorage.me.onMeRevealed.RaiseSpecific(cardRevealed.gameObject); // timepoint

			_infoDisplayer.RefreshDeckInfo();
			
			awaitingRevealConfirm = true;
		}
	}
}