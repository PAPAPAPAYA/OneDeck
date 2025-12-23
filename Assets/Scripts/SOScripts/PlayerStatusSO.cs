using System;
using UnityEngine;

// used to store player status
[CreateAssetMenu]
public class PlayerStatusSO : ScriptableObject
{
        public int hp;
        public int mana;
        [Header("DEFAULT VALUES")] 
        public int hpOg;
        public int manaOg;

        private void OnEnable()
        {
                Reset();
        }

        public void Reset()
        {
                hp = hpOg;
                mana = manaOg;
        }
}
