using System.Collections.Generic;
using UnityEngine;

namespace TestWriteRead
{
	// this struct is responsible for:
	// 1. correlate list of cards with its wins, hearts, and session number
	[System.Serializable]
	public struct DeckDataStruct
	{
		public List<GameObject> theDeck;
		public int winAmount;
		public int heartLeft;
		public int sessionNum;

		public DeckDataStruct(List<GameObject> theDeck, int winAmount, int heartLeft, int sessionNum)
		{
			this.theDeck = theDeck;
			this.winAmount = winAmount;
			this.heartLeft = heartLeft;
			this.sessionNum = sessionNum;
		}
	}
}