// File: Assets/Editor/ColliderAuditor.cs
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ColliderAuditor : EditorWindow
{
    private enum CountMode { SameGameObjectOnly, IncludeChildren }
    private enum Dim { Only3D, Only2D, Both }

    private CountMode countMode = CountMode.SameGameObjectOnly;
    private Dim dimension = Dim.Both;

    private bool requireExactlyTwo = true;   // exactly 2
    private int minCount = 2;                // used when requireExactlyTwo == false
    private int maxCount = 99;

    private string parentName = "TwoCollider_Group";
    private string tagToSet = "";
    private int layerToSet = 0;

    private readonly List<GameObject> results = new List<GameObject>();
    private Vector2 scroll;

    [MenuItem("Tools/Collider Auditor")]
    public static void ShowWindow()
    {
        var w = GetWindow<ColliderAuditor>("Collider Auditor");
        w.minSize = new Vector2(420, 300);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Scan Options", EditorStyles.boldLabel);
        countMode = (CountMode)EditorGUILayout.EnumPopup("Counting Scope", countMode);
        dimension = (Dim)EditorGUILayout.EnumPopup("Collider Dimension", dimension);

        requireExactlyTwo = EditorGUILayout.ToggleLeft("Exactly 2 colliders", requireExactlyTwo);
        EditorGUI.BeginDisabledGroup(requireExactlyTwo);
        EditorGUILayout.BeginHorizontal();
        minCount = EditorGUILayout.IntField("Min", Mathf.Max(0, minCount));
        maxCount = EditorGUILayout.IntField("Max", Mathf.Max(minCount, maxCount));
        EditorGUILayout.EndHorizontal();
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Scan", GUILayout.Height(28)))
        {
            Scan();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField($"Found: {results.Count}", EditorStyles.boldLabel);

        // Actions
        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Select Found"))
                Selection.objects = results.Cast<UnityEngine.Object>().ToArray();

            if (GUILayout.Button("Group Under Parent"))
                GroupUnderParent();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            tagToSet = EditorGUILayout.TextField("Set Tag", tagToSet);
            if (GUILayout.Button("Apply Tag", GUILayout.Width(100)))
                ApplyTag();
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            layerToSet = EditorGUILayout.LayerField("Set Layer", layerToSet);
            if (GUILayout.Button("Apply Layer", GUILayout.Width(100)))
                ApplyLayer();
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Results (click to ping):", EditorStyles.miniBoldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll);
        foreach (var go in results)
        {
            if (!go) continue;
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button(go.name, EditorStyles.objectField))
                    EditorGUIUtility.PingObject(go);
                EditorGUILayout.LabelField($"Path: {GetHierarchyPath(go)}", EditorStyles.miniLabel);
            }
        }
        EditorGUILayout.EndScrollView();
    }

    private void Scan()
    {
        results.Clear();

        var all = EnumerateAllGameObjects();
        int total = all.Count;
        try
        {
            for (int i = 0; i < total; i++)
            {
                var go = all[i];
                if (!go) continue;

                if (i % 128 == 0)
                    EditorUtility.DisplayProgressBar("Scanning colliders", go.name, (float)i / total);

                int c = CountColliders(go, countMode, dimension);
                bool match = requireExactlyTwo ? c == 2 : (c >= minCount && c <= maxCount);
                if (match)
                    results.Add(go);
            }
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Repaint();
        Debug.Log($"[ColliderAuditor] Found {results.Count} objects matching criteria.");
    }

    private static List<GameObject> EnumerateAllGameObjects()
    {
        var list = new List<GameObject>(1024);
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            var scene = SceneManager.GetSceneAt(s);
            if (!scene.isLoaded) continue;
            foreach (var root in scene.GetRootGameObjects())
                Traverse(root.transform, list);
        }
        return list;

        static void Traverse(Transform t, List<GameObject> acc)
        {
            acc.Add(t.gameObject);
            for (int i = 0; i < t.childCount; i++)
                Traverse(t.GetChild(i), acc);
        }
    }

    private static int CountColliders(GameObject go, CountMode mode, Dim dim)
    {
        if (mode == CountMode.SameGameObjectOnly)
        {
            int count = 0;
            if (dim == Dim.Only3D || dim == Dim.Both)
                count += go.GetComponents<Collider>().Length;
            if (dim == Dim.Only2D || dim == Dim.Both)
                count += go.GetComponents<Collider2D>().Length;
            return count;
        }
        else
        {
            int count = 0;
            if (dim == Dim.Only3D || dim == Dim.Both)
                count += go.GetComponentsInChildren<Collider>(true).Length;
            if (dim == Dim.Only2D || dim == Dim.Both)
                count += go.GetComponentsInChildren<Collider2D>(true).Length;
            return count;
        }
    }

    private void GroupUnderParent()
    {
        if (results.Count == 0) return;

        // Create a parent under each scene the objects belong to, keeping scene separation
        var byScene = results.Where(r => r).GroupBy(r => r.scene);
        foreach (var grp in byScene)
        {
            var scene = grp.Key;
            string name = UniqueNameInScene(scene, parentName);
            var parentGO = new GameObject(name);
            SceneManager.MoveGameObjectToScene(parentGO, scene);

            Undo.RegisterCreatedObjectUndo(parentGO, "Create Group Parent");

            foreach (var go in grp)
            {
                if (!go) continue;
                Undo.SetTransformParent(go.transform, parentGO.transform, "Group Under Parent");
            }
        }
    }

    private void ApplyTag()
    {
        if (string.IsNullOrEmpty(tagToSet)) return;
        if (!IsExistingTag(tagToSet))
        {
            EditorUtility.DisplayDialog("Tag not found",
                $"Tag '{tagToSet}' mevcut değil. Önce Tags & Layers üzerinden ekleyin.",
                "OK");
            return;
        }
        Undo.RecordObjects(results.Where(r=>r).ToArray(), "Apply Tag");
        foreach (var go in results)
            if (go) go.tag = tagToSet;
    }

    private void ApplyLayer()
    {
        Undo.RecordObjects(results.Where(r=>r).ToArray(), "Apply Layer");
        foreach (var go in results)
            if (go) go.layer = layerToSet;
    }

    private static bool IsExistingTag(string tag)
    {
        return InternalEditorUtility.tags.Contains(tag);
    }

    private static string UniqueNameInScene(Scene scene, string baseName)
    {
        var existing = scene.GetRootGameObjects().Select(g => g.name).ToHashSet();
        if (!existing.Contains(baseName)) return baseName;
        int i = 1;
        while (existing.Contains($"{baseName}_{i}")) i++;
        return $"{baseName}_{i}";
    }

    private static string GetHierarchyPath(GameObject go)
    {
        var stack = new Stack<string>();
        var t = go.transform;
        while (t != null)
        {
            stack.Push(t.name);
            t = t.parent;
        }
        return string.Join("/", stack);
    }
}

// Needed for tags API
static class InternalEditorUtility
{
    public static string[] tags => UnityEditorInternal.InternalEditorUtility.tags;
}
#endif
