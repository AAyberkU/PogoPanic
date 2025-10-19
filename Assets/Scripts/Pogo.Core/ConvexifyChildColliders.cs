// ConvexifyChildColliders.cs
// Ayberk için: Player root'un altındaki MeshCollider'ları güvenli şekilde CONVEX yapar.
// - SkinnedMesh altındaki collider'ları kaldırma (opsiyonel)
// - Convex cook başarısız olursa BoxCollider fallback (opsiyonel)
// - Editor'de Undo destekli (Ctrl+Z)
//
// Kullanım:
// 1) Script'i player root'una ekle.
// 2) Inspector'da "Convexify Children" butonuna bas.
// 3) Beğenmezsen Ctrl+Z ile geri alabilirsin.

using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DisallowMultipleComponent]
[ExecuteAlways]
public class ConvexifyChildColliders : MonoBehaviour
{
    [Header("Scope")]
    [Tooltip("Boş bırakılırsa bu component'in bulunduğu nesne kök kabul edilir.")]
    public Transform rootOverride;

    [Header("Filters")]
    [Tooltip("isTrigger olan MeshCollider'lar da işleme dahil edilsin mi?")]
    public bool includeTriggers = false;

    [Tooltip("SkinnedMeshRenderer altında bulunan MeshCollider'ları kaldır (önerilir).")]
    public bool removeCollidersUnderSkinnedMeshes = true;

    [Header("Fallback")]
    [Tooltip("Convex cook başarısız olursa (örn. 255 üçgen sınırı) otomatik BoxCollider ekle.")]
    public bool addBoxFallbackIfConvexFails = true;

    [Tooltip("Fallback eklendiğinde orijinal MeshCollider'ı devre dışı bırak.")]
    public bool disableOriginalOnFallback = true;

    [Header("Cooking Preferences (daha az şişirme, daha fazla doğruluk)")]
    [Tooltip("Kapatırsan daha hızlı ama daha kaba simülasyon tercih edilir.")]
    public bool cookForAccuracy = true;

    [Tooltip("Kopuk/bozuk mesh verisini temizlemeye çalış.")]
    public bool enableMeshCleaning = true;

    [Tooltip("Aynı noktadaki verteksleri kaynat (weld).")]
    public bool weldColocated = true;

