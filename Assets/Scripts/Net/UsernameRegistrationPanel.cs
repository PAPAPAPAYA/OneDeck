using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// First-launch username dialog - the UI layer for PlayerIdentity.RegistrationInputNeeded
/// (plan: plans/plan-username-registration-panel-2026-09-04.md). Runtime-built UI after the
/// ResultStatsPanel pattern: own Canvas + full-screen dim blocker, no prefab or scene wiring.
/// PhaseManager raises the event on scene start and on every shop entry while no identity
/// exists; the panel pops (never during the tutorial - the shop-entry raise retries later).
/// Confirm registers the typed name, Random falls back to RandomFallbackName, and Later hides
/// until the next raise. On success the panel destroys itself and backfills the card catalog
/// upload that scene start skipped for lack of identity.
/// </summary>
public class UsernameRegistrationPanel : MonoBehaviour
{
	private const int SortingOrder = 300;
	private const int MaxCharacterLimit = 16;
	private const int RandomNameMaxAttempts = 3;
	private static readonly Vector2 ReferenceResolution = new Vector2(1080f, 1920f);

	private static UsernameRegistrationPanel me;

	private GameObject overlay;
	private TMP_InputField inputField;
	private TextMeshProUGUI hintText;
	private bool busy;

	/// <summary>Idempotent: instantiates the panel once and subscribes to RegistrationInputNeeded.</summary>
	public static UsernameRegistrationPanel EnsureCreated()
	{
		if (me != null) return me;
		me = new GameObject("UsernameRegistrationPanel").AddComponent<UsernameRegistrationPanel>();
		PlayerIdentity.RegistrationInputNeeded += me.ShowIfNoIdentity;
		me.BuildUi();
		return me;
	}

	private void OnDestroy()
	{
		if (me != this) return;
		PlayerIdentity.RegistrationInputNeeded -= ShowIfNoIdentity;
		me = null;
	}

	/// <summary>Delegates to PlayerIdentity - events can only be raised from their declaring type.</summary>
	public static void RaiseIfNeeded()
	{
		PlayerIdentity.RaiseRegistrationInputNeededIfNeeded();
	}

	private void ShowIfNoIdentity()
	{
		if (TutorialManager.IsTutorialActive) return;
		if (PlayerIdentity.HasIdentity) return;
		// Re-raise while already open just resets state; the typed name is kept.
		hintText.text = string.Empty;
		busy = false;
		overlay.SetActive(true);
		inputField.ActivateInputField();
	}

	/// <summary>Maps a PlayerIdentity.Register failure message to the inline hint text.</summary>
	public static string HintFor(string message)
	{
		switch (message)
		{
			case "username_taken": return "这个名字已被占用，换一个试试。";
			case "invalid_username": return "名字需要 2-16 个字符。";
			case "bad_response": return "服务器响应异常，请重试。";
			default: return "网络错误，请稍后重试。";
		}
	}

