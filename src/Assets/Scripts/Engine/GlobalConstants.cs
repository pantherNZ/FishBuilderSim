using System;
using System.Collections.Generic;
using UnityEngine;

namespace Schema
{
	[Serializable]
	public class EncounterSchemaList
	{
		public List<EncounterSchema> encounters = new();
	}

	[Serializable]
	public class StartingPartEncounterDictionary : SerializableDictionary<PartSchema, EncounterSchemaList>
	{
	}

	[CreateAssetMenu(fileName = "GameConstants", menuName = "FishBuilderSim/Constants/GameConstants")]
	public class GlobalConstants : ScriptableObject
	{

		[Header("General")]
		public string rngSeed; // empty means non-fixed/random
		[ReadOnly] public int runtimeRngSeed; // empty means non-fixed/random

		[Header("Logging")]
		public bool enableLogging = false;
		public bool enableEventsLogging = false;

		//[Header("Balance")]

		[Header("Saves")]
		[HideInInspector] public string RootSavePath = "SaveGames";
		//[HideInInspector] public string InventoryOutpostSavePath => "Inventory/outpost";

		public List<PartSchema> StartingParts;

		[Header("Starting Encounters")]
		[Tooltip("Maps each starting part key/id to ordered start encounters. First entries are used for the initial encounters.")]
		public StartingPartEncounterDictionary StartingPartEncounters = new();

		// Callback
		public event Action onConstantsChanged;

		void OnValidate()
		{
			onConstantsChanged?.Invoke();

			runtimeRngSeed = Mathf.Abs(rngSeed.GetHashCode());
		}

		public int GenerateSeed(params int[] vars)
			=> GenerateSeedStatic(runtimeRngSeed, vars);

		public static int GenerateSeedStatic(int runtimeRngSeed, params int[] vars)
		{
			var result = runtimeRngSeed;
			for (int i = 0; i < vars.Length; ++i)
				result = (result * 9176) + vars[i];
			return Utility.Mod(result, 23423645);
		}
	}
}
