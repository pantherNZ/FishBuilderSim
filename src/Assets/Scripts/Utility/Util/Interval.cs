using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

[Serializable]
public class Interval : Pair<float, float>
{
	public Interval( float v )
		: base( v, v )
	{
	}
	
	public Interval( float min, float max )
        : base( min, max )
    {
    }

    public float Range() { return Second - First; }
    public float RangeAbs() { return Max - Min; }
    public bool Contains( float value ) { return value >= First && value <= Second; }
    public float Random( Utility.IRandom rng ) { return ( rng ?? Utility.DefaultRng ).Range( First, Second ); }
	public float Min => First <= Second ? First : Second;
	public float Max => First > Second ? First : Second;
	public float Mid => ( Min + Max / 2.0f );
	public bool IsZero => First == 0.0f && Second == 0.0f;
}


#if UNITY_EDITOR
[CustomPropertyDrawer( typeof( Interval ) )]
internal class IntervalDrawer : PropertyDrawer
{
	public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
	{
		EditorGUILayout.BeginHorizontal();

		EditorGUILayout.PrefixLabel( label );

		var firstProp = property.FindPropertyRelative( "First" );
		EditorGUILayout.PropertyField( firstProp, GUIContent.none );

		var secondProp = property.FindPropertyRelative( "Second" );
		EditorGUILayout.PropertyField( secondProp, GUIContent.none );

		EditorGUILayout.EndHorizontal();
	}
}

#endif
