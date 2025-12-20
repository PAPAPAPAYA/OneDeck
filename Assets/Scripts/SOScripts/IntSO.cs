using System;
using UnityEngine;

namespace SOScripts
{
        [CreateAssetMenu]
        public class IntSO : ScriptableObject
        {
                public int value;
                public int valueOg;
                public bool resetOnStart;
                private void OnEnable()
                {
                        if (resetOnStart) value = valueOg;
                }
        }
}