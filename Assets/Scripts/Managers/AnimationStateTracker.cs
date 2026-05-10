using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Global animation coordinator. When animations are playing, all GameEvent raises
/// are delayed until the current animation batch completes.
/// </summary>
public class AnimationStateTracker : MonoBehaviour
{
	#region SINGLETON
	public static AnimationStateTracker me;
	void Awake() { me = this; }
	#endregion

	[Header("SAFETY")]
	[Tooltip("Maximum seconds an animation batch can hold before forced release")]
	public float timeoutSeconds = 5f;

	private int _pendingAnimations;
	private Queue<Action> _delayedEvents = new Queue<Action>();
	private bool _isFlushing;
	private float _batchStartTime;
	private bool _hasActiveBatch;

	public int PendingAnimations => _pendingAnimations;
	public bool HasActiveBatch => _pendingAnimations > 0;

	/// <summary>
	/// Call when any animation starts.
	/// </summary>
	public void RegisterAnimation()
	{
		if (_pendingAnimations == 0)
		{
			_batchStartTime = Time.time;
			_hasActiveBatch = true;
		}
		_pendingAnimations++;
		// Animation registered
	}

	/// <summary>
	/// Call when any animation completes.
	/// </summary>
	public void CompleteAnimation()
	{
		_pendingAnimations--;
		// Animation completed
		if (_pendingAnimations <= 0)
		{
			_pendingAnimations = 0;
			_hasActiveBatch = false;
			if (!_isFlushing)
			{
				FlushDelayedEvents();
			}
		}
	}

	/// <summary>
	/// Attempts to execute an action immediately. If animations are playing,
	/// the action is queued for later execution.
	/// Called by GameEvent.Raise methods.
	/// </summary>
	public void TryExecute(Action action)
	{
		if (_pendingAnimations > 0)
		{
			_delayedEvents.Enqueue(action);
			Debug.Log("[FLUSH] Enqueued | pending=" + _pendingAnimations + " | queue=" + _delayedEvents.Count + " | frame=" + Time.frameCount);
			return;
		}
		// Executing immediately
		action();
	}

	private void FlushDelayedEvents()
	{
		_isFlushing = true;
		int processed = 0;
		int initialCount = _delayedEvents.Count;
		int openedChains = global::EffectChainManager.Me != null ? global::EffectChainManager.Me.openedEffectRecorders.Count : -1;
		int chainDepth = global::EffectChainManager.Me != null ? global::EffectChainManager.Me.chainDepth : -1;
		string parentChain = global::EffectChainManager.Me != null && global::EffectChainManager.Me.currentEffectRecorderParent != null ? global::EffectChainManager.Me.currentEffectRecorderParent.GetComponent<global::DefaultNamespace.EffectRecorder>().chainID.ToString() : "null";
		Debug.Log("[FLUSH] START | queue=" + initialCount + " | frame=" + Time.frameCount + " | openedChains=" + openedChains + " | depth=" + chainDepth + " | parent=" + parentChain);
		while (_delayedEvents.Count > 0)
		{
			var evt = _delayedEvents.Dequeue();
			openedChains = global::EffectChainManager.Me != null ? global::EffectChainManager.Me.openedEffectRecorders.Count : -1;
			chainDepth = global::EffectChainManager.Me != null ? global::EffectChainManager.Me.chainDepth : -1;
			parentChain = global::EffectChainManager.Me != null && global::EffectChainManager.Me.currentEffectRecorderParent != null ? global::EffectChainManager.Me.currentEffectRecorderParent.GetComponent<global::DefaultNamespace.EffectRecorder>().chainID.ToString() : "null";
			string openedDetail = "";
			if (global::EffectChainManager.Me != null)
			{
				foreach (var rec in global::EffectChainManager.Me.openedEffectRecorders)
				{
					var r = rec.GetComponent<global::DefaultNamespace.EffectRecorder>();
					openedDetail += "chain#" + r.chainID + "[" + r.cardObject.name + "/" + r.effectObject.name + "/proc=" + (r.processedEffectID ?? "null") + "];";
				}
			}
			// Debug.Log("[FLUSH] Executing | remaining=" + _delayedEvents.Count + " | frame=" + Time.frameCount + " | openedChains=" + openedChains + " | depth=" + chainDepth + " | parent=" + parentChain + " | openedDetail=" + openedDetail);
			evt();
			processed++;

			// If the executed event started new animations, stop flushing.
			// Those animations will trigger another flush when they complete.
			if (_pendingAnimations > 0)
			{
				Debug.Log("[FLUSH] BREAK after " + processed + "/" + initialCount + " | pending=" + _pendingAnimations + " | frame=" + Time.frameCount);
				break;
			}
		}
		openedChains = global::EffectChainManager.Me != null ? global::EffectChainManager.Me.openedEffectRecorders.Count : -1;
		chainDepth = global::EffectChainManager.Me != null ? global::EffectChainManager.Me.chainDepth : -1;
		parentChain = global::EffectChainManager.Me != null && global::EffectChainManager.Me.currentEffectRecorderParent != null ? global::EffectChainManager.Me.currentEffectRecorderParent.GetComponent<global::DefaultNamespace.EffectRecorder>().chainID.ToString() : "null";
		Debug.Log("[FLUSH] END | processed=" + processed + "/" + initialCount + " | remaining=" + _delayedEvents.Count + " | pending=" + _pendingAnimations + " | frame=" + Time.frameCount + " | openedChains=" + openedChains + " | depth=" + chainDepth + " | parent=" + parentChain);
		_isFlushing = false;
	}

	private void Update()
	{
		// Safety: force release if batch exceeds timeout
		if (_hasActiveBatch && Time.time - _batchStartTime > timeoutSeconds)
		{
			Debug.LogWarning(
				"[FLUSH] TIMEOUT after " + timeoutSeconds +
				"s | pending=" + _pendingAnimations + " | frame=" + Time.frameCount);
			_pendingAnimations = 0;
			_hasActiveBatch = false;
			FlushDelayedEvents();
		}
	}
}
