using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

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

	[Serializable]
	public enum PartSize
	{
		Small,
		Medium,
		Large
	}

	[CreateAssetMenu(fileName = "GameConstants", menuName = "FishBuilderSim/Constants/GameConstants")]
	public class GlobalConstants : ScriptableObject
	{
		[Header("General")]
		public string rngSeed; // empty means non-fixed/random
		[ReadOnly] public int runtimeRngSeed; // empty means non-fixed/random
		public SerializableDictionary<PartSize, float> partRadii;

		[Header("Logging")]
		public bool enableLogging = false;
		public bool enableEventsLogging = false;
		public bool enableSaving = false;
		public bool enableCheatParts = false;

		//[Header("Balance")]

		[Header("Saves")]
		[HideInInspector] public string RootSavePath = "SaveGames";
		//[HideInInspector] public string InventoryOutpostSavePath => "Inventory/outpost";

		public List<PartSchema> CheatParts;
		public List<PartSchema> StartingParts;
		public PartSchema defaultAttack;
		public PartSchema defaultDefend;
		public PartSchema defaultForage;

		[Header("Starting Encounters")]
		[Tooltip("Maps each starting part key/id to ordered start encounters. First entries are used for the initial encounters.")]
		public StartingPartEncounterDictionary StartingPartEncounters = new();

		[Header("Encounter Difficulty")]
		public float DifficultyScalerModifier = 0.1f;
		public AnimationCurve HPScaling;
		public AnimationCurve AttackScaling;
		public AnimationCurve DefenseScaling;
		public AnimationCurve ForageScaling;
		public AnimationCurve SizeScaling;
		public AnimationCurve EncounterDifficultyScaling;

		[Header("Assets")]
		public Sprite defaultSpeciesPortrait;


		[Header("UI")]
		public VisualTreeAsset genericPopupTemplate;
		public VisualTreeAsset yesNoPopupTemplate;
		public VisualTreeAsset closePopupTemplate;
		public float BodyRadiusMultiplier = 0.92f;
		[Tooltip("Pixels of player body radius per point of Size.")]
		public float PlayerSizePixelsPerUnit = 12f;


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
