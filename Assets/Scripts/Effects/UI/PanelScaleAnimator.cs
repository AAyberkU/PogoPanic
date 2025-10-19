using UnityEngine;
using DG.Tweening;

[RequireComponent(typeof(RectTransform))]
public class PanelScaleAnimator : MonoBehaviour
{
    [Header("Timings")]
    [SerializeField] float duration = 0.50f;

    [Header("Easing / Feel")]
    [SerializeField] float overshootScale = 1.1f;
    [SerializeField] Ease  openEase  = Ease.OutBack;
    [SerializeField] Ease  closeEase = Ease.InBack;

    public bool IsOpen { get; private set; }          // ← NEW

    Vector3 _hidden = Vector3.zero;
    Vector3 _shown  = Vector3.one;

    void Awake()
    {
        if (!IsOpen)
        {
            transform.localScale = _hidden;
            gameObject.SetActive(false);
        }
    }

    // --------------------------------------------------------------------
    public Sequence Open()
    {
        if (IsOpen) return null;
        IsOpen = true;

        DOTween.Kill(transform);
        gameObject.SetActive(true);
        transform.localScale = _hidden;

        Sequence s = DOTween.Sequence();
        s.Append(transform.DOScale(_shown * overshootScale, duration * 0.6f).SetEase(openEase));
        s.Append(transform.DOScale(_shown,                    duration * 0.4f).SetEase(Ease.OutElastic));
        return s;
    }

    public Sequence Close()
    {
        if (!IsOpen) return null;
        IsOpen = false;

        DOTween.Kill(transform);

        Sequence s = DOTween.Sequence();
        s.Append(transform.DOScale(_shown * overshootScale, duration * 0.15f).SetEase(Ease.OutQuad));
        s.Append(transform.DOScale(_hidden,                duration).SetEase(closeEase))
            .OnComplete(() => gameObject.SetActive(false));
        return s;
    }
}