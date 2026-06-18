using System;
using System.Text;
using DefaultNamespace.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// control the overall flow of one session, currently dictating current phase is shop or combat
public class PhaseManager : MonoBehaviour
{
	[Header("Flow Refs")]
	public GamePhaseSO currentGamePhaseRef;
	public IntSO sessionNum;
	public BoolSO combatFinished;
	public IntSO wins; // player needs a certain amount of wins to win the run
	public IntSO winCon;
	public IntSO hearts; // if player has no heart then the player loses the run
	public IntSO heartMax;

	[Header("Status Refs")]
	public PlayerStatusSO playerStatusRef;
	public PlayerStatusSO enemyStatusRef;

	[Header("Run Reset Refs")]
	public DeckSO playerDeckRef;
	public IntSO purseRef;
	public IntSO playerDeckSizeRef;

	// Run-ending state
	private bool _isRunEnded;
	private string _endMessage = "";

	#region Enter/Exit Events
	[Header("Phase Enter/Exit Events")]
	public UnityEvent onEnterCombatPhase;
	private void InvokeEnterCombatPhaseEvent()
	{
		onEnterCombatPhase?.Invoke();
	}

	public UnityEvent onExitCombatPhase;
	private void InvokeExitCombatPhaseEvent()
	{
		onExitCombatPhase?.Invoke();
	}

	public UnityEvent onEnterShopPhase;
	private void InvokeEnterShopPhaseEvent()
	{
		onEnterShopPhase?.Invoke();
	}

	public UnityEvent onExitShopPhase;
	private void InvokeExitShopPhaseEvent()
	{
		onExitShopPhase?.Invoke();
	}

	public UnityEvent onEnterResultPhase;
	private void InvokeEnterResultPhaseEvent()
	{
		onEnterResultPhase?.Invoke();
	}

	public UnityEvent onExitResultPhase;
	private void InvokeExitResultPhaseEvent()
	{
		onExitResultPhase?.Invoke();
	}

	public UnityEvent onGameStart;
	private void InvokeOnGameStartEvent()
	{
		onGameStart?.Invoke();
	}
	#endregion

	[Header("UI")]
	public TextMeshProUGUI resultInfoDisplay;
	private string _resultText;

	private void OnEnable()
	{
		_isRunEnded = false;
		_endMessage = "";
		InvokeOnGameStartEvent();
		ExitingCombatPhase();
		ExitingResultPhase();
		EnteringShopPhase();
	}

