using UnityEngine;

namespace TestWriteRead
{
	public struct DeckDataStruct
	{
		public DeckSO theDeck;
		public int winAmount;
		public int heartLeft;

		public DeckDataStruct(DeckSO theDeck, int winAmount, int heartLeft)
		{
			this.theDeck = theDeck;
			this.winAmount = winAmount;
			this.heartLeft = heartLeft;
		}
	}
}