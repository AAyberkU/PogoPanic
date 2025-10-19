using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

[RequireComponent(typeof(Canvas))]
[DisallowMultipleComponent]
[AddComponentMenu("UI/Circle Transition Manager")]
public class CircleTransitionManager : MonoBehaviour
{
    public static CircleTransitionManager Instance { get; private set; }

    [Header("Child Image with CircleCutout material")]
    [SerializeField] private Image cutoutImage;          // auto-found if left null

    [Header("Timings")]
    [SerializeField] private float closeTime = 0.6f;
    [SerializeField] private float openTime  = 0.6f;
    [SerializeField] private Ease  closeEase = Ease.InCubic;
    [SerializeField] private Ease  openEase  = Ease.OutCubic;

    [Header("Circle radius (>1 = fully open, 0 = fully closed)")]
    [SerializeField] private float openRadius   = 1.2f;
    [SerializeField] private float closedRadius = 0f;

    int  _radiusID;
    bool _busy;

    // ─────────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (cutoutImage == null)
            cutoutImage = GetComponentInChildren<Image>(includeInactive: true);

        if (cutoutImage == null)
        {
            Debug.LogError("[CircleTransitionManager] No child Image with CircleCutout material found.");
            enabled = false;
            return;
        }

        _radiusID = Shader.PropertyToID("_Radius");
        cutoutImage.material.SetFloat(_radiusID, openRadius); // start fully open
    }

    // ─────────────────────────────────────────────────────────────────────
    public void LoadScene(string sceneName)
    {
        if (_busy || string.IsNullOrEmpty(sceneName)) return;
        StartCoroutine(TransitionRoutine(sceneName));
    }

    IEnumerator TransitionRoutine(string sceneName)
    {
        _busy = true;

        // 1 ▸ close completely
        yield return cutoutImage.material
            .DOFloat(closedRadius, _radiusID, closeTime)
            .SetEase(closeEase)
            .SetUpdate(true)
            .WaitForCompletion();

        // 2 ▸ begin async load and ALLOW activation immediately
        var op = SceneManager.LoadSceneAsync(sceneName);
        op.allowSceneActivation = true;

        // 3 ▸ wait until new scene is 100 % done
        while (!op.isDone) yield return null;
        yield return null;                 // ...plus one extra frame for cameras/UI

        // 4 ▸ reopen the circle
        yield return cutoutImage.material
            .DOFloat(openRadius, _radiusID, openTime)
            .SetEase(openEase)
            .SetUpdate(true)
            .WaitForCompletion();

        _busy = false;
    }
}
