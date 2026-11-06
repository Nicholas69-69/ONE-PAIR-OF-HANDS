using System;
using UnityEngine;

namespace OnePairOfHands
{
    /// <summary>
    /// Player settings, saved with PlayerPrefs. Every change raises <see cref="Changed"/>,
    /// so the settings panel, the control bar, text scaling and audio stay in sync.
    /// </summary>
    public static class GameSettings
    {
        private const string KeyVolume = "opoh.masterVolume";
        private const string KeyTextSize = "opoh.textSize";
        private const string KeyDayLength = "opoh.dayLength";
        private const string KeyAudioOn = "opoh.audioOn";
        private const string KeyFullscreen = "opoh.fullscreen";
        private const string KeyTooltips = "opoh.tooltipsSeen";
        private const string KeyMinimap = "opoh.minimapOn";

        public static readonly string[] TextSizeLabels = { "S", "M", "L" };
        public static readonly float[] TextScales = { 0.85f, 1f, 1.2f };
        public static readonly string[] DayLengthLabels = { "Short 5 min", "Normal 8 min", "Long 12 min" };
        public static readonly float[] DayLengthMinutesOptions = { 5f, 8f, 12f };

        public static event Action Changed;

        private static int lastWindowedWidth = 1280;
        private static int lastWindowedHeight = 720;

        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(KeyVolume, 0.8f);
            set { PlayerPrefs.SetFloat(KeyVolume, Mathf.Clamp01(value)); Save(); }
        }

        /// <summary>0 = S, 1 = M, 2 = L.</summary>
        public static int TextSize
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyTextSize, 2), 0, 2); // default L
            set { PlayerPrefs.SetInt(KeyTextSize, Mathf.Clamp(value, 0, 2)); Save(); }
        }

        public static float TextScale => TextScales[TextSize];

        /// <summary>0 = Short, 1 = Normal, 2 = Long.</summary>
        public static int DayLength
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(KeyDayLength, 1), 0, 2);
            set { PlayerPrefs.SetInt(KeyDayLength, Mathf.Clamp(value, 0, 2)); Save(); }
        }

        public static float DayLengthMinutes => DayLengthMinutesOptions[DayLength];

        public static bool AudioOn
        {
            get => PlayerPrefs.GetInt(KeyAudioOn, 1) == 1;
            set { PlayerPrefs.SetInt(KeyAudioOn, value ? 1 : 0); Save(); }
        }

        /// <summary>Fullscreen only applies to desktop builds (the .exe).</summary>
        public static bool FullscreenSupported =>
            Application.platform == RuntimePlatform.WindowsPlayer ||
            Application.platform == RuntimePlatform.OSXPlayer ||
            Application.platform == RuntimePlatform.LinuxPlayer;

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(KeyFullscreen, 0) == 1;
            set
            {
                PlayerPrefs.SetInt(KeyFullscreen, value ? 1 : 0);
                ApplyDisplay();
                Save();
            }
        }

        public static bool MinimapOn
        {
            get => PlayerPrefs.GetInt(KeyMinimap, 1) == 1;
            set { PlayerPrefs.SetInt(KeyMinimap, value ? 1 : 0); Save(); }
        }

        /// <summary>All saved keys (used by the self-test to restore the player's settings exactly).</summary>
        public static readonly string[] AllKeys = { KeyVolume, KeyTextSize, KeyDayLength, KeyAudioOn, KeyFullscreen, KeyTooltips, KeyMinimap };

        public static bool TooltipsSeen
        {
            get => PlayerPrefs.GetInt(KeyTooltips, 0) == 1;
            set { PlayerPrefs.SetInt(KeyTooltips, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Effective listener volume: master volume, or silence when audio is off.</summary>
        public static float EffectiveVolume => AudioOn ? MasterVolume : 0f;

        public static void ApplyDisplay()
        {
            if (!FullscreenSupported) return;
            bool fs = PlayerPrefs.GetInt(KeyFullscreen, 0) == 1;
            if (fs)
            {
                if (Screen.fullScreenMode == FullScreenMode.Windowed)
                {
                    // Remember the window so switching back restores it.
                    lastWindowedWidth = Screen.width;
                    lastWindowedHeight = Screen.height;
                }
                var res = Screen.currentResolution;
                Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow);
            }
            else if (Screen.fullScreenMode != FullScreenMode.Windowed)
            {
                Screen.SetResolution(lastWindowedWidth, lastWindowedHeight, FullScreenMode.Windowed);
            }
        }

        private static void Save()
        {
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        /// <summary>Raise Changed without modifying anything (used after a bulk restore).</summary>
        public static void NotifyChanged() => Changed?.Invoke();
    }
}
