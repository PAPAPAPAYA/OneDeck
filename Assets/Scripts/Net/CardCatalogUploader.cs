using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using TestWriteRead;
using UnityEngine;

/// <summary>
/// Card catalog upload (plan §2.8): once per game version, after registration, the
/// whole card pool's metadata is posted so the admin dashboards can translate
/// cardTypeIDs into names/tags/rarity. Idempotent server-side (upsert per version);
/// delivered through the outbox, so version is only recorded after successful enqueue.
/// </summary>
public static class CardCatalogUploader
{
	private const string VersionFileName = "catalog_version.txt";

	/// <summary>Test seam: when set, overrides the persistentDataPath directory.</summary>
	public static string OverrideDirectoryForTests;

	private static string VersionFilePath
	{
		get { return Path.Combine(OverrideDirectoryForTests ?? Application.persistentDataPath, VersionFileName); }
	}

	/// <summary>Scene-start trigger (plan §2.8): uploads when version drifted since last success.</summary>
	public static void MaybeUpload()
	{
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.enabled || !config.uploadCardCatalog) return;
		if (!PlayerIdentity.HasIdentity) return;

		DeckSaver saver = DeckSaver.Me;
		if (saver == null) return;

		string currentVersion = DeckNetworkClient.GameVersion;
		if (ReadLocalVersion() == currentVersion) return;

		List<CatalogCardEntry> cards = CollectCards(saver);
		if (cards.Count == 0) return;

		UploadOutbox.Enqueue(NetUploadKind.CardCatalog, new CatalogUploadRequest
		{
			playerId = PlayerIdentity.PlayerId,
			gameVersion = currentVersion,
			cards = cards
		});
		WriteLocalVersion(currentVersion);
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

	private static string ReadLocalVersion()
	{
		try
		{
			if (File.Exists(VersionFilePath)) return File.ReadAllText(VersionFilePath, Encoding.UTF8).Trim();
		}
		catch (IOException e)
		{
			Debug.LogWarning("[CardCatalogUploader] version read failed: " + e.Message);
		}
		return null;
	}

	private static void WriteLocalVersion(string version)
	{
		try
		{
			File.WriteAllText(VersionFilePath, version, new UTF8Encoding(false));
		}
		catch (IOException e)
		{
			Debug.LogWarning("[CardCatalogUploader] version write failed: " + e.Message);
		}
	}
}
