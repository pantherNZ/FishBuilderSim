using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class ChanceItemDrop<T>
{
	[Serializable]
	public class ItemDropListItem
	{
		public int weight = 1;
		public T item;
	}

	[Range( 0, 100 )]
	public int chanceToDrop;
	public List<ItemDropListItem> drops = new();

	public (T, Interval)? getDrop( Runtime.Terrain.DepthScaleData depth, Utility.IRandom rng = null )
	{
		var dropSelector = new WeightedSelector<ItemDropListItem>( rng );
		foreach ( var drop in drops )
		{
			dropSelector.AddItem( drop, drop.weight );
		}

		if ( !dropSelector.HasResult() )
			return default;

		var result = dropSelector.GetResult();
		return (result.item, null);
	}
}

public class WeightedItemDrop<T>
{
	[Serializable]
	public class ItemDropListItem
	{
		public Schema.Terrain.DepthWeightingCountKeys depthWeights = new();
		public T item;
		[HideInInspector] public Interval count; // Set dynamically below
	}

	public List<ItemDropListItem> drops = new();

	public (T, Interval)? getDrop( Runtime.Terrain.DepthScaleData depth, Utility.IRandom rng = null )
	{
		var dropSelector = new WeightedSelector<ItemDropListItem>( rng );
		foreach ( var drop in drops )
		{
			var dropKey = drop.depthWeights.Evaluate( depth );
			drop.count = dropKey.countRange;
			dropSelector.AddItem( drop, dropKey.weighting );
		}

		if ( !dropSelector.HasResult() )
			return default;

		var result = dropSelector.GetResult();
		return (result.item, result.count);
	}
}
