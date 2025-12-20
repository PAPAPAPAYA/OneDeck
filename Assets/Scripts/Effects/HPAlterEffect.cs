using UnityEngine;

public class HPAlterEffect : MonoBehaviour
{
        #region GET MY CARD SCRIPT
        private CardScript _myCardScript;
        private void OnEnable()
        {
                _myCardScript = GetComponent<CardScript>();
        }
        #endregion
        public void AlterMyHP(int HPAlterAmount)
        {
                _myCardScript.myStatusRef.hp +=  HPAlterAmount;
        }
        public void AlterTheirHP(int HPAlterAmount)
        {
                _myCardScript.theirStatusRef.mana +=  HPAlterAmount;
        }
}
