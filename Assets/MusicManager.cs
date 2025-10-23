using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

[DisallowMultipleComponent]
[AddComponentMenu("Managers/Music Manager")]
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource mainMenuSource;
    [SerializeField] private AudioSource gameSource;

    [Header("Fade Settings")]
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private Ease fadeEase = Ease.InOutSine;

    private AudioSource _current;

    // ────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        
        // Safety setup for looping
        if (mainMenuSource != null)
        {
            mainMenuSource.loop = true;
            mainMenuSource.playOnAwake = false;
        }

        if (gameSource != null)
        {
            gameSource.loop = true;
            gameSource.playOnAwake = false;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    // ────────────────────────────────────────────────────────────────
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Stop *everything* before starting new track
        if (mainMenuSource != null) mainMenuSource.Stop();
        if (gameSource != null) gameSource.Stop();

        // Decide what to play
        if (scene.name.Contains("MainMenu"))
        {
            FadeTo(mainMenuSource);
        }
        else if (scene.name.Contains("FlippedDemo"))
        {
            FadeTo(gameSource);
        }
    }

// ────────────────────────────────────────────────────────────────
    private void FadeTo(AudioSource target)
    {
        if (target == null) return;

        // Prevent double-playing if already current
        if (_current == target && target.isPlaying)
            return;

        // Fade out current track cleanly
        if (_current != null && _current.isPlaying)
        {
            _current.DOFade(0f, fadeDuration)
                .SetEase(fadeEase)
                .OnComplete(() => _current.Stop());
        }

        // Immediately ensure target volume starts at 0, and loop is on
        target.volume = 0f;
        target.loop = true;
        target.Play();

        // Fade in target
        target.DOFade(1f, fadeDuration).SetEase(fadeEase);
        _current = target;
    }

    // ────────────────────────────────────────────────────────────────
    public void StopAllMusic()
    {
        if (_current != null)
        {
            _current.DOFade(0f, fadeDuration).SetEase(fadeEase)
                    .OnComplete(() => _current.Stop());
        }
    }
}