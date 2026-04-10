using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
#endif

public static class PropertyBoolExtensions
{
	public static Property<bool> Inverse( this Property<bool> property )
	{
		return new NotProperty( property );
	}
}

[System.Serializable]
public class AndProperty : Property<bool>
{
	private List<Property<bool>> properties = new();

	public AndProperty( params Property<bool>[] properties )
	{
		foreach ( var property in properties )
		{
			property.Bind( OnChanged );
			this.properties.Add( property );
		}
	}

	void OnChanged( bool newValue )
	{
		var isTrue = newValue;
		if ( isTrue && !value )
		{
			foreach ( var property in properties )
			{
				if ( !property.value )
				{
					isTrue = false;
					break;
				}
			}
			if ( isTrue )
			{
				value = true;
				TriggerChanged( false );
			}
		}
		if ( !isTrue && value )
		{
			value = false;
			TriggerChanged( true );
		}
	}
}

[System.Serializable]
public class OrProperty : Property<bool>
{
	private List<Property<bool>> properties = new();

	public OrProperty( params Property<bool>[] properties )
	{
		foreach ( var property in properties )
		{
			property.Bind( OnChanged );
			this.properties.Add( property );
		}
	}

	void OnChanged( bool newValue )
	{
		var isTrue = newValue;
		if ( !isTrue && value )
		{
			foreach ( var property in properties )
			{
				if ( property.value )
				{
					isTrue = true;
					break;
				}
			}
			if ( !isTrue )
			{
				value = false;
				TriggerChanged( true );
			}
		}
		if ( isTrue && !value )
		{
			value = true;
			TriggerChanged( false );
		}
	}
}

[System.Serializable]
public class SumProperty : Property<int>
{
	private List<Property<int>> properties = new();

	public SumProperty( params Property<int>[] properties )
	{
		foreach ( var property in properties )
		{
			value += property.value;
			property.Bind( OnChanged );
			this.properties.Add( property );
		}
	}

	public void Add( Property<int> property )
	{
		property.Bind( OnChanged );
		properties.Add( property );
		var oldValue = value;
		value += property.value;
		TriggerChanged( oldValue );
	}

	void OnChanged( int oldValue, int newValue )
	{
		if ( newValue != oldValue )
		{
			var oldSum = this.value;
			value += newValue - oldValue;
			TriggerChanged( oldSum );
		}
	}
}

[System.Serializable]
public class NotProperty : Property<bool>
{
	private Property<bool> property;

	public NotProperty( Property<bool> property )
	{
		this.property = property;
		property.Bind( OnChanged );
		value = !property.value;
	}

	void OnChanged( bool oldValue, bool newValue )
	{
		value = !newValue;
		TriggerChanged( !oldValue );
	}
}

public enum ComparisonOperation
{
	LessThan,
	LessThanOrEqualTo,
	EqualTo,
	NotEqualTo,
	GreaterThanOrEqualTo,
	GreaterThan
}

[System.Serializable]
public class ComparisonProperty<T> : Property<bool>
{
	Property<T> left;
	Property<T> right;
	ComparisonOperation operation;

	public ComparisonProperty( Property<T> left, Property<T> right, ComparisonOperation operation )
	{
		this.left = left;
		this.right = right;
		this.operation = operation;

		left.Bind( OnChanged );
		right.Bind( OnChanged );

		Evaluate();
	}

	void Evaluate()
	{
		switch ( operation )
		{
			case ComparisonOperation.LessThan:
				value = Comparer<T>.Default.Compare( left.value, right.value ) < 0;
				break;
			case ComparisonOperation.LessThanOrEqualTo:
				value = Comparer<T>.Default.Compare( left.value, right.value ) <= 0;
				break;
			case ComparisonOperation.EqualTo:
				value = Comparer<T>.Default.Compare( left.value, right.value ) == 0;
				break;
			case ComparisonOperation.NotEqualTo:
				value = Comparer<T>.Default.Compare( left.value, right.value ) != 0;
				break;
			case ComparisonOperation.GreaterThanOrEqualTo:
				value = Comparer<T>.Default.Compare( left.value, right.value ) >= 0;
				break;
			case ComparisonOperation.GreaterThan:
				value = Comparer<T>.Default.Compare( left.value, right.value ) > 0;
				break;
		}
	}

	void OnChanged( T oldValue, T newValue )
	{
		var oldResult = value;
		Evaluate();
		TriggerChanged( oldResult );
	}
}

#if UNITY_EDITOR
[CustomPropertyDrawer( typeof( AndProperty ) )]
internal class AndPropertyDrawer : PropertyDrawer
{
	public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
	{
		SerializedProperty prop = property.FindPropertyRelative( "<value>k__BackingField" );
		EditorGUI.BeginDisabledGroup( true );
		EditorGUI.PropertyField( position, prop, new GUIContent( label.text + " (AND)" ) );
		EditorGUI.EndDisabledGroup();
	}
}

[CustomPropertyDrawer( typeof( OrProperty ) )]
internal class OrPropertyDrawer : PropertyDrawer
{
	public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
	{
		SerializedProperty prop = property.FindPropertyRelative( "<value>k__BackingField" );
		EditorGUI.BeginDisabledGroup( true );
		EditorGUI.PropertyField( position, prop, label );
		EditorGUI.EndDisabledGroup();
	}
}

[CustomPropertyDrawer( typeof( NotProperty ) )]
internal class NotPropertyDrawer : PropertyDrawer
{
	public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
	{
		SerializedProperty prop = property.FindPropertyRelative( "<value>k__BackingField" );
		EditorGUI.BeginDisabledGroup( true );
		EditorGUI.PropertyField( position, prop, label );
		EditorGUI.EndDisabledGroup();
	}
}

[CustomPropertyDrawer( typeof( SumProperty ) )]
internal class SumPropertyDrawer : PropertyDrawer
{
	public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
	{
		SerializedProperty prop = property.FindPropertyRelative( "<value>k__BackingField" );
		EditorGUI.BeginDisabledGroup( true );
		EditorGUI.PropertyField( position, prop, label );
		EditorGUI.EndDisabledGroup();
	}
}

[CustomPropertyDrawer( typeof( ComparisonProperty<> ) )]
internal class ComparisonPropertyDrawer : PropertyDrawer
{
	public override void OnGUI( Rect position, SerializedProperty property, GUIContent label )
	{
		SerializedProperty prop = property.FindPropertyRelative( "<value>k__BackingField" );
		EditorGUI.BeginDisabledGroup( true );
		EditorGUI.PropertyField( position, prop, label );
		EditorGUI.EndDisabledGroup();
	}
}
#endif