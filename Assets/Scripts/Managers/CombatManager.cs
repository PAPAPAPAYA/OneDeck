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

	[Header("START CARD")]
	public GameObject startCardPrefab; // Start Card 预制体
	private GameObject _startCardInstance; // Start Card 实例（在牌组底部）
	[Tooltip("如果为true，Start Card触发后直接移除，不洗入牌组")]
	public bool removeStartCardInsteadOfShuffle = false;

	[Header("ZONES")]
	public List<GameObject> combinedDeckZone;
	public GameObject revealZone;

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
	
	[Header("FATIGUE BY REVEAL COUNT")]
	[Tooltip("基于揭晓卡数的疲劳机制：当累计揭晓多少张卡后触发疲劳（0表示禁用）")]
	public int fatigueRevealThreshold;
	[Tooltip("已累计揭晓的卡数")]
	public int totalCardsRevealed;

	#region Enter and exit funcs

	public void EnterCombat()
	{
		currentCombatState = EnumStorage.CombatState.GatherDeckLists;
		combatFinished.value = false;
	}

	public void ExitCombat()
	{
		// 停止所有攻击动画
		AttackAnimationManager.me?.StopAllAttackAnimations();
		
		// clean up ui
		_infoDisplayer.ClearInfo();
		// clean up combined deck
		foreach (var cardInstance in combinedDeckZone)
		{
			Destroy(cardInstance);
		}

		combinedDeckZone.Clear();
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
		totalCardsRevealed = 0;
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
				CheckFatigueNAddFatigue(); // 基于回合数的疲劳检查
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

		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	private void CheckFatigueNAddFatigue()
	{
		// 基于回合数的疲劳检查（在ShuffleDeck阶段调用）
		if (roundNumRef.value <= overtimeRoundThreshold) return;
		
		var msg = $"<color=red>FATIGUE!</color> Round {roundNumRef.value} > {overtimeRoundThreshold}";
		print(msg);
		_infoDisplayer.effectResultString.value += msg + "\n";
		AddFatigueCards();
	}
	
	private void CheckFatigueByRevealCount()
	{
		// 基于reveal卡数的疲劳检查（每次reveal时调用）
		// threshold为0时禁用
		if (fatigueRevealThreshold <= 0) return;
		if (totalCardsRevealed < fatigueRevealThreshold) return;
		// 只有当reveal卡数恰好等于阈值时才触发（避免重复触发）
		if (totalCardsRevealed != fatigueRevealThreshold) return;
		
		var msg = $"<color=red>FATIGUE!</color> Revealed {totalCardsRevealed} cards";
		print(msg);
		_infoDisplayer.effectResultString.value += msg + "\n";
		AddFatigueCards();
	}
	
	private void AddFatigueCards()
	{
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

	public void Shuffle()
	{
		// 直接洗牌（Start Card 已在牌组中）
		combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone);

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

	/// <summary>
	/// 检查卡牌是否应该被效果跳过（Start Card 等中立卡）
	/// 统一入口，方便后续扩展其他中立卡类型
	/// </summary>
	public static bool ShouldSkipEffectProcessing(CardScript card)
	{
		if (card == null) return true;
		return card.IsNeutralCard;
	}

	/// <summary>
	/// 获取牌组中实际参与效果计算的卡牌数量（排除 Start Card）
	/// </summary>
	public int GetEffectiveDeckSize()
	{
		int count = 0;
		foreach (var card in combinedDeckZone)
		{
			var cardScript = card.GetComponent<CardScript>();
			if (cardScript != null && !cardScript.IsNeutralCard)
			{
				count++;
			}
		}
		return count;
	}

	public void ResetCardsRevealedCount()
	{
		cardsRevealedThisRound = 0;
	}

	/// <summary>
	/// 等待攻击动画完成后再揭晓下一张
	/// </summary>
	private System.Collections.IEnumerator WaitForAttackAnimationsBeforeNextReveal()
	{
		// 如果有待播放的攻击动画，等待
		while (AttackAnimationManager.me != null && AttackAnimationManager.me.HasPendingAnimations())
		{
			yield return null;
		}
	}

	private void RevealCards()
	{
		if (blockPlayerInput) return;
		
		// 如果正在播放攻击动画，等待
		if (AttackAnimationManager.me != null && AttackAnimationManager.me.isPlayingAttackAnimation)
		{
			return;
		}

		_infoDisplayer.RefreshDeckInfo();

		// ========== 回合开始：自动揭晓 Start Card（玩家直接看到）==========
		if (revealZone == null && cardsRevealedThisRound == 0)
		{
			CombatUXManager.me.InstantiateAllPhysicalCards();
			
			// 揭晓 Start Card（它在牌组底部）
			if (combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false; // 进入触发 Start Card 效果阶段
			}
			return;
		}

		// ========== 阶段1: 等待处理当前卡并揭晓下一张 ==========
		if (awaitingRevealConfirm)
		{
			// 战斗结束检查
			if (ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0)
			{
				HandleCombatFinished();
				return;
			}

			// 提示文本
			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to reveal next card";
			
			CombatUXManager.me.InstantiateAllPhysicalCards();
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
			_infoDisplayer.effectResultString.value = "";

			// 1. 将当前卡放回牌组底部
			PutRevealedCardToBottom();

			// 2. 揭晓下一张卡（如果有）
			if (combinedDeckZone.Count > 0)
			{
				RevealNextCard();
				awaitingRevealConfirm = false; // 进入触发效果阶段
			}
			
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

			_infoDisplayer.combatTipsDisplay.text = "TAP / SPACE to trigger effect";
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

			// Start Card 特殊处理：触发效果 = 洗牌 + 新回合
			if (IsRevealedCardStartCard())
			{
				TriggerStartCardEffect();
			}
			else
			{
				// 普通卡触发效果
				TriggerRevealedCardEffect();
			}
			
			awaitingRevealConfirm = true;
			EffectChainManager.Me.CloseOpenedChain();
			
			// 等待所有攻击动画完成后再允许下一步操作
			StartCoroutine(WaitForAttackAnimationsBeforeNextReveal());
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
		totalCardsRevealed++;
		_infoDisplayer.ShowCardInfo(cardRevealed, cardsRevealedThisRound, cardRevealed.myStatusRef == ownerPlayerStatusRef);
		_infoDisplayer.RefreshDeckInfo();
		
		// 检查疲劳（基于reveal卡数）
		CheckFatigueByRevealCount();

		// 记录战斗统计
		GetComponent<CombatStatsLogger>()?.OnCardRevealed(cardRevealed);
	}

	private void TriggerRevealedCardEffect()
	{
		if (revealZone == null) return;
		
		var cardScript = revealZone.GetComponent<CardScript>();
		if (cardScript == null) return;

		GameEventStorage.me.onAnyCardRevealed.Raise();
		GameEventStorage.me.onMeRevealed.RaiseSpecific(revealZone);
		_infoDisplayer.RefreshDeckInfo();
	}

	private void PutRevealedCardToBottom()
	{
		if (revealZone == null) return;

		var cardToBottom = revealZone;
		revealZone = null;

		// 放回牌组底部（index 0）
		combinedDeckZone.Insert(0, cardToBottom);
		CombatUXManager.me.MoveRevealedCardToBottom(cardToBottom);
	}

	private void TriggerStartCardEffect()
	{
		if (revealZone == null) return;

		var startCard = revealZone;
		revealZone = null;

		// 根据配置决定动画和后续处理
		if (removeStartCardInsteadOfShuffle)
		{
			// 从牌组中移除 Start Card（它不会参与 Shuffle）
			combinedDeckZone.Remove(startCard);
			
			// 同时播放：Start Card 退场动画 + 其他卡片 Shuffle 动画
			CombatUXManager.me.PlayStartCardExitWithShuffleAnimation(startCard, combinedDeckZone, () =>
			{
				// 销毁逻辑卡片
				Destroy(startCard);
				_startCardInstance = null;
				
				// 逻辑上执行 Shuffle（Start Card 已经不在了）
				combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone);
				
				// 刷新UI显示
				_infoDisplayer.RefreshDeckInfo();
				GameEventStorage.me.afterShuffle.Raise();
				
				// 新回合开始
				HandleNewRoundStart();
			});
		}
		else
		{
			// Start Card 留在牌组中参与 Shuffle
			// 先将 Start Card 添加回 combinedDeckZone（它之前被移到了 revealZone）
			combinedDeckZone.Insert(0, startCard);
			
			// 同时播放：Start Card 移动到随机位置 + 其他卡片 Shuffle 动画
			CombatUXManager.me.PlayStartCardShuffleAnimation(startCard, combinedDeckZone, () =>
			{
				// 逻辑上执行 Shuffle
				combinedDeckZone = UtilityFuncManagerScript.ShuffleList(combinedDeckZone);
				
				// 刷新UI显示
				_infoDisplayer.RefreshDeckInfo();
				GameEventStorage.me.afterShuffle.Raise();
				
				// 新回合开始
				HandleNewRoundStart();
			});
		}
	}

	private void HandleNewRoundStart()
	{
		// 回合数增加
		roundNumRef.value++;
		cardsRevealedThisRound = 0;
		_infoDisplayer.ClearInfo();
		
		// 物理卡牌复位
		CombatUXManager.me.ReviveAllPhysicalCards();
		
		// 回合开始事件
		GameEventStorage.me.beforeRoundStart.Raise();
	}

	private void HandleCombatFinished()
	{
		if (combatFinished.value) return;

		_infoDisplayer.combatTipsDisplay.text = "COMBAT FINISHED\nTAP / SPACE to continue";
		if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;

		combatFinished.value = true;
		CombatUXManager.me.ClearAllPhysicalCards();
	}

	private bool IsRevealedCardStartCard()
	{
		if (revealZone == null) return false;
		var cardScript = revealZone.GetComponent<CardScript>();
		return cardScript != null && cardScript.isStartCard;
	}
}
