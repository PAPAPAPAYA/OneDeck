using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CostNEffectContainer))]
public class AddADividerToCostNEffectScript : Editor
{
	public override void OnInspectorGUI()
	{
		EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
		DrawDefaultInspector();
	}
}
