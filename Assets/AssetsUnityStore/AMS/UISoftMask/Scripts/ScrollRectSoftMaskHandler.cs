using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace AMS.UI.SoftMask
{
    [ExecuteAlways,
     AddComponentMenu("UI/AMS/Scroll Rect Soft Mask Handler"),
     RequireComponent(typeof(ScrollRect))]
    public class ScrollRectSoftMaskHandler : MonoBehaviour
    {
        [SerializeField,
         Tooltip(
             "Viewport margin threshold (in pixels) for visibility check.\nPositive = earlier / outside viewport;\nNegative = later / inside viewport.")]
        private float m_MarginThreshold = 0;

        private ScrollRect m_ScrollRect;
        private List<UISoftMask> m_SoftMasks = new();

        private readonly Vector3[] m_ViewportCorners = new Vector3[4];
        private readonly Vector3[] m_ChildCorners = new Vector3[4];

        private Vector3 m_ViewportMin;
        private Vector3 m_ViewportMax;

        private RectTransform m_ViewportRect = null;

        private RectTransform m_ContentRect = null;
        private Vector3 m_ContentPos = default;

        private int m_DessendantCount = -1;
        private int m_MaskCount = -1;

        private void OnEnable()
        {
            if (!m_ScrollRect)
                m_ScrollRect = GetComponent<ScrollRect>();

            if (!m_ViewportRect && m_ScrollRect.viewport is { } viewport)
                m_ViewportRect = viewport;

            if (!m_ContentRect && m_ScrollRect.content is { } content)
                m_ContentRect = content;

            CheckViewportData();
            CheckMasks();
        }

        private void OnDisable()
        {
            foreach (var mask in m_SoftMasks)
                if (mask)
                {
                    mask.enabled = true;
                    foreach (var g in mask.maskableObjects)
                        g.gameObject.SetActive(true);
                }
        }

        private void CheckViewportData()
        {
            if (!m_ViewportRect && m_ScrollRect.viewport is { } viewport)
                m_ViewportRect = viewport;

            if (!m_ViewportRect)
                return;

            m_ViewportRect.GetWorldCorners(m_ViewportCorners);

            m_ViewportMin = m_ViewportCorners[0]; // Bottom-left
            m_ViewportMax = m_ViewportCorners[2]; // Top-right
        }

        private void LateUpdate()
        {
            int descendants = transform.hierarchyCount;
            if (m_DessendantCount != descendants)
            {
                m_SoftMasks = GetComponentsInChildren<UISoftMask>(true).ToList();
                m_DessendantCount = descendants;
            }

            CheckMasks();
        }

        private void CheckMasks()
        {
            if (m_ContentRect &&
                (Vector3.Distance(m_ContentRect.position, m_ContentPos) > 0 ||
                 m_MaskCount != m_SoftMasks.Count))
            {
                m_ContentPos = m_ContentRect.position;
                m_MaskCount = m_SoftMasks.Count;

                CheckMasksInsideViewport();
            }
        }

        private void CheckMasksInsideViewport()
        {
            CheckViewportData();

            for (var i = 0; i < m_SoftMasks.Count; i++)
            {
                if (m_SoftMasks[i] is not { } mask)
                    continue;

                mask.SetMaskToActive(IsRectInsideViewport(mask.rectTransform));
            }
        }

        internal void AddUnique(UISoftMask mask)
        {
            if (!m_SoftMasks.Contains(mask))
                m_SoftMasks.Add(mask);
        }

        private bool IsRectInsideViewport(RectTransform child)
        {
            if (!child)
                return false;

            child.GetWorldCorners(m_ChildCorners);

            var threshold = new Vector3(m_MarginThreshold, m_MarginThreshold);
            Vector2 minWithMargin = m_ViewportMin - threshold;
            Vector2 maxWithMargin = m_ViewportMax + threshold;

            foreach (var corner in m_ChildCorners)
            {
                if (corner.x >= minWithMargin.x && corner.x <= maxWithMargin.x &&
                    corner.y >= minWithMargin.y && corner.y <= maxWithMargin.y)
                    return true;
            }

            var childMin = m_ChildCorners[0];
            var childMax = m_ChildCorners[2];

            if (minWithMargin.x >= childMin.x && minWithMargin.x <= childMax.x &&
                minWithMargin.y >= childMin.y && minWithMargin.y <= childMax.y)
            {
                return true;
            }

            return false;
        }
    }
}