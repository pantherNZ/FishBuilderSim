using System;
using System.Collections.Generic;
using UnityEditor;
#if UNITY_EDITOR
using UnityEditor.UIElements;
#endif
using UnityEngine;
using UnityEngine.UIElements;

namespace Schema.Terrain
{
	public abstract class DepthKeyBase
	{
		public int depth;
		public abstract DepthKeyBase Lerp(DepthKeyBase other, float interp);
	}

	[Serializable]
	public class DepthKeysBase<KeyType> where KeyType : DepthKeyBase
	{
		public List<KeyType> keys = new();

		public KeyType Evaluate(Runtime.Terrain.DepthScaleData depth)
		{
			if (keys.IsEmpty())
				return default;

			var depthV = depth.depth;
			DepthKeyBase depthKey1 = null;
			DepthKeyBase depthKey2 = null;

			for (int key = 0; key < keys.Count; ++key)
			{
				if (depthV >= keys[key].depth)
				{
					depthKey1 = keys[key];
					depthKey2 = keys[Mathf.Min(keys.Count - 1, key + 1)];
				}
			}

			// Too low to reach the first depth key
			if (depthKey1 == null)
				return default;

			var LerpV = Mathf.InverseLerp(depthKey1.depth, depthKey2.depth, depthV);
			return depthKey1.Lerp(depthKey2, LerpV) as KeyType;
		}
	}

	[Serializable]
	public class DepthChanceKeyValue : DepthKeyBase
	{
		[Range(0, 100)]
		public int chance;

		public override DepthKeyBase Lerp(DepthKeyBase other, float interp) => new DepthChanceKeyValue()
		{
			depth = Utility.Lerp(depth, (other as DepthChanceKeyValue).depth, interp),
			chance = Utility.Lerp(chance, (other as DepthChanceKeyValue).chance, interp),
		};
	}

	[Serializable]
	public class DepthMultiplierKeyValue : DepthKeyBase
	{
		public float multiplier = 1.0f;

		public override DepthKeyBase Lerp(DepthKeyBase other, float interp) => new DepthMultiplierKeyValue()
		{
			depth = Utility.Lerp(depth, (other as DepthMultiplierKeyValue).depth, interp),
			multiplier = Utility.Lerp(multiplier, (other as DepthMultiplierKeyValue).multiplier, interp),
		};
	}

	[Serializable]
	public class DepthChanceKeys : DepthKeysBase<DepthChanceKeyValue>
	{
	}

	[Serializable]
	public class DepthMultiplierKeys : DepthKeysBase<DepthMultiplierKeyValue>
	{
	}

	[Serializable]
	public class DepthWeightingKeyValue : DepthKeyBase
	{
		public int weighting;

		public override DepthKeyBase Lerp(DepthKeyBase other, float interp) => new DepthWeightingKeyValue()
		{
			depth = Utility.Lerp(depth, (other as DepthWeightingKeyValue).depth, interp),
			weighting = Utility.Lerp(weighting, (other as DepthWeightingKeyValue).weighting, interp),
		};
	}

	[Serializable]
	public class DepthWeightingKeys : DepthKeysBase<DepthWeightingKeyValue>
	{
	}

	[Serializable]
	public class DepthCountKeyBase : DepthKeyBase
	{
		public Interval countRange;

		public override DepthKeyBase Lerp(DepthKeyBase other, float interp) => new DepthCountKeyBase()
		{
			depth = Utility.Lerp(depth, (other as DepthCountKeyBase).depth, interp),
			countRange = new Interval(
						Utility.Lerp(countRange.First, (other as DepthCountKeyBase).countRange.First, interp),
						Utility.Lerp(countRange.Second, (other as DepthCountKeyBase).countRange.Second, interp)),
		};
	}

	[Serializable]
	public class DepthCountKeys : DepthKeysBase<DepthCountKeyBase>
	{
	}

	[Serializable]
	public class DepthWeightingCountKeyValue : DepthCountKeyBase
	{
		public int weighting;

		public override DepthKeyBase Lerp(DepthKeyBase other, float interp)
			=> new DepthWeightingCountKeyValue()
			{
				depth = Utility.Lerp(depth, (other as DepthWeightingCountKeyValue).depth, interp),
				countRange = new Interval(
						Utility.Lerp(countRange.First, (other as DepthWeightingCountKeyValue).countRange.First, interp),
						Utility.Lerp(countRange.Second, (other as DepthWeightingCountKeyValue).countRange.Second, interp)),
				weighting = Utility.Lerp(weighting, (other as DepthWeightingCountKeyValue).weighting, interp),
			};
	}

