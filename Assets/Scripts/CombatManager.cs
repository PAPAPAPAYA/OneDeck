using System;
using System.Collections.Generic;
using SOScripts;
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
        // public IntSO playerMana;
        // public IntSO playerHP;
        // public IntSO enemyHP;
        
        [Header("DECK REFS")]
        public DeckSO playerDeck;
        public DeckSO enemyDeck;
        public GameObject playerDeckParent;
        public GameObject enemyDeckParent;
        
        [Header("ZONES")]
        public List<GameObject> combinedDeckZone;
        public GameObject revealZone;
        public List<GameObject> graveZone;

        [Header("FLOW")] 
        public bool awaitingRevealConfirm = true;
        
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
                                GatherDecksNShuffle();
                                break;
                        case EnumStorage.CombatState.ShuffleDeck:
                                Shuffle();
                                break;
                        case EnumStorage.CombatState.Reveal:
                                RevealCards();
                                break;
                }
        }
        private void GatherDecksNShuffle()
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
                
                currentCombatState = EnumStorage.CombatState.Reveal;
        }
        private void Shuffle()
        {
                combinedDeckZone = UtilityFuncManagerScript.me.ShuffleList(combinedDeckZone);
                currentCombatState = EnumStorage.CombatState.Reveal;
        }
        private void RevealCards()
        {
                if (awaitingRevealConfirm)
                {
                        print("press space to reveal");
                        if (Input.GetKeyDown(KeyCode.Space))
                        {
                                awaitingRevealConfirm = false;
                        }
                }
                else
                {
                        // todo: reveal next card
                        var cardRevealed = combinedDeckZone[0].GetComponent<CardScript>();
                        if (cardRevealed.myStatusRef == ownerPlayerStatusRef) // if card revealed is session owner's
                        {
                                print("your card: "+cardRevealed.cardName+": "+cardRevealed.cardDesc);
                        }
                        else
                        {
                                print("their card: "+cardRevealed.cardName+": "+cardRevealed.cardDesc);
                        }
                        awaitingRevealConfirm = true;
                }
        }
}
