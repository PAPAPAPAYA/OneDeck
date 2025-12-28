using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

// this script functions as a variable storage in combat
public class CombatManager : MonoBehaviour
{
    #region SINGLETON

    public static CombatManager instance;

    private void Awake()
    {
        instance = this;
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

    [Header("TMP Objects")]
    public TextMeshProUGUI playerStatusDisplay;
    public TextMeshProUGUI enemyStatusDisplay;
    public TextMeshProUGUI combatInfoDisplay;
    public TextMeshProUGUI combatTipsDisplay;

    [Header("OVERTIME")]
    public IntSO roundNumRef;
    public int overtimeRoundThreshold;
    public GameObject cardToAddWhenOvertime;
    [Tooltip("add this amount of fatigue to both player")]
    public int fatigueAmount;

    public void EnterCombat()
    {
        print("Entering combat");
        currentCombatState = EnumStorage.CombatState.GatherDeckLists;
    }

    public void ExitCombat()
    {
        // clean up ui
        playerStatusDisplay.text = "";
        enemyStatusDisplay.text = "";
        combatInfoDisplay.text = "";
        combatTipsDisplay.text = "";
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
                ResetGraveAndShuffle();
                break;
            case EnumStorage.CombatState.Reveal:
                RevealCards();
                break;
        }

        DisplayStatusInfo();
    }

    private void GatherDecks()
    {
        combinedDeckZone.Clear();
        foreach (var card in playerDeck.deck)
        {
            var cardInstance = Instantiate(card, playerDeckParent.transform);
            // assign cards' targets
            cardInstance.GetComponent<CardScript>().myStatusRef = ownerPlayerStatusRef;
            cardInstance.GetComponent<CardScript>().theirStatusRef = enemyPlayerStatusRef;
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
        if (roundNumRef.value > overtimeRoundThreshold)
        {
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
    }

    private void AddCardInTheMiddleOfCombat(GameObject cardToAdd, bool belongsToSessionOwner)
    {
        var cardInstance = Instantiate(cardToAdd,
            belongsToSessionOwner ? playerDeckParent.transform : enemyDeckParent.transform);
        cardInstance.GetComponent<CardScript>().myStatusRef = belongsToSessionOwner ? ownerPlayerStatusRef : enemyPlayerStatusRef;
        cardInstance.GetComponent<CardScript>().theirStatusRef = belongsToSessionOwner ? enemyPlayerStatusRef : ownerPlayerStatusRef;
        combinedDeckZone.Add(cardInstance);
        deckSize = combinedDeckZone.Count;
    }

    private void ResetGraveAndShuffle()
    {
        if (graveZone.Count > 0) // not the first time shuffling deck
        {
            UtilityFuncManagerScript.CopyGameObjectList(graveZone, combinedDeckZone);
            graveZone.Clear();
        }

        CheckFatigueNAddFatigue();
        combinedDeckZone = UtilityFuncManagerScript.me.ShuffleList(combinedDeckZone);
        cardNum = combinedDeckZone.Count - 1;
        currentCombatState = EnumStorage.CombatState.Reveal;
    }

    private void RevealCards()
    {
        if (awaitingRevealConfirm)
        {
            if (cardNum < 0)
            {
                combatTipsDisplay.text = "all cards revealed";
                roundNumRef.value++;
                currentCombatState = EnumStorage.CombatState.ShuffleDeck;
            }
            else
            {
                combatTipsDisplay.text = "press space to reveal";
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    awaitingRevealConfirm = false;
                }
            }
        }
        else
        {
            var cardRevealed = combinedDeckZone[cardNum].GetComponent<CardScript>();
            revealZone = combinedDeckZone[cardNum];
            combinedDeckZone.RemoveAt(cardNum);
            if (cardRevealed.myStatusRef == ownerPlayerStatusRef) // if card revealed is session owner's
            {
                combatInfoDisplay.text = "#" + (deckSize - cardNum) +
                                         " your card: " + cardRevealed.cardName +
                                         "\n" + cardRevealed.cardDesc;
            }
            else
            {
                combatInfoDisplay.text = "#" + (deckSize - cardNum) +
                                         " their card: " + cardRevealed.cardName +
                                         "\n" + cardRevealed.cardDesc;
            }

            revealZone.GetComponent<CardEventTrigger>()?.InvokeActivateEvent(); //TIMEPOINT
            cardNum--;
            graveZone.Add(revealZone);
            revealZone = null;
            awaitingRevealConfirm = true;
        }
    }

    private void DisplayStatusInfo()
    {
        playerStatusDisplay.text =
            "Your HP: " + ownerPlayerStatusRef.hp + "\n" +
            "Your Mana: " + ownerPlayerStatusRef.mana;
        enemyStatusDisplay.text =
            "Their HP: " + enemyPlayerStatusRef.hp + "\n" +
            "Their Mana: " + enemyPlayerStatusRef.mana;
    }
}