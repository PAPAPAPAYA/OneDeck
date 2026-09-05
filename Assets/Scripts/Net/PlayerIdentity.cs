using System;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Username -> playerId registration and persistence (plan §2.1). playerId is the only
/// credential the API needs; there is no password. Identity survives across Play Mode
/// and builds because they share persistentDataPath. Online features degrade silently
/// until registration succeeds; shop-visit retries are wired by a later batch.
/// </summary>
public static class PlayerIdentity
{
	[Serializable]
	private class IdentityFile
	{
		public string playerId;
		public string username;
	}

	/// <summary>Raised when a flow wants to register but no username was chosen yet; the UI layer subscribes (batch B).</summary>
	public static event Action RegistrationInputNeeded;

	private const string FileName = "player_identity.json";
	private static IdentityFile cached;

	/// <summary>Test seam: when set, overrides the persistentDataPath directory.</summary>
	public static string OverrideDirectoryForTests;

	private static string FilePath
	{
		get { return Path.Combine(OverrideDirectoryForTests ?? Application.persistentDataPath, FileName); }
	}

	/// <summary>Test seam: drops the cached identity so the next read reloads from disk.</summary>
	public static void ResetForTests()
	{
		cached = null;
	}

	public static bool HasIdentity { get { return Load().playerId != null; } }

	public static string PlayerId { get { return Load().playerId; } }

	public static string Username { get { return Load().username; } }

	/// <summary>Skippable input falls back to an anonymous name (plan §2.1).</summary>
	public static string RandomFallbackName()
	{
		return "玩家#" + UnityEngine.Random.Range(1000, 10000);
	}

	/// <summary>
	/// Raises RegistrationInputNeeded when online features are enabled but no identity was
	/// chosen yet; no-op otherwise. Callers: UsernameRegistrationPanel wiring in PhaseManager.
	/// </summary>
	public static void RaiseRegistrationInputNeededIfNeeded()
	{
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.enabled) return;
		if (HasIdentity) return;
		RegistrationInputNeeded?.Invoke();
	}

	/// <summary>Applies the markAsTest prefix; public for tests, Register applies it automatically.</summary>
	public static string ApplyTestPrefix(string username)
	{
		ServerConfig config = ServerConfig.Active;
		if (config != null && config.markAsTest && !username.StartsWith(ServerConfig.TestUsernamePrefix))
		{
			return ServerConfig.TestUsernamePrefix + username;
		}
		return username;
	}

	/// <summary>
	/// Register desiredUsername. onDone(ok, message): ok=false with "invalid_username"
	/// (local length check), "username_taken" (409, UI should prompt for another name)
	/// or a transport error string. Success persists the identity to disk.
	/// </summary>
	public static void Register(string desiredUsername, Action<bool, string> onDone)
	{
		string username = (desiredUsername ?? string.Empty).Trim();
		// Server counts code points; Length is a safe upper-bound proxy for BMP-only input,
		// and the server remains the authority either way.
		if (username.Length < 2 || username.Length > 16)
		{
			onDone?.Invoke(false, "invalid_username");
			return;
		}
		string payload = JsonUtility.ToJson(new RegisterRequest { username = ApplyTestPrefix(username) });
		DeckNetworkClient.Me.PostJson("/api/players/register", payload,
			body =>
			{
				RegisterResponse response = JsonUtility.FromJson<RegisterResponse>(body);
				if (response == null || string.IsNullOrEmpty(response.playerId))
				{
					onDone?.Invoke(false, "bad_response");
					return;
				}
				cached = new IdentityFile { playerId = response.playerId, username = response.username };
				WriteCache();
				onDone?.Invoke(true, null);
			},
			(error, statusCode) =>
			{
				onDone?.Invoke(false, statusCode == 409 ? "username_taken" : error);
			});
	}

	private static IdentityFile Load()
	{
		if (cached != null) return cached;
		try
		{
			if (File.Exists(FilePath))
			{
				cached = JsonUtility.FromJson<IdentityFile>(File.ReadAllText(FilePath, Encoding.UTF8));
			}
		}
		catch (Exception e)
		{
			Debug.LogWarning("[PlayerIdentity] identity file unreadable: " + e.Message);
		}
		if (cached == null) cached = new IdentityFile();
		return cached;
	}

	private static void WriteCache()
	{
		try
		{
			File.WriteAllText(FilePath, JsonUtility.ToJson(cached), new UTF8Encoding(false));
		}
		catch (IOException e)
		{
			Debug.LogWarning("[PlayerIdentity] identity save failed: " + e.Message);
		}
	}
}