	private void Update()
	{
		// Press ESC to quit game
		if (Input.GetKeyDown(KeyCode.Escape))
		{
			Application.Quit();
#if UNITY_EDITOR
			UnityEditor.EditorApplication.isPlaying = false;
#endif
		}

		if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Shop) // if in shop phase
		{
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace) return;
			ExitingShopPhase();
			EnteringCombatPhase();
		}
		else if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat) // if in combat phase
		{
			if (!combatFinished.value) return;
			if (playerStatusRef.hp <= 0)
			{
				if (enemyStatusRef.hp <= 0)
				{
					// print("draw");
					_resultText = "DRAW";
					// Draws are not counted in statistics
				}
				else
				{
					// print("you lose");
					_resultText = "LOSE";
					hearts.value--;
					DeckTester.me.deckBWins++;
					DeckTester.me.currentSessionAmount++;
					DeckTester.me.deckAHPs.Add(CombatManager.Me.ownerPlayerStatusRef.hp);
					DeckTester.me.deckBHPs.Add(CombatManager.Me.enemyPlayerStatusRef.hp);
					DeckTester.me.CalculateSessionAveDmg();
					// Record player card loss
					TestWriteRead.CardWinRateTracker.Me?.RecordCombatResult(playerWon: false);
				}
			}
			else if (enemyStatusRef.hp <= 0)
			{
				// print("you win");
				_resultText = "WIN";
				wins.value++;
				DeckTester.me.deckAWins++;
				DeckTester.me.currentSessionAmount++;
				DeckTester.me.deckAHPs.Add(CombatManager.Me.ownerPlayerStatusRef.hp);
				DeckTester.me.deckBHPs.Add(CombatManager.Me.enemyPlayerStatusRef.hp);
				DeckTester.me.CalculateSessionAveDmg();
				// Record player card win
				TestWriteRead.CardWinRateTracker.Me?.RecordCombatResult(playerWon: true);
			}
			// Check run-ending conditions after this combat's result is applied
			if (hearts.value <= 0)
			{
				_isRunEnded = true;
				_endMessage = "GAME OVER";
			}
			else if (wins.value >= winCon.value)
			{
				_isRunEnded = true;
				_endMessage = "CONGRATS";
			}

			ExitingCombatPhase();
			EnteringResultPhase();
		}
		else if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Result) // if in result phase
		{
			ShowResult();
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace && !Input.GetMouseButtonDown(0)) return;
			ExitingResultPhase();
			sessionNum.value++;
			if (_isRunEnded)
			{
				ResetRun();
			}
			EnteringShopPhase();
		}
	}

	private void ShowResult()
	{
		var sb = new StringBuilder();
		sb.AppendLine(_resultText);
		sb.AppendLine("Your Wins: " + wins.value + "/" + winCon.value);
		sb.AppendLine("Your Hearts: " + hearts.value + "/" + heartMax.value);

		if (_isRunEnded)
		{
			sb.AppendLine("");
			sb.AppendLine("===== " + _endMessage + " =====");
			sb.AppendLine("");

			var winRateReport = TestWriteRead.CardWinRateTracker.Me?.GetAllStatsReportString();
			if (!string.IsNullOrEmpty(winRateReport))
			{
				sb.AppendLine(winRateReport);
				sb.AppendLine("");
			}

			var shopReport = ShopStatsManager.Me?.GetAllStatsReportString();
			if (!string.IsNullOrEmpty(shopReport))
			{
				sb.AppendLine(shopReport);
				sb.AppendLine("");
			}

			sb.AppendLine("TAP / SPACE to start a new run");
		}
		else
		{
			sb.AppendLine("");
			sb.AppendLine("TAP / SPACE to continue");
		}

		resultInfoDisplay.text = sb.ToString();
	}

	/// <summary>
	/// Resets all run-level state so the player can start a fresh run.
	/// CardWinRateTracker and ShopStatsManager data are intentionally preserved.
	/// </summary>
	private void ResetRun()
	{
		// Reset core IntSO/BoolSO refs to their original values
		hearts?.ResetToDefault();
		heartMax?.ResetToDefault();
		wins?.ResetToDefault();
		sessionNum?.ResetToDefault();
		purseRef?.ResetToDefault();
		playerDeckSizeRef?.ResetToDefault();
		combatFinished?.ResetToDefault();

		// Reset player deck back to its default deck
		playerDeckRef?.ResetToDefault();

		// Reset player/enemy status
		playerStatusRef?.ResetToDefault();
		playerStatusRef?.ResetHpMax();
		enemyStatusRef?.ResetToDefault();
		enemyStatusRef?.ResetHpMax();

		// Reset combat manager counters
		if (CombatManager.Me != null)
		{
			CombatManager.Me.roundNumRef?.ResetToDefault();
			CombatManager.Me.totalCardsRevealed = 0;
			CombatManager.Me.cardsRevealedThisRound = 0;
		}

		// Reset effect chain manager session counter
		if (EffectChainManager.Me != null)
		{
			EffectChainManager.Me.sessionNumberRef?.ResetToDefault();
			EffectChainManager.Me.chainNumber = 0;
		}

		// Reset value trackers
		if (ValueTrackerManager.me != null)
		{
			ValueTrackerManager.me.friendlyInGraveAmountRef?.ResetToDefault();
			ValueTrackerManager.me.enemyCursePowerCount?.ResetToDefault();
			ValueTrackerManager.me.ownerCursePowerCount?.ResetToDefault();
			ValueTrackerManager.me.totalPowerCountInDeckRef?.ResetToDefault();
			ValueTrackerManager.me.ownerCardCountInDeckRef?.ResetToDefault();
			ValueTrackerManager.me.enemyCardCountInDeckRef?.ResetToDefault();
			ValueTrackerManager.me.ownerCardsBuriedCountRef?.ResetToDefault();
			ValueTrackerManager.me.enemyCardsBuriedCountRef?.ResetToDefault();
			ValueTrackerManager.me.stagedOwnerRef?.ResetToDefault();
			ValueTrackerManager.me.stagedEnemyRef?.ResetToDefault();
			ValueTrackerManager.me.lastAppliedStatusEffectAmountRef?.ResetToDefault();
		}

		// Reset manager flags so starting cards and first-combat rewards can be given again
		var startingCardManager = FindFirstObjectByType<StartingCardManager>(FindObjectsInactive.Include);
		startingCardManager?.ResetForNewRun();

		var combatStartCardGiver = FindFirstObjectByType<CombatStartCardGiver>(FindObjectsInactive.Include);
		combatStartCardGiver?.ResetTriggerState();

		_isRunEnded = false;
		_endMessage = "";
	}

	#region entering and exiting funcs
	#region combat phase
	public void EnteringCombatPhase()
	{
		InvokeEnterCombatPhaseEvent();
		// change phase
		currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
	}

	private void ExitingCombatPhase()
	{
		InvokeExitCombatPhaseEvent();
		CardIDRetriever.Me.ResetCardID();
	}
	#endregion
	#region result phase
	private void EnteringResultPhase()
	{
		InvokeEnterResultPhaseEvent();
		
		// change phase
		currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Result;
	}

	private void ExitingResultPhase()
	{
		InvokeExitResultPhaseEvent();
		resultInfoDisplay.text = "";
	}
	#endregion
	#region shop phase
	private void EnteringShopPhase()
	{
		playerStatusRef.ResetToDefault();
		enemyStatusRef.ResetToDefault();
		InvokeEnterShopPhaseEvent();
		
		// change phase
		currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Shop;
	}

	public void ExitingShopPhase()
	{
		InvokeExitShopPhaseEvent();
	}
	#endregion
	#endregion
}