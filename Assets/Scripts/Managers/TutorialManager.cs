using System.Collections.Generic;
using DefaultNamespace.Managers;
using UnityEngine;

/// <summary>
/// First-launch tutorial combat. On the very first game start (tracked via PlayerPrefs),
/// PhaseManager boots into a scripted combat instead of the shop: dedicated tutorial decks,
/// a fixed reveal order (via ShuffleOrderOverride), and custom HP for both sides.
/// The tutorial result is not counted in wins/hearts/statistics; when the combat ends,
/// the game enters the first real shop phase and the normal run begins.
/// Plan: plans/plan-tutorial-combat-2026-07-28.md
/// </summary>
public class TutorialManager : MonoBehaviour
{
	private const string TutorialCompletedKey = "OneDeck_TutorialCompleted";

	public static TutorialManager Me { get; private set; }

	/// <summary>True while the tutorial combat is being set up / played.</summary>
	public static bool IsTutorialActive { get; private set; }

	[Header("Tutorial Decks")]
	[Tooltip("Player deck used for the tutorial combat")]
	public DeckSO tutorialPlayerDeck;
	[Tooltip("Enemy deck used for the tutorial combat")]
	public DeckSO tutorialEnemyDeck;

	[Header("Reveal Order")]
	[Tooltip("Card prefabs in reveal order, first revealed -> last revealed. " +
	         "Fed into ShuffleOrderOverride.customOrderPrefabs for the initial shuffle. " +
	         "Avoid using the same prefab in both decks (matching is faction-agnostic).")]
	public List<GameObject> tutorialRevealOrder = new List<GameObject>();

	[Header("HP")]
	public int tutorialPlayerHP = 20;
	public int tutorialEnemyHP = 10;

	private DeckSO _cachedPlayerDeck;
	private DeckSO _cachedEnemyDeck;

	private void Awake()
	{
		Me = this;
	}

	/// <summary>
	/// Wired to PhaseManager.onGameStart. Activates the tutorial on first launch only.
	/// </summary>
	public void CheckTutorialOnGameStart()
	{
		if (PlayerPrefs.GetInt(TutorialCompletedKey, 0) == 1) return;
		if (tutorialPlayerDeck == null || tutorialEnemyDeck == null)
		{
			Debug.LogWarning("[TutorialManager] Tutorial decks not assigned; skipping tutorial.");
			return;
		}
		IsTutorialActive = true;
	}

	/// <summary>
	/// Wired to PhaseManager.onEnterCombatPhase, ordered LAST (after CombatManager and DeckSaver).
	/// Swaps in the tutorial decks, applies custom HP, and fixes the reveal order.
	/// Runs before CombatManager.Update reaches GatherDecks, so the swap is safe.
	/// </summary>
	public void SetupTutorialCombat()
	{
		if (!IsTutorialActive) return;
		var cm = CombatManager.Me;
		if (cm == null) return;

		_cachedPlayerDeck = cm.playerDeck;
		_cachedEnemyDeck = cm.enemyDeck;
		cm.playerDeck = tutorialPlayerDeck;
		cm.enemyDeck = tutorialEnemyDeck;

		if (cm.ownerPlayerStatusRef != null)
		{
			cm.ownerPlayerStatusRef.hp = tutorialPlayerHP;
			cm.ownerPlayerStatusRef.hpMax = tutorialPlayerHP;
		}
		if (cm.enemyPlayerStatusRef != null)
		{
			cm.enemyPlayerStatusRef.hp = tutorialEnemyHP;
			cm.enemyPlayerStatusRef.hpMax = tutorialEnemyHP;
		}

		var shuffleOverride = cm.GetComponent<ShuffleOrderOverride>();
		if (shuffleOverride == null)
			shuffleOverride = cm.gameObject.AddComponent<ShuffleOrderOverride>();
		shuffleOverride.useCustomOrder = true;
		shuffleOverride.customOrderPrefabs = new List<GameObject>(tutorialRevealOrder);
	}

	/// <summary>
	/// Called by PhaseManager when the tutorial combat finishes. Persists the completion
	/// flag, restores the real decks, and disables the order override.
	/// </summary>
	public void EndTutorial()
	{
		if (!IsTutorialActive) return;

		PlayerPrefs.SetInt(TutorialCompletedKey, 1);
		PlayerPrefs.Save();

		var cm = CombatManager.Me;
		if (cm != null)
		{
			if (_cachedPlayerDeck != null) cm.playerDeck = _cachedPlayerDeck;
			if (_cachedEnemyDeck != null) cm.enemyDeck = _cachedEnemyDeck;
			if (cm.playerDeck != null) cm.playerDeck.ResetToDefault();

			var shuffleOverride = cm.GetComponent<ShuffleOrderOverride>();
			if (shuffleOverride != null)
			{
				shuffleOverride.useCustomOrder = false;
				shuffleOverride.customOrderPrefabs = null;
			}
		}

		_cachedPlayerDeck = null;
		_cachedEnemyDeck = null;
		IsTutorialActive = false;
	}

	[ContextMenu("Reset Tutorial Flag")]
	private void ResetTutorialFlag()
	{
		PlayerPrefs.DeleteKey(TutorialCompletedKey);
		PlayerPrefs.Save();
		Debug.Log("[TutorialManager] Tutorial flag reset; tutorial will run on next launch.");
	}
}
