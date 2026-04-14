using System.ComponentModel;
using Schema;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Runtime.Game
{
	[ExecuteAlways]
	public class GlobalConstantsHandler : MonoBehaviour
	{
		// Set via editor
		[SerializeField] bool constructRuntimeConstants = true;
		public Schema.GlobalConstants _constants;
		public Schema.UIConstants _uiConstants;

		public static GlobalConstantsHandler Instance;
		public static Schema.GlobalConstants Constants;
		public static Schema.UIConstants UIConstants;
		public static GlobalRuntimeConstants RuntimeConstants;


		private void OnEnable()
		{
			Instance = this;
			Constants = _constants;
			UIConstants = _uiConstants;

			if (constructRuntimeConstants)
				RuntimeConstants = new();

			SetupSeed();

			IEventSystem.EnableLogging = _constants.enableEventsLogging;
		}

		void SetupSeed()
		{
			if (!int.TryParse(Settings.Seed, out var seed))
#if UNITY_EDITOR
				seed = _constants.rngSeed.Length > 0 ? _constants.rngSeed.GetHashCode() : 0;
#else
				seed = Time.time.ToString().GetHashCode();
#endif
			Constants.runtimeRngSeed = Mathf.Abs(seed);

		}

		public static void Log(string str)
		{
			if (Constants.enableLogging)
				Debug.Log(str);
		}

		public static void LogWarning(string str)
		{
			if (Constants.enableLogging)
				Debug.LogWarning(str);
		}

		public static void LogError(string str)
		{
			if (Constants.enableLogging)
				Debug.LogError(str);
		}

		void Start()
		{
			if (RuntimeConstants != null)
				RuntimeConstants.Init();
		}

		void Update()
		{
			if (RuntimeConstants != null)
				RuntimeConstants.Update();
		}

		void OnApplicationQuit()
		{
			if (RuntimeConstants != null)
			{
				RuntimeConstants.Update();
			}
		}
	}
}
