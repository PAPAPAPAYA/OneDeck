using System;
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
		InvokeOnGameStartEvent();
		ExitingCombatPhase();
		ExitingResultPhase();
		EnteringShopPhase();
	}

	private void Update()
	{
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
					print("draw");
					_resultText = "DRAW";
				}
				else
				{
					print("you lose");
					_resultText = "LOSE";
					hearts.value--;
					DeckTester.me.deckBWins++;
					DeckTester.me.currentSessionAmount++;
					DeckTester.me.deckAHPs.Add(CombatManager.Me.ownerPlayerStatusRef.hp);
					DeckTester.me.deckBHPs.Add(CombatManager.Me.enemyPlayerStatusRef.hp);
					DeckTester.me.CalculateSessionAveDmg();
				}
			}
			else if (enemyStatusRef.hp <= 0)
			{
				print("you win");
				_resultText = "WIN";
				wins.value++;
				DeckTester.me.deckAWins++;
				DeckTester.me.currentSessionAmount++;
				DeckTester.me.deckAHPs.Add(CombatManager.Me.ownerPlayerStatusRef.hp);
				DeckTester.me.deckBHPs.Add(CombatManager.Me.enemyPlayerStatusRef.hp);
				DeckTester.me.CalculateSessionAveDmg();
			}
			sessionNum.value++;
			ExitingCombatPhase();
			EnteringResultPhase();
		}
		else if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Result) // if in result phase
		{
			ShowResult();
			if (!Input.GetKeyDown(KeyCode.Space) && !DeckTester.me.autoSpace) return;
			ExitingResultPhase();
			EnteringShopPhase();
		}
	}

	private void ShowResult()
	{
		resultInfoDisplay.text = _resultText +
		                         "\nYour Wins: " + wins.value + "/" + winCon.value +
		                         "\nYour Hearts: " + hearts.value + "/" + heartMax.value +
		                         "\n\npress SPACE to continue";
	}

	#region entering and exiting funcs
	#region combat phase
	private void EnteringCombatPhase()
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
		playerStatusRef.Reset();
		enemyStatusRef.Reset();
		InvokeEnterShopPhaseEvent();
		
		// change phase
		currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Shop;
	}

	private void ExitingShopPhase()
	{
		InvokeExitShopPhaseEvent();
	}
	#endregion
	#endregion
}