using TMPro;
using UnityEngine;

/// <summary>
/// Shows the PlayerIcon / EnemyIcon HUD elements only during the Combat phase.
/// Follows the same GamePhase-polled SetActive convention as CombatHPBarPresenter
/// and HPNumericDisplay. Pure presentation; no game-logic changes.
/// Optional name labels under each icon: player shows PlayerIdentity.Username,
/// enemy shows OpponentDeckCache.Current.username (both "???" when unset/absent).
/// Labels are children of the icons, so the phase toggles hide them with their
/// parent; the text re-polls per frame with a diff guard, matching the file's
/// polling convention.
/// </summary>
public class CombatIconPresenter : MonoBehaviour
{
	[Header("Wiring")]
	public GameObject playerIcon;
	public GameObject enemyIcon;
	public GamePhaseSO gamePhaseRef;

	[Header("Name labels (optional)")]
	public TMP_Text playerNameLabel;
	public TMP_Text enemyNameLabel;

	private bool _wasInCombat;
	private string _lastPlayerName;
	private string _lastEnemyName;

	private const string UnknownName = "???";

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
		// Combat input is click-driven: no label graphic may intercept raycasts.
		if (playerNameLabel != null) playerNameLabel.raycastTarget = false;
		if (enemyNameLabel != null) enemyNameLabel.raycastTarget = false;
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
		if (inCombat)
		{
			RefreshNameLabels();
		}
	}

	// Diff-guarded write so the TMP mesh only rebuilds on an actual change; the
	// enemy name can arrive shortly after the phase switch (ghost deck injection),
	// so polling here instead of a one-shot EnterCombat write is the safe pattern.
	private void RefreshNameLabels()
	{
		string playerName = PlayerIdentity.Username;
		if (string.IsNullOrEmpty(playerName))
		{
			playerName = UnknownName;
		}
		if (_lastPlayerName != playerName)
		{
			_lastPlayerName = playerName;
			if (playerNameLabel != null)
			{
				playerNameLabel.text = playerName;
				playerNameLabel.color = GameColorPalette.IconNameLabelColor;
			}
		}
		string enemyName = OpponentDeckCache.Current != null ? OpponentDeckCache.Current.username : null;
		if (string.IsNullOrEmpty(enemyName))
		{
			enemyName = UnknownName;
		}
		if (_lastEnemyName != enemyName)
		{
			_lastEnemyName = enemyName;
			if (enemyNameLabel != null)
			{
				enemyNameLabel.text = enemyName;
				enemyNameLabel.color = GameColorPalette.IconNameLabelColor;
			}
		}
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
