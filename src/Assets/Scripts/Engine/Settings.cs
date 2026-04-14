using System.Text.RegularExpressions;
using UnityEngine;

namespace Runtime
{
    /// <summary>
    /// Stateless accessor for all persisted player settings.
    /// Values are read directly from PlayerPrefs so they are always up-to-date
    /// without requiring a MonoBehaviour instance.
    /// </summary>
    public static class Settings
    {
        // Player Prefs Keys
        const string playerPrefsSeedKey = "Seed";
        const string playerPrefsMusicVolumeKey = "MusicVolume";
        const string playerPrefsSfxVolumeKey = "SfxVolume";
        const string playerPrefsMasterVolumeKey = "MasterVolume";
        const string playerPrefsUiVolumeKey = "UiVolume";
        const string playerPrefsResolutionKey = "Resolution";
        const string playerPrefsRefreshRateKey = "RefreshRate";
        const string playerPrefsFullscreenKey = "Fullscreen";
        const string playerPrefsGameFpsLimitKey = "GameFpsLimit";
        const string playerPrefsMenuFpsLimitKey = "MenuFpsLimit";
        const string playerPrefsLastInputTypeKey = "LastInputType";

        // ──────────────────────────────────────────────────────────────────────
        // Defaults
        // ──────────────────────────────────────────────────────────────────────

        public const int DefaultGameFpsLimit = -1;  // -1 = unlimited
        public const int DefaultMenuFpsLimit = 60;
        public const float DefaultMasterVolume = 1f;
        public const float DefaultMusicVolume = 1f;
        public const float DefaultSfxVolume = 1f;
        public const float DefaultUiVolume = 1f;
        public const bool DefaultFullscreen = true;
        public const int DefaultLastInputType = 0; // 0 = KeyboardMouse



        // ──────────────────────────────────────────────────────────────────────
        // Gameplay
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Returns the saved seed, or an empty string if no seed is saved.</summary>
        public static string Seed
        {
            get => PlayerPrefs.GetString(playerPrefsSeedKey, string.Empty);
            set => PlayerPrefs.SetString(playerPrefsSeedKey, value);
        }

        // ──────────────────────────────────────────────────────────────────────
        // FPS
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Returns the saved in-game FPS limit. -1 means unlimited.</summary>
        public static int GameFpsLimit
        {
            get => PlayerPrefs.GetInt(playerPrefsGameFpsLimitKey, DefaultGameFpsLimit);
            set => PlayerPrefs.SetInt(playerPrefsGameFpsLimitKey, value);
        }

        /// <summary>Returns the saved menu FPS limit.</summary>
        public static int MenuFpsLimit
        {
            get => PlayerPrefs.GetInt(playerPrefsMenuFpsLimitKey, DefaultMenuFpsLimit);
            set => PlayerPrefs.SetInt(playerPrefsMenuFpsLimitKey, value);
        }
        // ──────────────────────────────────────────────────────────────────────
        // Audio
        // ──────────────────────────────────────────────────────────────────────

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(playerPrefsMasterVolumeKey, DefaultMasterVolume);
            set => PlayerPrefs.SetFloat(playerPrefsMasterVolumeKey, value);
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(playerPrefsMusicVolumeKey, DefaultMusicVolume);
            set => PlayerPrefs.SetFloat(playerPrefsMusicVolumeKey, value);
        }

        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(playerPrefsSfxVolumeKey, DefaultSfxVolume);
            set => PlayerPrefs.SetFloat(playerPrefsSfxVolumeKey, value);
        }

        public static float UIVolume
        {
            get => PlayerPrefs.GetFloat(playerPrefsUiVolumeKey, DefaultUiVolume);
            set => PlayerPrefs.SetFloat(playerPrefsUiVolumeKey, value);
        }

        // ──────────────────────────────────────────────────────────────────────
        // Display
        // ──────────────────────────────────────────────────────────────────────

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(playerPrefsFullscreenKey, DefaultFullscreen ? 1 : 0) > 0;
            set => PlayerPrefs.SetInt(playerPrefsFullscreenKey, value ? 1 : 0);
        }

        /// <summary>Returns the saved resolution, falling back to the current screen resolution.</summary>
        public static Vector2Int Resolution
        {
            get
            {
                var raw = PlayerPrefs.GetString(playerPrefsResolutionKey, string.Empty);
                return ParseResolution(raw);
            }
            set => PlayerPrefs.SetString(playerPrefsResolutionKey, $"{value.x}x{value.y}");
        }

        /// <summary>Returns the saved refresh rate, falling back to the current screen refresh rate.</summary>
        public static RefreshRate RefreshRate
        {
            get
            {
                var raw = PlayerPrefs.GetString(playerPrefsRefreshRateKey, string.Empty);
                return ParseRefreshRate(raw);
            }
            set => PlayerPrefs.SetString(playerPrefsRefreshRateKey, $"{value.numerator}/{value.denominator}");
        }

        /// <summary>Persists all settings to PlayerPrefs in a single Save call.</summary>
        public static void Save() => PlayerPrefs.Save();


        // ──────────────────────────────────────────────────────────────────────
        // Parsing helpers (shared with SettingsView)
        // ──────────────────────────────────────────────────────────────────────

        static readonly Regex ResolutionRegex = new(@"(\d+)x(\d+)");
        static readonly Regex RefreshRateRegex = new(@"(\d+)/(\d+)");

        public static Vector2Int ParseResolution(string value)
        {
            var match = ResolutionRegex.Match(value ?? string.Empty);
            if (!match.Success)
                return new Vector2Int(Screen.currentResolution.width, Screen.currentResolution.height);

            var width = int.TryParse(match.Groups[1].Value, out var w) ? w : Screen.currentResolution.width;
            var height = int.TryParse(match.Groups[2].Value, out var h) ? h : Screen.currentResolution.height;
            return new Vector2Int(width, height);
        }

        public static RefreshRate ParseRefreshRate(string value)
        {
            var match = RefreshRateRegex.Match(value ?? string.Empty);
            if (!match.Success)
                return Screen.currentResolution.refreshRateRatio;

            if (!uint.TryParse(match.Groups[1].Value, out var numerator) ||
                 !uint.TryParse(match.Groups[2].Value, out var denominator) ||
                 denominator == 0)
                return Screen.currentResolution.refreshRateRatio;

            return new RefreshRate { numerator = numerator, denominator = denominator };
        }
    }
}
