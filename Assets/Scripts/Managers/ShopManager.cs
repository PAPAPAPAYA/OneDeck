using System;
using System.Collections.Generic;
using DefaultNamespace.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
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
	public IntSO maxDeckSize;
	public IntSO purse;

	[Header("shop")]
	public DeckSO shopPoolRef;
	public DeckSO currentShopItemDeckRef;
	[Range(1, 6)]
	public int shopItemAmount;
	public IntSO payCheck;
	[TextArea]
	[Tooltip("button prompts and other general info")]
	public string phaseInfo;
	public bool sellMode = false; // if it's not sell mode then its buy mode

	[Tooltip("存储购买时实例化的卡牌，退出商店时统一销毁")]
	private List<GameObject> _boughtCardInstances = new List<GameObject>();

	[Header("UI objects")]
	public TextMeshProUGUI phaseInfoDisplay;
	public TextMeshProUGUI deckInfoDisplay;
	public TextMeshProUGUI shopInfoDisplay;
	public TextMeshProUGUI playerStatsDisplay;
	public GameObject rerollButton;
	public GameObject rerollButtonBg;
	public GameObject exitButton;
	public GameObject sectionIdentifier;
	private string _deckInfoStr = "Your Deck: \n\n";
	private string _shopInfoStr = "Shop: \n\n";

	private void Update()
	{
		if (gamePhaseRef.currentGamePhase != EnumStorage.GamePhase.Shop) return;
		//ShowDeck();
		//ShowShopItems();
		//ShowShopTips();
		ShowPlayerStats();

		// toggle sell/buy mode
		if (Input.GetKeyDown(KeyCode.S))
		{
			//sellMode = !sellMode;
		}

		// reroll
		if (Input.GetKeyDown(KeyCode.R))
		{
			//Reroll();
		}

		/*
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
		}*/
	}

	public void BuyFunc(int itemIndex)
	{
		if (currentShopItemDeckRef.deck.Count - 1 < itemIndex) return; // check if item index valid
		var cardToBuy = currentShopItemDeckRef.deck[itemIndex]; // store card player tyring to buy
		var cardToBuyScript = cardToBuy.GetComponent<CardScript>();
		if (cardToBuyScript.takeUpSpace) // if card player trying to buy takes up space in deck
		{
			if (playerDeckRef.deck.Count >= deckSize.value) return; // check if player deck not full
		}
		if (purse.value < cardToBuyScript.price.value) return; // check if affordable
		purse.value -= cardToBuyScript.price.value; // pay the price
		if (cardToBuyScript.takeUpSpace) // if card player tyring to buy takes up space in deck
		{
			playerDeckRef.deck.Add(cardToBuy); // add it to player deck
		}
		currentShopItemDeckRef.deck.Remove(cardToBuy); // remove it from current shop item list
		var cardToBuyInst = Instantiate(cardToBuy, transform);
		cardToBuyInst.GetComponent<CardScript>().myStatusRef = CombatManager.Me.ownerPlayerStatusRef;
		GameEventStorage.me?.onMeBought?.RaiseSpecific(cardToBuyInst); // buy timepoint: instantiate so it register as a listener
		_boughtCardInstances.Add(cardToBuyInst); // 添加到列表，退出商店时统一销毁
		// record card bought
		if (ShopStatsManager.Me != null)
		{
			var cardTypeID = cardToBuy.GetComponent<CardScript>()?.cardTypeID;
			if (!string.IsNullOrEmpty(cardTypeID))
			{
				ShopStatsManager.Me.RecordCardBought(cardTypeID, cardToBuy.name);
			}
		}
		GatherPlayerDeckInfo();
		UpdateShopItemInfo();
		
		// 通知 ShopUXManager 处理购买后的视觉更新
		ShopUXManager.Instance?.OnCardPurchased(itemIndex);
	}
	public void SellFunc(int cardIndex, GameObject physicalCardInstance = null)
	{
		if (playerDeckRef.deck.Count - 1 < cardIndex) return; // check if card index valid
		var cardToSell = playerDeckRef.deck[cardIndex]; // store card player tyring to sell
		purse.value += cardToSell.GetComponent<CardScript>().price.value / 2; // get the money
		playerDeckRef.deck.Remove(cardToSell); // remove it from player deck
		
		// 通知 ShopUXManager 处理卖出动画
		if (physicalCardInstance != null)
		{
			ShopUXManager.Instance?.OnCardSold(physicalCardInstance, cardIndex);
		}
		
		GatherPlayerDeckInfo();
		UpdateShopItemInfo();
	}

	public void EnterShop()
	{
		// 清理可能残留的实例（保险起见）
		if (_boughtCardInstances.Count > 0)
		{
			foreach (var cardInst in _boughtCardInstances)
			{
				if (cardInst != null)
				{
					Destroy(cardInst);
				}
			}
			_boughtCardInstances.Clear();
		}
		
		// payday
		purse.value += payCheck.value;
		// process shop items and display
		GenerateShopItems();
		UpdateShopItemInfo();
		// process player deck and display
		GatherPlayerDeckInfo();
		// show reroll button
		rerollButton.SetActive(true);
		rerollButtonBg.SetActive(true);
		// show exit button
		exitButton.SetActive(true);
		// show section identifiers
		sectionIdentifier.SetActive(true);
		// record shop visit
		if (ShopStatsManager.Me != null)
		{
			ShopStatsManager.Me.RecordShopVisit();
		}
		// 注意：不需要在这里Flush，退出商店时Flush
	}

	public void ExitShop()
	{
		// 确保统计数据保存
		if (ShopStatsManager.Me != null)
		{
			ShopStatsManager.Me.Flush();
		}

		// 统一销毁购买时实例化的卡牌
		foreach (var cardInst in _boughtCardInstances)
		{
			if (cardInst != null)
			{
				Destroy(cardInst);
			}
		}
		_boughtCardInstances.Clear();

		deckInfoDisplay.text = "";
		shopInfoDisplay.text = "";
		phaseInfoDisplay.text = "";
		playerStatsDisplay.text = "";
		rerollButton.SetActive(false);
		rerollButtonBg.SetActive(false);
		exitButton.SetActive(false);
		sectionIdentifier.SetActive(false);
	}

	private void GatherPlayerDeckInfo()
	{
		_deckInfoStr = "Your Deck:\n\n";
		//deckInstList.Clear();
		for (var i = 0; i < playerDeckRef.deck.Count; i++)
		{
			var card = playerDeckRef.deck[i];
			var cardScript = card.GetComponent<CardScript>();
			if (!cardScript.takeUpSpace) continue; // if card doesn't take up space, skip it
			_deckInfoStr +=
				"#" + (i + 1) + " <size=+2><b>" + // number
				card.name + // name
				"</b></size>: <color=yellow>$" + cardScript.price.value / 2 + "</color>" + // price
				"\n" + cardScript.cardDesc + "\n\n"; // desc
		}
	}

	private void GenerateShopItems()
	{
		currentShopItemDeckRef.deck.Clear();
		for (var i = 0; i < shopItemAmount; i++)
		{
			var card = shopPoolRef.deck[Random.Range(0, shopPoolRef.deck.Count)];
			currentShopItemDeckRef.deck.Add(card);
			// record card appeared
			if (ShopStatsManager.Me != null)
			{
				var cardTypeID = card.GetComponent<CardScript>()?.cardTypeID;
				if (!string.IsNullOrEmpty(cardTypeID))
				{
					ShopStatsManager.Me.RecordCardAppeared(cardTypeID, card.name);
				}
			}
		}
	}

	private void UpdateShopItemInfo()
	{
		_shopInfoStr = "Shop:\n\n";
		for (var i = 0; i < currentShopItemDeckRef.deck.Count; i++)
		{
			var card = currentShopItemDeckRef.deck[i];
			var cardScript = card.GetComponent<CardScript>();
			_shopInfoStr +=
				"#" + (i + 1) + " <size=+2><b>" + // number
				card.name + // name
				"</b></size>: <color=yellow>$" + cardScript.price.value + "</color>" + // price
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
		string currentMode = sellMode ? "<color=yellow>Selling</color>" : "<color=yellow>Buying</color>";
		phaseInfoDisplay.text = phaseInfo + " Current: " + currentMode;
								
	}
	private void ShowPlayerStats()
	{
		playerStatsDisplay.text = 
			"HP Max: <color=#90EE90>" + CombatManager.Me.ownerPlayerStatusRef.hpMax + "</color>" +
			"\nYou have: <color=yellow>$" + purse.value + "</color> (+$12/combat)";
	}

	public void Reroll()
	{
		// 先生成新的商店物品数据
		GenerateShopItems();
		UpdateShopItemInfo();
		// record reroll
		if (ShopStatsManager.Me != null)
		{
			ShopStatsManager.Me.RecordReroll();
		}
		purse.value--;
		
		// 通知 ShopUXManager 处理 reroll 动画和重新生成物理卡片
		ShopUXManager.Instance?.OnReroll();
	}
}