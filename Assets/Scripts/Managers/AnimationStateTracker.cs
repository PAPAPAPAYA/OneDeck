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
		Debug.Log("[AnimationStateTracker] RegisterAnimation: pending=" + _pendingAnimations + " | frame=" + Time.frameCount);
	}

	/// <summary>
	/// Call when any animation completes.
	/// </summary>
	public void CompleteAnimation()
	{
		_pendingAnimations--;
		Debug.Log("[AnimationStateTracker] CompleteAnimation: pending=" + _pendingAnimations + " | delayedEvents=" + _delayedEvents.Count + " | frame=" + Time.frameCount);
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
			Debug.Log("[AnimationStateTracker] TryExecute: ENQUEUED (pending=" + _pendingAnimations + ", queue=" + _delayedEvents.Count + ")");
			return;
		}
		Debug.Log("[AnimationStateTracker] TryExecute: EXECUTE IMMEDIATELY (pending=" + _pendingAnimations + ")");
		action();
	}

	private void FlushDelayedEvents()
	{
		_isFlushing = true;
		int processed = 0;
		int initialCount = _delayedEvents.Count;
		Debug.Log("[AnimationStateTracker] FlushDelayedEvents START: queue=" + initialCount + " | frame=" + Time.frameCount);
		while (_delayedEvents.Count > 0)
		{
			var evt = _delayedEvents.Dequeue();
			evt();
			processed++;

			// If the executed event started new animations, stop flushing.
			// Those animations will trigger another flush when they complete.
			if (_pendingAnimations > 0)
			{
				Debug.Log("[AnimationStateTracker] FlushDelayedEvents BREAK after " + processed + "/" + initialCount + " events (pending=" + _pendingAnimations + ")");
				break;
			}
		}
		Debug.Log("[AnimationStateTracker] FlushDelayedEvents END: processed=" + processed + "/" + initialCount + ", remaining=" + _delayedEvents.Count + ", pending=" + _pendingAnimations);
		_isFlushing = false;
	}

	private void Update()
	{
		// Safety: force release if batch exceeds timeout
		if (_hasActiveBatch && Time.time - _batchStartTime > timeoutSeconds)
		{
			Debug.LogWarning(
				"[AnimationStateTracker] Animation batch timed out after " + timeoutSeconds +
				"s. Pending=" + _pendingAnimations + ". Forcing release.");
			_pendingAnimations = 0;
			_hasActiveBatch = false;
			FlushDelayedEvents();
		}
	}
}
