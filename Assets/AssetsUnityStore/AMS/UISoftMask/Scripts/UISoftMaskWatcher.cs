using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [ExecuteAlways,
     RequireComponent(typeof(MaskableGraphic)), DisallowMultipleComponent]
    public class UISoftMaskWatcher : MonoBehaviour, IMaterialModifier, IMeshModifier
    {
        private UISoftMask m_SoftMask;

        internal UISoftMask softMask
        {
            set
            {
                if (!m_SoftMask || m_SoftMask != value)
                {
                    m_SoftMask = value;
                    m_MaskableObject.SetAllDirty();
                }
            }
            get => m_SoftMask ? m_SoftMask : null;
        }

        private MaskableGraphic m_MaskableObject;

        internal MaskableGraphic maskableObject => m_MaskableObject;

        private TMPTextForUISoftMask m_TMPText;

        private Material m_ExternalMaterial;

        private Material m_LateBaseMaterial;

        private Shader m_LateShader;

        internal Material materialForRendering => m_MaskableObject.materialForRendering;

        private const int k_TMPMaskDataUVChannel = 1;

        private void Awake()
        {
            hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        private void OnEnable()
        {
            if (!m_MaskableObject)
                m_MaskableObject = GetComponent<MaskableGraphic>();

            if (!m_TMPText && m_MaskableObject is TMPTextForUISoftMask tmpText)
                m_TMPText = tmpText;

            if (m_TMPText)
                m_TMPText.AfterGenerateTextMesh += ModifyTMPMesh;

            m_MaskableObject.SetVerticesDirty();
        }

        private void OnDisable()
        {
            if (m_TMPText)
                m_TMPText.AfterGenerateTextMesh -= ModifyTMPMesh;

            if (m_ExternalMaterial)
                m_SoftMask?.UnregisterExternalMaterial(m_MaskableObject, m_ExternalMaterial);
        }

        private void OnTransformParentChanged()
        {
            if (GetComponentsInParent<UISoftMask>().FirstOrDefault() is { } parentMask)
            {
                if (!m_SoftMask || parentMask != m_SoftMask)
                    m_SoftMask = parentMask;
            }
            else if (m_SoftMask)
            {
                m_SoftMask = null;
                m_MaskableObject.SetAllDirty();
            }
        }

        public Material GetModifiedMaterial(Material baseMaterial)
        {
            m_LateBaseMaterial = baseMaterial;

            CheckExternalMaterial();

            Material selectedMaterial = null;

            if (m_SoftMask && m_SoftMask.enabled && m_MaskableObject.maskable)
            {
                if (m_TMPText)
                {
                    if (m_SoftMask.GetTMPInstanceMaskMaterial(m_TMPText) is { } tmpMat)
                        selectedMaterial = tmpMat;
                }
                else if (m_ExternalMaterial)
                {
                    if (MaterialHasSoftMask(m_ExternalMaterial))
                        selectedMaterial = m_SoftMask.GetInstanceExternalMaterial(m_ExternalMaterial);
                }
                else if (m_SoftMask.GetMaskMaterial() is { } softMaskMaterial)
                    selectedMaterial = softMaskMaterial;
            }

            if (selectedMaterial)
            {
                if (!selectedMaterial.HasFloat(s_StencilID))
                    return selectedMaterial;

                selectedMaterial.SetFloat(s_StencilCompID, baseMaterial.GetFloat(s_StencilCompID));
                selectedMaterial.SetFloat(s_StencilID, baseMaterial.GetFloat(s_StencilID));
                selectedMaterial.SetFloat(s_StencilOpID, baseMaterial.GetFloat(s_StencilOpID));
                selectedMaterial.SetFloat(s_StencilReadMaskID, baseMaterial.GetFloat(s_StencilReadMaskID));
                selectedMaterial.SetFloat(s_StencilWriteMaskID, baseMaterial.GetFloat(s_StencilWriteMaskID));
                return selectedMaterial;
            }

            // TODO : Check if we want to reassign relative TMP material  
            if (!m_TMPText || !m_SoftMask)
                return m_LateBaseMaterial;

            FontMaterialData.FindData(m_SoftMask.TMPFontMaterialData, m_TMPText.font, out var materialData);
            return materialData == null
                ? m_LateBaseMaterial
                : materialData.GetRelativeKeyMaterial(m_TMPText.fontSharedMaterial);
        }

        private void CheckExternalMaterial()
        {
            var usingMaterial = m_LateBaseMaterial == m_MaskableObject.defaultMaterial
                ? null
                : m_LateBaseMaterial;

            if (usingMaterial)
                if (!m_LateShader)
                    m_LateShader = usingMaterial.shader;
                else if (m_LateShader != usingMaterial.shader)
                {
                    m_LateShader = usingMaterial.shader;
#if UNITY_EDITOR
                    m_MaskableObject.SetMaterialDirty();
                    UnityEditor.EditorUtility.SetDirty(m_MaskableObject);
#endif
                }

            if (!m_ExternalMaterial || m_ExternalMaterial && m_ExternalMaterial != usingMaterial)
            {
                if (m_ExternalMaterial)
                    m_SoftMask?.UnregisterExternalMaterial(m_MaskableObject, m_ExternalMaterial);

                m_ExternalMaterial = usingMaterial;
            }

            m_SoftMask?.RegisterExternalMaterial(m_ExternalMaterial);
        }

        internal async void SafeDestroy()
        {
            try
            {
                var endFrame = Time.renderedFrameCount + 1;
                while (endFrame > Time.renderedFrameCount)
                    await Task.Yield();
                DestroyImmediate(this);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private void ModifyTMPMesh()
        {
            var tmpMesh = m_TMPText.mesh;
            var maskable = IsMaskable();

            var maskDataUVs = new List<Vector4>();
            // TODO: Unique uv index?
            tmpMesh.GetUVs(k_TMPMaskDataUVChannel, maskDataUVs);
            for (var i = 0; i < maskDataUVs.Count; i++)
            {
                var value = maskDataUVs[i];
                value.z = maskable;
                maskDataUVs[i] = value;
            }

            tmpMesh.SetUVs(k_TMPMaskDataUVChannel, maskDataUVs);
            m_TMPText.canvasRenderer.SetMesh(tmpMesh);
        }

        private int IsMaskable()
        {
            return m_MaskableObject.maskable && m_SoftMask && m_SoftMask.enabled ? 1 : 0;
        }


        public void ModifyMesh(VertexHelper vh)
        {
            var vert = new UIVertex();
            var maskable = IsMaskable();

            // TODO: Unique uv index?
            for (var i = 0; i < vh.currentVertCount; i++)
            {
                vh.PopulateUIVertex(ref vert, i);
                vert.uv0.z = maskable;
                vh.SetUIVertex(vert, i);
            }
        }

        public void ModifyMesh(Mesh mesh)
        {
        }
    }
}