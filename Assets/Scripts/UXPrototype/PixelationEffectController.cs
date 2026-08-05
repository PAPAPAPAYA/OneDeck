using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Runtime control for the fullscreen pixelation effect (FullScreenPassRendererFeature
/// named "PixelationFullscreen" on the PC/Mobile URP renderers). Lazy singleton, no
/// scene wiring needed: finds the features by scanning the renderer data list of the
/// active and default render pipeline assets. Pure presentation, no game logic.
/// </summary>
public class PixelationEffectController : MonoBehaviour
{
	private const string FeatureName = "PixelationFullscreen";
	private const string BlockCountProperty = "_BlockCount";

	private static PixelationEffectController _me;
	public static PixelationEffectController me
	{
		get
		{
			if (_me == null)
			{
				var go = new GameObject("PixelationEffectController");
				_me = go.AddComponent<PixelationEffectController>();
			}
			return _me;
		}
	}

	private List<FullScreenPassRendererFeature> _features;
	private float _blockCount = 128f;
	private bool _enabled = true;

	private void Awake()
	{
		_me = this;
	}

	/// <summary>Enable/disable the pixelation pass on every renderer that has it.</summary>
	public void SetEnabled(bool value)
	{
		_enabled = value;
		foreach (var feature in GetFeatures())
		{
			feature.SetActive(value);
		}
	}

	public bool IsEnabled()
	{
		return _enabled;
	}

	/// <summary>Set the block grid width (64-512, same range as the shader property).</summary>
	public void SetBlockCount(float value)
	{
		_blockCount = Mathf.Clamp(value, 64f, 512f);
		foreach (var feature in GetFeatures())
		{
			if (feature.passMaterial != null)
			{
				feature.passMaterial.SetFloat(BlockCountProperty, _blockCount);
			}
		}
	}

	private List<FullScreenPassRendererFeature> GetFeatures()
	{
		if (_features != null) return _features;
		_features = new List<FullScreenPassRendererFeature>();
		CollectFeatures(GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset);
		CollectFeatures(QualitySettings.renderPipeline as UniversalRenderPipelineAsset);
		return _features;
	}

	private void CollectFeatures(UniversalRenderPipelineAsset pipelineAsset)
	{
		if (pipelineAsset == null) return;
		// m_RendererDataList is serialized but not public; reflection is the only runtime access.
		var field = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", BindingFlags.NonPublic | BindingFlags.Instance);
		if (field == null) return;
		var dataList = field.GetValue(pipelineAsset) as ScriptableRendererData[];
		if (dataList == null) return;
		foreach (var data in dataList)
		{
			if (data == null) continue;
			foreach (var feature in data.rendererFeatures)
			{
				if (feature is FullScreenPassRendererFeature fullscreen && feature.name == FeatureName && !_features.Contains(fullscreen))
				{
					_features.Add(fullscreen);
				}
			}
		}
	}
}