    // ---- Public action ----
#if UNITY_EDITOR
    [ContextMenu("Convexify Children (Run Once)")]
#endif
    public void Convexify()
    {
        var root = rootOverride ? rootOverride : transform;

        // SkinnedMesh altındaki dalları işaretle (opsiyonel kaldırma için)
        HashSet<Transform> skinnedBranches = new HashSet<Transform>();
        if (removeCollidersUnderSkinnedMeshes)
        {
            foreach (var smr in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                skinnedBranches.Add(smr.transform);
        }

        int changed = 0, removed = 0, fallbacked = 0, skipped = 0;

        var colliders = root.GetComponentsInChildren<MeshCollider>(true);
        foreach (var mc in colliders)
        {
            if (!includeTriggers && mc.isTrigger) { skipped++; continue; }

            // Skinned altı ise opsiyonel kaldır
            if (removeCollidersUnderSkinnedMeshes && IsUnderAny(mc.transform, skinnedBranches))
            {
#if UNITY_EDITOR
                Undo.RecordObject(mc.gameObject, "Remove MeshCollider under SkinnedMesh");
                Undo.DestroyObjectImmediate(mc);
#else
                Destroy(mc);
#endif
                removed++;
                continue;
            }

            ApplyCookingPrefs(mc);

            if (mc.convex) { skipped++; continue; } // zaten convex

#if UNITY_EDITOR
            Undo.RecordObject(mc, "Make MeshCollider Convex");
#endif
            bool success = TryMakeConvex(mc);

            if (!success && addBoxFallbackIfConvexFails)
            {
                var box = mc.gameObject.GetComponent<BoxCollider>();
                if (box == null) box = mc.gameObject.AddComponent<BoxCollider>();

                // Boyutlandırma: mümkün olduğunca local mesh bounds üzerinden
                Bounds b = GetLocalBoundsFor(mc);
                box.center = b.center;
                box.size   = b.size;

                if (disableOriginalOnFallback) mc.enabled = false;
                fallbacked++;
                continue;
            }

            if (success) changed++; else skipped++;
        }

        Debug.Log($"[ConvexifyChildColliders] changed:{changed}, removed:{removed}, fallback:{fallbacked}, skipped:{skipped}");
    }

    // ---- Helpers ----
    private bool TryMakeConvex(MeshCollider mc)
    {
        try
        {
            mc.convex = true; // Unity burada convex hull cook eder
            // Bazı sürümlerde cook tetiklemek için erişim:
            var _ = mc.sharedMesh;
            return mc.convex && mc.enabled;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Convex cook failed on '{mc.name}': {e.Message}", mc);
            return false;
        }
    }

    private void ApplyCookingPrefs(MeshCollider mc)
    {
#if UNITY_2020_1_OR_NEWER
        var opts = MeshColliderCookingOptions.None;

        // Daha hızlı ama daha kaba simülasyon istenirse:
        if (!cookForAccuracy)
            opts |= MeshColliderCookingOptions.CookForFasterSimulation;

        if (enableMeshCleaning)
            opts |= MeshColliderCookingOptions.EnableMeshCleaning;

        if (weldColocated)
            opts |= MeshColliderCookingOptions.WeldColocatedVertices;

        // NOT: InflateConvexMesh obsolet; kullanmıyoruz.
        // NOT: MakeTrianglesUnique bazı sürümlerde yok; kullanmıyoruz.

        mc.cookingOptions = opts;
#endif
    }

    private static bool IsUnderAny(Transform t, HashSet<Transform> ancestors)
    {
        var p = t;
        while (p != null)
        {
            if (ancestors.Contains(p)) return true;
            p = p.parent;
        }
        return false;
    }

    private static Bounds GetLocalBoundsFor(MeshCollider mc)
    {
        // Öncelik: MeshFilter.sharedMesh.bounds (local space)
        var mf = mc.GetComponent<MeshFilter>();
        if (mf && mf.sharedMesh) return mf.sharedMesh.bounds;

        // SkinnedMeshRenderer için sharedMesh.bounds (local space)
        var smr = mc.GetComponent<SkinnedMeshRenderer>();
        if (smr && smr.sharedMesh) return smr.sharedMesh.bounds;

        // Son çare: Renderer.bounds world space -> local'a dönüştür
        var rend = mc.GetComponent<Renderer>();
        if (rend)
        {
            var world = rend.bounds;
            // World -> local dönüşümü yaklaşık: inverse lossyScale uygula
            var t = mc.transform;
            Vector3 sizeLocal = Divide(world.size, t.lossyScale);
            // center local'e kabaca taşı:
            Vector3 centerLocal = t.InverseTransformPoint(world.center);
            return new Bounds(centerLocal, Abs(sizeLocal));
        }

        return new Bounds(Vector3.zero, Vector3.one);
    }

    private static Vector3 Divide(Vector3 a, Vector3 b)
        => new Vector3(
            b.x != 0 ? a.x / b.x : 0f,
            b.y != 0 ? a.y / b.y : 0f,
            b.z != 0 ? a.z / b.z : 0f);

    private static Vector3 Abs(Vector3 v)
        => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

#if UNITY_EDITOR
    // ---- Inspector butonu ----
    [CustomEditor(typeof(ConvexifyChildColliders))]
    public class ConvexifyChildCollidersEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.Space(8);

            var comp = (ConvexifyChildColliders)target;
            using (new EditorGUI.DisabledScope(comp == null))
            {
                if (GUILayout.Button("Convexify Children"))
                    comp.Convexify();
            }

            EditorGUILayout.HelpBox(
                "Not: Unity tek bir MeshCollider için TEK convex hull üretir. "
              + "“Sadece içbükey bölgeleri düzleştir” gibi çoklu hull ayrıştırma için harici VHACD benzeri araç gerekir. "
              + "Convex cook başarısız olursa isteğe bağlı BoxCollider fallback eklenir. "
              + "Editor'de yapılan değişiklikleri Ctrl+Z ile geri alabilirsin.",
                MessageType.Info);
        }
    }
#endif
}
