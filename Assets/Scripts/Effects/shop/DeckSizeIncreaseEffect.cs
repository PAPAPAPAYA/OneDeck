using System;
using UnityEngine;

public class DeckSizeIncreaseEffect : EffectScript
{
	public IntSO myDeckSize;
	public IntSO maxDeckSize;

	public void IncreaseDeckSizeBy(int amount)
	{
		myDeckSize.value += amount;
		myDeckSize.value = Mathf.Clamp(myDeckSize.value, 1, maxDeckSize.value);
		print($"deck size increased by {amount}");
		
		// 通知 ShopUXManager 生成新的占位卡片
		ShopUXManager.Instance?.SpawnAdditionalEmptySpaces();
	}
}