	private void BuildUi()
	{
		GameObject rootGo = gameObject;
		rootGo.AddComponent<Canvas>();
		Canvas canvas = rootGo.GetComponent<Canvas>();
		// A runtime Canvas on a ROOT object defaults to WorldSpace (a 780x720 plane at the world
		// origin); ResultStatsPanel only escapes this because it nests under the scene canvas.
		// Overlay is what lets the CanvasScaler drive the on-screen pixel size.
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.overrideSorting = true;
		canvas.sortingOrder = SortingOrder;
		CanvasScaler scaler = rootGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = ReferenceResolution;
		scaler.matchWidthOrHeight = 0f; // portrait game: match width
		rootGo.AddComponent<GraphicRaycaster>();
		EnsureEventSystem();

		// Full-screen dim blocker: raycastTarget=true swallows clicks aimed at the game beneath.
		overlay = new GameObject("Overlay", typeof(RectTransform));
		overlay.transform.SetParent(rootGo.transform, false);
		Stretch(overlay.GetComponent<RectTransform>());
		Image dim = overlay.AddComponent<Image>();
		dim.color = new Color(0f, 0f, 0f, 0.6f);
		overlay.SetActive(false);

		GameObject dialogGo = new GameObject("Dialog", typeof(RectTransform));
		dialogGo.transform.SetParent(overlay.transform, false);
		RectTransform dialog = dialogGo.GetComponent<RectTransform>();
		dialog.anchorMin = new Vector2(0.5f, 0.5f);
		dialog.anchorMax = new Vector2(0.5f, 0.5f);
		dialog.pivot = new Vector2(0.5f, 0.5f);
		dialog.sizeDelta = new Vector2(780f, 720f);
		Image dialogBg = dialogGo.AddComponent<Image>();
		dialogBg.color = PaletteColor(Field(p => p.tooltipBg), new Color(0.12f, 0.12f, 0.14f));

		CreateText(
			CreateContainer(dialogGo.transform, new Vector2(40f, -140f), new Vector2(-40f, -40f)),
			"输入你的名字", 46f, FontStyles.Bold, TextAlignmentOptions.Center, false)
			.color = PaletteColor(Field(p => p.resultPanelText), Color.white);

		inputField = CreateInputField(dialogGo.transform, new Vector2(40f, -330f), new Vector2(-40f, -200f));

		hintText = CreateText(
			CreateContainer(dialogGo.transform, new Vector2(40f, -430f), new Vector2(-40f, -340f)),
			string.Empty, 30f, FontStyles.Normal, TextAlignmentOptions.Center, true);
		hintText.color = PaletteColor(Field(p => p.damage), new Color(1f, 0.5f, 0.5f));

		CreateButton(dialogGo.transform, "确认", new Vector2(40f, -660f), new Vector2(-540f, -560f), OnConfirmClicked);
		CreateButton(dialogGo.transform, "随机名字", new Vector2(280f, -660f), new Vector2(-280f, -560f), OnRandomClicked);
		CreateButton(dialogGo.transform, "稍后再说", new Vector2(540f, -660f), new Vector2(-40f, -560f), OnLaterClicked);
	}

	private void OnConfirmClicked()
	{
		if (busy) return;
		busy = true;
		PlayerIdentity.Register(inputField.text, HandleResult);
	}

	private void OnRandomClicked()
	{
		if (busy) return;
		TryRegisterRandom(0);
	}

	private void TryRegisterRandom(int attempt)
	{
		busy = true;
		PlayerIdentity.Register(PlayerIdentity.RandomFallbackName(), (ok, message) =>
		{
			// A random-name collision is practically impossible; retry defensively anyway.
			if (!ok && message == "username_taken" && attempt < RandomNameMaxAttempts - 1)
			{
				TryRegisterRandom(attempt + 1);
				return;
			}
			HandleResult(ok, message);
		});
	}

	private void OnLaterClicked()
	{
		overlay.SetActive(false);
	}

	private void HandleResult(bool ok, string message)
	{
		busy = false;
		if (ok)
		{
			Debug.Log("[UsernameRegistrationPanel] registered as " + PlayerIdentity.Username);
			Destroy(gameObject);
			// Scene start skipped these for lack of identity - backfill right away.
			CardCatalogUploader.MaybeUpload();
			UploadOutbox.Flush();
			return;
		}
		hintText.text = HintFor(message);
	}

