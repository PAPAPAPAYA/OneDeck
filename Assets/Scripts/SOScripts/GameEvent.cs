using System.Collections.Generic;
using DefaultNamespace;
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
	}

	public void RegisterListener(GameEventListener listener)
	{
		_listeners.Add(listener);
	}

	public void UnregisterListener(GameEventListener listener)
	{
		_listeners.Remove(listener);
	}
}