	[Serializable]
	public class DepthWeightingCountKeys : DepthKeysBase<DepthWeightingCountKeyValue>
	{
	}

	[Serializable]
	public class DepthChanceCountKeyValue : DepthCountKeyBase
	{
		[Range(0, 100)]
		public int chance;

		public override DepthKeyBase Lerp(DepthKeyBase other, float interp)
		 => new DepthChanceCountKeyValue()
		 {
			 depth = Utility.Lerp(depth, (other as DepthChanceCountKeyValue).depth, interp),
			 countRange = new Interval(
					Utility.Lerp(countRange.First, (other as DepthChanceCountKeyValue).countRange.First, interp),
					Utility.Lerp(countRange.Second, (other as DepthChanceCountKeyValue).countRange.Second, interp)),
			 chance = Utility.Lerp(chance, (other as DepthChanceCountKeyValue).chance, interp),
		 };
	}

	[Serializable]
	public class DepthChanceCountKeys : DepthKeysBase<DepthChanceCountKeyValue>
	{
	}
}

#if UNITY_EDITOR
[CustomPropertyDrawer(typeof(Schema.Terrain.DepthChanceKeyValue))]
public class DepthChanceKeyValueEditor : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		bool isListElement = label != null && label.text != null && label.text.StartsWith("Element");
		// Unity often supplies GUIContent.none for ReorderableList elements; treat that as list element too
		if (string.IsNullOrEmpty(label?.text))
			isListElement = true;

		Rect contentRect;
		if (isListElement)
		{
			// Use full row for fields; no PrefixLabel so we don't reserve space for the element label twice
			contentRect = EditorGUI.IndentedRect(position);
			contentRect.x += 2f; contentRect.width -= 4f;
		}
		else
		{
			// Keep the element/property label narrow so fields have room
			float savedLWForPrefix = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = Mathf.Min(80f, position.width * 0.25f);
			contentRect = EditorGUI.PrefixLabel(position, label);
			EditorGUIUtility.labelWidth = savedLWForPrefix;
		}

		int oldIndent = EditorGUI.indentLevel;
		float oldLabelWidth = EditorGUIUtility.labelWidth;
		EditorGUI.indentLevel = 0;

		var depthProp = property.FindPropertyRelative("depth");
		var chanceProp = property.FindPropertyRelative("chance");

		float spacing = 6f;
		float half = (contentRect.width - spacing) * 0.5f;
		Rect left = new Rect(contentRect.x, contentRect.y, half, EditorGUIUtility.singleLineHeight);
		Rect right = new Rect(left.xMax + spacing, contentRect.y, half, EditorGUIUtility.singleLineHeight);

		if (isListElement)
		{
			const float lw = 46f;
			var leftLabel = new Rect(left.x, left.y, lw, left.height);
			var leftField = new Rect(left.x + lw, left.y, left.width - lw, left.height);
			EditorGUI.LabelField(leftLabel, "Depth");
			EditorGUI.PropertyField(leftField, depthProp, GUIContent.none);

			var rightLabel = new Rect(right.x, right.y, lw, right.height);
			var rightField = new Rect(right.x + lw, right.y, right.width - lw, right.height);
			EditorGUI.LabelField(rightLabel, "Chance");
			EditorGUI.PropertyField(rightField, chanceProp, GUIContent.none);
		}
		else
		{
			EditorGUIUtility.labelWidth = Mathf.Min(70f, left.width * 0.45f);
			EditorGUI.PropertyField(left, depthProp, new GUIContent("Depth"));
			EditorGUIUtility.labelWidth = Mathf.Min(80f, right.width * 0.55f);
			EditorGUI.PropertyField(right, chanceProp, new GUIContent("Chance"));
		}

		EditorGUIUtility.labelWidth = oldLabelWidth;
		EditorGUI.indentLevel = oldIndent;
		EditorGUI.EndProperty();
	}
}

