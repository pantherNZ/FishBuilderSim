using System;
using UnityEngine;

namespace Schema
{
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
