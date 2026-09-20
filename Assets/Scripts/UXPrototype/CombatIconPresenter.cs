using TMPro;
using UnityEngine;

/// <summary>
/// Shows the PlayerIcon / EnemyIcon HUD elements per game phase.
/// Player icon: Combat AND Shop (2026-09-19: the shop top bar reuses the combat
/// avatar + username block, repositioned via ShopTopBarLayout; the combat anchor
/// and scale are captured at Awake and restored on combat entry).
/// Enemy icon: Combat only.
/// Same GamePhase-polled SetActive convention as CombatHPBarPresenter and
/// HPNumericDisplay. Pure presentation; no game-logic changes.
/// Name label under the icon shows PlayerIdentity.Username ("???" when unset).
/// The label is a child of the icon, so the phase toggles hide it with the parent;
/// the text re-polls per frame with a diff guard, matching the polling convention.
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

	private const string UnknownName = "???";

	private EnumStorage.GamePhase _lastPhase = EnumStorage.GamePhase.Result;
	private string _lastPlayerName;
	private string _lastEnemyName;

	private RectTransform _playerIconRt;
	private Canvas _canvas;
	private Vector2 _combatAnchoredPos;
	private Vector3 _combatScale;

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
		//   Affects:  PlayerIcon/EnemyIcon under Combat Canvas
		//   Regress:  Enter Shop phase: ENEMY icon must be inactive (the PLAYER icon now
		//             intentionally stays, repositioned to the shop top bar); re-enter
		//             Combat: both icons at their original anchored positions/scale.
		playerIcon.SetActive(false);
		enemyIcon.SetActive(false);
		_playerIconRt = playerIcon.transform as RectTransform;
		if (_playerIconRt != null)
		{
			_combatAnchoredPos = _playerIconRt.anchoredPosition;
			_combatScale = _playerIconRt.localScale;
		}
		_canvas = playerIcon.GetComponentInParent<Canvas>();
		// Combat/shop input is click-driven: no label graphic may intercept raycasts.
		if (playerNameLabel != null) playerNameLabel.raycastTarget = false;
		if (enemyNameLabel != null) enemyNameLabel.raycastTarget = false;
	}

	private void Update()
	{
		EnumStorage.GamePhase phase = gamePhaseRef.Value();
		if (phase != _lastPhase)
		{
			ApplyPhase(phase);
			_lastPhase = phase;
		}
		if (phase == EnumStorage.GamePhase.Combat)
		{
			RefreshPlayerNameLabel();
			RefreshEnemyNameLabel();
		}
		else if (phase == EnumStorage.GamePhase.Shop)
		{
			RefreshPlayerNameLabel();
		}
	}

	private void ApplyPhase(EnumStorage.GamePhase phase)
	{
		bool inCombat = phase == EnumStorage.GamePhase.Combat;
		bool inShop = phase == EnumStorage.GamePhase.Shop;
		playerIcon.SetActive(inCombat || inShop);
		enemyIcon.SetActive(inCombat);
		if (_playerIconRt == null)
		{
			return;
		}
		if (inShop)
		{
			_playerIconRt.anchoredPosition = ShopTopBarLayout.ViewportToCanvasAnchored(ShopTopBarLayout.PlayerIconViewport, _canvas);
			_playerIconRt.localScale = Vector3.one * ShopTopBarLayout.PlayerIconShopScale;
		}
		else
		{
			_playerIconRt.anchoredPosition = _combatAnchoredPos;
			_playerIconRt.localScale = _combatScale;
		}
	}

	// Diff-guarded writes so the TMP mesh only rebuilds on an actual change; the
	// enemy name can arrive shortly after the phase switch (ghost deck injection),
	// so polling here instead of a one-shot entry write is the safe pattern.
	private void RefreshPlayerNameLabel()
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
	}

	private void RefreshEnemyNameLabel()
	{
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
}
