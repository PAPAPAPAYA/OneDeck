using System;
using UnityEngine;
// used to store an int
[CreateAssetMenu(fileName = "IntSO", menuName = "SORefs/IntSO")]
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
