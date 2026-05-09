using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using UnityEngine;

[CreateAssetMenu]
public class GameEvent : ScriptableObject
{
	private List<GameEventListener> _listeners = new List<GameEventListener>();

	public void Raise()
	{
		// GameEvent Raise
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaise());
			return;
		}
		ExecuteRaise();
	}

	// used for events with specific player (owner) ex. OnMeTookDmg
	public void RaiseOwner()
	{
		// GameEvent RaiseOwner
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaiseOwner());
			return;
		}
		ExecuteRaiseOwner();
	}

	// used for events with specific player (opponent) ex. OnMeTookDmg
	public void RaiseOpponent()
	{
		// GameEvent RaiseOpponent
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaiseOpponent());
			return;
		}
		ExecuteRaiseOpponent();
	}

	public void RaiseSpecific(GameObject target) // will also raise target's children's events
	{
		if (target == null) return;
		// GameEvent RaiseSpecific
		var tracker = AnimationStateTracker.me;
		if (tracker != null)
		{
			tracker.TryExecute(() => ExecuteRaiseSpecific(target));
			return;
		}
		ExecuteRaiseSpecific(target);
	}

	// --- Internal execution methods ---

	private void ExecuteRaise()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			_listeners[i].OnEventRaised();
		}
	}

	private void ExecuteRaiseOwner()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			if (_listeners[i].GetComponent<CardScript>().myStatusRef == CombatManager.Me.ownerPlayerStatusRef)
			{
				_listeners[i].OnEventRaised();
			}
		}
	}

	private void ExecuteRaiseOpponent()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			if (_listeners[i].GetComponent<CardScript>().myStatusRef == CombatManager.Me.enemyPlayerStatusRef)
			{
				_listeners[i].OnEventRaised();
			}
		}
	}

	private void ExecuteRaiseSpecific(GameObject target)
	{
		var listeners = target.GetComponentsInChildren<GameEventListener>();
		foreach (var listenerFromParentOrChild in listeners)
		{
			if (_listeners.Contains(listenerFromParentOrChild))
			{
				listenerFromParentOrChild.OnEventRaised();
			}
		}
	}

	public void RegisterListener(GameEventListener listener)
	{
		_listeners.Add(listener);
	}

	public void UnregisterListener(GameEventListener listener)
	{
		if (_listeners != null)
		{
			_listeners.Remove(listener);
		}
	}

	public int ReturnAmountOfListeners()
	{
		return _listeners.Count;
	}
}
