using UnityEngine;

/// <summary>
/// Generic IntSO setter effect (4.0 step-5): sets the configured IntSO to a fixed value when
/// the bound event fires. Used by RELIC_GRAVE_LORD to arm its side's per-round grave-creature
/// aura on each shuffle (ValueTrackerManager.graveCreatureAura*ThisRoundRef, reset at round
/// start before afterShuffle re-arms it).
/// </summary>
public class ValueSetterEffect : EffectScript
{
	[Tooltip("IntSO to set")]
	public IntSO targetIntSO;
	[Tooltip("Value to write")]
	public int value = 1;

	public void SetIntSO()
	{
		if (targetIntSO != null)
		{
			targetIntSO.value = value;
		}
	}
}
