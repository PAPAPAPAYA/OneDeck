using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Card catalog upload (plan §2.8, dedup reworked by
/// plans/plan-card-catalog-content-fingerprint-2026-09-17.md): after registration,
/// the whole card pool's metadata is posted so the admin dashboards can translate
/// cardTypeIDs into names/tags/rarity. Dedup is content-based: a SHA-1 fingerprint
/// of the collected catalog is compared against the local sentinel, so any pool
/// change (name / tags / rarity / price / membership) re-uploads without waiting
/// for a build-version bump. Idempotent server-side (upsert per version); delivered
/// through the outbox, so the fingerprint is only recorded after successful enqueue.
/// </summary>
public static class CardCatalogUploader
{
	// Sentinel file name kept from the version-keyed era; the content is now the
	// pool fingerprint. Legacy files hold a game-version string, which never equals
	// a fingerprint - the first post-upgrade combat uploads once and re-stamps it.
	private const string VersionFileName = "catalog_version.txt";

	/// <summary>Test seam: when set, overrides the persistentDataPath directory.</summary>
	public static string OverrideDirectoryForTests;

	private static string VersionFilePath
	{
		get { return Path.Combine(OverrideDirectoryForTests ?? Application.persistentDataPath, VersionFileName); }
	}

	/// <summary>Settlement trigger (plan §2.8): uploads when the pool fingerprint drifted since last success.</summary>
	public static void MaybeUpload()
	{
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.enabled || !config.uploadCardCatalog) return;
		if (!PlayerIdentity.HasIdentity) return;
		// Upload gate: catalog metadata only starts flowing once the current run has a
		// completed combat; CombatCompletionGate.MarkCompleted backfills at that settlement.
		if (!CombatCompletionGate.HasCompletedCombatThisRun) return;

		DeckSaver saver = DeckSaver.Me;
		if (saver == null) return;

		List<CatalogCardEntry> cards = CollectCards(saver);
		if (cards.Count == 0) return;

		string fingerprint = ComputeFingerprint(cards);
		if (ReadLocalFingerprint() == fingerprint) return;

		UploadOutbox.Enqueue(NetUploadKind.CardCatalog, new CatalogUploadRequest
		{
			playerId = PlayerIdentity.PlayerId,
			gameVersion = DeckNetworkClient.GameVersion,
			cards = cards
		});
		WriteLocalFingerprint(fingerprint);
	}

	/// <summary>Pure-ish collection so tests can exercise it through a real DeckSaver-less path.</summary>
	public static List<CatalogCardEntry> CollectCards(DeckSaver saver)
	{
		List<CatalogCardEntry> cards = new List<CatalogCardEntry>();
		if (saver == null) return cards;

		HashSet<string> seen = new HashSet<string>();
		AppendFrom(saver.shopPoolRef != null ? saver.shopPoolRef.deck : null, cards, seen);
		if (saver.additionalCardPrefabs != null)
		{
			AppendFrom(saver.additionalCardPrefabs, cards, seen);
		}
		return cards;
	}

	private static void AppendFrom(List<GameObject> prefabs, List<CatalogCardEntry> cards, HashSet<string> seen)
	{
		if (prefabs == null) return;
		foreach (GameObject prefab in prefabs)
		{
			if (prefab == null) continue;
			CardScript cardScript = prefab.GetComponent<CardScript>();
			if (cardScript == null) continue;

			string typeID = !string.IsNullOrEmpty(cardScript.cardTypeID) ? cardScript.cardTypeID : cardScript.name;
			if (string.IsNullOrEmpty(typeID) || !seen.Add(typeID)) continue;

			List<string> tags = new List<string>();
			if (cardScript.myTags != null)
			{
				foreach (EnumStorage.Tag tag in cardScript.myTags)
				{
					if (tag != EnumStorage.Tag.None && !tags.Contains(tag.ToString())) tags.Add(tag.ToString());
				}
			}
			if (cardScript.reservedTag != EnumStorage.Tag.None && !tags.Contains(cardScript.reservedTag.ToString()))
			{
				tags.Add(cardScript.reservedTag.ToString());
			}

			cards.Add(new CatalogCardEntry
			{
				cardTypeID = typeID,
				name = cardScript.GetDisplayName(),
				tags = tags,
				rarity = cardScript.rarity.ToString(),
				cost = ShopManager.me != null ? ShopManager.me.GetCardPrice(cardScript) : 0
			});
		}
	}

	/// <summary>
	/// Deterministic SHA-1 digest of the collected catalog (content-fingerprint plan
	/// §2.2): entries are sorted by cardTypeID (ordinal, on a copy so the caller's
	/// list order survives for the outbox payload), tags are copied + sorted and
	/// comma-joined (insensitive to myTags serialization order), and every field is
	/// length-prefixed (`len:value|`) so embedded delimiters cannot shift fields.
	/// UTF-8 bytes → SHA-1 → lowercase hex.
	/// </summary>
	public static string ComputeFingerprint(List<CatalogCardEntry> cards)
	{
		if (cards == null || cards.Count == 0) return string.Empty;

		List<CatalogCardEntry> sorted = new List<CatalogCardEntry>(cards);
		sorted.Sort((a, b) => string.CompareOrdinal(a != null ? a.cardTypeID : null, b != null ? b.cardTypeID : null));

		StringBuilder content = new StringBuilder();
		foreach (CatalogCardEntry card in sorted)
		{
			List<string> tags = card != null && card.tags != null ? new List<string>(card.tags) : new List<string>();
			tags.Sort(string.CompareOrdinal);
			AppendField(content, card != null ? card.cardTypeID : null);
			AppendField(content, card != null ? card.name : null);
			AppendField(content, string.Join(",", tags.ToArray()));
			AppendField(content, card != null ? card.rarity : null);
			AppendField(content, card != null ? card.cost.ToString(System.Globalization.CultureInfo.InvariantCulture) : null);
		}

		using (System.Security.Cryptography.SHA1 sha1 = System.Security.Cryptography.SHA1.Create())
		{
			byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(content.ToString()));
			StringBuilder hex = new StringBuilder(hash.Length * 2);
			foreach (byte value in hash)
			{
				hex.Append(value.ToString("x2"));
			}
			return hex.ToString();
		}
	}

	private static void AppendField(StringBuilder builder, string value)
	{
		builder.Append(value == null ? 0 : value.Length).Append(':').Append(value ?? string.Empty).Append('|');
	}

	private static string ReadLocalFingerprint()
	{
		try
		{
			if (File.Exists(VersionFilePath)) return File.ReadAllText(VersionFilePath, Encoding.UTF8).Trim();
		}
		catch (IOException e)
		{
			Debug.LogWarning("[CardCatalogUploader] fingerprint read failed: " + e.Message);
		}
		return null;
	}

	private static void WriteLocalFingerprint(string fingerprint)
	{
		try
		{
			File.WriteAllText(VersionFilePath, fingerprint, new UTF8Encoding(false));
		}
		catch (IOException e)
		{
			Debug.LogWarning("[CardCatalogUploader] fingerprint write failed: " + e.Message);
		}
	}
}
