// File: PauseExceptCamera.cs
using UnityEngine;
using System.Collections.Generic;

public class PauseExceptCamera : MonoBehaviour
{
    [Header("Hot‑key (toggle)")]
    [SerializeField] KeyCode toggleKey = KeyCode.T;

    [Header("Animator that must KEEP playing")]
    [SerializeField] Animator cameraAnimator;

    [Header("Particles that must KEEP playing (slow‑mo)")]
    [SerializeField] ParticleSystem[] particleSystems;

    [Tooltip("Speed multiplier while the world is paused (0.25 = ¼ speed).")]
    [Range(0.01f, 1f)]
    [SerializeField] float slowMoFactor = 0.25f;

    bool isPaused = true;                                       // start frozen

    // remember original sim‑speeds so we can restore them
    readonly Dictionary<ParticleSystem, float> originalSpeeds = new Dictionary<ParticleSystem, float>();

    void Awake()
    {
        //------------------------------------------------ camera
        if (cameraAnimator)
            cameraAnimator.updateMode = AnimatorUpdateMode.UnscaledTime;   // ignores Time.timeScale

        //------------------------------------------------ particles
        foreach (var ps in particleSystems)
        {
            if (!ps) continue;

            var main = ps.main;
            main.useUnscaledTime = true;                                    // keeps simulating :contentReference[oaicite:0]{index=0}
            originalSpeeds[ps] = main.simulationSpeed;                      // store default
            main.simulationSpeed = originalSpeeds[ps] * slowMoFactor;       // slow‑motion :contentReference[oaicite:1]{index=1}
            ps.Play();
        }

        Time.timeScale = 0f;   // freeze the rest of the scene
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (isPaused) ResumeWorld();
            else          PauseWorld();
        }
    }

    //----------------------------------------------------------------
    void PauseWorld()
    {
        if (isPaused) return;
        isPaused = true;
        Time.timeScale = 0f;                            // stop everything else

        // drop particle speed to slow‑mo
        foreach (var ps in particleSystems)
        {
            if (!ps) continue;
            var main = ps.main;
            main.simulationSpeed = originalSpeeds[ps] * slowMoFactor;
        }
    }

    void ResumeWorld()
    {
        if (!isPaused) return;
        isPaused = false;
        Time.timeScale = 1f;                            // world back to normal time

        // restore particle speed to normal
        foreach (var ps in particleSystems)
        {
            if (!ps) continue;
            var main = ps.main;
            main.simulationSpeed = originalSpeeds[ps];
        }
    }
}
