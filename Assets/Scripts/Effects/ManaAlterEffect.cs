using System;
using UnityEngine;

public class ManaAlterEffect : MonoBehaviour
{
        #region GET MY CARD SCRIPT
        private CardScript _myCardScript;
        private void OnEnable()
        {
                _myCardScript = GetComponent<CardScript>();
        }
        #endregion
        public void AlterMyMana(int manaAlterAmount)
        {
                _myCardScript.myStatusRef.mana +=  manaAlterAmount;
        }
}
