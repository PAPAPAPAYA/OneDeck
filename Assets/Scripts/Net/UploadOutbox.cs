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
			enqueuedAt = DateTime.UtcNow.ToString("o")
		});
		while (items.Count > MaxItems) items.RemoveAt(0);
		Save(items);
	}

	/// <summary>Test seam: drops the in-memory cache so the next read reloads from disk.</summary>
	public static void ResetCacheForTests()
	{
		cache = null;
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
	/// Drain the queue via DeckNetworkClient. Stops at the first failure and keeps the
	/// failed head as next trigger's first item, so ordering is preserved.
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
				DeckNetworkClient.Me.PostJson(head.path, head.jsonPayload,
					(body) => { ok = true; done = true; },
					(error, statusCode) => { done = true; });
				while (!done) yield return null;
				if (!ok) yield break;
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
