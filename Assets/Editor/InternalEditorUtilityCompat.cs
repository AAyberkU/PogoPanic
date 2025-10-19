// Unity 6 uyumluluk şimi: Eski UnityEditorInternal.InternalEditorUtility API'lerini sağlar.
// Vendor asset'leri (VolFx vs.) değiştirmeden derlenmesini sağlar.
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityEditorInternal
{
    public static class InternalEditorUtility
    {
        // Eski: InternalEditorUtility.layers
        public static string[] layers
        {
            get
            {
                var list = new List<string>(32);
                for (int i = 0; i < 32; i++)
                {
                    string n = LayerMask.LayerToName(i);
                    if (!string.IsNullOrEmpty(n)) list.Add(n);
                }
                return list.ToArray();
            }
        }

        // Eski: InternalEditorUtility.tags
        public static string[] tags
        {
            get
            {
                // ProjectSettings/TagManager.asset içinden okur
                var objs = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
                if (objs == null || objs.Length == 0) return System.Array.Empty<string>();

                var so = new SerializedObject(objs[0]);
                var sp = so.FindProperty("tags");
                if (sp == null) return System.Array.Empty<string>();

                var list = new List<string>(sp.arraySize);
                for (int i = 0; i < sp.arraySize; i++)
                {
                    var el = sp.GetArrayElementAtIndex(i);
                    list.Add(el.stringValue);
                }
                return list.ToArray();
            }
        }

        // Eski: InternalEditorUtility.LayerMaskToConcatenatedLayersMask
        public static int LayerMaskToConcatenatedLayersMask(int mask) => mask;

        // Eski: InternalEditorUtility.ConcatenatedLayersMaskToLayerMask
        public static int ConcatenatedLayersMaskToLayerMask(int mask) => mask;
    }
}
#endif