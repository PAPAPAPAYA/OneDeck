using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

// control the overall flow of one session, currently dictating current phase is shop or combat
public class PhaseManager : MonoBehaviour
{
    [Header("Flow Refs")] public GamePhaseSO currentGamePhaseRef;
    public IntSO roundCurrent;
    public IntSO wins; // player needs a certain amount of wins to win the run
    public IntSO winCon;
    public IntSO hearts; // if player has no heart then the player loses the run
    public IntSO heartMax;

    [Header("Status Refs")] public PlayerStatusSO playerStatusRef;
    public PlayerStatusSO enemyStatusRef;

    [Header("Phase Enter/Exit Events")] public UnityEvent onEnterCombatPhase;

    [Header("TMP Objects")] public TextMeshProUGUI resultInfoDisplay;
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

    private void OnEnable()
    {
        ExitingCombatPhase();
        ExitingResultPhase();
        EnteringShopPhase();
    }

    private void Update()
    {
        if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Shop)
        {
            if (!Input.GetKeyDown(KeyCode.Space)) return;
            ExitingShopPhase();
            EnteringCombatPhase();
        }
        else if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Combat)
        {
            if (playerStatusRef.hp <= 0)
            {
                print("you lose");
                hearts.value--;
                ExitingCombatPhase();
                EnteringResultPhase();
            }
            else if (enemyStatusRef.hp <= 0)
            {
                print("you win");
                wins.value++;
                ExitingCombatPhase();
                EnteringResultPhase();
            }
        }
        else if (currentGamePhaseRef.Value() == EnumStorage.GamePhase.Result)
        {
            ShowResult();
            if (!Input.GetKeyDown(KeyCode.Space)) return;
            print("entering shop");
            ExitingResultPhase();
            EnteringShopPhase();
        }
    }

    private void ShowResult()
    {
        resultInfoDisplay.text = "Your Wins: " + wins.value + "/" + winCon.value + 
                                 "\n" + "Your Hearts: " + hearts.value + "/" + heartMax.value +
                                 "\n\n" + "press SPACE to continue";
    }
    #region entering and exiting funcs
    private void EnteringCombatPhase()
    {
        InvokeEnterCombatPhaseEvent();
        currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Combat;
    }
    private void ExitingCombatPhase()
    {
        InvokeExitCombatPhaseEvent();
    }

    private void EnteringResultPhase()
    {
        InvokeEnterResultPhaseEvent();
        currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Result;
    }

    private void ExitingResultPhase()
    {
        InvokeExitResultPhaseEvent();
        resultInfoDisplay.text = "";
    }
    private void EnteringShopPhase()
    {
        playerStatusRef.Reset();
        enemyStatusRef.Reset();
        InvokeEnterShopPhaseEvent();
        currentGamePhaseRef.currentGamePhase = EnumStorage.GamePhase.Shop;
    }
    private void ExitingShopPhase()
    {
        InvokeExitShopPhaseEvent();
    }
    #endregion
}