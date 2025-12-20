using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using UnityEngine;
using Random = UnityEngine.Random;

public class UtilityFuncManagerScript : MonoBehaviour
{
	#region SINGLETON
	public static UtilityFuncManagerScript me;
	private void Awake()
	{
		me = this;
	}
	#endregion

	public float ConvertV2ToAngle(Vector2 dir)
	{
		return Mathf.Atan2(dir.x, dir.y) * (180 / Mathf.PI);
	}

	// shuffle given GameObject list
	public List<GameObject> ShuffleList(List<GameObject> list)
	{
		List<GameObject> shuffled = new();
		shuffled = list.OrderBy(x => Random.value).ToList();
		return shuffled;
	}
	// copy game object list
	public void CopyGameObjectList(List<GameObject> from, List<GameObject> to)
	{
		to.Clear();
		foreach (var gO in from)
		{
			to.Add(gO);
		}
	}
	// get a random point on a circle
	public Vector3 RandomPointOnUnitCircle(float radius)
	{
		float angle = Random.Range(0f, Mathf.PI * 2);
		float x = Mathf.Sin(angle) * radius;
		float y = Mathf.Cos(angle) * radius;

		return new Vector3(x, y, 0);
	}
}
