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

		#region Log Switches

		public enum LogCategory
		{
			CombatFlow,
			EffectChains,
			AnimationPlayback,
			VisualSync,
			EditorTools,
			TestManager,
			DynamicDamageDisplay,
			StatusEffectDisplay
		}

		[Header("Log Switches")]
		[Tooltip("Log combat flow messages from CombatManager and PhaseManager.")]
		public bool logCombatFlow = true;

		[Tooltip("Log effect chain messages from EffectChainManager, BuryEffect, StageEffect, and ApplyStatusEffectCore.")]
		public bool logEffectChains = true;

		[Tooltip("Log animation playback messages from RecorderAnimationPlayer and AnimationStateTracker.")]
		public bool logAnimationPlayback = true;

		[Tooltip("Log visual/deck sync messages from CombatUXManager and CardPhysObjScript.")]
		public bool logVisualSync = true;

		[Tooltip("Log editor tool messages from EnemyDeckRecorder and CardTypeIDValidator.")]
		public bool logEditorTools = true;

		[Tooltip("Log TestManager internal messages.")]
		public bool logTestManager = true;

		[Tooltip("Log dynamic damage display messages from CardScript, HPAlterEffect, and CardPhysObjScript.")]
		public bool logDynamicDamageDisplay = true;

		[Tooltip("Log status effect display messages from CardPhysObjScript and CardScript display state.")]
		public bool logStatusEffectDisplay = true;

		#endregion

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

			TestManager.Log("[TestManager] Test mode " + (isTestMode ? "ENABLED" : "DISABLED"));
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

		#region Logging API

		public static void Log(object message)
		{
			LogInternal(message, null, LogType.Log);
		}

		public static void Log(object message, Object context)
		{
			LogInternal(message, context, LogType.Log);
		}

		public static void LogWarning(object message)
		{
			LogInternal(message, null, LogType.Warning);
		}

		public static void LogWarning(object message, Object context)
		{
			LogInternal(message, context, LogType.Warning);
		}

		public static void LogError(object message)
		{
			LogInternal(message, null, LogType.Error);
		}

		public static void LogError(object message, Object context)
		{
			LogInternal(message, context, LogType.Error);
		}

		private static void LogInternal(object message, Object context, LogType logType)
		{
			if (Me == null)
			{
				ForwardToUnity(message, context, logType);
				return;
			}

			LogCategory category = InferCategory(message?.ToString() ?? string.Empty);
			if (!IsEnabled(category))
			{
				return;
			}

			ForwardToUnity(message, context, logType);
		}

		private static void ForwardToUnity(object message, Object context, LogType logType)
		{
			bool hasContext = context != null;
			switch (logType)
			{
				case LogType.Warning:
					if (hasContext) Debug.LogWarning(message, context);
					else Debug.LogWarning(message);
					break;
				case LogType.Error:
					if (hasContext) Debug.LogError(message, context);
					else Debug.LogError(message);
					break;
				default:
					if (hasContext) Debug.Log(message, context);
					else Debug.Log(message);
					break;
			}
		}

		private static LogCategory InferCategory(string message)
		{
			if (message.Contains("[CombatManager]") || message.Contains("[PhaseManager]"))
			{
				return LogCategory.CombatFlow;
			}
			if (message.Contains("[EffectChainManager]") || message.Contains("[BuryEffect]") ||
			    message.Contains("[StageEffect]") || message.Contains("[ApplyStatusEffectCore]"))
			{
				return LogCategory.EffectChains;
			}
			if (message.Contains("[RecorderAnimationPlayer]") || message.Contains("[AnimationStateTracker]"))
			{
				return LogCategory.AnimationPlayback;
			}
			if (message.Contains("[CombatUXManager]") || message.Contains("[CardPhysObjScript]"))
			{
				return LogCategory.VisualSync;
			}
			if (message.Contains("[EnemyDeckRecorder]") || message.Contains("[CardTypeIDValidator]"))
			{
				return LogCategory.EditorTools;
			}
			if (message.Contains("[TestManager]"))
			{
				return LogCategory.TestManager;
			}
			if (message.Contains("[DynamicDamageDisplay]"))
			{
				return LogCategory.DynamicDamageDisplay;
			}
			if (message.Contains("[StatusEffectDisplay]"))
			{
				return LogCategory.StatusEffectDisplay;
			}
			return LogCategory.CombatFlow;
		}

		private static bool IsEnabled(LogCategory category)
		{
			switch (category)
			{
				case LogCategory.CombatFlow: return Me.logCombatFlow;
				case LogCategory.EffectChains: return Me.logEffectChains;
				case LogCategory.AnimationPlayback: return Me.logAnimationPlayback;
				case LogCategory.VisualSync: return Me.logVisualSync;
				case LogCategory.EditorTools: return Me.logEditorTools;
				case LogCategory.TestManager: return Me.logTestManager;
				case LogCategory.DynamicDamageDisplay: return Me.logDynamicDamageDisplay;
				case LogCategory.StatusEffectDisplay: return Me.logStatusEffectDisplay;
				default: return true;
			}
		}

		#endregion
	}
}