[CustomPropertyDrawer(typeof(Schema.Terrain.DepthWeightingKeyValue))]
public class DepthWeightingKeyValueEditor : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		bool isListElement = label != null && label.text != null && label.text.StartsWith("Element");
		if (string.IsNullOrEmpty(label?.text))
			isListElement = true;

		Rect contentRect;
		if (isListElement)
		{
			contentRect = EditorGUI.IndentedRect(position);
			contentRect.x += 2f; contentRect.width -= 4f;
		}
		else
		{
			float savedLWForPrefix = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = Mathf.Min(80f, position.width * 0.25f);
			contentRect = EditorGUI.PrefixLabel(position, label);
			EditorGUIUtility.labelWidth = savedLWForPrefix;
		}

		int oldIndent = EditorGUI.indentLevel;
		float oldLabelWidth = EditorGUIUtility.labelWidth;
		EditorGUI.indentLevel = 0;

		var depthProp = property.FindPropertyRelative("depth");
		var weightingProp = property.FindPropertyRelative("weighting");

		float spacing = 6f;
		float half = (contentRect.width - spacing) * 0.5f;
		Rect left = new Rect(contentRect.x, contentRect.y, half, EditorGUIUtility.singleLineHeight);
		Rect right = new Rect(left.xMax + spacing, contentRect.y, half, EditorGUIUtility.singleLineHeight);

		if (isListElement)
		{
			const float lw = 46f;
			var leftLabel = new Rect(left.x, left.y, lw, left.height);
			var leftField = new Rect(left.x + lw, left.y, left.width - lw, left.height);
			EditorGUI.LabelField(leftLabel, "Depth");
			EditorGUI.PropertyField(leftField, depthProp, GUIContent.none);

			var rightLabel = new Rect(right.x, right.y, lw + 12f, right.height);
			var rightField = new Rect(right.x + lw + 12f, right.y, right.width - (lw + 12f), right.height);
			EditorGUI.LabelField(rightLabel, "Weight");
			EditorGUI.PropertyField(rightField, weightingProp, GUIContent.none);
		}
		else
		{
			EditorGUIUtility.labelWidth = Mathf.Min(60f, left.width * 0.45f);
			EditorGUI.PropertyField(left, depthProp, new GUIContent("Depth"));
			EditorGUIUtility.labelWidth = Mathf.Min(80f, right.width * 0.55f);
			EditorGUI.PropertyField(right, weightingProp, new GUIContent("Weighting"));
		}

		EditorGUIUtility.labelWidth = oldLabelWidth;
		EditorGUI.indentLevel = oldIndent;
		EditorGUI.EndProperty();
	}
}


[CustomPropertyDrawer(typeof(Schema.Terrain.DepthMultiplierKeyValue))]
public class DepthMultiplierKeyValueEditor : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		bool isListElement = label != null && label.text != null && label.text.StartsWith("Element");
		if (string.IsNullOrEmpty(label?.text))
			isListElement = true;

		Rect contentRect;
		if (isListElement)
		{
			contentRect = EditorGUI.IndentedRect(position);
			contentRect.x += 2f; contentRect.width -= 4f;
		}
		else
		{
			float savedLWForPrefix = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = Mathf.Min(80f, position.width * 0.25f);
			contentRect = EditorGUI.PrefixLabel(position, label);
			EditorGUIUtility.labelWidth = savedLWForPrefix;
		}

		int oldIndent = EditorGUI.indentLevel;
		float oldLabelWidth = EditorGUIUtility.labelWidth;
		EditorGUI.indentLevel = 0;

		var depthProp = property.FindPropertyRelative("depth");
		var weightingProp = property.FindPropertyRelative("multiplier");

		float spacing = 6f;
		float half = (contentRect.width - spacing) * 0.5f;
		Rect left = new Rect(contentRect.x, contentRect.y, half, EditorGUIUtility.singleLineHeight);
		Rect right = new Rect(left.xMax + spacing, contentRect.y, half, EditorGUIUtility.singleLineHeight);

		if (isListElement)
		{
			const float lw = 46f;
			var leftLabel = new Rect(left.x, left.y, lw, left.height);
			var leftField = new Rect(left.x + lw, left.y, left.width - lw, left.height);
			EditorGUI.LabelField(leftLabel, "Depth");
			EditorGUI.PropertyField(leftField, depthProp, GUIContent.none);

			var rightLabel = new Rect(right.x, right.y, lw + 12f, right.height);
			var rightField = new Rect(right.x + lw + 12f, right.y, right.width - (lw + 12f), right.height);
			EditorGUI.LabelField(rightLabel, "Multiplier");
			EditorGUI.PropertyField(rightField, weightingProp, GUIContent.none);
		}
		else
		{
			EditorGUIUtility.labelWidth = Mathf.Min(60f, left.width * 0.45f);
			EditorGUI.PropertyField(left, depthProp, new GUIContent("Depth"));
			EditorGUIUtility.labelWidth = Mathf.Min(80f, right.width * 0.55f);
			EditorGUI.PropertyField(right, weightingProp, new GUIContent("Multiplier"));
		}

		EditorGUIUtility.labelWidth = oldLabelWidth;
		EditorGUI.indentLevel = oldIndent;
		EditorGUI.EndProperty();
	}
}

