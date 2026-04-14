using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

namespace Schema.Items
{
	[Serializable]
	public class WeightedAsset
	{
		public BaseDataSchema asset;
		public int weight;
	}

	[Serializable]
	public class WeightedTable
	{
		public List<WeightedAsset> assetPool = new();

		public void Validate<T>()
		{
			if ( assetPool == null || assetPool.Count == 0 )
				throw new Exception( "WeightedTable has no assets" );
			if ( assetPool.Sum( x => x.weight ) == 0 )
				throw new Exception( "WeightedTable has no weights" );
			foreach ( var weightedAsset in assetPool )
			{
				if ( weightedAsset.asset is WeightedTableSchema weightedTableSchema )
					weightedTableSchema.Validate<T>();
				else if ( !( weightedAsset.asset is T ) )
					throw new Exception( $"WeightedTable has invalid asset type {weightedAsset.asset.GetType()}" );
			}
		}

		public BaseDataSchema GetRandomAsset( Utility.IRandom rng )
		{
			var totalWeight = assetPool.Sum( x => x.weight );
			var roll = rng.Range( 0, totalWeight );
			foreach ( var weightedAsset in assetPool )
			{
				roll -= weightedAsset.weight;
				if ( roll < 0 )
				{
					if ( weightedAsset.asset is WeightedTableSchema weightedTableSchema )
						return weightedTableSchema.GetRandomAsset( rng );
					return weightedAsset.asset;
				}
			}
			return null;
		}
	}

	[CreateAssetMenu( fileName = "WeightedTable", menuName = "NQ/Items/WeightedTable" )]
	public class WeightedTableSchema : BaseDataSchema
	{
		public WeightedTable table = new();

		public void Validate<T>() => table.Validate<T>();

		public BaseDataSchema GetRandomAsset( Utility.IRandom rng ) => table.GetRandomAsset( rng );
	}
}


#if UNITY_EDITOR
[CustomPropertyDrawer( typeof( Schema.Items.WeightedAsset ) )]
public class WeightedAssetEditor : PropertyDrawer
{
	public override VisualElement CreatePropertyGUI( SerializedProperty property )
	{
		VisualElement myInspector = new VisualElement();

		myInspector.Add( Utility.UI.CreatePropertyField( property, "asset", 80 ) );
		myInspector.Add( Utility.UI.CreatePropertyField( property, "weight", 20 ) );
		myInspector.style.flexDirection = new StyleEnum<FlexDirection>( FlexDirection.Row );
		myInspector.style.flexGrow = 1;

		return myInspector;
	}
}

#endif
