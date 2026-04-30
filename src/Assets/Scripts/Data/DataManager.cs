using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using System.Collections.ObjectModel;
using UnityEditor;
using UnityEngine.TextCore.Text;
using System.Xml.Serialization;
using UnityEngine.InputSystem;


#if UNITY_EDITOR
using UnityEditor.Build;
#endif

namespace Schema
{
	public enum DataType
	{
		Audio,
		Encounters,
		Parts,
		Species,

		// Add new entires before Misc
		Misc,
	}

	[ExecuteAlways]
	[DisallowMultipleComponent]
	public class DataManager : MonoBehaviour
	{
		public struct DataTypeParams
		{
			public DataTypeParams(string path, Type type, bool recursiveLoad = false)
			{
				this.path = path;
				this.type = type;
				this.recursiveLoad = recursiveLoad;
			}

			public string path;
			public Type type;
			public bool recursiveLoad;
		};

		public Dictionary<DataType, DataTypeParams> DataSourcePaths => new()
		{
			{ DataType.Audio, new( "Data/Audio/", typeof( Audio.AudioDataSchema ), recursiveLoad: true )  },
			{ DataType.Encounters, new( "Data/Encounters/", typeof( EncounterSchema ), recursiveLoad: true )  },
			{ DataType.Parts, new( "Data/Parts/", typeof( PartSchema ), recursiveLoad: true )  },
			{ DataType.Species, new( "Data/Species/", typeof( SpeciesSchema ), recursiveLoad: true )  },

			// Add new entries before Misc
			{ DataType.Misc, new( "Data/", typeof( BaseDataSchema ),  recursiveLoad: true ) },
		};

		// Singleton
		static DataManager dataManager;
		public static DataManager Instance { get { return dataManager; } }

		static SerializableHashSet<BaseDataSchema> registeredAssets = new();


		Dictionary<int, BaseDataSchema> allDataAssets = new();



		List<Audio.AudioDataSchema> audioClips;
		public Dictionary<Audio.AudioDataSchema, List<Audio.AudioDataSchema>> GroupedAudioClips;
		public ReadOnlyCollection<Audio.AudioDataSchema> AudioClips => audioClips.AsReadOnly();

		List<EncounterSchema> encounters;
		public ReadOnlyCollection<EncounterSchema> Encounters => encounters.AsReadOnly();

		List<PartSchema> parts;
		public ReadOnlyCollection<PartSchema> Parts => parts.AsReadOnly();

		List<SpeciesSchema> species;
		public ReadOnlyCollection<SpeciesSchema> Species => species.AsReadOnly();


		public static string GetDataJsonResourcePath(DataType source) => $"DataPaths/{source}";
		public static string GetDataJsonPathFull(DataType source) => $"{Application.dataPath}/Resources/{GetDataJsonResourcePath(source)}.json";

		private void OnEnable()
		{
			Init();
		}

		private void Init()
		{
			if (dataManager != null && dataManager != this)
			{
				Debug.LogError("Multiple data managers found!");
				Destroy(gameObject);
				return;
			}

			dataManager = this;
			LoadAllData();
		}

		public BaseDataSchema FindAssetByHash(int hash)
		{
			if (allDataAssets.TryGetValue(hash, out var result))
				return result;
			Debug.LogError($"DataManager failed to find asset by hash: {hash}");
			return null;
		}

		// Game strings
		//public string GetGameString(GameStringType key)
		//	=> gameStrings.GetGameString(key);
		//public string GetGameString(StatType stat, bool errorOnFail = true)
		//	=> gameStrings.GetGameString(stat, errorOnFail);
		// public string GetGameStringFormatted(GameStringType key, int arg)
		// 	=> gameStrings.GetGameStringFormatted(key, arg);
		// public string GetGameStringFormatted(GameStringType key, string arg)
		// 	=> gameStrings.GetGameStringFormatted(key, arg);
		// public string GetGameStringFormatted(StatType stat, int arg, bool errorOnFail = true)
		// 	=> gameStrings.GetGameStringFormatted(stat, arg, errorOnFail);

#if UNITY_EDITOR
		[MenuItem("Scripts/Regenerate Game Data")]
		public static void LoadDataEditor()
		{
			new PreBuildFileNamesSaver().OnPreprocessBuild(null);
			DataManager.Instance.GenerateAllData();
		}

		public void GenerateAllData()
		{
			GenerateData();
		}
#endif

		public void LoadAllData()
		{
			LoadData();
		}

		public static string StringToEnumVal(string val)
			=> val.Replace(" ", string.Empty).Replace("-", string.Empty);

