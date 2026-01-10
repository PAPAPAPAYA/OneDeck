using System;
using UnityEngine;

// used to store player status
[CreateAssetMenu(fileName = "PlayerStatusSO", menuName = "SORefs/PlayerStatusSO")]
public class PlayerStatusSO : ScriptableObject
{
        public int hp;
        public int hpMax;
        [Header("DEFAULT VALUES")] 
        public int hpOg;
        public int hpMaxOg;

        private void OnEnable()
        {
                Reset();
        }

        public void Reset()
        {
                hp = hpOg;
                hpMax = hpMaxOg;
        }
}
