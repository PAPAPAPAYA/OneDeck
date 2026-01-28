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
	public List<GameObject> enemyCardInstances =  new List<GameObject>();

	[Header("ZONES")]
	public List<GameObject> combinedDeckZone;
	public int deckSize;
	public GameObject revealZone;
	public List<GameObject> graveZone;

	[Header("FLOW")]
	public bool awaitingRevealConfirm = true;
	public int cardNum;
	public BoolSO combatFinished; // identify if this session of combat is finished

	[Header("SUPPLEMENT COMPONENTS")]
	private CombatInfoDisplayer _infoDisplayer;
	private CombatFuncs  _combatFuncs;

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
		EffectChainManager.Me.CloseOpenedChain();
		EffectChainManager.Me.chainNumber = 0;
	}

	#endregion

	private void OnEnable()
	{
		_infoDisplayer = GetComponent<CombatInfoDisplayer>();
		_combatFuncs =  GetComponent<CombatFuncs>();
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

	private void GatherDecks() // collect player and enemy decks and instantiate cards
	{
		combinedDeckZone.Clear();
		foreach (var card in playerDeck.deck)
		{
			var cardInstance = Instantiate(card, playerDeckParent.transform);
			cardInstance.name =  cardInstance.name.Replace("(Clone)", "");
			var cardInstanceScript = cardInstance.GetComponent<CardScript>();
			// assign cards' targets
			cardInstanceScript.myStatusRef = ownerPlayerStatusRef;
			cardInstanceScript.theirStatusRef = enemyPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		foreach (var card in enemyDeck.deck)
		{
			var cardInstance = Instantiate(card, enemyDeckParent.transform);
			cardInstance.name =  cardInstance.name.Replace("(Clone)", "");
			var cardInstanceScript = cardInstance.GetComponent<CardScript>();
			// assign cards' targets
			cardInstanceScript.myStatusRef = enemyPlayerStatusRef;
			cardInstanceScript.theirStatusRef = ownerPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		deckSize = combinedDeckZone.Count;
		_infoDisplayer.RefreshDeckInfo();
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
		//EffectChainManager.Me.CloseEffectChain(); // close current effect chain
		GameEventStorage.me.afterShuffle.Raise(); // TIMEPOINT: after shuffle
		UpdateTrackingVariables();
		

		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	public void UpdateTrackingVariables()
	{
		deckSize = combinedDeckZone.Count; // refresh deck size
		cardNum = combinedDeckZone.Count - 1; // reveal from last to first cause we remove the revealed card from list
	}

	private void RevealCards()
	{
		if (awaitingRevealConfirm)
		{
			// combat finished
			if (ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0)
			{
				if (combatFinished.value) return;
				_infoDisplayer.combatTipsDisplay.text = "COMBAT FINISHED\npress space to continue";
				if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace) return;
				combatFinished.value = true;
			}
			// round finished
			else if (cardNum < 0)
			{
				_infoDisplayer.combatTipsDisplay.text = "ROUND FINISHED\npress space to shuffle";
				if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace) return;
				roundNumRef.value++;
				_infoDisplayer.ClearInfo();
				currentCombatState = EnumStorage.CombatState.ShuffleDeck;
			}
			// need to reveal next card
			else
			{
				_infoDisplayer.combatTipsDisplay.text = "press space to reveal";
				if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace) return;
				awaitingRevealConfirm = false;
				_infoDisplayer.effectResultString.value = "";
			}
			EffectChainManager.Me.CloseOpenedChain();
		}
		else
		{
			var cardRevealed = combinedDeckZone[cardNum].GetComponent<CardScript>();
			revealZone = combinedDeckZone[cardNum];
			combinedDeckZone.RemoveAt(cardNum);
			_infoDisplayer.ShowCardInfo(
				cardRevealed,
				deckSize,
				graveZone.Count,
				cardRevealed.myStatusRef == ownerPlayerStatusRef);
			cardNum--;
			GameEventStorage.me.onAnyCardRevealed?.Raise(); // timepoint
			GameEventStorage.me.onMeRevealed?.RaiseSpecific(cardRevealed.gameObject); // timepoint
			
			graveZone.Add(revealZone);
			GameEventStorage.me.onAnyCardSentToGrave?.Raise(); // timepoint
			GameEventStorage.me.onMeSentToGrave?.RaiseSpecific(cardRevealed.gameObject); // timepoint
			revealZone = null;
			awaitingRevealConfirm = true;
			_infoDisplayer.RefreshDeckInfo();
		}
	}
}