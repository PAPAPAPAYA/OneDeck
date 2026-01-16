using System.Collections.Generic;
using UnityEngine;

namespace TestWriteRead
{
	[System.Serializable]
	public struct DeckDataStruct
	{
		public List<GameObject> theDeck;
		public int winAmount;
		public int heartLeft;

		public DeckDataStruct(List<GameObject> theDeck, int winAmount, int heartLeft)
		{
			this.theDeck = theDeck;
			this.winAmount = winAmount;
			this.heartLeft = heartLeft;
		}
	}
}