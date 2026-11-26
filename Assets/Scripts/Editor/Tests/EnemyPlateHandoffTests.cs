using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// EditMode tests for the selection-leg2 enemy-plate handoff size math (option A,
/// 2026-11-26): CombatPlateWorldWidthOf must predict the face's rendered world width at
/// combat scale while dividing out whatever scale the hidden pill was parked at. The raw
/// lossyScale ratio it replaced crossed unit bases (canvas px vs world authored) and
/// shrank the plate to ~1/111 — O-L1 of plan-opponent-select-page-2026-10-06.
/// </summary>
public class EnemyPlateHandoffTests
{
	private readonly List<GameObject> _spawned = new List<GameObject>();

	/// <summary>
	/// Builds root(scale) → face(local 2, width 100) and measures, passing the root's REAL
	/// lossy scale as the parked pill scale — self-lossy and face-lossy must stay consistent
	/// for the divide-out to mean "the face's own local scale".
	/// </summary>
	private float BuildAndMeasure(float rootScale, float combatScaleX)
	{
		var root = new GameObject("plate-root", typeof(RectTransform));
		_spawned.Add(root);
		root.transform.localScale = Vector3.one * rootScale;
		var faceGo = new GameObject("face", typeof(RectTransform));
		faceGo.transform.SetParent(root.transform, false);
		faceGo.transform.localScale = Vector3.one * 2f;
		var faceRt = faceGo.GetComponent<RectTransform>();
		faceRt.sizeDelta = new Vector2(100f, 40f);
		return HPNumericDisplayHorizontal.CombatPlateWorldWidthOf(faceRt, root.transform.lossyScale.x, combatScaleX);
	}

	[TearDown]
	public void TearDown()
	{
		foreach (var go in _spawned)
		{
			if (go != null) Object.DestroyImmediate(go);
		}
		_spawned.Clear();
	}

	[Test]
	public void CombatPlateWorldWidth_PredictsFaceWidthAtCombatScale()
	{
		// Authored face: local scale 2 x width 100 → per-self-unit width 200; combat 0.8
		// → 160, whatever scale the root (the hidden pill) is parked at.
		Assert.AreEqual(160f, BuildAndMeasure(3f, 0.8f), 0.001f, "parked root scale 3");
		Assert.AreEqual(160f, BuildAndMeasure(0.5f, 0.8f), 0.001f, "parked root scale 0.5");
		Assert.AreEqual(200f, BuildAndMeasure(3f, 1f), 0.001f, "combat scale 1");
		// The combat-scale floor clamps at 0.0001, so scale 0 lands at 200*0.0001 = 0.02 —
		// effectively zero (degenerate authored state, never real).
		Assert.AreEqual(0f, BuildAndMeasure(3f, 0f), 0.05f, "combat scale 0 clamps to ~0");
	}

	[Test]
	public void CombatPlateWorldWidth_NullFace_ReturnsZero()
	{
		Assert.AreEqual(0f, HPNumericDisplayHorizontal.CombatPlateWorldWidthOf(null, 1f, 0.8f));
	}
}
