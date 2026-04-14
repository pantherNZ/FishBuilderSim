using System;
using UnityEngine.InputSystem;

namespace Runtime.Events
{
	// General
	public class RequestGlobalSave : BaseEvent<RequestGlobalSave> { }

	// Game State
	public class GamePaused : BaseEvent<GamePaused> { public bool showPauseUi; }
	public class GameResumed : BaseEvent<GameResumed> { }
	public class SettingsModified : BaseEvent<SettingsModified> { }

}
