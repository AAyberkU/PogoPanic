using UnityEngine;
using DG.Tweening;                 // Make sure DOTween is installed

/// <summary>
/// Elastic scale-bounce for Michsky.UI.Hex ButtonManager.
/// Hook AnimateIn → onHover / onSelect
/// and  AnimateOut → onLeave / onDeselect.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class ElasticScaleOnHover : MonoBehaviour
{
    [Header("Target sizes")]
    [Tooltip("Final scale once the animation settles")]
    [SerializeField] float hoverScale = 1.1f;         // 110 %

    [Tooltip("First, we ‘over-shoot’ to this scale before settling")]
    [SerializeField] float overshootScale = 1.25f;    // 125 %

    [Header("Timings")]
    [SerializeField] float overshootTime = 0.10f;     // seconds
    [SerializeField] float settleTime   = 0.22f;     // seconds
    [SerializeField] float outTime      = 0.15f;     // return time

    [Header("Easing")]
    [SerializeField] Ease overshootEase = Ease.OutQuart;
    [SerializeField] Ease settleEase    = Ease.OutElastic;
    [SerializeField] Ease outEase       = Ease.InBack;

    Vector3 _baseScale;

    void Awake() => _baseScale = transform.localScale;
    

    public void AnimateIn()          // onHover / onSelect
    {
        DOTween.Kill(transform);                     // stop any running tween on this transform

        Sequence jump = DOTween.Sequence().SetUpdate(true);
        jump.Append(transform                          // quick “pop past” the target
            .DOScale(_baseScale * overshootScale, overshootTime)
            .SetEase(overshootEase));

        jump.Append(transform                          // elastic snap-back and settle
            .DOScale(_baseScale * hoverScale, settleTime)
            .SetEase(settleEase));
    }

    public void AnimateOut()         // onLeave / onDeselect
    {
        DOTween.Kill(transform);
        transform.DOScale(_baseScale, outTime)        // glide back home
                 .SetEase(outEase)
                 .SetUpdate(true);
    }
}
