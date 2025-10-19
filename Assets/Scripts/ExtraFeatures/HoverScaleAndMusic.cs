// File: HoverScaleAndMusic.cs
using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(Collider))]
public class HoverScaleAndMusic : MonoBehaviour
{
    [Header("Scale")]
    [Tooltip("Multiply the original scale by this amount while hovering")]
    [SerializeField] float scaleFactor = 1.1f;
    [SerializeField] float tweenTime   = 0.15f;

    Vector3 baseScale;

    [Header("Animation")]
    [SerializeField] Animator animator;
    [SerializeField] string   openTrigger  = "Open";
    [SerializeField] string   closeTrigger = "Close";

    [Header("Music")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip[] tracks;          // assign clips in order
    int trackIndex;

    // ─────────────────────────────────────────────────────────────
    void Awake()
    {
        baseScale = transform.localScale;
        if (!animator)    animator    = GetComponent<Animator>();
        if (!audioSource) audioSource = FindAnyObjectByType<AudioSource>();
    }

    // ─────────────────────────  HOVER  ───────────────────────────
    void OnMouseEnter()
    {
        TweenScale(baseScale * scaleFactor);
        animator?.SetTrigger(openTrigger);
    }

    void OnMouseExit()
    {
        TweenScale(baseScale);
        animator?.SetTrigger(closeTrigger);
    }

    // ─────────────────────────  CLICK  ───────────────────────────
    void OnMouseDown()
    {
        if (audioSource == null || tracks == null || tracks.Length == 0) return;

        audioSource.clip = tracks[trackIndex];
        audioSource.Play();
        trackIndex = (trackIndex + 1) % tracks.Length;   // loop through list
    }

    // ─────────────────────────  helper  ──────────────────────────
    void TweenScale(Vector3 target)
    {
        DOTween.Kill(transform);                    // cancel previous tween
        transform.DOScale(target, tweenTime)
            .SetEase(Ease.OutQuad)
            .SetUpdate(true);                  // ignore Time.timeScale
    }
}