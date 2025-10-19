// File: SceneTimer.cs
using UnityEngine;
using TMPro;

public class SceneTimer : MonoBehaviour
{
    [Header("UI")]
    [Tooltip("Label that displays the timer. "
           + "If left empty the script uses the TMP on this GameObject.")]
    [SerializeField] private TextMeshProUGUI readout;

    [Header("Format")]
    [Tooltip("C# TimeSpan format; default shows minutes : seconds . milliseconds")]
    [SerializeField] private string timeFormat = @"mm\:ss\.fff";

    [Header("Random start range (seconds)")]   // ← NEW
    [SerializeField] private float startMinSeconds = 600f;   // 10 min
    [SerializeField] private float startMaxSeconds = 7200f;  // 2 h

    // ────────────────────────── state ───────────────────────────
    float  elapsed;          // seconds since start / last reset
    bool   isPaused;

    // ───────────────────────── Unity flow ───────────────────────
    void Awake()
    {
        if (!readout) readout = GetComponent<TextMeshProUGUI>();

        // Pick a random starting time >10 min and <2 h
        elapsed = Random.Range(startMinSeconds, startMaxSeconds);
        UpdateLabel();                    // show the chosen start value
    }

    void Update()
    {
        if (isPaused) return;

        elapsed += Time.deltaTime;
        UpdateLabel();
    }

    // ───────────────────────── methods ──────────────────────────
    public void PauseTimer()    => isPaused = true;
    public void ContinueTimer() => isPaused = false;

    public void ResetTimer()
    {
        elapsed = 0f;
        UpdateLabel();
    }

    public void SaveTimer()
    {
        Debug.Log("[SceneTimer] SaveTimer() called – implement your own persistence logic.");
    }

    public void LoadTimer()
    {
        Debug.Log("[SceneTimer] LoadTimer() called – implement your own persistence logic.");
        UpdateLabel();
    }

    // ───────────────────────── helpers ──────────────────────────
    void UpdateLabel()
    {
        if (!readout) return;

        System.TimeSpan t = System.TimeSpan.FromSeconds(elapsed);
        readout.text = t.ToString(timeFormat);
    }
}
