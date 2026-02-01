using System;
using UnityEngine;
using UnityEngine.Events;

namespace DefaultNamespace
{
	public class GameEventListener : MonoBehaviour
	{
		[Tooltip("register to which game event SO?")]
		public GameEvent @event;
		[Tooltip("assign child effect object(s)")]
		public UnityEvent response = new UnityEvent();

		private void OnEnable()
		{
			if (@event == null)
			{
				Debug.LogError("Assign Game Event to GameEventListener");
			}
			@event.RegisterListener(this);
		}

		private void OnDisable()
		{
			@event.UnregisterListener(this);
		}

		public void OnEventRaised()
		{
			response?.Invoke();
		}
	}
}