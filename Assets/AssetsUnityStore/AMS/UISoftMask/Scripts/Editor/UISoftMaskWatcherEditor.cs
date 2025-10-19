#if UNITY_EDITOR
using UnityEditor;

namespace AMS.UI.SoftMask
{
    using static UISoftMaskUtils;

    [CustomEditor(typeof(UISoftMaskWatcher), true), CanEditMultipleObjects]
    public class UISoftMaskWatcherEditor : Editor
    {
        private UISoftMaskWatcher m_Target;

        private const string m_InvalidMaterialMessage =
            " doesn't support UI Soft Mask. Please add support to it or select a different shader.\n";

        private void OnEnable()
        {
            m_Target = target as UISoftMaskWatcher;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            m_Target.enabled = true;

            if (m_Target.softMask is { enabled: true } && m_Target.maskableObject.maskable &&
                m_Target.materialForRendering is { } baseMaterial && !MaterialHasSoftMask(baseMaterial))
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.HelpBox(
                    $"\nBase material [{baseMaterial.name}]" + m_InvalidMaterialMessage,
                    MessageType.Warning);
                EditorGUILayout.EndHorizontal();
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif