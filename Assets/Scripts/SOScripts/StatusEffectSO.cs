using UnityEngine;

[CreateAssetMenu(fileName = "StatusEffectSO", menuName = "SORefs/StatusEffectSO")]
public class StatusEffectSO : ScriptableObject
{
	public EnumStorage.StatusEffect value;
	public EnumStorage.StatusEffect valueOg;
	public bool resetOnStart;
	[TextArea]
	public string description;

	private void OnEnable()
	{
		if (resetOnStart) value = valueOg;
	}
}
