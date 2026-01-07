using System;
using UnityEngine;

namespace DefaultNamespace.SOScripts
{
	[CreateAssetMenu(fileName = "StringSO", menuName = "SORefs/StringSO")]
	public class StringSO : ScriptableObject
	{
		public string value;
		public bool reset = true;
		private void OnEnable()
		{
			if (!reset) return;
			value = "";
		}
	}
}