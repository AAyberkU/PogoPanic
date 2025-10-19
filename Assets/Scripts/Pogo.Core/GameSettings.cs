// File: GameSettings.cs
// Purpose: centralised getter / setter for all user‑configurable options.
// Nothing here touches the UI.  You simply read or assign the properties;
// the value is saved instantly in PlayerPrefs.

using UnityEngine;

namespace Pogo.Core   // use any namespace you prefer
{
    public static class GameSettings
    {
        // ──────────────────────────────────────────────────────────────
        // KEYS (const so there is no typo risk)
        // ──────────────────────────────────────────────────────────────
        const string RESOLUTION_KEY          = "gs_resolutionIndex";
        const string FPSCAP_KEY              = "gs_fpsCapIndex";
        const string MONITOR_KEY             = "gs_monitorIndex";
        const string MOTIONBLUR_KEY          = "gs_motionBlur";
        const string MASTER_VOL_KEY          = "gs_masterVol";
        const string MUSIC_VOL_KEY           = "gs_musicVol";
        const string SFX_VOL_KEY             = "gs_sfxVol";
        const string UI_VOL_KEY              = "gs_uiVol";
        const string LANGUAGE_KEY            = "gs_languageIndex";
        const string CONTROLLER_KEY          = "gs_controllerIndex";
        const string CAMERA_SENS_KEY         = "gs_cameraSens";
        const string CONTROL_SENS_KEY        = "gs_controlSens";

        // ──────────────────────────────────────────────────────────────
        // ENUM helpers (optional, purely for code readability)
        // ──────────────────────────────────────────────────────────────
        public enum FpsCap { Unlimited, _240, _144, _120, _60, _30 }
        public enum ControllerType { KeyboardMouse, PlayStation, Xbox }

        // ──────────────────────────────────────────────────────────────
        // PROPERTIES  (read ⇄ write auto‑saves)
        // ──────────────────────────────────────────────────────────────

        // Dropdown – resolution index
        public static int ResolutionIndex
        {
            get => PlayerPrefs.GetInt(RESOLUTION_KEY, 0);
            set => PlayerPrefs.SetInt(RESOLUTION_KEY, value);
        }

        // HorizontalSelector – fps cap
        public static FpsCap FrameCap
        {
            get => (FpsCap)PlayerPrefs.GetInt(FPSCAP_KEY, 0);
            set => PlayerPrefs.SetInt(FPSCAP_KEY, (int)value);
        }

        // Dropdown – monitor index
        public static int MonitorIndex
        {
            get => PlayerPrefs.GetInt(MONITOR_KEY, 0);
            set => PlayerPrefs.SetInt(MONITOR_KEY, value);
        }

        // Switch – motion blur
        public static bool MotionBlur
        {
            get => PlayerPrefs.GetInt(MOTIONBLUR_KEY, 1) == 1;
            set => PlayerPrefs.SetInt(MOTIONBLUR_KEY, value ? 1 : 0);
        }

        // Volume sliders (0–1)
        public static float MasterVolume
        {
            get => PlayerPrefs.GetFloat(MASTER_VOL_KEY, 1f);
            set => PlayerPrefs.SetFloat(MASTER_VOL_KEY, Mathf.Clamp01(value));
        }
        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(MUSIC_VOL_KEY, 1f);
            set => PlayerPrefs.SetFloat(MUSIC_VOL_KEY, Mathf.Clamp01(value));
        }
        public static float SfxVolume
        {
            get => PlayerPrefs.GetFloat(SFX_VOL_KEY, 1f);
            set => PlayerPrefs.SetFloat(SFX_VOL_KEY, Mathf.Clamp01(value));
        }
        public static float UiVolume
        {
            get => PlayerPrefs.GetFloat(UI_VOL_KEY, 1f);
            set => PlayerPrefs.SetFloat(UI_VOL_KEY, Mathf.Clamp01(value));
        }

        // Dropdown – language index
        public static int LanguageIndex
        {
            get => PlayerPrefs.GetInt(LANGUAGE_KEY, 0);
            set => PlayerPrefs.SetInt(LANGUAGE_KEY, value);
        }

        // Dropdown – controller type
        public static ControllerType ActiveController
        {
            get => (ControllerType)PlayerPrefs.GetInt(CONTROLLER_KEY, 0);
            set => PlayerPrefs.SetInt(CONTROLLER_KEY, (int)value);
        }

        // Sensitivity sliders (0–1)
        public static float CameraSensitivity
        {
            get => PlayerPrefs.GetFloat(CAMERA_SENS_KEY, 0.5f);
            set => PlayerPrefs.SetFloat(CAMERA_SENS_KEY, Mathf.Clamp01(value));
        }
        public static float ControlSensitivity
        {
            get => PlayerPrefs.GetFloat(CONTROL_SENS_KEY, 0.5f);
            set => PlayerPrefs.SetFloat(CONTROL_SENS_KEY, Mathf.Clamp01(value));
        }

        // ──────────────────────────────────────────────────────────────
        // Convenience: wipe everything (e.g., for a “Reset Defaults” btn)
        // ──────────────────────────────────────────────────────────────
        public static void ResetToDefaults()
        {
            PlayerPrefs.DeleteKey(RESOLUTION_KEY);
            PlayerPrefs.DeleteKey(FPSCAP_KEY);
            PlayerPrefs.DeleteKey(MONITOR_KEY);
            PlayerPrefs.DeleteKey(MOTIONBLUR_KEY);
            PlayerPrefs.DeleteKey(MASTER_VOL_KEY);
            PlayerPrefs.DeleteKey(MUSIC_VOL_KEY);
            PlayerPrefs.DeleteKey(SFX_VOL_KEY);
            PlayerPrefs.DeleteKey(UI_VOL_KEY);
            PlayerPrefs.DeleteKey(LANGUAGE_KEY);
            PlayerPrefs.DeleteKey(CONTROLLER_KEY);
            PlayerPrefs.DeleteKey(CAMERA_SENS_KEY);
            PlayerPrefs.DeleteKey(CONTROL_SENS_KEY);
            PlayerPrefs.Save();
        }
    }
}
