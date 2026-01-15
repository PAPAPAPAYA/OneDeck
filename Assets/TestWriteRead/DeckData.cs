using System.Collections.Generic;
using UnityEngine;

namespace TestWriteRead
{
	[System.Serializable]
	public class DeckData
	{
		//public DeckSO deckSaved;
		// deck data struct
		public List<DeckDataStruct> savedDecks;
	}
}