using System;
using System.Collections.Generic;
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

    [Header("PHASE AND STATE REFS")] public GamePhaseSO currentGamePhaseRef;
    public EnumStorage.CombatState currentCombatState;

    [Header("PLAYER STATUS REFS")] public PlayerStatusSO ownerPlayerStatusRef;
    public PlayerStatusSO enemyPlayerStatusRef;

    [Header("DECK REFS")] public DeckSO playerDeck;
    public DeckSO enemyDeck;
    public GameObject playerDeckParent;
    public GameObject enemyDeckParent;

    [Header("ZONES")] public List<GameObject> combinedDeckZone;
    public int deckSize;
    public GameObject revealZone;
    public List<GameObject> graveZone;

    [Header("FLOW")] public bool awaitingRevealConfirm = true;
    public int cardNum;

    public static void EnterCombat()
    {
        print("Entering combat");
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
                Shuffle();
                break;
            case EnumStorage.CombatState.Reveal:
                RevealCards();
                break;
        }
    }

    private void GatherDecks()
    {
        foreach (var card in playerDeck.deck)
        {
            var cardInstance = Instantiate(card, playerDeckParent.transform);
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

    private void Shuffle()
    {
        if (graveZone.Count > 0) // not the first time shuffling deck
        {
            UtilityFuncManagerScript.CopyGameObjectList(graveZone, combinedDeckZone);
            graveZone.Clear();
        }

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
                print("all cards revealed");
                currentCombatState = EnumStorage.CombatState.ShuffleDeck;
            }
            else
            {
                print("press space to reveal");
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
                print("#" + (deckSize - cardNum) + " your card: " + cardRevealed.cardName + ": " +
                      cardRevealed.cardDesc);
            }
            else
            {
                print("#" + (deckSize - cardNum) + " their card: " + cardRevealed.cardName + ": " +
                      cardRevealed.cardDesc);
            }

            revealZone.GetComponent<CardEventTrigger>()?.InvokeActivateEvent(); //TIMEPOINT
            cardNum--;
            graveZone.Add(revealZone);
            revealZone = null;
            awaitingRevealConfirm = true;
        }
    }
}