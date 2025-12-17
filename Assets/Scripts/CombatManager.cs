using System;
using UnityEngine;

// this script functions as a variable storage in combat
public class CombatManager : MonoBehaviour
{
        public static CombatManager instance;
        private void Awake()
        {
                instance = this;
        }
        public int playerMana;
        public int playerHP;
}
