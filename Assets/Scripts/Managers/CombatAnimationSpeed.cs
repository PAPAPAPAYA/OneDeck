/// <summary>
/// Global combat animation speed scaler.
/// Only affects card animations that run during the Combat phase.
/// </summary>
public static class CombatAnimationSpeed
{
	/// <summary>
	/// Speed multiplier. 1 = normal speed, 2 = double speed (half duration), 0.5 = half speed (double duration).
	/// </summary>
	public static float SpeedScale { get; set; } = 1f;

	/// <summary>
	/// Scales a base animation duration by the current speed scale.
	/// </summary>	public static float ScaleDuration(float baseDuration)
	{
		if (SpeedScale <= 0.001f)
		{
			SpeedScale = 0.001f;
		}
		return baseDuration / SpeedScale;
	}
}