		private List<T> LoadDataOfType<T>(DataType dataType, bool allowEmptyResults = false) where T : BaseDataSchema
		{
			var path = GetDataJsonResourcePath(dataType);
			var fileListJson = Resources.Load<UnityEngine.TextAsset>(path);

#if UNITY_EDITOR
			if (fileListJson == null)
			{
				new PreBuildFileNamesSaver().OnPreprocessBuild(null);
				fileListJson = Resources.Load<UnityEngine.TextAsset>(path);
			}
#endif

			if (fileListJson == null)
			{
				Debug.LogError($"LoadDataOfType failed to find file list json for {dataType} at path: {path}\nDid you miss adding the entry to DataSourcePaths?");
				return null;
			}

			var filePaths = JsonHelper.FromJson<string>(fileListJson.text);
			return LoadDataOfType<T>(path, filePaths, allowEmptyResults);
		}

		private List<T> LoadDataOfType<T>(string path, string[] filePaths, bool allowEmptyResults = false) where T : BaseDataSchema
		{
			if (filePaths.IsEmpty())
			{
				Debug.LogError($"LoadDataOfType failed to find any assets from path: {path}");
				return null;
			}

			var dataFiles = filePaths
				.Select(x => (x, Resources.Load(x)))
				.Where(x => x.Item2 != null && typeof(T).IsAssignableFrom(x.Item2.GetType())).ToList();
			var results = new List<T>();

			foreach (var (newAssetPath, newAsset) in dataFiles)
				if (AddDataAsset(newAssetPath, newAsset as T))
					results.Add(newAsset as T);

			if (!allowEmptyResults && results.IsEmpty())
			{
				Debug.LogError($"LoadDataOfType failed to find any matching assets of type {typeof(T)} from path: {path}");
				return null;
			}

			return results.OrderBy(x => x.name).ToList();
		}

		private bool AddDataAsset(string path, BaseDataSchema newAsset)
		{
			if (!registeredAssets.Contains(newAsset))
			{
				newAsset.Init(true);
				registeredAssets.Add(newAsset);
			}

			// Debug hash collision checker
			var found = allDataAssets.TryGetValue(newAsset.GetHashCode(), out var existing);

#if UNITY_EDITOR
			if (found && path != existing.path)
			{
				existing.Init(true);
				newAsset.Init(true);
				found = allDataAssets.TryGetValue(newAsset.GetHashCode(), out existing);
			}

			if (found && path != existing.path)
			{
				Debug.LogError($"Data type hash collision detected between: {path}   |   {existing.path}");
				return false;
			}
			newAsset.path = path;
#endif

			if (!found)
			{
				allDataAssets.Add(newAsset.GetHashCode(), newAsset);

				if (Application.isPlaying)
					newAsset.OnDataLoaded();
			}

			return !found;
		}

#if UNITY_EDITOR
		private void GenerateData()
		{
			LoadData();

			GenerateEnumSchema(nameof(AudioClips), typeof(Audio.AudioDataSchema), "AudioType", audioClips.Select(x => x.name), prependFlags: new string[] { "None" });
			//GenerateGameStringsEnum( "GameStringType", gameStrings.gameStrings[( int )Language.English].strings.Select( x => x.key ) );

			AssetDatabase.SaveAssets();
		}
#endif

		private void LoadData()
		{
			allDataAssets.Clear();

			registeredAssets = new(registeredAssets.Where(obj => obj != null).ToHashSet());

			audioClips = LoadDataOfType<Audio.AudioDataSchema>(DataType.Audio);

			GroupedAudioClips = audioClips.Where(x => x.groupedWith != null).GroupBy(x => x.groupedWith).ToDictionary(g => g.Key, g => g.ToList());
			foreach (var (key, group) in GroupedAudioClips)
			{
				group.Add(key);
			}

			//			var gameStringFiles = LoadDataOfType<GameStringsSchema>(DataType.GameStrings);
			//			gameStrings = new GameStringsManager(gameStringFiles);

			encounters = LoadDataOfType<EncounterSchema>(DataType.Encounters);
			parts = LoadDataOfType<PartSchema>(DataType.Parts);
			species = LoadDataOfType<SpeciesSchema>(DataType.Species);

			// Load all assets generically so they are cached and hashed
			LoadDataOfType<BaseDataSchema>(DataType.Misc, allowEmptyResults: true);

#if UNITY_EDITOR
			// Try to find assets outside of Data folder
			var wrongFolderDataAssets = new List<string>();
			Utility.GetResourcePaths("", ref wrongFolderDataAssets);
			foreach (var found in LoadDataOfType<BaseDataSchema>("", wrongFolderDataAssets.ToArray(), allowEmptyResults: true))
				Debug.LogError($"LoadDataOfType found a BaseDataSchema asset outside of the data folder: {Utility.GetResourcePath(found)}");
#endif
		}


