using System;
using UnityEngine;
// used to store an int
[CreateAssetMenu]
public class IntSO : ScriptableObject
{
        public int value;
        public int valueOg;
        public bool resetOnStart;
        [TextArea]
        public string description;
        private void OnEnable()
        {
                if (resetOnStart) value = valueOg;
        }
}
