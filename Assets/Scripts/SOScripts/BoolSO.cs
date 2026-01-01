using System;
using UnityEngine;

[CreateAssetMenu(fileName =  "BoolSO", menuName = "SORefs/BoolSO")]
public class BoolSO : ScriptableObject
{
    public bool value;
    public bool valueOg;
    public bool resetOnStart;
    [TextArea]
    public string description;

    private void OnEnable()
    {
        if (resetOnStart) value = valueOg;
    }
}
