using UnityEngine;

/// <summary>
/// Shows the PlayerIcon / EnemyIcon HUD elements only during the Combat phase.
/// Follows the same GamePhase-polled SetActive convention as CombatHPBarPresenter
/// and HPNumericDisplay. Pure presentation; no game-logic changes.
/// </summary>
public class CombatIconPresenter : MonoBehaviour
{
	[Header("Wiring")]
	public GameObject playerIcon;
	public GameObject enemyIcon;
	public GamePhaseSO gamePhaseRef;

	private bool _wasInCombat;

	private void Awake()
	{
		if (playerIcon == null || enemyIcon == null || gamePhaseRef == null)
		{
			Debug.LogError("[CombatIconPresenter] Missing serialized reference(s), disabling.");
			enabled = false;
			return;
		}
		// VISUAL-FIX(2026-07-22): Player/Enemy icons stay visible outside the Combat phase
		//   Cause:    PlayerIcon/EnemyIcon were scene-only objects with no script ever
		//             toggling them, so they rendered during Shop/Result phases too.
		//   Affects:  PlayerIcon, EnemyIcon under Combat Canvas
		//   Regress:  Enter Shop phase: both icons must be inactive; re-enter Combat:
		//             both icons must reappear at their anchored positions.
		playerIcon.SetActive(false);
		enemyIcon.SetActive(false);
	}

	private void Update()
	{
		bool inCombat = gamePhaseRef.Value() == EnumStorage.GamePhase.Combat;
		if (inCombat && !_wasInCombat)
		{
			EnterCombat();
		}
		else if (!inCombat && _wasInCombat)
		{
			ExitCombat();
		}
		_wasInCombat = inCombat;
	}

	private void EnterCombat()
	{
		playerIcon.SetActive(true);
		enemyIcon.SetActive(true);
	}

	private void ExitCombat()
	{
		playerIcon.SetActive(false);
		enemyIcon.SetActive(false);
	}
}
