#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEditor.U2D;
using UnityEngine.U2D;
using UnityEngine.Rendering;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [CustomEditor(typeof(UISoftMask), true), CanEditMultipleObjects]
    public class UISoftMaskEditor : Editor
    {
        private UISoftMask m_Target;

        private SerializedProperty m_Script;
        private SerializedProperty m_IncludedShaders;

        private SerializedObject m_GraphicsSettingsObject;

        public List<string> m_ExcludeProperties = new List<string> { "m_Script" };

        private const string k_PixelsPerUnitMultiplierProperty = "m_PixelsPerUnitMultiplier";
        private const string k_SoftMaskBlitMaterial = "m_SoftMaskBlitMateria";
        private const string k_ScriptProperty = "m_Script";

        private const string k_AtlasTightModeWarningMessage =
            "\nSprite mask is part of an atlas and uses a 'Tight' packing mode. To prevent rendering outbound seams please set packing mode to 'Full Rect'.\n";

        private const string k_OverrideMaterialMessage =
            " doesn't support UI Soft Mask. Please add support to it or select a different shader.\n";

        private const string k_InvalidFontMaterialMessage =
            " doesn't support UI Soft Mask.\nPlease add support to it or select a different shader.\n\nHave you imported TMP_SoftMaskSupport package?\n" +
            "For TMP support please import package at plugin's folder 'Resources/Packages/TMP_SoftMaskSupport.unitypackage'.\n" +
            "Supported shaders:\n-TMP_SDF;\n-TMP_SDF_Mobile;\n";

        private const string k_IncludeShaderWarningMessage =
            "\nIt's required to include AMS shaders into 'Project Settings > Graphics > Always Included Shaders' to prevent Unity skip variants/shader from builds.\n";

        private const string k_UnsupportedTMPTypeMessage =
            "\nUnsupported TMP component(s) found!\n\n" +
            "Press 'Fix' to automatically replace it with a supported 'TMPTextForUISoftMask' component.\n\n" +
            "This action will:\n" +
            "- Remove all unsupported TMP component;\n" +
            "- Add a new corresponding TMPTextForUISoftMask component;\n" +
            "- Preserve all serialized data from the original component;\n" +
            "- Update references in other components to point to the new one.\n\n" +
            "Note: If you're using a custom TMP-derived component, please make it inherit from TMPTextForUISoftMask instead.\n\n";

        private void OnEnable()
        {
            m_Target = target as UISoftMask;
            m_Script = serializedObject.FindProperty(k_ScriptProperty);

            var graphicsSettingsAsset =
                AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            m_GraphicsSettingsObject = new SerializedObject(graphicsSettingsAsset);
            m_IncludedShaders = m_GraphicsSettingsObject.FindProperty("m_AlwaysIncludedShaders");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(m_Script);
            EditorGUI.EndDisabledGroup();

            switch (m_Target.maskUV)
            {
                case MaskUV.Simple:
                    if (!m_ExcludeProperties.Contains(k_PixelsPerUnitMultiplierProperty))
                        m_ExcludeProperties.Add(k_PixelsPerUnitMultiplierProperty);
                    break;

                case MaskUV.Sliced:
                    if (m_ExcludeProperties.Contains(k_PixelsPerUnitMultiplierProperty))
                        m_ExcludeProperties.Remove(k_PixelsPerUnitMultiplierProperty);
                    break;
            }

            DrawPropertiesExcluding(serializedObject, m_ExcludeProperties.ToArray());

            CheckAtlasPackingMode();
            CheckValidTargetMaterial();
            CheckIncludedShaders();
            CheckFontMaterials();
            CheckUnsupportedTMPTypes();

            serializedObject.ApplyModifiedProperties();
        }

        private void CheckValidTargetMaterial()
        {
            if (m_Target.overrideMaterial is var overrideMaterial && overrideMaterial &&
                !MaterialHasSoftMask(overrideMaterial))
            {
                EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox("\n" + overrideMaterial.name + k_OverrideMaterialMessage, MessageType.Warning);
                EditorGUILayout.EndHorizontal();
            }
        }

        private void CheckAtlasPackingMode()
        {
            if (m_Target.mask is var sprite && sprite && sprite.packed)
            {
                var assetPath = AssetDatabase.GetAssetPath(sprite);
                var textureImporter = AssetImporter.GetAtPath(assetPath) as TextureImporter;

                if (!textureImporter)
                    return;

                var textureSettings = new TextureImporterSettings();
                textureImporter.ReadTextureSettings(textureSettings);

                if (textureSettings.spriteMeshType == SpriteMeshType.Tight)
                {
                    EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Fix", GUILayout.Width(50), GUILayout.ExpandHeight(true)))
                    {
                        textureSettings.spriteMeshType = SpriteMeshType.FullRect;
                        textureImporter.SetTextureSettings(textureSettings);
                        textureImporter.SaveAndReimport();
                        UpdateAtlas(sprite);
                    }

                    EditorGUILayout.HelpBox(k_AtlasTightModeWarningMessage, MessageType.Warning);
                    EditorGUILayout.EndHorizontal();
                }
            }
        }

        private void CheckFontMaterials()
        {
            foreach (var fontMaterialData in m_Target.TMPFontMaterialData)
                if (fontMaterialData is { Instances: { } instances })
                    foreach (var material in instances)
                        if (material.Value is var fontMaterial && !MaterialHasSoftMask(material.Value))
                        {
                            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
                            EditorGUILayout.BeginHorizontal();
                            EditorGUILayout.HelpBox(
                                $"\nTMP material [{fontMaterial.name}]" + k_InvalidFontMaterialMessage,
                                MessageType.Warning);
                            EditorGUILayout.EndHorizontal();
                            break;
                        }
        }

        private void CheckIncludedShaders()
        {
            m_GraphicsSettingsObject.Update();

            var shaders = new List<Shader>();
            for (var i = 0; i < m_IncludedShaders.arraySize; i++)
            {
                if (m_IncludedShaders.GetArrayElementAtIndex(i).objectReferenceValue is Shader shader && shader)
                    shaders.Add(shader);
            }

            if (shaders.Contains(s_SoftMaskShader) && shaders.Contains(s_SoftMaskBlitShader))
                return;

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fix", GUILayout.Width(50), GUILayout.ExpandHeight(true)))
                ForceIncludeShaders();
            EditorGUILayout.HelpBox(k_IncludeShaderWarningMessage, MessageType.Error);
            EditorGUILayout.EndHorizontal();
        }

        private void UpdateAtlas(Sprite sprite)
        {
            AssetDatabase.FindAssets("t:spriteatlas").ToList().ForEach(guid =>
            {
                var atlasPath = AssetDatabase.GUIDToAssetPath(guid);
                var atlas = AssetDatabase.LoadAssetAtPath<SpriteAtlas>(atlasPath);
                var sprites = atlas.GetPackables().ToList()
                    .Select(o => AssetDatabase.LoadAssetAtPath<Sprite>(AssetDatabase.GetAssetPath(o)));

                if (sprites.Contains(sprite))
                {
                    var spriteAtlasImporter = AssetImporter.GetAtPath(atlasPath) as SpriteAtlasImporter;
                    spriteAtlasImporter?.SaveAndReimport();
                }
            });

            m_Target.ForceUpdateMask();
        }

        private void CheckUnsupportedTMPTypes()
        {
            if (Application.isPlaying)
                return;

            var m_MaskableObjects = m_Target.maskableObjects;

            var tmpObjects = m_MaskableObjects.Where(o => o is TMP_Text).Where(t => t is not TMPTextForUISoftMask)
                .ToArray();

            if (!tmpObjects.Any())
                return;

            EditorGUILayout.Space(EditorGUIUtility.singleLineHeight);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Fix", GUILayout.Width(50), GUILayout.ExpandHeight(true)))
                m_Target.FixTMPComponents();
            EditorGUILayout.HelpBox(k_UnsupportedTMPTypeMessage, MessageType.Error);
            EditorGUILayout.EndHorizontal();
        }
    }
}
#endif