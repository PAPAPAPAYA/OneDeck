using UnityEngine;

public class HpMaxAlterEffect : EffectScript
{
	public void IncreaseMyHpMax(int amount)
	{
		myCardScript.myStatusRef.hpMax += amount;
		myCardScript.myStatusRef.hp = myCardScript.myStatusRef.hpMax;
	}
}
