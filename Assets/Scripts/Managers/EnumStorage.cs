using System.Collections.Generic;
using UnityEngine;

public class EnumStorage : MonoBehaviour
{
    public enum GamePhase
    {
        Combat,
        Shop,
        Result
    }

    public enum CombatState
    {
        GatherDeckLists,
        ShuffleDeck,
        Reveal
    }
    
    public enum Tag
    {
        None,
        Infected,
        Mana,
        HeartChanged,
        Power
    }

    public static bool DoesListContainAmountOfTag(List<Tag> listToCheck, int amount, Tag tagToCheck)
    {
	    var amountOfTag = 0;
	    foreach (var listTag in listToCheck)
	    {
		    if (listTag == tagToCheck)
		    {
			    amountOfTag++;
		    }
	    }
	    return amountOfTag >= amount;
    }
}