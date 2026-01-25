using System;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	public class DeckTester : MonoBehaviour
	{
		#region SINGLETON

		public static DeckTester me;

		private void Awake()
		{
			me = this;
		}

		#endregion

		public DeckSO deckARef;
		public DeckSO deckBRef;
		public bool autoSpace;
		public int sessionAmountTarget;
		[HideInInspector]
		public int currentSessionAmount;
		public float deckAWins;
		public float deckBWins;

		private void Update()
		{
			if (currentSessionAmount >= sessionAmountTarget && autoSpace)
			{
				autoSpace = false;
				print(deckARef.defaultDeck.name + " win rate: " + deckAWins / (deckAWins + deckBWins) * 100 + "%");
				print(deckBRef.defaultDeck.name + " win rate: " + deckBWins / (deckAWins + deckBWins) * 100 + "%");
			}
		}
	}
}