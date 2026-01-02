using System;
using UnityEngine;
using UnityEngine.Events;

namespace DefaultNamespace
{
    public class GameEventListener : MonoBehaviour
    {
        public GameEvent @event;
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
            response.Invoke();
        }
    }
}