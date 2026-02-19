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

	[Header("START CARD")]
	public GameObject startCardPrefab; // Start Card 预制体
	private GameObject _startCardInstance; // Start Card 实例（在牌组底部）

	[Header("ZONES")]
	public List<GameObject> combinedDeckZone;
	public GameObject revealZone;
	public List<GameObject> graveZone;

	[Header("FLOW")]
	public bool awaitingRevealConfirm = true;
	public BoolSO combatFinished; // identify if this session of combat is finished
	public int cardsRevealedThisRound; // tracks how many cards have been revealed this round (for display numbering)
	[Tooltip("播放Stage/Bury动画时屏蔽玩家输入")]
	public bool blockPlayerInput = false;

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
		Destroy(revealZone);
		revealZone = null;
		
		// clean up start card
		if (_startCardInstance != null)
		{
			Destroy(_startCardInstance);
			_startCardInstance = null;
		}
		
		// clean up tracking stats
		roundNumRef.value = 0;
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

		// 实例化 Start Card 并添加到牌组底部
		_startCardInstance = Instantiate(startCardPrefab, playerDeckParent.transform);
		_startCardInstance.name = "Start Card";
		var startCardScript = _startCardInstance.GetComponent<CardScript>();
		if (startCardScript != null)
		{
			startCardScript.isStartCard = true;
		}
		combinedDeckZone.Add(_startCardInstance);

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
		// 洗牌前移除 Start Card
		combinedDeckZone.Remove(_startCardInstance);

		combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone); // shuffle deck

		// 洗牌后将 Start Card 放回牌组底部
		combinedDeckZone.Add(_startCardInstance);

		_infoDisplayer.RefreshDeckInfo();
		GameEventStorage.me.afterShuffle.Raise(); // TIMEPOINT: after shuffle

		CombatUXManager.me.SyncPhysicalCardsWithCombinedDeck();
		CombatUXManager.me.UpdateAllPhysicalCardTargets();

		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	public int GetCurrentDeckSize()
	{
		return combinedDeckZone.Count;
	}

	public void ResetCardsRevealedCount()
	{
		cardsRevealedThisRound = 0;
	}

	private void RevealCards()
	{
		if (blockPlayerInput) return;

		_infoDisplayer.RefreshDeckInfo();

		// ========== 回合开始：自动揭晓 Start Card（玩家直接看到）==========
		if (revealZone == null && cardsRevealedThisRound == 0)
		{
			CombatUXManager.me.InstantiateAllPhysicalCards();
			
			// 揭晓 Start Card（它在牌组底部）
			if (combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false; // 进入触发效果阶段（但 Start Card 会特殊处理）
			}
			return;
		}

		// ========== 阶段1: 等待送当前卡入墓地并揭晓下一张 ==========
		if (awaitingRevealConfirm)
		{
			// 战斗结束检查
			if (ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0)
			{
				HandleCombatFinished();
				return;
			}

			// 回合结束检查：revealZone 空 + 牌组空
			if (revealZone == null && combinedDeckZone.Count == 0)
			{
				HandleRoundFinished();
				return;
			}

			// 提示文本
			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to send card to grave";
			
			CombatUXManager.me.InstantiateAllPhysicalCards();
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
			_infoDisplayer.effectResultString.value = "";

			// 1. 送当前卡入墓地
			SendRevealedCardToGrave();

			// 2. 揭晓下一张卡（如果有）
			if (combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false; // 进入触发效果阶段
			}
			// 3. 如果牌组空了，保持 awaitingRevealConfirm = true，下次循环进入回合结束
			
			EffectChainManager.Me.CloseOpenedChain();
		}
		// ========== 阶段2: 等待触发当前卡效果 ==========
		else
		{
			// 检查当前卡是否有效
			if (revealZone == null)
			{
				awaitingRevealConfirm = true;
				return;
			}

			// Start Card 不需要触发效果，直接送回墓地阶段
			if (IsRevealedCardStartCard())
			{
				awaitingRevealConfirm = true;
				return;
			}

			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to trigger effect";
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

			// 触发效果
			TriggerRevealedCardEffect();
			awaitingRevealConfirm = true;
			EffectChainManager.Me.CloseOpenedChain();
		}
	}

	// ========== 辅助方法 ==========

	private void RevealNextCard()
	{
		var cardRevealed = combinedDeckZone[^1].GetComponent<CardScript>();
		revealZone = combinedDeckZone[^1];
		combinedDeckZone.RemoveAt(combinedDeckZone.Count - 1);

		// 物理移动：从牌堆移到揭晓区域
		var physicalCard = CombatUXManager.me.GetPhysicalCardFromLogicalCard(cardRevealed);
		if (physicalCard != null)
		{
			CombatUXManager.me.MovePhysicalCardToRevealZone(physicalCard);
		}

		// 显示信息（不触发效果）
		cardsRevealedThisRound++;
		_infoDisplayer.ShowCardInfo(cardRevealed, cardsRevealedThisRound, cardRevealed.myStatusRef == ownerPlayerStatusRef);
		_infoDisplayer.RefreshDeckInfo();
	}

	// [已移除] RevealStartCard 方法不再需要，Start Card 作为普通逻辑卡处理

	private void TriggerRevealedCardEffect()
	{
		if (revealZone == null) return;
		
		var cardScript = revealZone.GetComponent<CardScript>();
		if (cardScript == null) return; // Start Card 不触发效果

		GameEventStorage.me.onAnyCardRevealed.Raise();
		GameEventStorage.me.onMeRevealed.RaiseSpecific(revealZone);
		_infoDisplayer.RefreshDeckInfo();
	}

	private void SendRevealedCardToGrave()
	{
		if (revealZone == null) return;

		var cardToGrave = revealZone;
		revealZone = null;

		graveZone.Add(cardToGrave);
		CombatUXManager.me.MoveRevealedCardToGrave(cardToGrave);
		GameEventStorage.me.onAnyCardSentToGrave.Raise();
		GameEventStorage.me.onMeSentToGrave.RaiseSpecific(cardToGrave);
	}

	// [已移除] SendStartCardToGrave 方法不再需要，Start Card 使用 SendRevealedCardToGrave

	private void HandleCombatFinished()
	{
		if (combatFinished.value) return;

		_infoDisplayer.combatTipsDisplay.text = "COMBAT FINISHED\nTAP / SPACE to continue";
		if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

		combatFinished.value = true;
		CombatUXManager.me.ClearAllPhysicalCards();
	}

	private void HandleRoundFinished()
	{
		_infoDisplayer.combatTipsDisplay.text = "ROUND FINISHED\nTAP / SPACE to shuffle";
		_infoDisplayer.revealZoneDisplay.text = "";
		_infoDisplayer.effectResultString.value = "";
		if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

		GameEventStorage.me.beforeRoundStart.Raise();
		roundNumRef.value++;
		cardsRevealedThisRound = 0;
		_infoDisplayer.ClearInfo();
		CombatUXManager.me.ReviveAllPhysicalCards();
		currentCombatState = EnumStorage.CombatState.ShuffleDeck;
	}

	private bool IsRevealedCardStartCard()
	{
		if (revealZone == null) return false;
		var cardScript = revealZone.GetComponent<CardScript>();
		return cardScript != null && cardScript.isStartCard;
	}

	// [已移除] IsOnlyStartCardLeftInPhysicalDeck 方法不再需要，逻辑层已包含 Start Card

	// [已移除] HasNextCardToReveal 方法不再需要，直接检查 combinedDeckZone.Count > 0 即可
}
