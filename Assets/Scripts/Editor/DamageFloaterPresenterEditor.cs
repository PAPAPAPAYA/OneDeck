using DG.Tweening;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Inspector preview buttons for DamageFloaterPresenter: spawn a damage floater
/// in EDIT MODE and drive its DOTween Sequence manually (DOTween does not tick
/// outside Play Mode), so timing/scale/color tweaks can be evaluated without
/// entering combat. The preview object is named DamageFloater_Preview, marked
/// HideFlags.DontSave, and destroyed with DestroyImmediate on completion.
/// Player/enemy buttons only differ in the spawn offset (left/right of the
/// floater layer center); the real spawn anchor (attack target position) does
/// not exist in edit mode.
/// </summary>
[CustomEditor(typeof(DamageFloaterPresenter))]
public class DamageFloaterPresenterEditor : Editor
{
	private const string PreviewName = "DamageFloater_Preview";
	private static int s_previewAmount = 7;

	private Sequence _seq;
	private GameObject _go;
	private double _lastTick;

	public override void OnInspectorGUI()
	{
		DrawDefaultInspector();

		var presenter = (DamageFloaterPresenter)target;
		EditorGUILayout.Space();
		EditorGUILayout.LabelField("Edit-Mode Preview", EditorStyles.boldLabel);
		s_previewAmount = Mathf.Max(1, EditorGUILayout.IntField("Damage Amount", s_previewAmount));
		using (new EditorGUI.DisabledScope(presenter.floaterLayer == null))
		{
			using (new EditorGUILayout.HorizontalScope())
			{
				if (GUILayout.Button("Preview Player Hit"))
				{
					StartPreview(presenter, -0.25f);
				}
				if (GUILayout.Button("Preview Enemy Hit"))
				{
					StartPreview(presenter, 0.25f);
				}
			}
		}
		if (_go != null && GUILayout.Button("Stop Preview"))
		{
			StopPreview();
		}
	}

	private void StartPreview(DamageFloaterPresenter presenter, float xFraction)
	{
		StopPreview();
		if (Application.isPlaying)
		{
			Debug.LogWarning("[DamageFloater] Edit-mode preview is meant for edit mode; in Play Mode floaters spawn from real hits.");
			return;
		}
		Rect rect = presenter.floaterLayer.rect;
		Vector2 local = rect.center + new Vector2(rect.width * xFraction, 0f);
		_seq = presenter.SpawnPreviewFloater(local, s_previewAmount, out _go);
		_go.name = PreviewName;
		_go.hideFlags = HideFlags.DontSave;
		_lastTick = EditorApplication.timeSinceStartup;
		EditorApplication.update += Tick;
		EditorApplication.playModeStateChanged += OnPlayModeChanged;
	}

	private void Tick()
	{
		if (_seq == null || _go == null || !_seq.IsActive())
		{
			StopPreview();
			return;
		}
		double now = EditorApplication.timeSinceStartup;
		float dt = (float)(now - _lastTick);
		_lastTick = now;
		DOTween.ManualUpdate(dt, dt);
		// Force player-loop + view repaints, otherwise edit-mode views only
		// redraw on user input and the tween appears frozen.
		EditorApplication.QueuePlayerLoopUpdate();
		SceneView.RepaintAll();
		if (_seq.IsComplete())
		{
			StopPreview();
		}
	}

	private void OnPlayModeChanged(PlayModeStateChange state)
	{
		StopPreview();
	}

	private void StopPreview()
	{
		EditorApplication.update -= Tick;
		EditorApplication.playModeStateChanged -= OnPlayModeChanged;
		if (_seq != null && _seq.IsActive())
		{
			_seq.Kill();
		}
		_seq = null;
		if (_go != null)
		{
			DestroyImmediate(_go);
			_go = null;
		}
	}

	private void OnEnable()
	{
		// A domain reload mid-preview kills statics and delegates but leaves the
		// floater GameObject behind: sweep leftovers by name.
		var presenter = (DamageFloaterPresenter)target;
		if (presenter == null || presenter.floaterLayer == null)
		{
			return;
		}
		for (int i = presenter.floaterLayer.childCount - 1; i >= 0; i--)
		{
			Transform child = presenter.floaterLayer.GetChild(i);
			if (child.name == PreviewName)
			{
				DestroyImmediate(child.gameObject);
			}
		}
	}

	private void OnDisable()
	{
		StopPreview();
	}
}
