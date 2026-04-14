using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace Runtime.Game
{
	public class GlobalRuntimeConstants : Save.BaseSave.IThreadManager
	{
		public GameObject localPlayer;
		public GameObject outpost;
		public UnityEngine.UIElements.VisualElement centralElement;

		Save.SaveMetaData _saveMetaData;

		Schema.GlobalConstants _constants => GlobalConstantsHandler.Constants;

		private HashSet<Save.BaseSave> _pendingSaves = new HashSet<Save.BaseSave>();

		int? _mainThreadId;
		public static string GameName;
		public static int SaveIdx;

		public void Init()
		{
			_mainThreadId = Thread.CurrentThread.ManagedThreadId;

			Save.BaseSave save = saveMetaData;
			//save = terrainSpawnNodeListSave;
		}

		public bool IsMainThread()
		{
			Debug.Assert(_mainThreadId.HasValue, "Thread manager not initialized!");
			return Thread.CurrentThread.ManagedThreadId == _mainThreadId.Value;
		}

		public void ScheduleSave(Save.BaseSave save)
		{
			_pendingSaves.Add(save);
		}

		public void Update()
		{
			foreach (var save in _pendingSaves)
			{
				save.Save();
			}
			_pendingSaves.Clear();
		}

		public string GetPath(string ending)
		{
			return $"{_constants.RootSavePath}/{GameName}/World/{ending}";
		}

		public T GetSave<T>(string ending) where T : Save.BaseSave
		{
			var save = Save.SaveManager.Instance.Get<T>(GetPath(ending));
			save.threadManager = this;
			return save;
		}

		public Save.SaveMetaData saveMetaData
		{
			get
			{
				_saveMetaData ??= Save.SaveManager.Instance.Get<Save.SaveMetaData>($"{_constants.RootSavePath}/{GameName}");
				_saveMetaData.threadManager = this;
				_saveMetaData.gameName = GameName;
				_saveMetaData.saveIdx = SaveIdx;
				_saveMetaData.pendingSave = true;
				_saveMetaData.alwaysAutoSave = true;
				if (!_saveMetaData.ExistsOnDisk())
					_saveMetaData.Save();
				return _saveMetaData;
			}
		}

		// public Save.PlayerSave playerSave
		// {
		// 	get
		// 	{
		// 		_playerSave ??= GetSave<Save.PlayerSave>(_constants.PlayerSavePath);
		// 		return _playerSave;
		// 	}
		// }

	}
}
