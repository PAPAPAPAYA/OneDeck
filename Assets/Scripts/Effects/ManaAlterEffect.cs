using System;
using SOScripts;
using UnityEngine;

public class ManaAlterEffect : MonoBehaviour
{
        private CardScript _myCardScript;
        private void OnEnable()
        {
                _myCardScript = GetComponent<CardScript>();
        }
        public void AlterMyMana(int manaAlterAmount)
        {
                _myCardScript.myStatusRef.mana +=  manaAlterAmount;
        }
}
