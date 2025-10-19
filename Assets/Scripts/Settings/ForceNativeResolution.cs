// File: ForceNativeResolution.cs
using UnityEngine;

/// <summary>
/// Forces the game window to the monitor's *desktop* resolution as early
/// as possible so UI scale is always correct.  Add this to a bootstrap
/// object in your very first scene (or mark the object DontDestroyOnLoad).
/// </summary>
public class ForceNativeResolution : MonoBehaviour
{
    [Tooltip("Only do this once per launch (recommended). "
             + "If false, it will run every time this component awakes.")]
    [SerializeField] bool runOnlyOnce = true;

    static bool alreadyDone;          // session flag

    void Awake()
    {
        if (runOnlyOnce && alreadyDone) return;

        // 1️⃣  Desktop resolution (= current display mode outside the game)
        int nativeW = Display.main.systemWidth;
        int nativeH = Display.main.systemHeight;
        int hz      = Screen.currentResolution.refreshRate > 0
            ? Screen.currentResolution.refreshRate
            : 60;                              // sensible fallback

        // 2️⃣  If the window is *already* native, leave it
        if (Screen.width  == nativeW &&
            Screen.height == nativeH)
        {
            alreadyDone = true;
            return;
        }

        // 3️⃣  Apply
        Screen.SetResolution(nativeW, nativeH,
            Screen.fullScreenMode, hz);

        Debug.Log($"[ForceNativeResolution] Switched to "
                  + $"{nativeW}×{nativeH}@{hz} Hz ({Screen.fullScreenMode})");

        alreadyDone = true;
    }
}