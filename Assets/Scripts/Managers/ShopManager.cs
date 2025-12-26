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
        public DeckSO currenShopItemDeckRef;
        // todo: find out how to customize inspector description
        public int shopItemAmount; // max 5?
        [TextArea]
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
                                
                        }
                }
                else // sell mode
                {
                        
                }
        }

        public void EnterShop()
        {
                print("entering shop");
                GatherPlayerDeckInfo();
                GenerateShopItems();
        }
        private void GatherPlayerDeckInfo()
        {
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
                for (int i = 0; i < shopItemAmount; i++)
                {
                        var card = shopPoolRef.deck[Random.Range(0, shopPoolRef.deck.Count)];
                        currenShopItemDeckRef.deck.Add(card);
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
                currenShopItemDeckRef.deck.Clear();
                _shopInfoStr = "";
                GenerateShopItems();
        }
}
