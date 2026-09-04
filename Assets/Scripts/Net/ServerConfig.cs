using UnityEngine;

/// <summary>
/// Which deployment the client talks to. Local = repo dev server started with
/// DATA_DIR=data (plan §3.2); Production = the ECS box.
/// </summary>
public enum ServerEnvironment
{
	Local,
	Production
}

/// <summary>
/// One entry per event-shaped upload path. Stats snapshots are counter-shaped:
/// they upload latest full state directly (plan §2.7) and never queue in the outbox.
/// </summary>
public enum NetUploadKind
{
	DeckSnapshot,
	MatchReport,
	StatsSnapshot,
	RunRecord,
	CardCatalog
}

/// <summary>
/// Master switch panel for all server traffic (plans/plan-async-pvp-client-2026-09-03.md §3.1).
/// Asset goes to Assets/Resources/ServerConfig.asset; with no asset present everything
/// defaults to enabled=false, which is exactly the pre-networking single-player behavior.
/// </summary>
[CreateAssetMenu(fileName = "ServerConfig", menuName = "OneDeck/ServerConfig")]
public class ServerConfig : ScriptableObject
{
	public const string TestUsernamePrefix = "test_";

	[Header("Master")]
	public bool enabled;

	public ServerEnvironment environment = ServerEnvironment.Local;

	[Header("Endpoints")]
	public string localBaseUrl = "http://127.0.0.1:3000";

	public string productionBaseUrl = "http://8.153.150.197";

	[Header("Per-kind upload switches (off = never captured, never queued)")]
	public bool uploadDeckSnapshots = true;

	public bool uploadMatchReports = true;

	public bool uploadStatsSnapshots = true;

	public bool uploadRunRecords = true;

	public bool uploadCardCatalog = true;

	[Header("Opponent ghosts")]
	public bool fetchOpponentDecks = true;

	[Header("Test data")]
	[Tooltip("Prefixes the registered username with 'test_' so production rows can be cleaned by name.")]
	public bool markAsTest;

	private static ServerConfig active;

	/// <summary>
	/// Asset from Resources when present, otherwise a disabled in-memory default.
	/// Tests may inject an instance directly.
	/// </summary>
	public static ServerConfig Active
	{
		get
		{
			if (active == null)
			{
				active = Resources.Load<ServerConfig>("ServerConfig");
				if (active == null) active = ScriptableObject.CreateInstance<ServerConfig>();
			}
			return active;
		}
		set { active = value; }
	}

	public string BaseUrl
	{
		get { return (environment == ServerEnvironment.Local ? localBaseUrl : productionBaseUrl).TrimEnd('/'); }
	}

	private ServerEnvironment lastEnvironment;
	private bool lastEnabled;
	private bool changeTrackingInitialized;

	private void OnValidate()
	{
		// Environment / master switch flips must never let locally-captured payloads reach
		// production (or vice versa), so the pending queue is dropped on any such change.
		if (changeTrackingInitialized && (environment != lastEnvironment || enabled != lastEnabled))
		{
			UploadOutbox.DiscardAll();
			Debug.LogWarning("[ServerConfig] environment/enabled changed - outbox discarded to avoid cross-environment leakage.");
		}
		lastEnvironment = environment;
		lastEnabled = enabled;
		changeTrackingInitialized = true;
	}
}