		void GenerateEnumSchema(string dataContainer, Type dataType, string name, IEnumerable<string> values, bool generateEnumFlags = false, IEnumerable<string> extraFlags = null, IEnumerable<string> prependFlags = null)
		{
			int enumOffset = 0;
			values = values.Where(x => !x.IsEmpty()).Select(x => StringToEnumVal(x));
			if (extraFlags != null)
				extraFlags = extraFlags.Where(x => !x.IsEmpty());
			if (prependFlags != null)
			{
				foreach (var v in prependFlags)
				{
					values = values.Prepend(v);
					enumOffset++;
				}
			}
			if (generateEnumFlags)
			{
				values = values.Prepend("None");
				++enumOffset;

				if (values.Count() + (extraFlags != null ? extraFlags.Count() : 0) >= 32)
					Debug.LogError("Over 32 flags is not supported!");
			}

#if UNITY_EDITOR
			var lines =
@"// This file is manually generated by DataManager.cs, don't modify directly
// Entries are generated from found {1} scriptable objects in the Data folder

public static partial class {0}Utility
{{
	public static {1} GetSchema( this Schema.{5} enumValue )
	{{
		return Schema.DataManager.Instance.{2}[( int )enumValue{3}];
	}}
}}

namespace Schema
{{
	public enum {5}
	{{
		{4}
		_Count
	}}
";
			lines = lines.Format(
				name,
				dataType.ToString(),
				dataContainer,
				enumOffset > 0 ? (" - " + enumOffset) : string.Empty,
				string.Join("\r\n\t\t", values.Select(x => x + ',')),
				name);

			var path = $"{Application.dataPath}/Scripts/Schema/{name}Schema_Generated.cs";

			if (generateEnumFlags)
			{
				var flags = values.Skip(1);
				if (extraFlags != null)
					flags = flags.Concat(extraFlags);

				var flagsLines = @"
	[System.Flags]
	public enum {1}Flags
	{{
		None,
		{0}
		All = ~0
	}}";
				lines += flagsLines.Format(
					string.Join("\r\n\t\t",
					flags.Select((x, idx) => $"{x} = 1 << {idx},")),
					name);
			}

			lines += "\r\n}";

			if (!File.Exists(path) || File.ReadAllText(path) != lines)
				File.WriteAllText(path, lines);
#endif
		}

		void GenerateGameStringsEnum(string name, IEnumerable<string> values)
		{
			values = values.Where(x => !x.IsEmpty()).Select(x => StringToEnumVal(x));

#if UNITY_EDITOR
			var lines =
@"// This file is manually generated by DataManager.cs, don't modify directly
// Entries are generated from English GameStrings keys

public static partial class {0}Utility
{{
	public static string GetString( this Schema.{0} enumValue )
	{{
		return Schema.DataManager.Instance.GetGameString(enumValue);
	}}
}}

namespace Schema
{{
	public enum {0}
	{{
		{1}
		_Count
	}}
";
			lines = lines.Format(
				name,
				string.Join("\r\n\t\t", values.Select(x => x + ',')));

			var path = $"{Application.dataPath}/Scripts/Schema/{name}Schema_Generated.cs";

			lines += "\r\n}";

			if (!File.Exists(path) || File.ReadAllText(path) != lines)
				File.WriteAllText(path, lines);
#endif
		}
	}

	public static class JsonHelper
	{
		public static T[] FromJson<T>(string json)
		{
			Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>(json);
			return wrapper.Items;
		}

		public static string ToJson<T>(T[] array, bool prettyPrint = false)
		{
			Wrapper<T> wrapper = new Wrapper<T> { Items = array };
			return JsonUtility.ToJson(wrapper, prettyPrint);
		}

		[Serializable]
		private class Wrapper<T>
		{
			public T[] Items;
		}
	}

#if UNITY_EDITOR
	class PreBuildFileNamesSaver : IPreprocessBuildWithReport
	{
		public int callbackOrder { get { return 0; } }
		public void OnPreprocessBuild(UnityEditor.Build.Reporting.BuildReport _)
		{
			var allAssets = new HashSet<string>();

			foreach (var (source, dataParams) in DataManager.Instance.DataSourcePaths)
			{
				List<string> filePaths = new();
				Utility.GetResourcePaths(dataParams.path, ref filePaths, recursive: dataParams.recursiveLoad);

				filePaths.RemoveAll(x => !dataParams.type.IsAssignableFrom(Resources.Load(x)?.GetType()));

				if (source == DataType.Misc)
					filePaths.RemoveAll(x => allAssets.Contains(x));

				var fileInfoJson = JsonHelper.ToJson(filePaths.ToArray(), true);
				var path = DataManager.GetDataJsonPathFull(source);
				if (!File.Exists(path) || File.ReadAllText(path) != fileInfoJson)
					File.WriteAllText(path, fileInfoJson);

				if (source != DataType.Misc)
					foreach (var assetPath in filePaths)
						allAssets.Add(assetPath);
			}

			AssetDatabase.Refresh();
		}
	}
#endif
}
