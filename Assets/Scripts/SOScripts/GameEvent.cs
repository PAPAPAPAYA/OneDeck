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
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			_listeners[i].OnEventRaised();
		}
	}

	// used for events with specific player (owner) ex. OnMeTookDmg
	public void RaiseOwner()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			if (_listeners[i].GetComponent<CardScript>().myStatusRef == CombatManager.Me.ownerPlayerStatusRef)
			{
				_listeners[i].OnEventRaised();
			}
		}
	}

	// used for events with specific player (opponent) ex. OnMeTookDmg
	public void RaiseOpponent()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			if (_listeners[i].GetComponent<CardScript>().myStatusRef == CombatManager.Me.enemyPlayerStatusRef)
			{
				_listeners[i].OnEventRaised();
			}
		}
	}
	
	public void RaiseSpecific(GameObject target) // will also raise target's children's events
	{
		if (target == null) return;
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