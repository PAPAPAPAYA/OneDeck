using System;
using TMPro;
using UnityEngine;
using Random = UnityEngine.Random;

public class ShopManager : MonoBehaviour
{
        #region singleton
        public static ShopManager me;
        private void Awake()
        {
                me = this;
        }
        #endregion
        [Header("flow ref")]
        public GamePhaseSO gamePhaseRef;
        
        [Header("deck ref")]
        public DeckSO playerDeckRef;
        
        [Header("shop")]
        public DeckSO shopPoolRef;
        public DeckSO currentShopItemDeckRef;
        // todo: find out how to customize inspector description
        [Range(1, 5)]
        public int shopItemAmount; // max 5?
        [TextArea]
        [Tooltip("button prompts and other general info")]
        public string phaseInfo;
        public bool sellMode = false; // if it's not sell mode then its buy mode
        
        [Header("tmp objects")] 
        public TextMeshProUGUI phaseInfoDisplay;
        public TextMeshProUGUI deckInfoDisplay;
        public TextMeshProUGUI shopInfoDisplay;
        private string _deckInfoStr = "Your Deck: \n\n";
        private string _shopInfoStr = "Shop: \n\n";
        private void Update()
        {
                if (gamePhaseRef.currentGamePhase != EnumStorage.GamePhase.Shop) return;
                ShowDeck();
                ShowShop();
                ShowShopInfo();
                if (Input.GetKeyDown(KeyCode.R))
                {
                        Reroll();
                }
                if (!sellMode) // buy mode
                {
                        if (Input.GetKeyDown(KeyCode.Alpha1))
                        {
                                if (!currentShopItemDeckRef.deck[0]) return;
                                var cardToBuy = currentShopItemDeckRef.deck[0];
                                playerDeckRef.deck.Add(cardToBuy);
                                RefreshInfoDisplay();
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha2))
                        {
                                
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha3))
                        {
                                
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha4))
                        {
                                
                        }
                        else if (Input.GetKeyDown(KeyCode.Alpha5))
                        {
                                
                        }
                }
                else // sell mode
                {
                        
                }
        }

        public void EnterShop()
        {
                print("entering shop");
                RefreshInfoDisplay();
        }
        private void RefreshInfoDisplay()
        {
                GatherPlayerDeckInfo();
                GenerateShopItems();
        }
        private void GatherPlayerDeckInfo()
        {
                _deckInfoStr = "";
                for (int i = 0; i < playerDeckRef.deck.Count; i++)
                {
                        var card = playerDeckRef.deck[i];
                        _deckInfoStr += 
                                "#" + (i + 1) + " " + 
                                card.GetComponent<CardScript>().cardName + 
                                ": \n" + card.GetComponent<CardScript>().cardDesc + "\n\n";
                }
        }
        private void GenerateShopItems()
        {
                _shopInfoStr = "";
                for (int i = 0; i < shopItemAmount; i++)
                {
                        var card = shopPoolRef.deck[Random.Range(0, shopPoolRef.deck.Count)];
                        currentShopItemDeckRef.deck.Add(card);
                        _shopInfoStr +=
                                "#" + (i + 1) + " " +
                                card.GetComponent<CardScript>().cardName + 
                                ": \n" + card.GetComponent<CardScript>().cardDesc + "\n\n";
                }
        }
        private void ShowDeck()
        {
                deckInfoDisplay.text = _deckInfoStr;
        }
        private void ShowShop()
        {
                shopInfoDisplay.text = _shopInfoStr;
        }
        private void ShowShopInfo()
        {
                phaseInfoDisplay.text = phaseInfo;
        }
        private void Reroll()
        {
                currentShopItemDeckRef.deck.Clear();
                _shopInfoStr = "";
                GenerateShopItems();
        }
}
