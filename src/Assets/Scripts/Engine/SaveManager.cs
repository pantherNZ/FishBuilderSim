using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Save
{
	[ExecuteAlways]
	[DisallowMultipleComponent]
	public class SaveManager : MonoEventReceiver
	{
		// Singleton
		static SaveManager _instance;
		public static SaveManager Instance { get { return _instance; } }

		public float autoSaveIntervalSeconds = 300f;

		readonly Dictionary<string, BaseSave> _saves = new();

		List<string> nestedGetCalls = new();

		private void Awake()
		{
			if (_instance != null && _instance != this)
			{
				Debug.LogError("Multiple managers found!");
				Destroy(gameObject);
				return;
			}

			_instance = this;
		}

		private void Start()
		{
			StartCoroutine(TrackAutoSaving());
		}

		System.Collections.IEnumerator TrackAutoSaving()
		{
			while (true)
			{
				SaveDirtySaves();
				yield return new WaitForSeconds(autoSaveIntervalSeconds);
			}
		}

		void SaveDirtySaves()
		{
			foreach (var save in _saves.Values)
			{
				if (save.pendingSave || save.alwaysAutoSave)
				{
					save.Save();
					save.pendingSave = false;
				}
			}
		}

		protected override void OnDestroy()
		{
			base.OnDestroy();
		}

		string CleanPath(string path) => path.Replace("\\", "/");

		public T Get<T, Args>(string path, params Args[] args) where T : BaseSave
		{
			path = CleanPath(path);

			if (_saves.ContainsKey(path))
			{
				object value = _saves[path];
				if (value is T typed)
					return typed;
				else
					Debug.LogError($"Key {path} is not of type {typeof(T)}");
			}

			T savable;
			if (args.Length == 0)
				savable = Activator.CreateInstance(typeof(T), path) as T;
			else if (args.Length == 1)
				savable = Activator.CreateInstance(typeof(T), path, args[0]) as T;
			else
				savable = Activator.CreateInstance(typeof(T), path, args) as T;

			_saves.Add(path, savable);
			return savable;
		}

		public T Get<T>(string path) where T : BaseSave
		{
			path = CleanPath(path);

			if (_saves.ContainsKey(path))
			{
				object value = _saves[path];
				if (value is T typed)
					return typed;
				else
					Debug.LogError($"Key {path} is not of type {typeof(T)}");
			}

			if (nestedGetCalls.Contains(path))
			{
				Debug.LogError($"Looping get call detected for {path}");
				return null;
			}

			nestedGetCalls.Add(path);
			T savable = Activator.CreateInstance(typeof(T), path) as T;
			_saves.Add(path, savable);
			nestedGetCalls.Remove(path);
			return savable;
		}
		public List<T> GetFromDirectory<T, Args>(string path, params Args[] args) where T : BaseSave
		{
			path = CleanPath(path);

			List<T> saves = new();
			if (!Directory.Exists(Application.persistentDataPath + "/" + path))
				return saves;
			var files = Directory.GetFiles(Application.persistentDataPath + "/" + path);
			foreach (var file in files)
				saves.Add(Get<T, Args>(Util.GetRelativeDirectory(path, file), args));
			return saves;
		}

		public List<T> GetFromDirectory<T>(string path) where T : BaseSave
		{
			path = CleanPath(path);

			List<T> saves = new();
			if (!Directory.Exists(Application.persistentDataPath + "/" + path))
				return saves;
			var files = Directory.GetFiles(Application.persistentDataPath + "/" + path);
			foreach (var file in files)
				saves.Add(Get<T>(Util.GetRelativeDirectory(path, file)));
			return saves;
		}

		private void OnApplicationQuit()
		{
			Runtime.Events.RequestGlobalSave.Trigger(new());

			SaveDirtySaves();
		}

		public void DeleteSave(string path)
		{
			path = CleanPath(path);
			_saves.RemoveAll(x => x.Key.StartsWith(path));
			string fullPath = Application.persistentDataPath + "/" + path;
			if (Directory.Exists(fullPath))
				Directory.Delete(fullPath, true);

			var metaPath = fullPath + ".json";
			if (File.Exists(metaPath))
				File.Delete(metaPath);
		}
	}
}
