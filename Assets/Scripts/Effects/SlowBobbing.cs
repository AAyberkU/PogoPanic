using UnityEngine;
using DG.Tweening;   // ← make sure DOTween is installed (Tools ▸ Demigiant ▸ DOTween Setup)

/// <summary>
/// Simple bobbing motion: moves the object up a small distance, then back down,
/// repeating forever.  Uses DOTween for smooth easing.
/// </summary>
public class SlowBobbing : MonoBehaviour
{
    [Header("Motion Settings")]
    [Tooltip("Vertical distance (units) moved up from the start position.\n" +
             "The object will travel the same distance back down.")]
    [SerializeField] private float amplitude = 0.15f;   // “really small”

    [Tooltip("Seconds it takes to go from bottom to top (or top to bottom).\n" +
             "Complete cycle duration is 2 × period.")]
    [SerializeField] private float period = 3.5f;       // “really slow”

    [Tooltip("Choose an easing for the bob.\n" +
             "InOutSine feels like gentle breathing.")]
    [SerializeField] private Ease ease = Ease.InOutSine;

    // Cache the starting Y so we can return to it on disable / enable
    private float startY;
    private Tween bobTween;

    void OnEnable()
    {
        startY = transform.position.y;

        // Build a Yoyo loop: up by +amplitude, then back to start.
        bobTween = transform
            .DOMoveY(startY + amplitude, period)
            .SetEase(ease)
            .SetLoops(-1, LoopType.Yoyo);   // loop forever
    }

    void OnDisable()
    {
        // Kill the tween and snap exactly back to the original height
        bobTween?.Kill();
        Vector3 pos = transform.position;
        pos.y = startY;
        transform.position = pos;
    }

#if UNITY_EDITOR
    // Draw the travel range in the Scene view for convenience
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 a = transform.position;
        Vector3 b = a + Vector3.up * amplitude;
        Gizmos.DrawLine(a, b);
        Gizmos.DrawSphere(a, 0.02f);
        Gizmos.DrawSphere(b, 0.02f);
    }
#endif
}
