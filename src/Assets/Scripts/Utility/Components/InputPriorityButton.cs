using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class InputPriorityButton : MultiImageButton
{
	public string key;
	public int priority;
}

#if UNITY_EDITOR
[CustomEditor( typeof( InputPriorityButton ) )]
public class InputPriorityButtonEditor : UnityEditor.UI.ButtonEditor
{
    public override void OnInspectorGUI()
    {
        var targetMenuButton = ( InputPriorityButton )target;

        targetMenuButton.key = EditorGUILayout.TextField( "Key", targetMenuButton.key );
        targetMenuButton.priority = EditorGUILayout.IntField( "Priority", targetMenuButton.priority );

        base.OnInspectorGUI();
    }
}
#endif
