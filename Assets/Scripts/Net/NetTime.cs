using System;

/// <summary>
/// Timestamp source for net-layer uploads (2026-09-13): Beijing wall clock (UTC+8)
/// with an explicit +08:00 suffix so stored timestamps stay self-describing and
/// lexicographically sortable. Fixed offset on purpose: China has no DST, and
/// TimeZoneInfo Windows IDs ("China Standard Time") are not portable to
/// non-Windows player builds.
/// </summary>
public static class NetTime
{
	public static string NowIsoCst8()
	{
		return DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd'T'HH:mm:ss.fff'+08:00'");
	}
}
