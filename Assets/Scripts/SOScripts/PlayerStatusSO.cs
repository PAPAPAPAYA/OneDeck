using System;
using UnityEngine;

// used to store player status
[CreateAssetMenu(fileName = "PlayerStatusSO", menuName = "SORefs/PlayerStatusSO")]
public class PlayerStatusSO : ScriptableObject
{
        public int hp;
        public int hpMax;
        public int shield;
        [Header("DEFAULT VALUES")] 
        public int hpOg;
        public int hpMaxOg;
        public int shieldOg;

        private void OnEnable()
        {
                Reset();
        }

        public void Reset()
        {
                hp = hpOg;
                hpMax = hpMaxOg;
                shield = shieldOg;
        }
}
