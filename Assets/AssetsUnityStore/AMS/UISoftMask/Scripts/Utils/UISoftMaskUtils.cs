using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using System.Threading.Tasks;
using UnityEditor;
#endif

namespace AMS.UI.SoftMask
{
    public abstract class UISoftMaskUtils
    {
        //UISoftMaskShader
        private const string k_DefaultSoftMaskShader = "AMS/UISoftMask";
        private const string k_USING_SOFT_MASK = "USING_SOFT_MASK";
        public const string k_DEBUG_MASK = "_DEBUG_MASK";

        public static int s_WORLDCANVAS = Shader.PropertyToID("_WORLDCANVAS");

        public static int s_SoftMaskID = Shader.PropertyToID("_SoftMask");
        public static int s_RectUvSizeID = Shader.PropertyToID("_RectUvSize");
        public static int s_WorldCanvasMatrixID = Shader.PropertyToID("_WorldCanvasMatrix");
        public static int s_OverlayCanvasMatrixID = Shader.PropertyToID("_OverlayCanvasMatrix");

        public static int s_ParentMaskID = Shader.PropertyToID("_ParentMask");
        public static int s_ParentMaskMatrixID = Shader.PropertyToID("_ParentMaskMatrix");

        public static int s_MaskDataSettingsID = Shader.PropertyToID("_MaskDataSettings");

        public static Shader s_SoftMaskShader = Shader.Find(k_DefaultSoftMaskShader);

        //UISoftMaskBlitShader
        public const string k_SoftMaskBlitShader = "Hidden/AMS/UISoftMaskBlit";
        public static Shader s_SoftMaskBlitShader = Shader.Find(k_SoftMaskBlitShader);
        public static int s_FalloffID = Shader.PropertyToID("_FallOff");
        public static int s_OpacityID = Shader.PropertyToID("_Opacity");
        public static int s_AtlasDataID = Shader.PropertyToID("_AtlasData");
        public const string k_SLICED = "_SLICED";
        public static int s_SliceScaleID = Shader.PropertyToID("_SliceScale");
        public static int s_SliceBorderID = Shader.PropertyToID("_SliceBorder");

        public const string k_SoftMaskMatTag = "SoftMaskMat:";
        public const string k_SoftMaskBlitMatTag = "SoftMaskBlitMat:";
        public const string k_SoftMaskFontMatTag = "SoftMaskFontMat:";

        // Stencil
        public static int s_StencilCompID = Shader.PropertyToID("_StencilComp");
        public static int s_StencilID = Shader.PropertyToID("_Stencil");
        public static int s_StencilOpID = Shader.PropertyToID("_StencilOp");
        public static int s_StencilWriteMaskID = Shader.PropertyToID("_StencilWriteMask");
        public static int s_StencilReadMaskID = Shader.PropertyToID("_StencilReadMask");

        public const string k_DefaultSoftMaskBlitMatPath = "AMS/SoftMask/AMSUISoftMaskBlit";

        public enum MaskUV
        {
            Simple,
            Sliced
        }

        [Serializable]
        internal class MaskData
        {
            internal MaskUV uvType = default;
            internal Sprite sprite = null;
            internal float pixelsPerUnitMultiplier = 0;
            internal Vector4 slicedBorder = default;
            internal Vector2 settings = default; //x: enabled | y: gamma2linear
            internal int size = 0;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            internal bool preview = false;
#endif

            public void SetMaterialDataSettings(Material material)
            {
                material.SetVector(s_MaskDataSettingsID, settings);
            }
        }

        public enum MaskSize
        {
            _32 = 32,
            _64 = 64,
            _128 = 128,
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096
        }

        internal class ExternalMaterialData
        {
            private readonly Material keyMaterial;
            internal readonly Material instanceMaterial;

            internal ExternalMaterialData(Material keyMaterial, Material instanceMaterial)
            {
                this.keyMaterial = keyMaterial;
                this.instanceMaterial = instanceMaterial;
            }

            internal void UpdateInstance(Action<Material> UpdateMaterial)
            {
                if (!keyMaterial || !instanceMaterial)
                    return;

                if (keyMaterial.shader != instanceMaterial.shader)
                    instanceMaterial.shader = keyMaterial.shader;

                instanceMaterial.CopyPropertiesFromMaterial(keyMaterial);
                UpdateMaterial?.Invoke(instanceMaterial);
            }

            internal static bool FindData(List<ExternalMaterialData> externalMaterialDataList, Material targetMaterial,
                out ExternalMaterialData foundData)
            {
                foreach (var externalMatData in externalMaterialDataList)
                {
                    if (externalMatData is not { keyMaterial: var value } || value != targetMaterial)
                        continue;

                    foundData = externalMatData;
                    return true;
                }

                foundData = null;
                return false;
            }
        }

        internal class FontMaterialData
        {
            private readonly TMP_FontAsset fontAsset;
            private readonly List<Material> Keys = new();
            internal Dictionary<string, Material> Instances = new();
            internal int maskID;

            internal FontMaterialData(TMP_FontAsset fontAsset)
            {
                this.fontAsset = fontAsset;
            }

            internal Material GetRelativeKeyMaterial(Material instanceFontMaterial)
            {
                return !instanceFontMaterial
                    ? null
                    : Keys.FirstOrDefault(key => key && instanceFontMaterial.name.Contains(key.name));
            }

