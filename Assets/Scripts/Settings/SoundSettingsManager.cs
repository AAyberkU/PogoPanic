// File: SoundSettingsManager.cs
// Purpose: bind the four Hex sliders to one AudioMixer with
//          exposed parameters "MasterVol", "MusicVol", "SFXVol", "UIVol".

using UnityEngine;
using UnityEngine.Audio;
using Michsky.UI.Hex;
using Pogo.Core;          // the GameSettings static prefs wrapper

public class SoundSettingsManager : MonoBehaviour
{
    //──────────────────────────────────────────────────────────────
    [Header("Slider references (Hex SliderManager)")]
    [SerializeField] private SliderManager masterSlider;
    [SerializeField] private SliderManager musicSlider;
    [SerializeField] private SliderManager sfxSlider;
    [SerializeField] private SliderManager uiSlider;

    [Header("Single AudioMixer asset (with exposed params)")]
    [SerializeField] private AudioMixer mainMixer;

    // exposed parameter names inside *mainMixer*
    const string MASTER_PARAM = "Master";
    const string MUSIC_PARAM  = "Music";
    const string SFX_PARAM    = "SFX";
    const string UI_PARAM     = "UI";

    //──────────────────────────────────────────────────────────────
    void Awake()
    {
        // 1. Initialise sliders from saved prefs
        if (masterSlider) masterSlider.mainSlider.value = GameSettings.MasterVolume;
        if (musicSlider)  musicSlider .mainSlider.value = GameSettings.MusicVolume;
        if (sfxSlider)    sfxSlider   .mainSlider.value = GameSettings.SfxVolume;
        if (uiSlider)     uiSlider    .mainSlider.value = GameSettings.UiVolume;

        // 2. Apply volumes immediately
        ApplyMaster(GameSettings.MasterVolume);
        ApplyMusic (GameSettings.MusicVolume);
        ApplySfx   (GameSettings.SfxVolume);
        ApplyUi    (GameSettings.UiVolume);

        // 3. Listen to slider changes
        if (masterSlider) masterSlider.onValueChanged.AddListener(ApplyMaster);
        if (musicSlider)  musicSlider .onValueChanged.AddListener(ApplyMusic);
        if (sfxSlider)    sfxSlider   .onValueChanged.AddListener(ApplySfx);
        if (uiSlider)     uiSlider    .onValueChanged.AddListener(ApplyUi);
    }

    //──────────────────────────────────────────────────────────────
    // Handlers
    //──────────────────────────────────────────────────────────────
    void ApplyMaster(float linear)
    {
        GameSettings.MasterVolume = linear;
        float dB = LinearToDb(linear);

        if (mainMixer)  mainMixer.SetFloat(MASTER_PARAM, dB);
        else            AudioListener.volume = linear;   // emergency fallback
    }

    void ApplyMusic(float linear)
    {
        GameSettings.MusicVolume = linear;
        if (mainMixer) mainMixer.SetFloat(MUSIC_PARAM, LinearToDb(linear));
    }

    void ApplySfx(float linear)
    {
        GameSettings.SfxVolume = linear;
        if (mainMixer) mainMixer.SetFloat(SFX_PARAM, LinearToDb(linear));
    }

    void ApplyUi(float linear)
    {
        GameSettings.UiVolume = linear;
        if (mainMixer) mainMixer.SetFloat(UI_PARAM, LinearToDb(linear));
    }

    //──────────────────────────────────────────────────────────────
    // Utility: convert linear 0‑1 to decibels  (‑80 dB ≈ mute)
    //──────────────────────────────────────────────────────────────
    static float LinearToDb(float lin) =>
        lin <= 0.0001f ? -80f : Mathf.Log10(lin) * 20f;
}