	private TMP_InputField CreateInputField(Transform parent, Vector2 offsetMin, Vector2 offsetMax)
	{
		GameObject go = new GameObject("InputField", typeof(RectTransform));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.offsetMin = offsetMin;
		rect.offsetMax = offsetMax;
		Image bg = go.AddComponent<Image>();
		bg.color = PaletteColor(Field(p => p.slotRecess), new Color(0f, 0f, 0f, 0.35f));
		TMP_InputField field = go.AddComponent<TMP_InputField>();

		GameObject textArea = new GameObject("Text Area", typeof(RectTransform));
		textArea.transform.SetParent(go.transform, false);
		RectTransform areaRect = textArea.GetComponent<RectTransform>();
		Stretch(areaRect);
		areaRect.offsetMin = new Vector2(20f, 8f);
		areaRect.offsetMax = new Vector2(-20f, -8f);
		textArea.AddComponent<RectMask2D>();

		TextMeshProUGUI placeholder = CreateText(textArea.transform, "2-16 个字符", 40f, FontStyles.Normal, TextAlignmentOptions.Left, false);
		placeholder.name = "Placeholder";
		Color dimmed = PaletteColor(Field(p => p.resultPanelText), Color.white);
		dimmed.a = 0.45f;
		placeholder.color = dimmed;

		TextMeshProUGUI text = CreateText(textArea.transform, string.Empty, 40f, FontStyles.Normal, TextAlignmentOptions.Left, false);
		text.name = "Text";
		text.color = PaletteColor(Field(p => p.resultPanelText), Color.white);

		field.textViewport = areaRect;
		field.textComponent = text;
		field.placeholder = placeholder;
		field.targetGraphic = bg;
		field.characterLimit = MaxCharacterLimit;
		return field;
	}

	private void CreateButton(Transform parent, string label, Vector2 offsetMin, Vector2 offsetMax, UnityEngine.Events.UnityAction onClick)
	{
		GameObject go = new GameObject("Button_" + label, typeof(RectTransform));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.offsetMin = offsetMin;
		rect.offsetMax = offsetMax;
		Image bg = go.AddComponent<Image>();
		bg.color = PaletteColor(Field(p => p.shield), new Color(0.8f, 0.8f, 0.8f));
		Button button = go.AddComponent<Button>();
		button.onClick.AddListener(onClick);
		CreateText(go.transform, label, 40f, FontStyles.Normal, TextAlignmentOptions.Center, false)
			.color = PaletteColor(Field(p => p.resultPanelText), Color.white);
	}

	/// <summary>Top-stretch strip inside the dialog that positions a text; the text itself fills it.</summary>
	private RectTransform CreateContainer(Transform parent, Vector2 offsetMin, Vector2 offsetMax)
	{
		GameObject go = new GameObject("Panel", typeof(RectTransform));
		go.transform.SetParent(parent, false);
		RectTransform rect = go.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, 1f);
		rect.anchorMax = new Vector2(1f, 1f);
		rect.offsetMin = offsetMin;
		rect.offsetMax = offsetMax;
		return rect;
	}

	/// <summary>Text always stretches to fill its parent; parents define placement.</summary>
	private TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, FontStyles style,
		TextAlignmentOptions alignment, bool wrap)
	{
		GameObject textGo = new GameObject("Text", typeof(RectTransform));
		textGo.transform.SetParent(parent, false);
		Stretch(textGo.GetComponent<RectTransform>());
		TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
		tmp.font = LoadCjkFont();
		tmp.text = content;
		tmp.fontSize = fontSize;
		tmp.fontStyle = style;
		tmp.alignment = alignment;
		tmp.raycastTarget = false;
		tmp.textWrappingMode = wrap ? TextWrappingModes.Normal : TextWrappingModes.NoWrap;
		return tmp;
	}

	private static TMP_FontAsset LoadCjkFont()
	{
		// TMP's default font (RobotoCondensed) has no CJK glyphs; every runtime-built Chinese
		// UI must load the SourceHanSans asset explicitly.
		return Resources.Load<TMP_FontAsset>("Fonts & Materials/SourceHanSansCN-Regular SDF");
	}

	private static void Stretch(RectTransform rect)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private static Color PaletteColor(ColorSO so, Color fallback)
	{
		GameColorPalette palette = GameColorPalette.Me;
		if (palette == null || so == null) return fallback;
		return so.value;
	}

	private static ColorSO Field(System.Func<GameColorPalette, ColorSO> selector)
	{
		GameColorPalette palette = GameColorPalette.Me;
		return palette == null ? null : selector(palette);
	}

	private static void EnsureEventSystem()
	{
		if (FindAnyObjectByType<EventSystem>() != null) return;
		GameObject eventSystemGo = new GameObject("EventSystem_UsernamePanel");
		eventSystemGo.AddComponent<EventSystem>();
		eventSystemGo.AddComponent<StandaloneInputModule>();
	}
}