            internal void UpdateInstances(Action<Material> UpdateMaterial)
            {
                if (Instances == null)
                    return;

                foreach (var instanceMaterial in Instances.Values)
                {
                    if (!instanceMaterial)
                        continue;

                    //TODO: Do we want to check shader change in here?
                    if (GetRelativeKeyMaterial(instanceMaterial) is { } keyMat)
                    {
                        instanceMaterial.CopyPropertiesFromMaterial(keyMat);
                        //Force enable soft mask shader feature to prevent missing maskData for unsupported TMP components
                        instanceMaterial.EnableKeyword(k_USING_SOFT_MASK);
                    }

                    UpdateMaterial?.Invoke(instanceMaterial);
                }
            }

            internal static bool FindData(List<FontMaterialData> fontMaterialDataList, TMP_FontAsset targetAsset,
                out FontMaterialData foundData)
            {
                foreach (var fontMaterialData in fontMaterialDataList)
                {
                    if (fontMaterialData is not { fontAsset: var value } || value != targetAsset)
                        continue;

                    foundData = fontMaterialData;
                    return true;
                }

                foundData = null;
                return false;
            }

            internal Material TryRegisterInstanceMaterial(Material material)
            {
                Instances ??= new Dictionary<string, Material>();

                if (Instances.Values.Contains(material))
                    return material;

                if (Keys.Contains(material))
                {
                    var keyName = material.name;
                    if (Instances.Count == Keys.Count && Instances[keyName] is { } foundInstanceMaterial)
                        return foundInstanceMaterial;

                    Instances.Clear();
                    foreach (var key in Keys)
                        if (CreateNewFontMaterial(key) is { } newInstanceMaterial)
                            Instances.Add(key.name, newInstanceMaterial);

                    return Instances[keyName];
                }

                var newInstance = CreateNewFontMaterial(material);

                if (!newInstance)
                    return null;

                Keys.Add(material);
                Instances.Add(material.name, newInstance);

                return newInstance;
            }

            private Material CreateNewFontMaterial(Material material)
            {
                return !material
                    ? null
                    : new Material(material)
                    {
                        name = $"{k_SoftMaskFontMatTag}{maskID}:{material.name}",
                        hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor |
                                    HideFlags.NotEditable
                    };
            }
        }

        public static bool MaterialHasSoftMask(Material targetMaterial) =>
            targetMaterial && targetMaterial.HasProperty(s_SoftMaskID);
        
        internal static int DescendantsCount(Transform parent)
        {
            var count = 0;

            for (var i = 0; i < parent.childCount; i++)
            {
                var child = parent.GetChild(i);
                count++;
                count += DescendantsCount(child);
            }

            return count;
        }

#if UNITY_EDITOR
        [MenuItem("Window/AMS/UnloadUnusedAssets", priority = 0)]
        public static void ClearResources()
        {
            GC.Collect();
            Resources.UnloadUnusedAssets();
        }

        [MenuItem("GameObject/UI/Text - TextMeshPro (For UISoftMask)", false, 2001)]
        public static void CreateTMPForUISoftMaskObject()
        {
            var obj = new GameObject("Text (TMP)");
            obj.AddComponent<TMPTextForUISoftMask>().text = "New Text";

            Undo.RegisterCreatedObjectUndo(obj, "Create Text (TMP) For UISoftMask");

            if (Selection.activeTransform is { } transform)
            {
                obj.transform.parent = transform;
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
            }

            Selection.activeGameObject = obj;
        }

        internal static void RepaintGameAndSceneViews()
        {
            var gameViewType = Type.GetType("UnityEditor.GameView,UnityEditor");
            var views = Resources.FindObjectsOfTypeAll<EditorWindow>();
            foreach (var view in views)
                if (view.GetType() is { } viewType &&
                    (viewType == typeof(SceneView) || gameViewType != null && viewType == gameViewType))
                    view.Repaint();
        }

        [MenuItem("Window/AMS/UISoftMask/Force Include Shaders (ProjectSettings)", priority = 0)]
        public static void ForceIncludeShaders()
        {
            var graphicsSettingsObj =
                AssetDatabase.LoadAssetAtPath<GraphicsSettings>(
                    "ProjectSettings/GraphicsSettings.asset");
            var serializedObject = new SerializedObject(graphicsSettingsObj);
            var includedShaderProperty = serializedObject.FindProperty("m_AlwaysIncludedShaders");
            var shaders = new List<Shader>();
            for (var i = 0; i < includedShaderProperty.arraySize; i++)
            {
                if (includedShaderProperty.GetArrayElementAtIndex(i).objectReferenceValue is Shader shader && shader)
                    shaders.Add(shader);
                else
                {
                    includedShaderProperty.DeleteArrayElementAtIndex(i);
                    i--;
                }
            }

            if (!shaders.Contains(s_SoftMaskShader))
            {
                var index = includedShaderProperty.arraySize;
                includedShaderProperty.InsertArrayElementAtIndex(index);
                var arrayElem = includedShaderProperty.GetArrayElementAtIndex(index);
                arrayElem.objectReferenceValue = s_SoftMaskShader;
            }

            if (!shaders.Contains(s_SoftMaskBlitShader))
            {
                var index = includedShaderProperty.arraySize;
                includedShaderProperty.InsertArrayElementAtIndex(index);
                var arrayElem = includedShaderProperty.GetArrayElementAtIndex(index);
                arrayElem.objectReferenceValue = s_SoftMaskBlitShader;
            }

            serializedObject.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
        }
#endif
    }
}