using System.Collections.Generic;
using DefaultNamespace;
using UnityEngine;

[RequireComponent(typeof(CombatInfoDisplayer))]
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

	[Header("ZONES")]
	public List<GameObject> combinedDeckZone;
	public int deckSize;
	public GameObject revealZone;
	public List<GameObject> graveZone;

	[Header("FLOW")]
	public bool awaitingRevealConfirm = true;
	public int cardNum;
	public BoolSO combatFinished; // identify if this session of combat is finished

	[Header("INFO DISPLAYER")]
	public CombatInfoDisplayer infoDisplayer;

	[Header("OVERTIME")]
	public IntSO roundNumRef;
	public int overtimeRoundThreshold;
	public GameObject cardToAddWhenOvertime;
	[Tooltip("add this amount of fatigue to both player")]
	public int fatigueAmount;

	#region Enter and exit funcs
	public void EnterCombat()
	{
		print("Entering combat");
		currentCombatState = EnumStorage.CombatState.GatherDeckLists;
		combatFinished.value = false;
	}

	public void ExitCombat()
	{
		// clean up ui
		infoDisplayer.ClearInfo();
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
		EffectChainManager.Me.CloseEffectChain();
		EffectChainManager.Me.chainNumber = 0;
	}
	#endregion

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
			var cardInstanceScript = cardInstance.GetComponent<CardScript>();
			// assign cards' targets
			cardInstanceScript.myStatusRef = ownerPlayerStatusRef;
			cardInstanceScript.theirStatusRef = enemyPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		foreach (var card in enemyDeck.deck)
		{
			var cardInstance = Instantiate(card, enemyDeckParent.transform);
			cardInstance.GetComponent<CardScript>().myStatusRef = enemyPlayerStatusRef;
			cardInstance.GetComponent<CardScript>().theirStatusRef = ownerPlayerStatusRef;
			combinedDeckZone.Add(cardInstance);
		}

		deckSize = combinedDeckZone.Count;
		currentCombatState = EnumStorage.CombatState.ShuffleDeck;
	}

	private void CheckFatigueNAddFatigue()
	{
		if (roundNumRef.value <= overtimeRoundThreshold) return; // check if overtime
		print("fatigue kicked in");
		// add fatigue to owner side
		for (var i = 0; i < fatigueAmount; i++)
		{
			AddCardInTheMiddleOfCombat(cardToAddWhenOvertime, true);
		}

		// add fatigue to enemy side
		for (var i = 0; i < fatigueAmount; i++)
		{
			AddCardInTheMiddleOfCombat(cardToAddWhenOvertime, false);
		}
	}

	public void AddCardInTheMiddleOfCombat(GameObject cardToAdd, bool belongsToSessionOwner)
	{
		var cardInstance = Instantiate(cardToAdd,
			belongsToSessionOwner ? playerDeckParent.transform : enemyDeckParent.transform); // instantiate and assign corresponding parent
		cardInstance.GetComponent<CardScript>().myStatusRef = belongsToSessionOwner ? ownerPlayerStatusRef : enemyPlayerStatusRef; // assign corresponding target
		cardInstance.GetComponent<CardScript>().theirStatusRef = belongsToSessionOwner ? enemyPlayerStatusRef : ownerPlayerStatusRef; // assign corresponding target
		combinedDeckZone.Add(cardInstance); // add the new card to combined deck
		deckSize = combinedDeckZone.Count; // refresh deck size
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
		EffectChainManager.Me.CloseEffectChain(); // close current effect chain
		GameEventStorage.me.afterShuffle.Raise(); // TIMEPOINT: after shuffle
		cardNum = combinedDeckZone.Count - 1; // reveal from last to first cause we remove the revealed card from list
		currentCombatState = EnumStorage.CombatState.Reveal; // change state to reveal
	}

	private void RevealCards()
	{
		if (awaitingRevealConfirm)
		{
			if (ownerPlayerStatusRef.hp <= 0 || enemyPlayerStatusRef.hp <= 0)
			{
				if (combatFinished.value) return;
				infoDisplayer.combatTipsDisplay.text = "COMBAT FINISHED\npress space to continue";
				if (!Input.GetKeyDown(KeyCode.Space)) return;
				combatFinished.value = true;
			}
			else if (cardNum < 0)
			{
				infoDisplayer.combatTipsDisplay.text = "ROUND FINISHED\npress space to shuffle";
				if (!Input.GetKeyDown(KeyCode.Space)) return;
				roundNumRef.value++;
				infoDisplayer.ClearInfo();
				currentCombatState = EnumStorage.CombatState.ShuffleDeck;
			}
			else
			{
				infoDisplayer.combatTipsDisplay.text = "press space to reveal";
				if (!Input.GetKeyDown(KeyCode.Space)) return;
				awaitingRevealConfirm = false;
				infoDisplayer.effectResultString.value = "";
			}
		}
		else
		{
			var cardRevealed = combinedDeckZone[cardNum].GetComponent<CardScript>();
			revealZone = combinedDeckZone[cardNum];
			combinedDeckZone.RemoveAt(cardNum);
			infoDisplayer.ShowCardInfo(
				cardRevealed, 
				deckSize, 
				graveZone.Count, 
				cardRevealed.myStatusRef == ownerPlayerStatusRef);
			cardNum--;
			GameEventStorage.me.onAnyCardRevealed?.Raise();
			GameEventStorage.me.onMeRevealed?.RaiseSpecific(cardRevealed.gameObject);
			graveZone.Add(revealZone);
			GameEventStorage.me.onAnyCardSentToGrave?.Raise();
			GameEventStorage.me.onMeSentToGrave?.RaiseSpecific(cardRevealed.gameObject);
			revealZone = null;
			awaitingRevealConfirm = true;
		}
	}
}