[CustomPropertyDrawer(typeof(Schema.Terrain.DepthCountKeyBase), true)]
public class DepthCountKeyBaseEditor : PropertyDrawer
{
	public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
	{
		EditorGUI.BeginProperty(position, label, property);

		bool isListElement = label != null && label.text != null && label.text.StartsWith("Element");
		if (string.IsNullOrEmpty(label?.text))
			isListElement = true;

		Rect contentRect;
		if (isListElement)
		{
			contentRect = EditorGUI.IndentedRect(position);
			contentRect.x += 2f; contentRect.width -= 4f;
		}
		else
		{
			float savedLWForPrefix = EditorGUIUtility.labelWidth;
			EditorGUIUtility.labelWidth = Mathf.Min(80f, position.width * 0.25f);
			contentRect = EditorGUI.PrefixLabel(position, label);
			EditorGUIUtility.labelWidth = savedLWForPrefix;
		}

		int oldIndent = EditorGUI.indentLevel;
		float oldLabelWidth = EditorGUIUtility.labelWidth;
		EditorGUI.indentLevel = 0;

		var depthProp = property.FindPropertyRelative("depth");
		var weightOrChanceProp = property.FindPropertyRelative("weighting");
		var rangeProp = property.FindPropertyRelative("countRange");
		bool isWeight = weightOrChanceProp != null;
		weightOrChanceProp ??= property.FindPropertyRelative("chance");

		float spacing = 6f;
		float wDepth = Mathf.Floor(contentRect.width * 0.3f);
		float wWeight = Mathf.Floor(contentRect.width * 0.3f);
		float wRange = Mathf.Max(0f, contentRect.width - wDepth - wWeight - 2 * spacing);

		Rect rDepth = new Rect(contentRect.x, contentRect.y, wDepth, EditorGUIUtility.singleLineHeight);
		Rect rWeight = new Rect(rDepth.xMax + spacing, contentRect.y, wWeight, EditorGUIUtility.singleLineHeight);
		Rect rRange = new Rect(rWeight.xMax + spacing, contentRect.y, wRange, EditorGUIUtility.singleLineHeight);

		if (isListElement)
		{
			const float lw = 46f;
			// Depth
			var dLabel = new Rect(rDepth.x, rDepth.y, lw, rDepth.height);
			var dField = new Rect(rDepth.x + lw, rDepth.y, rDepth.width - lw, rDepth.height);
			EditorGUI.LabelField(dLabel, "Depth");
			EditorGUI.PropertyField(dField, depthProp, GUIContent.none);

			// Weight
			if (weightOrChanceProp != null)
			{
				var wLabel = new Rect(rWeight.x, rWeight.y, lw, rWeight.height);
				var wField = new Rect(rWeight.x + lw, rWeight.y, rWeight.width - lw, rWeight.height);
				EditorGUI.LabelField(wLabel, isWeight ? "Weight" : "Chance");
				EditorGUI.PropertyField(wField, weightOrChanceProp, GUIContent.none);
			}

			// Count range (inline two numeric fields)
			var cLabel = new Rect(rRange.x, rRange.y, lw, rRange.height);
			EditorGUI.LabelField(cLabel, "Count");
			var firstProp = rangeProp.FindPropertyRelative("First");
			var secondProp = rangeProp.FindPropertyRelative("Second");
			float innerSpacing = 4f;
			float twoColW = (rRange.width - lw - innerSpacing);
			var minRect = new Rect(rRange.x + lw, rRange.y, Mathf.Floor(twoColW * 0.5f), rRange.height);
			var maxRect = new Rect(minRect.xMax + innerSpacing, rRange.y, rRange.x + rRange.width - (minRect.xMax + innerSpacing), rRange.height);
			EditorGUI.PropertyField(minRect, firstProp, GUIContent.none);
			EditorGUI.PropertyField(maxRect, secondProp, GUIContent.none);
		}
		else
		{
			EditorGUIUtility.labelWidth = Mathf.Min(60f, rDepth.width * 0.45f);
			EditorGUI.PropertyField(rDepth, depthProp, new GUIContent("Depth"));
			if (weightOrChanceProp != null)
			{
				EditorGUIUtility.labelWidth = Mathf.Min(70f, rWeight.width * 0.55f);
				EditorGUI.PropertyField(rWeight, weightOrChanceProp, new GUIContent(isWeight ? "Weight" : "Chance"));
			}
			EditorGUIUtility.labelWidth = Mathf.Min(90f, rRange.width * 0.35f);
			EditorGUI.PropertyField(rRange, rangeProp, new GUIContent("Count Range"), true);
		}

		EditorGUIUtility.labelWidth = oldLabelWidth;
		EditorGUI.indentLevel = oldIndent;
		EditorGUI.EndProperty();
	}
}

#endif
