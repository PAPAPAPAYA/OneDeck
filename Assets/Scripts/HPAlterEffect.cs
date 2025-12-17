using UnityEngine;

public class HPAlterEffect : MonoBehaviour
{
        public void AlterHP(int HPAlterAmount)
        {
                CombatManager.instance.playerHP +=  HPAlterAmount;
        }
}
