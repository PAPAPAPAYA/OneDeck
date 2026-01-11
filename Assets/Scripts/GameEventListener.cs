using System;
using UnityEngine;
using UnityEngine.Events;

namespace DefaultNamespace
{
	public class GameEventListener : MonoBehaviour
	{
		[Tooltip("register to which game event SO?")]
		public GameEvent @event;
		[Tooltip("assign a child effect object")]
		public UnityEvent response;

		private void OnEnable()
		{
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