using System;
using System.Linq;
using System.Collections.Generic;
using Runtime.Audio;
using Runtime.Game;
using Runtime.Items;
using Schema.Items;
using UnityEngine;
using Runtime.Terrain;

namespace Schema.Loot
{
	[Serializable]
	public class ItemDrop
	{
		public BaseItemSchema item;
		public int tier;
		public int count = 1;
	}

	[Serializable]
	public class WeightedLootItem : WeightedItemDrop<ItemDrop> { }
	[Serializable]
	public class ChanceLootItem : ChanceItemDrop<ItemDrop> { }

	[CreateAssetMenu(fileName = "LootSchema", menuName = "NQ/Loot")]
	public class LootSchema : ScriptableObject
	{
		public IntervalInt numLootItems = new IntervalInt(1);
		public List<WeightedLootItem> weightedLootItems = new();
		public List<ChanceLootItem> chanceLootItems = new();

		// If true, we try to distribute numLootItems across weightedLootItems & chanceLootItems
		// Will still be random if the numLootItems is less than (or not a multiple) of the loot items
		public bool evenlySelectLootItems = false;

		public (ItemDrop, Interval)? GetDrop(WeightedLootItem drop, DepthScaleData depth, Utility.IRandom rng)
		{
			return drop.getDrop(depth, rng);
		}
	}
}
