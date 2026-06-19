using TestWriteRead;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	/// <summary>
	/// Centralized toggle for one-click test mode.
	/// When enabled, synchronizes debug-related flags across shuffle override,
	/// enemy deck loader, and inverts combat auto-reveal.
	/// </summary>
	public class TestManager : MonoBehaviour
	{
		#region SINGLETON

		public static TestManager Me;

		private void Awake()
		{
			Me = this;
		}

		#endregion

		[Header("Test Mode")]
		[Tooltip("Enable test mode: custom shuffle order, debug enemy deck, and disable combat auto-reveal.")]
		public bool isTestMode;

		[Header("Targets")]
		[Tooltip("Optional ShuffleOrderOverride reference. Auto-resolves from CombatManager if null.")]
		[SerializeField] private ShuffleOrderOverride shuffleOrderOverride;

		[Tooltip("Optional DeckSaver reference. Auto-resolves from DeckSaver.Me if null.")]
		[SerializeField] private DeckSaver deckSaver;

		[Tooltip("Optional CombatManager reference. Auto-resolves from CombatManager.Me if null.")]
		[SerializeField] private CombatManager combatManager;

		private void Start()
		{
			ResolveReferences();
			ApplyTestMode();
		}

		private void OnValidate()
		{
			// Sync target flags immediately whenever the toggle changes in the Inspector.
			ResolveReferences();
			ApplyTestMode();

#if UNITY_EDITOR
			if (!Application.isPlaying)
			{
				UnityEditor.EditorUtility.SetDirty(this);
				if (shuffleOrderOverride != null) UnityEditor.EditorUtility.SetDirty(shuffleOrderOverride);
				if (deckSaver != null) UnityEditor.EditorUtility.SetDirty(deckSaver);
				if (combatManager != null) UnityEditor.EditorUtility.SetDirty(combatManager);
			}
#endif
		}

		/// <summary>
		/// Toggle test mode at runtime.
		/// </summary>
		public void SetTestMode(bool enabled)
		{
			isTestMode = enabled;
			ApplyTestMode();
		}

		/// <summary>
		/// Apply the current test mode state to all registered target systems.
		/// </summary>
		private void ApplyTestMode()
		{
			if (shuffleOrderOverride != null)
			{
				shuffleOrderOverride.useCustomOrder = isTestMode;
			}

			if (deckSaver != null)
			{
				deckSaver.useDebugEnemyDeck = isTestMode;
			}

			if (combatManager != null)
			{
				combatManager.autoReveal = !isTestMode;
			}

			Debug.Log("[TestManager] Test mode " + (isTestMode ? "ENABLED" : "DISABLED"));
		}

		/// <summary>
		/// Resolve target references from singletons when not assigned in the Inspector.
		/// </summary>
		private void ResolveReferences()
		{
			if (shuffleOrderOverride == null && CombatManager.Me != null)
			{
				shuffleOrderOverride = CombatManager.Me.GetComponent<ShuffleOrderOverride>();
			}

			if (deckSaver == null)
			{
				deckSaver = DeckSaver.Me;
			}

			if (combatManager == null)
			{
				combatManager = CombatManager.Me;
			}
		}
	}
}
