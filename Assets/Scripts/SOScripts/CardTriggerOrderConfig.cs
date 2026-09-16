using System;
using UnityEngine;

/// <summary>
/// Designer config for the GameEvent trigger order of specific card types.
/// GameEvent.Raise iterates its listener list backwards, so trigger order is the REVERSE of
/// instantiation order; CombatManager.GatherDecks consumes this config through
/// CardTriggerOrderManager to reorder the instantiation sequence accordingly.
/// Semantics: smaller triggerOrder = triggers earlier. Ordering is applied per deck side
/// (player block / enemy block independently); cross-side order is not configurable here.
/// </summary>
[CreateAssetMenu(menuName = "OneDeck/Card Trigger Order Config")]
public class CardTriggerOrderConfig : ScriptableObject
{
	[Serializable]
	public class Entry
	{
		[Tooltip("CardScript.cardTypeID of the affected card, e.g. RELIC_TRAINER")]
		public string cardTypeID;

		[Tooltip("Smaller = triggers earlier. Configured cards trigger before all unconfigured cards of the same side.")]
		public int triggerOrder;
	}

	[Tooltip("Cards whose GameEvent trigger order should be fixed. First entry wins on duplicate cardTypeID.")]
	public Entry[] entries = Array.Empty<Entry>();
}
