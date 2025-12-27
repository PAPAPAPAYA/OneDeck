using System;
using System.Collections.Generic;
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

    [Header("player ref")]
    public DeckSO playerDeckRef;
    public IntSO deckSize;
    public IntSO purse;

    [Header("shop")]
    public DeckSO shopPoolRef;
    public DeckSO currentShopItemDeckRef;
    // todo: find out how to customize inspector description
    [Range(1, 6)] public int shopItemAmount;
    public IntSO payCheck;
    [TextArea] [Tooltip("button prompts and other general info")]
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
        ShowShopItems();
        ShowShopTips();
        if (Input.GetKeyDown(KeyCode.R))
        {
            Reroll();
        }

        if (Input.GetKeyDown(KeyCode.S))
        {
            sellMode = !sellMode;
        }

        if (!sellMode) // buy mode TEMP
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                BuyFunc(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                BuyFunc(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                BuyFunc(2);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                BuyFunc(3);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                BuyFunc(4);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                BuyFunc(5);
            }
        }
        else // sell mode TEMP
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                SellFunc(0);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha2))
            {
                SellFunc(1);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha3))
            {
                SellFunc(2);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha4))
            {
                SellFunc(3);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha5))
            {
                SellFunc(4);
            }
            else if (Input.GetKeyDown(KeyCode.Alpha6))
            {
                SellFunc(5);
            }
        }
    }

    private void BuyFunc(int itemIndex)
    {
        if (currentShopItemDeckRef.deck.Count - 1 < itemIndex) return; // check if item index valid
        if (currentShopItemDeckRef.deck[itemIndex].GetComponent<CardScript>().takeUpSpace) // if card player trying to buy takes up space in deck
        {
            if (playerDeckRef.deck.Count >= deckSize.value) return; // check if player deck not full
        }
        var cardToBuy = currentShopItemDeckRef.deck[itemIndex]; // store card player tyring to buy
        if (purse.value < cardToBuy.GetComponent<CardScript>().price) return; // check if affordable
        purse.value -= cardToBuy.GetComponent<CardScript>().price; // pay the price
        playerDeckRef.deck.Add(cardToBuy); // add it to player deck
        currentShopItemDeckRef.deck.Remove(cardToBuy); // remove it from current shop item list
        cardToBuy.GetComponent<CardEventTrigger>()?.InvokeCardBoughtEvent(); //TIMEPOINT
        GatherPlayerDeckInfo();
        UpdateShopItemInfo();
    }
    private void SellFunc(int cardIndex)
    {
        if (playerDeckRef.deck.Count - 1 < cardIndex) return; // check if card index valid
        var cardToSell = playerDeckRef.deck[cardIndex]; // store card player tyring to sell
        purse.value += cardToSell.GetComponent<CardScript>().price / 2; // get the money
        playerDeckRef.deck.Remove(cardToSell); // remove it from player deck
        GatherPlayerDeckInfo();
        UpdateShopItemInfo();
    }

    public void EnterShop()
    {
        print("entering shop");
        // payday
        purse.value += payCheck.value;
        // process shop items and display
        GenerateShopItems();
        UpdateShopItemInfo();
        // process player deck and display
        GatherPlayerDeckInfo();
    }

    private void GatherPlayerDeckInfo()
    {
        _deckInfoStr = "";
        for (int i = 0; i < playerDeckRef.deck.Count; i++)
        {
            var card = playerDeckRef.deck[i];
            var cardScript = card.GetComponent<CardScript>();
            if (!cardScript.takeUpSpace) continue; // if card doesn't take up space, skip it
            _deckInfoStr +=
                "#" + (i + 1) + " " + // number
                cardScript.cardName + // name
                ": $" + cardScript.price / 2 + // price
                "\n" + cardScript.cardDesc + "\n\n"; // desc
        }
    }

    private void GenerateShopItems()
    {
        for (var i = 0; i < shopItemAmount; i++)
        {
            var card = shopPoolRef.deck[Random.Range(0, shopPoolRef.deck.Count)];
            currentShopItemDeckRef.deck.Add(card);
        }
    }

    private void UpdateShopItemInfo()
    {
        _shopInfoStr = "";
        for (var i = 0; i < currentShopItemDeckRef.deck.Count; i++)
        {
            var card = currentShopItemDeckRef.deck[i];
            var cardScript = card.GetComponent<CardScript>();
            _shopInfoStr +=
                "#" + (i + 1) + " " + // number
                cardScript.cardName + // name
                ": $" + cardScript.price + // price
                "\n" + cardScript.cardDesc + "\n\n"; // desc
        }
    }

    private void ShowDeck()
    {
        deckInfoDisplay.text = _deckInfoStr;
    }

    private void ShowShopItems()
    {
        shopInfoDisplay.text = _shopInfoStr;
    }

    private void ShowShopTips()
    {
        string currentMode = sellMode ? "Selling" : "Buying";
        phaseInfoDisplay.text = phaseInfo + " Current: " + currentMode +
                                "\nDeck Size: " + deckSize.value + 
                                "\n$"+purse.value;
    }

    private void Reroll()
    {
        currentShopItemDeckRef.deck.Clear();
        _shopInfoStr = "";
        GenerateShopItems();
        UpdateShopItemInfo();
    }
}