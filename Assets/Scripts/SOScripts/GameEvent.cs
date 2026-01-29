using System.Collections.Generic;
using DefaultNamespace;
using DefaultNamespace.Effects;
using UnityEngine;

[CreateAssetMenu]
public class GameEvent : ScriptableObject
{
	private List<GameEventListener> _listeners;

	public void Raise()
	{
		for (var i = _listeners.Count - 1; i >= 0; i--)
		{
			_listeners[i].OnEventRaised();
		}
	}

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
		var listeners = target.GetComponentsInChildren<GameEventListener>();
		foreach (var listenerFromParentOrChild in listeners)
		{
			if (_listeners.Contains(listenerFromParentOrChild))
			{
				listenerFromParentOrChild.OnEventRaised();
			}
		}
		Debug.Log(name+": raised specific");
	}

	public void RegisterListener(GameEventListener listener)
	{
		_listeners.Add(listener);
	}

	public void UnregisterListener(GameEventListener listener)
	{
		_listeners.Remove(listener);
	}
	
	public int ReturnAmountOfListeners()
	{
		return _listeners.Count;
	}
}