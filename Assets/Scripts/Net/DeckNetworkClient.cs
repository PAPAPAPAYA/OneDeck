using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Thin UnityWebRequest wrapper for the OneDeck API (plan §2.2). Payloads are UTF-8 JSON
/// built with JsonUtility from NetDtos.cs. Retries twice (1s/3s backoff) on transport
/// errors / timeouts / 5xx only; 4xx answers are final because the server rejected the
/// request itself (409 username_taken must reach the caller without delay).
/// </summary>
public class DeckNetworkClient : MonoBehaviour
{
	private const float TimeoutSeconds = 10f;
	private const int MaxRetries = 2;
	private static readonly float[] RetryDelaysSeconds = { 1f, 3f };

	private static DeckNetworkClient me;

	public static DeckNetworkClient Me
	{
		get
		{
			if (me == null)
			{
				GameObject host = new GameObject("DeckNetworkClient");
				me = host.AddComponent<DeckNetworkClient>();
			}
			return me;
		}
	}

	private void Awake()
	{
		if (me != null && me != this)
		{
			Destroy(gameObject);
			return;
		}
		me = this;
		DontDestroyOnLoad(gameObject);
	}

	private void OnDestroy()
	{
		if (me == this) me = null;
	}

	/// <summary>Game version used as the match key on every endpoint (plan §3.3).</summary>
	public static string GameVersion
	{
		get { return Application.version; }
	}

	/// <summary>POST jsonPayload to path; onOk receives the raw response body.</summary>
	public void PostJson(string path, string jsonPayload, Action<string> onOk, Action<string, long> onFail)
	{
		string url = ResolveUrl(path);
		if (url == null)
		{
			onFail?.Invoke("server_disabled", 0);
			return;
		}
		StartCoroutine(SendWithRetry(() =>
		{
			UnityWebRequest request = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
			request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonPayload ?? string.Empty));
			request.downloadHandler = new DownloadHandlerBuffer();
			request.SetRequestHeader("Content-Type", "application/json");
			return request;
		}, onOk, onFail));
	}

	/// <summary>GET path; query is the raw query string (encode values at the call site).</summary>
	public void GetJson(string path, string query, Action<string> onOk, Action<string, long> onFail)
	{
		string url = ResolveUrl(path);
		if (url == null)
		{
			onFail?.Invoke("server_disabled", 0);
			return;
		}
		if (!string.IsNullOrEmpty(query)) url += "?" + query;
		StartCoroutine(SendWithRetry(() => UnityWebRequest.Get(url), onOk, onFail));
	}

	private static string ResolveUrl(string path)
	{
		ServerConfig config = ServerConfig.Active;
		if (config == null || !config.enabled) return null;
		return config.BaseUrl + path;
	}

	private IEnumerator SendWithRetry(Func<UnityWebRequest> createRequest, Action<string> onOk, Action<string, long> onFail)
	{
		long lastStatusCode = 0;
		string lastError = "unknown_error";
		for (int attempt = 0; attempt <= MaxRetries; attempt++)
		{
			if (attempt > 0) yield return new WaitForSecondsRealtime(RetryDelaysSeconds[attempt - 1]);
			using (UnityWebRequest request = createRequest())
			{
				request.timeout = (int)(TimeoutSeconds * 1000);
				yield return request.SendWebRequest();

				if (request.result == UnityWebRequest.Result.Success)
				{
					onOk?.Invoke(request.downloadHandler != null ? request.downloadHandler.text : string.Empty);
					yield break;
				}
				lastStatusCode = request.responseCode;
				lastError = request.error;
				bool retryable = request.result == UnityWebRequest.Result.ConnectionError
					|| (request.result == UnityWebRequest.Result.ProtocolError && request.responseCode >= 500);
				if (!retryable) break;
			}
		}
		onFail?.Invoke(lastError, lastStatusCode);
	}
}
