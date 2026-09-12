using UnityEngine;

/// <summary>
/// Solid diagonal strikethrough line for shop discount prices (2026-09-11).
/// TMP's built-in <s> line samples the underscore glyph SDF from the font atlas;
/// for RobotoCondensed that bar is thinner than the SDF spread, so the built-in
/// line can never render fully opaque (thickening it only stretches the soft
/// profile into a translucent band). ShopCardView therefore keeps the <s> markup
/// as pure metadata and overlays this crisp quad instead.
/// Attached lazily by ShopCardView as a child of the CardPrice object. Every frame
/// it scans the TMP character infos for characters carrying the Strikethrough style
/// and spans a top-left -> bottom-right diagonal quad across that region.
/// All numbers are public so they can be tuned live in the inspector during play.
/// </summary>
public class PriceStrikeLine : MonoBehaviour
{
	[Header("Look")]
	[Tooltip("Solid color of the line (sRGB hex; converted to linear before writing vertex colors so it matches TMP text of the same hex in Linear color space).")]
	public Color color = new Color32(26, 48, 55, 255);

	[Tooltip("Line thickness as a fraction of the struck region's height.")]
	[Range(0.02f, 1f)]
	public float thicknessRatio = 0.12f;

	[Tooltip("Line length multiplier over the region diagonal (1 = corner to corner, >1 overhangs).")]
	public float lengthScale = 1.15f;

	[Tooltip("Extra rotation in degrees applied on top of the corner-to-corner diagonal.")]
	public float extraAngleDegrees = 0f;

	[Tooltip("Shrinks the struck region inward (fraction of its width/height) before the diagonal is computed.")]
	[Range(0f, 0.45f)]
	public float insetRatio = 0.05f;

	[Tooltip("Local-space offset applied to the line center (x along text width, y along text height).")]
	public Vector2 centerOffset = Vector2.zero;

	[Tooltip("Local z of the line relative to the price text (negative = closer to camera).")]
	public float zOffset = -0.01f;

	[Tooltip("Optional material override; when empty a Sprites/Default vertex-color material is created at runtime.")]
	public Material materialOverride;

	private TMPro.TextMeshPro _priceText;
	private MeshRenderer _renderer;
	private Mesh _mesh;
	private Material _material;
	private Color _appliedColor;
	private bool _visible;

	private void Awake()
	{
		_priceText = GetComponentInParent<TMPro.TextMeshPro>();
		BuildQuad();
	}

	private void LateUpdate()
	{
		if (_renderer == null || _priceText == null)
		{
			return;
		}

		_renderer.enabled = _visible;
		if (!_visible)
		{
			return;
		}

		if (!TryGetStruckRegion(out float minX, out float maxX, out float minY, out float maxY))
		{
			_renderer.enabled = false;
			return;
		}

		ApplyColorIfChanged();

		// Inset the region before computing the diagonal.
		float insetX = (maxX - minX) * insetRatio;
		float insetY = (maxY - minY) * insetRatio;
		Vector2 topLeft = new Vector2(minX + insetX, maxY - insetY);
		Vector2 bottomRight = new Vector2(maxX - insetX, minY + insetY);

		Vector2 center = (topLeft + bottomRight) * 0.5f + centerOffset;
		float length = Vector2.Distance(topLeft, bottomRight) * lengthScale;
		float angle = Mathf.Atan2(bottomRight.y - topLeft.y, bottomRight.x - topLeft.x) * Mathf.Rad2Deg + extraAngleDegrees;
		float thickness = Mathf.Max(0.005f, (maxY - minY) * thicknessRatio);

		transform.localPosition = new Vector3(center.x, center.y, zOffset);
		transform.localRotation = Quaternion.Euler(0f, 0f, angle);
		transform.localScale = new Vector3(length, thickness, 1f);
	}

	/// <summary>Show/hide the line; driven by ShopCardView when a discount is present.</summary>
	public void SetVisible(bool visible)
	{
		_visible = visible;
		if (_renderer != null)
		{
			_renderer.enabled = visible;
		}
	}

	private bool TryGetStruckRegion(out float minX, out float maxX, out float minY, out float maxY)
	{
		minX = float.MaxValue;
		maxX = float.MinValue;
		minY = float.MaxValue;
		maxY = float.MinValue;
		var infos = _priceText.textInfo != null ? _priceText.textInfo.characterInfo : null;
		if (infos == null || infos.Length == 0)
		{
			return false;
		}

		bool found = false;
		for (int i = 0; i < infos.Length; i++)
		{
			if ((infos[i].style & TMPro.FontStyles.Strikethrough) == 0)
			{
				continue;
			}
			found = true;
			minX = Mathf.Min(minX, infos[i].topLeft.x);
			maxX = Mathf.Max(maxX, infos[i].bottomRight.x);
			maxY = Mathf.Max(maxY, infos[i].topLeft.y);
			minY = Mathf.Min(minY, infos[i].bottomRight.y);
		}
		return found;
	}

	private void BuildQuad()
	{
		_mesh = new Mesh
		{
			vertices = new[]
			{
				new Vector3(-0.5f, -0.5f, 0f),
				new Vector3(-0.5f, 0.5f, 0f),
				new Vector3(0.5f, 0.5f, 0f),
				new Vector3(0.5f, -0.5f, 0f),
			},
			triangles = new[] { 0, 1, 2, 0, 2, 3 },
			uv = new[]
			{
				new Vector2(0f, 0f),
				new Vector2(0f, 1f),
				new Vector2(1f, 1f),
				new Vector2(1f, 0f),
			},
		};
		_mesh.MarkDynamic();

		var filter = gameObject.AddComponent<MeshFilter>();
		filter.sharedMesh = _mesh;

		_renderer = gameObject.AddComponent<MeshRenderer>();
		_renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
		_renderer.receiveShadows = false;

		_material = materialOverride != null ? materialOverride : new Material(Shader.Find("Sprites/Default"));
		_renderer.sharedMaterial = _material;
		_appliedColor = color;
		_mesh.colors32 = MakeColorArray(_appliedColor);
	}

	private void ApplyColorIfChanged()
	{
		if (_appliedColor != color)
		{
			_appliedColor = color;
			_mesh.colors32 = MakeColorArray(_appliedColor);
		}
	}

	/// <summary>
	/// Vertex colors are NOT sRGB-decoded by the GPU. In a Linear color space project
	/// (this project is Linear, verified 2026-09-12) an unconverted hex would render
	/// brightened after the final linear->sRGB output pass; convert first so the line
	/// renders exactly the configured sRGB hex, matching TMP's material-based colors.
	/// </summary>
	private static Color32[] MakeColorArray(Color srgbColor)
	{
		Color32 c = srgbColor.linear;
		return new[] { c, c, c, c };
	}
}
