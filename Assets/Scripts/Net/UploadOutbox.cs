using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Persistent send-queue for event-shaped uploads (plan §2.3): deck snapshots, match
/// reports, run records, catalog. Counter-shaped stats snapshots never queue here - they
/// upload their latest full state directly. Cap 100 items, overflow drops the oldest.
/// Flush triggers (game start / leaving the shop / run end) are wired by caller batches.
/// Per-kind switches are checked at enqueue time: a disabled kind is never captured.
/// </summary>
public static class UploadOutbox
{
	[Serializable]
	public class PendingRequest
	{
		public string kind;
		public string path;
		public string jsonPayload;
		public string enqueuedAt;
	}

	[Serializable]
	private class OutboxFile
	{
		public List<PendingRequest> items = new List<PendingRequest>();
	}

	public const int MaxItems = 100;

	/// <summary>Test seam: when set, overrides the persistentDataPath file location.</summary>
	public static string OverrideFilePathForTests;

	private const string FileName = "outbox.json";
	private static List<PendingRequest> cache;
	private static bool flushing;

	private static string FilePath
	{
		get { return OverrideFilePathForTests ?? Path.Combine(Application.persistentDataPath, FileName); }
	}

	public static int PendingCount { get { return Load().Count; } }

	/// <summary>Serialize dto and queue it for the kind's endpoint. No-op when the kind is switched off.</summary>
	public static void Enqueue(NetUploadKind kind, object dto)
	{
		if (!IsUploadEnabled(kind)) return;
		EnqueueRaw(kind.ToString(), EndpointFor(kind), JsonUtility.ToJson(dto));
	}

	public static void EnqueueRaw(string kind, string path, string jsonPayload)
	{
		List<PendingRequest> items = Load();
		items.Add(new PendingRequest
		{
			kind = kind,
			path = path,
			jsonPayload = jsonPayload,
			enqueuedAt = NetTime.NowIsoCst8()
		});
		while (items.Count > MaxItems) items.RemoveAt(0);
		Save(items);
	}

	/// <summary>Test seam: drops the in-memory cache so the next read reloads from disk.</summary>
	public static void ResetCacheForTests()
	{
		cache = null;
	}

	/// <summary>
	/// Test seam: when set, replaces the network send during a flush
	/// (path, jsonPayload, onOk, onFail) so tests can simulate any HTTP status without network.
	/// </summary>
	public static Action<string, string, Action<string>, Action<string, long>> SendOverrideForTests;

	/// <summary>
	/// Test seam: pumps one whole flush synchronously. Pair with SendOverrideForTests — a
	/// synchronously-completing send makes every callback fire inside MoveNext. Edit Mode has no
	/// coroutine pump, and Flush() would lazily create DeckNetworkClient in the active scene.
	/// </summary>
	public static void FlushSynchronouslyForTests()
	{
		IEnumerator routine = FlushCoroutine();
		while (routine.MoveNext()) { }
	}

	/// <summary>Clears the queue and deletes its file. Also the OnValidate path for environment flips.</summary>
	public static void DiscardAll()
	{
		cache = new List<PendingRequest>();
		try
		{
			if (File.Exists(FilePath)) File.Delete(FilePath);
		}
		catch (IOException e)
		{
			Debug.LogWarning("[UploadOutbox] discard failed: " + e.Message);
		}
	}

	/// <summary>
	/// Drain the queue via DeckNetworkClient. A permanent rejection (HTTP 4xx) drops the head
	/// and continues with the next entry; a transient failure (transport error, 5xx) stops the
	/// flush and keeps the failed head as next trigger's first item, so ordering is preserved.
	/// </summary>
	public static void Flush()
	{
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.enabled || flushing) return;
		if (Load().Count == 0) return;
		flushing = true;
		DeckNetworkClient.Me.StartCoroutine(FlushCoroutine());
	}

	private static IEnumerator FlushCoroutine()
	{
		try
		{
			List<PendingRequest> items = Load();
			while (items.Count > 0)
			{
				PendingRequest head = items[0];
				bool done = false;
				bool ok = false;
				long statusCode = 0;
				Action<string> onOk = (body) => { ok = true; done = true; };
				Action<string, long> onFail = (error, code) => { statusCode = code; done = true; };
				if (SendOverrideForTests != null)
					SendOverrideForTests(head.path, head.jsonPayload, onOk, onFail);
				else
					DeckNetworkClient.Me.PostJson(head.path, head.jsonPayload, onOk, onFail);
				while (!done) yield return null;
				if (!ok)
				{
					// 4xx = the server rejected the request itself (401 unknown_player /
					// 400 own_deck / 404 deck_not_found): retrying can never succeed, and
					// DeckNetworkClient already treats 4xx as final. Drop the poison head so
					// one bad entry cannot stall every later upload behind it (2026-09-20).
					if (statusCode >= 400 && statusCode < 500)
					{
						Debug.LogWarning("[UploadOutbox] dropping " + head.kind + " -> " + head.path
							+ ": permanent server rejection (HTTP " + statusCode + ")");
						items.RemoveAt(0);
						Save(items);
						continue;
					}
					yield break;
				}
				items.RemoveAt(0);
				Save(items);
			}
		}
		finally
		{
			flushing = false;
		}
	}

	public static bool IsUploadEnabled(NetUploadKind kind)
	{
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.enabled) return false;
		switch (kind)
		{
			case NetUploadKind.DeckSnapshot: return config.uploadDeckSnapshots;
			case NetUploadKind.MatchReport: return config.uploadMatchReports;
			case NetUploadKind.StatsSnapshot: return config.uploadStatsSnapshots;
			case NetUploadKind.RunRecord: return config.uploadRunRecords;
			case NetUploadKind.CardCatalog: return config.uploadCardCatalog;
			case NetUploadKind.LoopReport: return config.uploadLoopReports;
			default: return false;
		}
	}

	public static string EndpointFor(NetUploadKind kind)
	{
		switch (kind)
		{
			case NetUploadKind.DeckSnapshot: return "/api/decks";
			case NetUploadKind.MatchReport: return "/api/matches/report";
			case NetUploadKind.StatsSnapshot: return "/api/stats/snapshot";
			case NetUploadKind.RunRecord: return "/api/runs";
			case NetUploadKind.CardCatalog: return "/api/cards/catalog";
			case NetUploadKind.LoopReport: return "/api/loop-reports";
			default: return "/api/unknown";
		}
	}

	private static List<PendingRequest> Load()
	{
		if (cache != null) return cache;
		try
		{
			// Broad catch: a corrupt queue file must never break the game; worst case we
			// drop pending uploads (they are event-shaped telemetry, not player progress).
			if (File.Exists(FilePath))
			{
				OutboxFile file = JsonUtility.FromJson<OutboxFile>(File.ReadAllText(FilePath, Encoding.UTF8));
				cache = file != null && file.items != null ? file.items : new List<PendingRequest>();
			}
			else
			{
				cache = new List<PendingRequest>();
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[UploadOutbox] queue file unreadable, starting empty: " + e.Message);
			cache = new List<PendingRequest>();
		}
		return cache;
	}

	private static void Save(List<PendingRequest> items)
	{
		cache = items;
		try
		{
			string tmp = FilePath + ".tmp";
			File.WriteAllText(tmp, JsonUtility.ToJson(new OutboxFile { items = items }), new UTF8Encoding(false));
			if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
			else File.Move(tmp, FilePath);
		}
		catch (IOException e)
		{
			Debug.LogWarning("[UploadOutbox] save failed: " + e.Message);
		}
	}
}
