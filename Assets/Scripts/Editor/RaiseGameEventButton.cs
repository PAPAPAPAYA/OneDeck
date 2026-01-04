using UnityEngine;
using UnityEditor;

namespace DefaultNamespace
{
	[CustomEditor(typeof(GameEvent))]
	public class RaiseGameEventButton : Editor
	{
		public override void OnInspectorGUI()
		{
			DrawDefaultInspector();
			var gameEvent = (GameEvent)target;
			if (GUILayout.Button("Raise"))
			{
				gameEvent.Raise();
			}
		}
	}
}