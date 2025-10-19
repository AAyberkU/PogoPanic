using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [ExecuteAlways, RequireComponent(typeof(RectTransform)), DisallowMultipleComponent]
    public class RectUV : MonoBehaviour
    {
        private Canvas m_Canvas;

        protected Canvas canvas => m_Canvas
            ? m_Canvas
            : m_Canvas =
                rectTransform && rectTransform.GetComponentsInParent<Canvas>() is { Length: > 0 } canvasArray
                    ? canvasArray.First()
                    : null;

        private RectTransform m_CanvasTransform;

        private RectTransform canvasTransform =>
            m_CanvasTransform ? m_CanvasTransform :
            canvas is { } foundCanvas ? m_CanvasTransform = (RectTransform)foundCanvas.transform : null;

        private RectTransform m_RectTransform;

        public RectTransform rectTransform => m_RectTransform ? m_RectTransform :
            GetComponent<RectTransform>() is
                { } foundRect ? m_RectTransform = foundRect : null;

        private Vector2 m_RectUVSize;

        private Matrix4x4 m_WorldCanvasMatrix = Matrix4x4.identity;
        private Matrix4x4 m_OverlayCanvasMatrix = Matrix4x4.identity;

        internal readonly RectProperties m_RectProperties = new RectProperties();

        [Serializable]
        internal class RectProperties
        {
            public Vector3 pos;
            public Vector3 rotation;
            public Vector2 size;
            public Vector2 pivot;
            public Vector3 scale;

            public float scaleFactor = 1;
            public bool gamma2linear;

            public bool HasChange(Canvas canvas, RectTransform rect)
            {
                if (canvas)
                {
                    gamma2linear = canvas.vertexColorAlwaysGammaSpace;
                    if (!Mathf.Approximately(canvas.scaleFactor, scaleFactor))
                    {
                        scaleFactor = canvas.scaleFactor;
                        return true;
                    }
                }

                var transformChanged = pos != rect.position ||
                                       pivot != rect.pivot ||
                                       rotation != rect.eulerAngles ||
                                       size != rect.sizeDelta ||
                                       scale != rect.lossyScale; //TODO: check parent & child count?

                if (!transformChanged)
                    return false;

                UpdateValues(rect);
                return true;
            }

            private void UpdateValues(RectTransform rect)
            {
                rotation = rect.eulerAngles;
                pos = rect.position;
                size = rect.sizeDelta;
                pivot = rect.pivot;
                scale = rect.lossyScale;
            }
        }

        internal delegate void OnBeginContextRendering(List<Camera> cameras);

        internal OnBeginContextRendering m_OnBeginContextRendering;

        internal delegate void DuringPreCullCamera(Camera cam);

        internal DuringPreCullCamera m_DuringCameraPreRender;

        internal void OnEnable()
        {
            CheckCurrentRenderPipeline(GraphicsSettings.defaultRenderPipeline);
#if UNITY_2021_3
            RenderPipelineManager.activeRenderPipelineTypeChanged += RenderPipelineTypeChanged;
#else
            RenderPipelineManager.activeRenderPipelineAssetChanged += RenderPipelineAssetChanged;
#endif
        }

        internal void OnDisable()
        {
#if UNITY_2021_3
            RenderPipelineManager.activeRenderPipelineTypeChanged -= RenderPipelineTypeChanged;
#else
            RenderPipelineManager.activeRenderPipelineAssetChanged -= RenderPipelineAssetChanged;
#endif
            m_OnBeginContextRendering = null;
            m_DuringCameraPreRender = null;
            RenderPipelineManager.beginContextRendering -= BeginContextRendering;
            Camera.onPreRender -= OnCameraPreRender;
        }

#if UNITY_2021_3
        private void RenderPipelineTypeChanged()
        {
            CheckCurrentRenderPipeline(RenderPipelineManager.currentPipeline != null);
        }
#else
        private void RenderPipelineAssetChanged(RenderPipelineAsset from, RenderPipelineAsset to)
        {
            CheckCurrentRenderPipeline(to);
        }
#endif

        private void CheckCurrentRenderPipeline(bool hasRenderPipeline)
        {
            if (hasRenderPipeline)
            {
                Camera.onPreRender -= OnCameraPreRender;
                RenderPipelineManager.beginContextRendering += BeginContextRendering;
            }
            else
            {
                RenderPipelineManager.beginContextRendering -= BeginContextRendering;
                Camera.onPreRender += OnCameraPreRender;
            }
#if UNITY_EDITOR
            RepaintGameAndSceneViews();
#endif
        }

        private void BeginContextRendering(ScriptableRenderContext context, List<Camera> cameras)
        {
            m_OnBeginContextRendering?.Invoke(cameras);
        }

        private void OnCameraPreRender(Camera cam)
        {
            m_DuringCameraPreRender?.Invoke(cam);
        }

        protected void UpdateWorldRectParams(RectTransform targetRectTransform)
        {
            var rect = targetRectTransform.rect;

            var rectSize = rect.size;

            m_RectUVSize.x = rectSize.x;
            m_RectUVSize.y = rectSize.y;

            m_WorldCanvasMatrix = Matrix4x4.TRS(
                targetRectTransform.TransformPoint(new Vector3(rect.x, rect.y, 0f)),
                targetRectTransform.rotation,
                targetRectTransform.lossyScale).inverse;

            if (canvas is { renderMode: RenderMode.ScreenSpaceOverlay })
                m_OverlayCanvasMatrix = m_WorldCanvasMatrix * canvasTransform.localToWorldMatrix;
        }

        /// <summary>
        /// Return true if rectUV has changed.
        /// </summary>
        /// <param name="overrideTransform">Override transform to decouple RectUV.</param>
        /// <returns></returns>
        protected bool HasChangedRectUV(RectTransform overrideTransform = null)
        {
            var targetRect = overrideTransform ? overrideTransform : rectTransform;

            if (!m_RectProperties.HasChange(canvas, targetRect))
                return false;

            UpdateWorldRectParams(targetRect);
            return true;
        }

        /// <summary>
        /// Set material rect params;
        /// </summary>
        /// <param name="material"></param>
        protected void SetMaterialRectParams(Material material)
        {
            material.SetVector(s_RectUvSizeID, m_RectUVSize);
            material.SetMatrix(s_WorldCanvasMatrixID, m_WorldCanvasMatrix);
            material.SetMatrix(s_OverlayCanvasMatrixID, m_OverlayCanvasMatrix);
        }
    }
}