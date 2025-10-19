// File: VisualSettingsManager.cs
using System.Collections.Generic;
using UnityEngine;
using Michsky.UI.Hex;
using Pogo.Core;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class VisualSettingsManager : MonoBehaviour
{
    [Header("UI Widgets (Hex)")]
    [SerializeField] Dropdown           resolutionDropdown;
    [SerializeField] Dropdown           monitorDropdown;
    [SerializeField] HorizontalSelector fpsSelector;
    [SerializeField] SwitchManager      motionBlurSwitch;

    [Header("Volume Profile (contains Motion-Blur override)")]
    [SerializeField] VolumeProfile motionBlurProfile;

    MotionBlur motionBlur;
    readonly List<Resolution> resolutions = new();
    int monitorCount;

    /* ─────────────────────────  UNITY FLOW  ───────────────────── */
    void Awake()
    {
        /* 1️⃣  Force a safe window FIRST so the menu is click-able */
        ForceStartupResolution();          // 1920×1080, full-screen-mode unchanged

        /* Then continue as normal                                */
        CacheMotionBlur();
        BuildResolutionList();
        BuildMonitorList();
        RestoreFromPrefs();
        WireEvents();
    }

    /* ─────────────  FORCE 1920×1080 AT BOOT  ───────────── */
    void ForceStartupResolution()
    {
        // Only do it once per launch; skip if we’re already ≥ Full-HD
        const int MIN_WIDTH  = 1920;
        const int MIN_HEIGHT = 1080;

        if (Screen.width  >= MIN_WIDTH &&
            Screen.height >= MIN_HEIGHT)
            return;                         // good enough – leave as-is

        int hz = Screen.currentResolution.refreshRate > 0
               ? Screen.currentResolution.refreshRate
               : 60;

        Screen.SetResolution(MIN_WIDTH, MIN_HEIGHT,
                             Screen.fullScreenMode, hz);

        Debug.Log($"[VisualSettings] Forced startup resolution to "
                + $"{MIN_WIDTH}×{MIN_HEIGHT}@{hz} Hz");
    }

    /* ─────────────────────────  INITIALISATION  ───────────────────── */
    void CacheMotionBlur()
    {
        if (motionBlurProfile) motionBlurProfile.TryGet(out motionBlur);
    }

    void BuildResolutionList()
    {
        resolutions.Clear();
        resolutions.AddRange(Screen.resolutions);

        // Ensure 1920×1080 is present (Unity sometimes omits it)
        if (!resolutions.Exists(r => r.width == 1920 && r.height == 1080))
            resolutions.Add(new Resolution { width = 1920, height = 1080,
                                              refreshRate = 60 });

        resolutions.Sort((a,b) => a.width == b.width ?
                                   a.height.CompareTo(b.height) :
                                   a.width.CompareTo(b.width));

        resolutionDropdown.items.Clear();
        foreach (var r in resolutions)
            resolutionDropdown.CreateNewItem(
                $"{r.width}×{r.height} {r.refreshRate} Hz", notify:false);
        resolutionDropdown.Initialize();
    }

    void BuildMonitorList()
    {
        monitorCount = Display.displays.Length;
        monitorDropdown.items.Clear();
        for (int i = 0; i < monitorCount; ++i)
            monitorDropdown.CreateNewItem($"Monitor {i+1}", notify:false);
        monitorDropdown.Initialize();
    }

    void RestoreFromPrefs()
    {
        /* Resolution – just take what’s currently active so the
           dropdown matches what we forced a moment ago              */
        int idx = resolutions.FindIndex(r => r.width  == Screen.width &&
                                             r.height == Screen.height);
        if (idx < 0) idx = 0;

        resolutionDropdown.SetDropdownIndex(idx);
        GameSettings.ResolutionIndex = idx;

        /* FPS, monitor, motion-blur (unchanged) */
        fpsSelector.index = (int)GameSettings.FrameCap;
        fpsSelector.UpdateUI();

        monitorDropdown.SetDropdownIndex(
            Mathf.Clamp(GameSettings.MonitorIndex, 0, monitorCount-1));

        if (motionBlurSwitch) motionBlurSwitch.isOn = GameSettings.MotionBlur;
        ApplyMotionBlur(GameSettings.MotionBlur);
    }

    /* ─────────────────────────  EVENT WIRING  ───────────────────── */
    void WireEvents()
    {
        resolutionDropdown.onValueChanged.AddListener(ApplyResolution);
        monitorDropdown   .onValueChanged.AddListener(ApplyMonitor);
        fpsSelector       .onValueChanged.AddListener(ApplyFpsCap);
        motionBlurSwitch  .onValueChanged.AddListener(ApplyMotionBlur);
    }

    /* ─────────────────────────  APPLY METHODS  ───────────────────── */
    void ApplyResolution(int idx)
    {
        idx = Mathf.Clamp(idx, 0, resolutions.Count-1);
        var r = resolutions[idx];
        Screen.SetResolution(r.width, r.height, Screen.fullScreenMode, r.refreshRate);
        GameSettings.ResolutionIndex = idx;
    }

    void ApplyFpsCap(int idx)
    {
        int[] caps = { -1, 240, 144, 120, 60, 30 };
        Application.targetFrameRate = caps[Mathf.Clamp(idx,0,caps.Length-1)];
        GameSettings.FrameCap = (GameSettings.FpsCap)idx;
    }

    void ApplyMonitor(int idx)
    {
        GameSettings.MonitorIndex = Mathf.Clamp(idx, 0, monitorCount-1);
        Debug.Log("[VisualSettings] Monitor change stored (restart required).");
    }

    void ApplyMotionBlur(bool enable)
    {
        GameSettings.MotionBlur = enable;
        if (motionBlur) motionBlur.active = enable;
    }
}
