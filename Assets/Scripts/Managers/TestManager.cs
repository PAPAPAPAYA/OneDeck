using System.Collections.Generic;
using TestWriteRead;
using UnityEngine;

namespace DefaultNamespace.Managers
{
	/// <summary>
	/// Central panel of independent test toggles.
	/// Each toggle pushes its own flag to the target system: shuffle order override,
	/// debug enemy deck loader, and combat auto-reveal.
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

		[Header("Test Toggles (independent)")]
		[Tooltip("Start-card shuffle uses ShuffleOrderOverride.customOrderPrefabs instead of real shuffle.")]
		public bool overrideShuffleOrder;

		[Tooltip("Enemy deck always uses DeckSaver.debugEnemyDeck (bypasses JSON save and default pool).")]
		public bool useTestEnemyDeck;

		[Tooltip("Combat auto-reveal. Uncheck to reveal cards by manual click only.")]
		public bool autoReveal;

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
		StatusEffectDisplay,
		DamageFloater,
		ShopFlow,
		Uncategorized
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

		[Tooltip("Log damage floater messages from DamageFloaterPresenter.")]
		public bool logDamageFloater = true;

		[Tooltip("Log shop flow messages from ShopManager, ShopUXManager, and PhaseManager shop transitions.")]
		public bool logShopFlow = true;

		[Tooltip("Log messages whose tag is not recognized by InferCategory (new or untagged logs).")]
		public bool logUncategorized = true;

		#endregion

		private void Start()
		{
			ResolveReferences();
			ApplyTestToggles();
		}

		private void OnValidate()
		{
			// Sync target flags immediately whenever a toggle changes in the Inspector.
			ResolveReferences();
			ApplyTestToggles();

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
		/// Push each independent test toggle to its registered target system.
		/// </summary>
		private void ApplyTestToggles()
		{
			if (shuffleOrderOverride != null)
			{
				shuffleOrderOverride.useCustomOrder = overrideShuffleOrder;
			}

			if (deckSaver != null)
			{
				deckSaver.useDebugEnemyDeck = useTestEnemyDeck;
			}

			if (combatManager != null)
			{
				combatManager.autoReveal = autoReveal;
			}

			TestManager.Log("[TestManager] Toggles - shuffleOverride=" + (overrideShuffleOrder ? "ON" : "OFF")
				+ " testEnemyDeck=" + (useTestEnemyDeck ? "ON" : "OFF")
				+ " autoReveal=" + (autoReveal ? "ON" : "OFF"));
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
				// Edit-mode / pre-Awake logs: resolve the scene instance so the switches still apply.
				Me = Object.FindFirstObjectByType<TestManager>();
				if (Me == null)
				{
					ForwardToUnity(message, context, logType);
					return;
				}
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

#if UNITY_EDITOR
		private static readonly HashSet<string> _warnedUnrecognizedTags = new HashSet<string>();
#endif

		private static LogCategory InferCategory(string message)
		{
			if (message.Contains("[CombatManager]") || message.Contains("[PhaseManager]"))
			{
				return LogCategory.CombatFlow;
			}
			if (message.Contains("[ShopButton]"))
			{
				return LogCategory.ShopFlow;
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
			if (message.Contains("[CombatUXManager]") || message.Contains("[CardPhysObjScript]") ||
			    message.Contains("[Hover]"))
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
			if (message.Contains("[StatusEffectDisplay]") || message.Contains("[TagDisplay]"))
			{
				return LogCategory.StatusEffectDisplay;
			}
			if (message.Contains("[DamageFloater]"))
			{
				return LogCategory.DamageFloater;
			}
			WarnUnrecognizedTag(message);
			return LogCategory.Uncategorized;
		}

		/// <summary>
		/// Editor-only tripwire: warn once per unrecognized [Tag] prefix so new logs
		/// cannot silently fall into the Uncategorized bucket.
		/// </summary>
		private static void WarnUnrecognizedTag(string message)
		{
#if UNITY_EDITOR
			int open = message.IndexOf('[');
			if (open < 0) return;
			int close = message.IndexOf(']', open + 1);
			if (close < 0) return;
			string tag = message.Substring(open + 1, close - open - 1);
			if (!_warnedUnrecognizedTags.Add(tag)) return;
			Debug.LogWarning("TestManager: unrecognized log tag '[" + tag + "]' routed to Uncategorized — add it to InferCategory if it should have its own switch.");
#endif
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
				case LogCategory.DamageFloater: return Me.logDamageFloater;
				case LogCategory.ShopFlow: return Me.logShopFlow;
				case LogCategory.Uncategorized: return Me.logUncategorized;
				default: return true;
			}
		}

		#endregion
	}
}
