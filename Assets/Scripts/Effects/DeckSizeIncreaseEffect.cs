using System;
using UnityEngine;

public class DeckSizeIncreaseEffect : MonoBehaviour
{
	public IntSO myDeckSize;
	public IntSO maxDeckSize;

	public void IncreaseDeckSize()
	{
		myDeckSize.value++;
		myDeckSize.value = Mathf.Clamp(myDeckSize.value, 1, maxDeckSize.value);
		print("deck size increased");
	}
}