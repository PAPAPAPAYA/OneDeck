using System.Collections.Generic;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	/// <summary>
	/// Test-only component to override the shuffle result in combat.
	/// Attach to the same GameObject as CombatManager.
	/// </summary>
	public class ShuffleOrderOverride : MonoBehaviour
	{
		[Tooltip("Enable custom shuffle order override")]
		public bool useCustomOrder;

		[Tooltip("Desired reveal order after shuffle, from first revealed (top of deck) to last revealed (bottom). " +
		         "Drag card prefabs here in the order you want them to be revealed. " +
		         "Start Card can also be included. " +
		         "Any cards in the deck but not in this list will remain at the bottom in their original relative order.")]
		public List<GameObject> customOrderPrefabs;
	}
}
