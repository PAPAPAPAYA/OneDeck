using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using UnityEngine;
using Random = UnityEngine.Random;

public static class UtilityFuncManagerScript
{
	#region SINGLETON

	// public static UtilityFuncManagerScript me;
	//
	// private void Awake()
	// {
	// 	me = this;
	// }

	#endregion
	
	public static float ConvertV2ToAngle(Vector2 dir)
	{
		return Mathf.Atan2(dir.x, dir.y) * (180 / Mathf.PI);
	}

	// shuffle given list
	public static List<T> ShuffleList<T>(List<T> list)
	{
		return list.OrderBy(x => Random.value).ToList();
	}

	// copy game object list
	public static void CopyGameObjectList(List<GameObject> from, List<GameObject> to, bool clearTargetList)
	{
		if (clearTargetList) to.Clear();
		foreach (var gO in from)
		{
			to.Add(gO);
		}
	}

	// copy generic type list
	public static void CopyList<T>(List<T> from, List<T> to, bool clearTargetList)
	{
		if (clearTargetList) to.Clear();
		foreach (var gO in from)
		{
			to.Add(gO);
		}
	}

	/// <summary>
	/// Generates a random number following Gaussian (normal) distribution using Box-Muller transform.
	/// </summary>
	/// <param name="mean">Center of the distribution.</param>
	/// <param name="stdDev">Standard deviation (spread). Higher = more dispersed.</param>
	public static float GaussianRandom(float mean, float stdDev)
	{
		float u1 = 1.0f - Random.value;
		float u2 = 1.0f - Random.value;
		float randStdNormal = Mathf.Sqrt(-2.0f * Mathf.Log(u1)) * Mathf.Sin(2.0f * Mathf.PI * u2);
		return mean + stdDev * randStdNormal;
	}

	// get a random point on a circle
	public static Vector3 RandomPointOnUnitCircle(float radius)
	{
		float angle = Random.Range(0f, Mathf.PI * 2);
		float x = Mathf.Sin(angle) * radius;
		float y = Mathf.Cos(angle) * radius;

		return new Vector3(x, y, 0);
	}
}