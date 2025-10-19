using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [AddComponentMenu("UI/AMS/UI Soft Mask"),
     HelpURL("https://ams.sorialexandre.tech/ui-soft-mask/")]
    public class UISoftMask : RectUV
    {
        [SerializeField]
        private Sprite m_Mask;

        public Sprite mask
        {
            get => m_Mask;
            set
            {
                if (m_Mask == value)
                    return;

                m_Mask = value;
                ComputeFinalMaskForRendering();
            }
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [SerializeField,
         Tooltip("Preview mask output.")]
        private bool m_MaskPreview;

        /// <summary>
        /// Enable/disable mask preview for debugging purposes. (Editor or Development Build only)
        /// </summary>
        public bool maskPreview
        {
            get => m_MaskPreview || m_MaskData.preview;
            set
            {
                m_MaskData.preview = value; //We want to isolate it for child mask preview purpose
                UpdateMaterials();
            }
        }
#endif

        [SerializeField, Tooltip("The mask output size.\n\nNote: Keep it low to save memory allocation.")]
        private MaskSize m_MaskSize = MaskSize._128;

        public MaskSize maskSize
        {
            get => m_MaskSize;

            set
            {
                if (m_MaskSize == value)
                    return;

                m_MaskSize = value;
                ComputeFinalMaskForRendering();
            }
        }

        [SerializeField, Tooltip("Select between a Simple or a Sliced (9-slicing) uv coordinate.")]
        private MaskUV m_MaskUV = MaskUV.Simple;

        public MaskUV maskUV => m_MaskUV;

        [SerializeField, Min(0.01f)]
        private float m_PixelsPerUnitMultiplier = 1;

        public float pixelsPerUnitMultiplier
        {
            get => m_PixelsPerUnitMultiplier;
            set
            {
                if (Mathf.Approximately(m_PixelsPerUnitMultiplier, value))
                    return;

                m_PixelsPerUnitMultiplier = value;
                ComputeFinalMaskForRendering();
            }
        }

        private MaskData m_MaskData = new();

        internal MaskData maskData
        {
            get { return m_MaskData; }
        }

        [SerializeField, Range(0, 1)]
        private float m_FallOff = 1;

        public float fallOff
        {
            get => m_FallOff;
            set
            {
                m_FallOff = value;
                ComputeFinalMaskForRendering();
            }
        }

        [SerializeField, Range(0, 1)]
        private float m_Opacity = 1;

        public float opacity
        {
            get => m_Opacity;
            set
            {
                m_Opacity = value;
                ComputeFinalMaskForRendering();
            }
        }

        [SerializeField,
         Tooltip(
             "Use this to override the temporary mask material with a material asset from your project. Note: It requires a unique material per mask and the shader must be compatible with AMS UI Soft Mask.")]
        private Material m_OverrideMaskMaterial;

        /// <summary>
        /// Use this to override the temporary mask material with a material asset from your project. Note: It requires a unique material per mask and the shader must be compatible with AMS UI Soft Mask. 
        /// </summary>
        public Material overrideMaterial
        {
            get => m_OverrideMaskMaterial;
            set
            {
                m_OverrideMaskMaterial = value;
                ForceUpdateMask();
            }
        }

        [SerializeField, Tooltip("Override transform to decouple mask size, position and rotation.")]
        private RectTransform m_OverrideTransform;

        /// <summary>
        /// Override transform to decouple mask size, position and rotation.
        /// </summary>
        public RectTransform overrideTransform
        {
            get => m_OverrideTransform ? m_OverrideTransform : rectTransform;

            set
            {
                if (m_OverrideTransform == value)
                    return;

                m_OverrideTransform = value;
                ForceUpdateMask();
            }
        }

        private RenderTexture m_MaskForRenderingRT;

        private List<MaskableGraphic> m_MaskableObjects = new();

        /// <summary>
        /// Return mask maskable objects.
        /// </summary>
        public List<MaskableGraphic> maskableObjects => m_MaskableObjects;

        private Dictionary<MaskableGraphic, bool> m_MakableObjectsState = new();

        private int m_DescendantCount = -1;

        private Material m_SoftMaskBlitMaterial;

        private Material m_TempMaterial;

        public Material GetMaskMaterial() => overrideMaterial ? overrideMaterial : m_TempMaterial;

        private readonly List<ExternalMaterialData> m_ExternalMaterialsData = new();

        internal List<ExternalMaterialData> externalMaterialsData => m_ExternalMaterialsData;

        public void RegisterExternalMaterial(Material material)
        {
            if (!material || !MaterialHasSoftMask(material))
                return;

            // TODO: Check impact of have instance external material per mask group
            if (!material || ExternalMaterialData.FindData(m_ExternalMaterialsData, material, out _))
                return;

            var mewInstance = new Material(material)
            {
                name = $"{k_SoftMaskMatTag}{GetInstanceID()}:{material.name}",
                hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor |
                            HideFlags.NotEditable
            };
            var newData = new ExternalMaterialData(material, mewInstance);
            m_ExternalMaterialsData.Add(newData);
        }

        public void UnregisterExternalMaterial(MaskableGraphic graphic, Material material)
        {
            if (!material)
                return;

            var maskableObjs = m_MaskableObjects;
            maskableObjs.Remove(graphic);

            if (maskableObjs.Any(x => graphic.material == material))
                return;

            if (!ExternalMaterialData.FindData(m_ExternalMaterialsData, material, out var foundData))
                return;

            if (foundData.instanceMaterial is { } instanceMat)
                DestroyImmediate(instanceMat);

            m_ExternalMaterialsData.Remove(foundData);
        }

        private readonly List<FontMaterialData> m_TMPFontMaterialData = new();

        internal List<FontMaterialData> TMPFontMaterialData => m_TMPFontMaterialData;

        private Matrix4x4 m_OverrideMaskMatrix;

        private bool m_Inactive = false;

        private List<UISoftMask> m_ParentMasks = new();
        private List<UISoftMask> m_ChildMasks = new();

        private RenderMode m_LateCanvasMode;

        private bool m_Started = false;

        private new void OnEnable()
        {
            base.OnEnable();

            m_OnBeginContextRendering = OnBeginFrameRendering;
            m_DuringCameraPreRender = DuringCameraPreRender;

            UpdateMaskableObjects(this);

            m_ParentMasks = GetComponentsInParent<UISoftMask>().Skip(1).ToList();
            for (var i = 0; i < m_ParentMasks.Count; i++)
                if (m_ParentMasks[i] is { } parentMask)
                {
                    parentMask.m_DescendantCount = -1;
                    break;
                }
        }

        private new void OnDisable()
        {
            base.OnDisable();

            m_MaskData.settings = Vector2.zero;

            UpdateMaskableObjects(m_ParentMasks.FirstOrDefault());

            for (var i = 0; i < m_ChildMasks.Count; i++)
                if (m_ChildMasks[i] is { enabled: true } childMasks)
                    childMasks.ForceUpdateMask();

            m_DescendantCount = -1;
            m_Started = false;
        }


        private void OnDestroy()
        {
            SafeReleaseTempRT(m_MaskForRenderingRT);

            if (m_TempMaterial && m_TempMaterial.name.Contains(k_SoftMaskMatTag)) //Destroy if temp material
                DestroyImmediate(m_TempMaterial);

            if (m_SoftMaskBlitMaterial &&
                m_SoftMaskBlitMaterial.name.Contains(k_SoftMaskBlitMatTag)) //Destroy if temp material
                DestroyImmediate(m_SoftMaskBlitMaterial);

            if (!m_ParentMasks.FirstOrDefault()) //Destroy watchers if none parent mask
                foreach (var maskableObj in m_MaskableObjects)
                    if (maskableObj && maskableObj.gameObject.GetComponent<UISoftMaskWatcher>() is { } watcher)
                        watcher.SafeDestroy();

            // Make sure to destroy TMP instance materials
            for (var i = 0; i < m_TMPFontMaterialData.Count; i++)
            {
                var instances = m_TMPFontMaterialData[i].Instances;
                foreach (var instancePair in instances)
                {
                    if (instancePair.Value is not { } instanceMaterial)
                        continue;
                    DestroyImmediate(instanceMaterial);
                }
            }

            // Make sure to destroy external instance materials
            for (var i = 0; i < m_ExternalMaterialsData.Count; i++)
            {
                if (m_ExternalMaterialsData[i]?.instanceMaterial is not { } externalInstanceMaterial)
                    continue;
                DestroyImmediate(externalInstanceMaterial);
            }

            m_MaskableObjects.Clear();
            m_TMPFontMaterialData.Clear();
            m_ExternalMaterialsData.Clear();
        }

        private void OnValidate()
        {
            if (!m_Started || !enabled)
                return;

            CheckType();
            ComputeFinalMaskForRendering();
        }

        private void LateUpdate()
        {
            if (m_Inactive)
                return;

            if (!m_Started)
            {
                m_Started = true;
                ForceUpdateMask();
                return;
            }

            if (HasChangedRectUV(overrideTransform) || CheckType())
                ComputeFinalMaskForRendering();

            CheckMaskableObjects();
            UpdateMaterials();
        }

        private void UpdateMaterials()
        {
            m_MaskData.settings.x = enabled ? 1 : 0;
            m_MaskData.settings.y = m_RectProperties.gamma2linear ? 1 : 0;

            CheckTargetMaterial();

            UpdateMaterial(m_TempMaterial);

            foreach (var externalMaterialData in m_ExternalMaterialsData)
                externalMaterialData.UpdateInstance(UpdateMaterial);

            foreach (var fontData in m_TMPFontMaterialData)
                fontData.UpdateInstances(UpdateMaterial);
        }

        private void SetWorldCanvasMaterials() => SetWorldCanvasProperty(1);

        private void SetOverlayCanvasMaterials() => SetWorldCanvasProperty(0);

        private void SetWorldCanvasProperty(int value)
        {
            if (m_TempMaterial)
                m_TempMaterial.SetInt(s_WORLDCANVAS, value);

            for (var i = 0; i < m_ExternalMaterialsData.Count; i++)
                if (m_ExternalMaterialsData[i]?.instanceMaterial is { } externalMat)
                    externalMat.SetInt(s_WORLDCANVAS, value);

            foreach (var fontMaterialKey in m_TMPFontMaterialData)
                if (fontMaterialKey is { Instances: { } instances })
                    foreach (var fontMaterial in instances)
                        fontMaterial.Value.SetInt(s_WORLDCANVAS, value);
        }

        private void OnBeginFrameRendering(List<Camera> cameras)
        {
            if (canvas && (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                           (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                            !canvas.worldCamera)))
            {
                SetOverlayCanvasMaterials();
#if UNITY_EDITOR
                foreach (var cam in cameras)
                    if (cam.cameraType != CameraType.Game)
                        SetWorldCanvasMaterials();
#endif
            }
            else
                SetWorldCanvasMaterials();
        }

        private void DuringCameraPreRender(Camera targetCamera)
        {
            if (canvas && (canvas.renderMode == RenderMode.ScreenSpaceOverlay ||
                           (canvas.renderMode == RenderMode.ScreenSpaceCamera &&
                            !canvas.worldCamera)))
            {
#if UNITY_EDITOR
                if (targetCamera.cameraType == CameraType.SceneView)
                    SetWorldCanvasMaterials();
                else
#endif
                    SetOverlayCanvasMaterials();
            }
            else
                SetWorldCanvasMaterials();
        }

        private void UpdateMaterial(Material material)
        {
            if (!material)
                return;

            material.SetTexture(s_SoftMaskID, m_MaskForRenderingRT);

            SetMaterialRectParams(material);
            m_MaskData.SetMaterialDataSettings(material);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (maskPreview)
                material.EnableKeyword(k_DEBUG_MASK);
            else
                material.DisableKeyword(k_DEBUG_MASK);
#endif
        }

        /// <summary>
        /// Force update mask setup.
        /// </summary>
        public void ForceUpdateMask()
        {
            CheckType();
            HasChangedRectUV(overrideTransform);
            ComputeFinalMaskForRendering();
            CheckMaskableObjects();
            UpdateMaterials();
        }

        private bool CheckType()
        {
            var update = false;
            switch (m_MaskUV)
            {
                case MaskUV.Simple:
                    if (m_MaskData.uvType != m_MaskUV)
                    {
                        m_MaskData.uvType = m_MaskUV;
                        update = true;
                    }

                    break;

                case MaskUV.Sliced:
                    if (m_MaskData.uvType != m_MaskUV ||
                        m_Mask && (!m_MaskData.sprite || m_MaskData.sprite != m_Mask) ||
                        !Mathf.Approximately(m_MaskData.pixelsPerUnitMultiplier, m_PixelsPerUnitMultiplier))
                    {
                        m_MaskData.uvType = m_MaskUV;

                        var sprite = m_MaskData.sprite = m_Mask;

                        var size = new Vector2(sprite.rect.width, sprite.rect.height);
                        var borders = sprite.border;
                        borders.x /= size.x;
                        borders.y /= size.y;
                        borders.z /= size.x;
                        borders.w /= size.y;

                        m_MaskData.slicedBorder = borders;
                        m_MaskData.pixelsPerUnitMultiplier = m_PixelsPerUnitMultiplier;

                        update = true;
                    }

                    break;
            }

            return update;
        }

        private Vector2 GetSliceScale(Vector2 textureSize) =>
            rectTransform.rect.size / textureSize * m_MaskData.pixelsPerUnitMultiplier;

        private void CheckRenderingMaskSetup()
        {
            if (!m_SoftMaskBlitMaterial)
            {
                m_SoftMaskBlitMaterial = Resources.Load<Material>(k_DefaultSoftMaskBlitMatPath);

                if (!m_SoftMaskBlitMaterial)
                {
                    if (!s_SoftMaskBlitShader)
                        s_SoftMaskBlitShader = Shader.Find(k_SoftMaskBlitShader);

                    m_SoftMaskBlitMaterial = new Material(s_SoftMaskBlitShader);
                    m_SoftMaskBlitMaterial.name = $"{k_SoftMaskBlitMatTag}{m_SoftMaskBlitMaterial.GetInstanceID()}";
                    m_SoftMaskBlitMaterial.hideFlags =
                        HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.NotEditable;
                }
            }

            var selectedSize = m_MaskData.size = (int)m_MaskSize;

            if (!m_MaskForRenderingRT)
            {
                m_MaskForRenderingRT =
                    RenderTexture.GetTemporary(selectedSize, selectedSize, 0,
                        RenderTextureFormat.R16); //R8 is unsupported for some platforms
                m_MaskForRenderingRT.name = $"{k_SoftMaskMatTag}{m_MaskForRenderingRT.GetInstanceID()}";
                m_MaskForRenderingRT.Release();
                m_MaskForRenderingRT.autoGenerateMips = false;
                m_MaskForRenderingRT.useMipMap = false;
            }
            else if (m_MaskForRenderingRT && m_MaskForRenderingRT.width != selectedSize)
            {
                m_MaskForRenderingRT.Release();
                m_MaskForRenderingRT.height = m_MaskForRenderingRT.width = selectedSize;
            }
        }

        private void CheckMaskableObjects()
        {
            var descendants = DescendantsCount(transform);
            if (m_DescendantCount == descendants)
            {
                CheckCanvasModeChange();
                return;
            }

            m_DescendantCount = descendants;

            m_ChildMasks = GetComponentsInChildren<UISoftMask>().Skip(1).ToList();
            m_ParentMasks = GetComponentsInParent<UISoftMask>().Skip(1).ToList();

            m_MaskableObjects = GetComponentsInChildren<MaskableGraphic>(true)
                .ToList();

            for (var i = 0; i < m_MaskableObjects.Count; i++)
            {
                var obj = m_MaskableObjects[i];
                if (m_ChildMasks.Any(childMask => childMask.enabled && obj.transform.IsChildOf(childMask.transform)))
                {
                    m_MaskableObjects.Remove(obj);
                    i--;
                }
            }

            for (var i = 0; i < m_MaskableObjects.Count; i++)
            {
                var maskableObj = m_MaskableObjects[i];

                //Skipping unsupported TMP since we ask user to fix manually to prevent brake things
                if (maskableObj is TMP_Text and not TMPTextForUISoftMask)
                    continue;

                var obj = maskableObj.gameObject;
                if (obj.GetComponent<UISoftMaskWatcher>() is { } watcher)
                    watcher.softMask = this;
                else
                    obj.AddComponent<UISoftMaskWatcher>().softMask = this;
            }
        }

        private void CheckCanvasModeChange()
        {
            if (canvas is not { } targetCanvas || targetCanvas.renderMode == m_LateCanvasMode)
                return;

            m_LateCanvasMode = canvas.renderMode;

            //Update TMP meshes for maskData purposes
            for (var i = 0; i < m_MaskableObjects.Count; i++)
                if (m_MaskableObjects[i] is TMPTextForUISoftMask tmpText)
                    tmpText.ForceMeshUpdate();
        }

        internal Material GetInstanceExternalMaterial(Material keyMaterial)
        {
            if (!keyMaterial)
                return null;

            //TODO: Do we want to register external materials in here?
            return !ExternalMaterialData.FindData(m_ExternalMaterialsData, keyMaterial, out var data)
                ? null
                : data.instanceMaterial;
        }

        internal Material GetTMPInstanceMaskMaterial(TMP_Text textMesh)
        {
            if (!textMesh ||
                textMesh.fontSharedMaterial is not { } fontSharedMaterial)
                return null;

            var fontAsset = textMesh.font;
            if (FontMaterialData.FindData(m_TMPFontMaterialData, fontAsset, out var softMaskFontData))
                return softMaskFontData?.TryRegisterInstanceMaterial(fontSharedMaterial);

            if (!MaterialHasSoftMask(fontSharedMaterial))
                return null;

            var newFontData = new FontMaterialData(fontAsset)
            {
                maskID = GetInstanceID()
            };
            m_TMPFontMaterialData.Add(newFontData);
            return newFontData.TryRegisterInstanceMaterial(fontSharedMaterial);
        }


        private void ComputeFinalMaskForRendering()
        {
            CheckRenderingMaskSetup();

            m_SoftMaskBlitMaterial.SetFloat(s_OpacityID, m_Opacity);
            m_SoftMaskBlitMaterial.SetFloat(s_FalloffID, m_FallOff);

            var textureMask = Texture2D.whiteTexture;

            // Check parent tex before final blit
            var parentTex = CheckParentTex();

            if (m_Mask)
            {
                var sourceTexRect = m_Mask.rect;
                var texRect = m_Mask.textureRect;
                var rectOffset = m_Mask.textureRectOffset;
                textureMask = m_Mask.texture;
                var textureAtlasFactor = Vector2.one / new Vector2(textureMask.width, textureMask.height);
                var spriteOffset = (texRect.min - rectOffset) * textureAtlasFactor;
                Vector4 atlasData = sourceTexRect.size * textureAtlasFactor;
                atlasData.z = spriteOffset.x;
                atlasData.w = spriteOffset.y;
                m_SoftMaskBlitMaterial.SetVector(s_AtlasDataID, atlasData);

                if (maskUV == MaskUV.Sliced)
                {
                    m_SoftMaskBlitMaterial.EnableKeyword(k_SLICED);
                    m_SoftMaskBlitMaterial.SetVector(s_SliceScaleID, GetSliceScale(sourceTexRect.size));
                    m_SoftMaskBlitMaterial.SetVector(s_SliceBorderID, m_MaskData.slicedBorder);
                }
                else
                    m_SoftMaskBlitMaterial.DisableKeyword(k_SLICED);
            }

            if (!parentTex)
                m_OverrideMaskMatrix = Matrix4x4.identity;

            m_SoftMaskBlitMaterial.SetMatrix(s_ParentMaskMatrixID, m_OverrideMaskMatrix);
            m_SoftMaskBlitMaterial.SetTexture(s_ParentMaskID, parentTex ? parentTex : Texture2D.whiteTexture);

            Graphics.Blit(textureMask, m_MaskForRenderingRT, m_SoftMaskBlitMaterial);

            if (parentTex)
                RenderTexture.ReleaseTemporary(parentTex);

            // Compute children mask
            ComputeChildrenMaskChain();
        }

        private RenderTexture CheckParentTex()
        {
            for (var i = 0; i < m_ParentMasks.Count; i++)
            {
                if (m_ParentMasks[i] is not { } parentMask)
                {
                    m_ParentMasks.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!parentMask.enabled)
                    continue;

                GetTemporaryParentMask(parentMask, m_MaskForRenderingRT.descriptor, out var parentTex);
                return parentTex;
            }

            return null;
        }

        private void ComputeChildrenMaskChain()
        {
            for (var i = 0; i < m_ChildMasks.Count; i++)
            {
                if (m_ChildMasks[i] is not { } childMask)
                {
                    m_ChildMasks.RemoveAt(i);
                    i--;
                    continue;
                }

                if (!childMask.enabled)
                    continue;

                var isChildMask = false;
                for (var j = 1; j < m_ChildMasks.Count; j++)
                    if (m_ChildMasks[j] is { enabled: true } parentMask && parentMask != childMask &&
                        childMask.transform.IsChildOf(parentMask.transform))
                    {
                        isChildMask = true;
                        break;
                    }

                if (isChildMask)
                    continue;

                childMask.ForceUpdateMask();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                childMask.maskPreview = maskPreview;
#endif
            }
        }

        private void GetTemporaryParentMask(UISoftMask parentMask, RenderTextureDescriptor descriptor,
            out RenderTexture parentTex)
        {
            parentTex = null;

            if (!parentMask)
                return;

            if (overrideTransform is not { } childTransform || parentMask.overrideTransform is not { } parentTransform)
                return;

            parentTex = RenderTexture.GetTemporary(descriptor);
            parentTex.name = $"ParentSoftMask [{name}]";

            var parentCenterWorld = parentTransform.TransformPoint(parentTransform.rect.center);
            var childCenterWorld = childTransform.TransformPoint(childTransform.rect.center);
            var offsetWorld = childCenterWorld - parentCenterWorld;

            var parentRight = parentTransform.right;
            var parentUp = parentTransform.up;

            var offset = new Vector2(
                Vector3.Dot(offsetWorld, parentRight),
                Vector3.Dot(offsetWorld, parentUp)
            );

            var parentSize = parentTransform.rect.size * parentTransform.lossyScale;
            var maxSize = Mathf.Max(parentSize.x, parentSize.y);
            var squareSize = new Vector2(maxSize, maxSize);
            offset /= squareSize;

            parentSize = squareSize / parentSize;
            var childSize = childTransform.rect.size * childTransform.lossyScale;
            var scale = childSize / squareSize;

            m_SoftMaskBlitMaterial.DisableKeyword(k_SLICED);
            m_SoftMaskBlitMaterial.SetMatrix(s_ParentMaskMatrixID, Matrix4x4.Scale(parentSize));
            m_SoftMaskBlitMaterial.SetTexture(s_ParentMaskID, parentMask.m_MaskForRenderingRT);
            Graphics.Blit(Texture2D.whiteTexture, parentTex, m_SoftMaskBlitMaterial);

            var rotationDelta = Quaternion.Inverse(parentTransform.rotation) * childTransform.rotation;
            m_OverrideMaskMatrix = Matrix4x4.TRS(offset, rotationDelta, scale);
        }

        private void CheckTargetMaterial()
        {
            switch (m_OverrideMaskMaterial && MaterialHasSoftMask(m_OverrideMaskMaterial))
            {
                case true when m_TempMaterial != m_OverrideMaskMaterial:
                    m_TempMaterial = m_OverrideMaskMaterial;
                    Canvas.ForceUpdateCanvases();
                    return;
                case false when !m_TempMaterial:
                case false when m_TempMaterial && m_TempMaterial == m_OverrideMaskMaterial:
                    m_TempMaterial = new Material(s_SoftMaskShader)
                    {
                        hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor | HideFlags.NotEditable
                    };
                    m_TempMaterial.name = $"{k_SoftMaskMatTag}{m_TempMaterial.GetInstanceID()}";
                    Canvas.ForceUpdateCanvases();
                    break;
            }
        }

        private static void SafeReleaseTempRT(RenderTexture renderTexture)
        {
            if (!renderTexture)
                return;

            if (RenderTexture.active == renderTexture)
                RenderTexture.active = null;

            RenderTexture.ReleaseTemporary(renderTexture);
        }

        private void UpdateMaskableObjects(UISoftMask targetMask)
        {
            if (targetMask)
            {
                targetMask.m_DescendantCount = -1;
                m_MaskableObjects.ForEach(maskableObj =>
                {
                    if (!maskableObj)
                        return;

                    if (maskableObj.gameObject.GetComponent<UISoftMaskWatcher>() is { } watcher)
                        watcher.softMask = targetMask;

                    maskableObj.SetAllDirty();
                });
            }
            else
                m_MaskableObjects.ForEach(maskableObj =>
                {
                    if (!maskableObj)
                        return;

                    maskableObj.SetAllDirty();
                });
        }

        internal void FixTMPComponents()
        {
            var components = FindObjectsOfType<Component>();
            for (var i = 0; i < m_MaskableObjects.Count; i++)
            {
                var maskableObj = m_MaskableObjects[i];
                if (maskableObj is not (TMP_Text tmpText and not TMPTextForUISoftMask))
                    continue;

                maskableObj = m_MaskableObjects[i] = CheckTMPSoftMask(tmpText, components);

                var obj = maskableObj.gameObject;
                if (obj.GetComponent<UISoftMaskWatcher>() is { } watcher)
                    watcher.softMask = this;
                else
                {
                    var newWatcher = obj.AddComponent<UISoftMaskWatcher>();
                    newWatcher.softMask = this;
                }

                maskableObj.SetAllDirty();
            }
        }

        private static MaskableGraphic CheckTMPSoftMask(TMP_Text textMesh, Component[] components = null)
        {
            if (textMesh as TMPTextForUISoftMask is { } isSoftMaskText)
                return isSoftMaskText;

            // Check references
            var usedFields = new List<(Component, FieldInfo)>();
            if (components != null)
                foreach (var comp in components)
                {
                    if (comp == textMesh)
                        continue;

                    var compFields = comp.GetType()
                        .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    foreach (var field in compFields)
                    {
                        if (field.FieldType.IsAssignableFrom(typeof(TMP_Text)) &&
                            field.GetValue(comp) is TMP_Text tmpValue &&
                            tmpValue == textMesh)
                            usedFields.Add((comp, field));
                    }
                }

            var fields = textMesh.GetType()
                .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fieldDictionary = fields.ToDictionary(field => field, field => field.GetValue(textMesh));

            var targetObject = textMesh.gameObject;
            DestroyImmediate(textMesh);

            if (textMesh)
                return null;

            var softMaskText = targetObject.AddComponent<TMPTextForUISoftMask>();

            foreach (var fieldPair in fieldDictionary)
                fieldPair.Key.SetValue(softMaskText, fieldPair.Value);

            softMaskText.Awake(); //Make sure to reconstruct mesh after deletion

            //If used update references
            foreach (var field in usedFields)
                field.Item2.SetValue(field.Item1, softMaskText);

            return softMaskText;
        }

        internal void SetMaskToActive(bool active)
        {
            if (!active && !m_Inactive)
                m_MakableObjectsState = m_MaskableObjects.ToDictionary(g => g, g => g.gameObject.activeInHierarchy);

            var changed = false;
            if (m_Inactive != !active)
            {
                m_Inactive = !active;
                if (m_Inactive)
                    SafeReleaseTempRT(m_MaskForRenderingRT);

                changed = true;
            }

            if (!changed)
                return;

            //TODO: Resolve it by shader instead?
            var graphicObjects = m_MakableObjectsState.Keys.ToArray();
            for (var i = 0; i < m_MakableObjectsState.Count; i++)
            {
                var graphicObj = graphicObjects[i];
                m_MakableObjectsState.TryGetValue(graphicObj, out var state);
                graphicObj.gameObject.SetActive(active && state);
            }

            enabled = active;

            if (!m_Inactive)
                m_MakableObjectsState.Clear();
        }
    }
}