// Assets/AssetsUnityStore/ARTnGAME/GLAMOR/GLAMOR URP/VolFx/VolFx/Editor/OptionalDrawer.cs
using UnityEditor;
using UnityEngine;

//  VolFx © NullTale - https://twitter.com/NullTale/
namespace Artngame.GLAMOR.VolFx.Tools.Editor
{
    [CustomPropertyDrawer(typeof(Optional<>))]
    public class OptionalDrawer : PropertyDrawer
    {
        public const float k_ToggleWidth = 18;

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            var valueProperty = property.FindPropertyRelative("value");
            return EditorGUI.GetPropertyHeight(valueProperty);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var valueProperty   = property.FindPropertyRelative("value");
            var enabledProperty = property.FindPropertyRelative("enabled");
            OnGui(position, label, enabledProperty, valueProperty);
        }

        public static void OnGui(Rect position, GUIContent label, SerializedProperty enabledProperty, SerializedProperty valueProperty)
        {
            position.width -= k_ToggleWidth;

            using (new EditorGUI.DisabledGroupScope(!enabledProperty.boolValue))
            {
                // Unity 6 uyumlu LayerMask çizimi
                if (valueProperty.propertyType == SerializedPropertyType.LayerMask)
                {
                    var current = new LayerMask { value = valueProperty.intValue };
                    var updated = LayerMaskField(position, label, current);
                    valueProperty.intValue = updated.value;
                }
                else
                {
                    EditorGUI.PropertyField(position, valueProperty, label, true);
                }
            }

            int indent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            var togglePos = new Rect(
                position.x + position.width + EditorGUIUtility.standardVerticalSpacing,
                position.y, k_ToggleWidth, EditorGUIUtility.singleLineHeight);

            EditorGUI.PropertyField(togglePos, enabledProperty, GUIContent.none);

            EditorGUI.indentLevel = indent;
        }

        /// <summary>
        /// InternalEditorUtility.* olmadan LayerMask için MaskField.
        /// Sadece isimli (LayerMask.LayerToName(i) boş olmayan) katmanları listeler.
        /// </summary>
        private static LayerMask LayerMaskField(Rect pos, GUIContent label, LayerMask selected)
        {
            var layerNames = new System.Collections.Generic.List<string>(32);
            var layerNumbers = new System.Collections.Generic.List<int>(32);

            for (int i = 0; i < 32; i++)
            {
                string name = LayerMask.LayerToName(i);
                if (!string.IsNullOrEmpty(name))
                {
                    layerNames.Add(name);
                    layerNumbers.Add(i);
                }
            }

            int maskWithoutEmpty = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                int bit = 1 << layerNumbers[i];
                if ((selected.value & bit) != 0)
                    maskWithoutEmpty |= (1 << i);
            }

            int newMaskWithoutEmpty = EditorGUI.MaskField(pos, label, maskWithoutEmpty, layerNames.ToArray());

            int newMask = 0;
            for (int i = 0; i < layerNumbers.Count; i++)
            {
                if ((newMaskWithoutEmpty & (1 << i)) != 0)
                    newMask |= 1 << layerNumbers[i];
            }

            selected.value = newMask;
            return selected;
        }
    }
}
