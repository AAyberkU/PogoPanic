#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using Unity.Netcode;
using System;
using System.Linq;

public class PivotFixerWindow : EditorWindow
{
    private enum Mode
    {
        MoveParentToChildrenBoundsCenter,   // parent'ı children bounds merkezine taşır (children world korunur)
        WrapWithNewPivotAtBoundsCenter      // seçili objeyi yeni bir pivot parent içine alır
    }

    private Mode mode = Mode.MoveParentToChildrenBoundsCenter;

    // Options (genel)
    private bool ensureRigidbodyPresetOnPivot = true; // (Kinematic + Preset)
    private bool ensureNetworkObjectOnPivot   = true;
    private bool moveRotatorPlatformToPivot   = true;
    private bool removeChildRigidbodies       = true;

    // Wrap özel opsiyonlar
    private bool stripChildNetworkObjectsOnWrap = true;
    private bool moveMotionClockRotateToPivot   = true; // name-based reflection ile

    [MenuItem("Tools/Pivot Fixer")]
    static void Open() => GetWindow<PivotFixerWindow>("Pivot Fixer");

    void OnGUI()
    {
        EditorGUILayout.LabelField("Batch Pivot & Network Fix", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        mode = (Mode)EditorGUILayout.EnumPopup("Mode", mode);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        ensureRigidbodyPresetOnPivot = EditorGUILayout.ToggleLeft("Ensure Rigidbody on Pivot (Kinematic + Preset)", ensureRigidbodyPresetOnPivot);
        ensureNetworkObjectOnPivot   = EditorGUILayout.ToggleLeft("Ensure NetworkObject on Pivot", ensureNetworkObjectOnPivot);
        moveRotatorPlatformToPivot   = EditorGUILayout.ToggleLeft("Move RotatorPlatform to Pivot (if exists)", moveRotatorPlatformToPivot);
        removeChildRigidbodies       = EditorGUILayout.ToggleLeft("Remove Child Rigidbodies (compound rule)", removeChildRigidbodies);

        EditorGUILayout.Space(4);
        using (new EditorGUI.DisabledScope(mode != Mode.WrapWithNewPivotAtBoundsCenter))
        {
            EditorGUILayout.LabelField("Wrap-only Options", EditorStyles.miniBoldLabel);
            stripChildNetworkObjectsOnWrap = EditorGUILayout.ToggleLeft("Strip NetworkObjects on Children (Wrap only)", stripChildNetworkObjectsOnWrap);
            moveMotionClockRotateToPivot   = EditorGUILayout.ToggleLeft("Move MotionClockRotate to Pivot (Wrap only)", moveMotionClockRotateToPivot);
        }

        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button($"Process Selection ({Selection.gameObjects.Length})"))
            {
                ProcessSelection();
            }
        }

        if (Selection.gameObjects.Length == 0)
        {
            EditorGUILayout.HelpBox("Hierarchy’den düzeltmek istediğin GameObject’leri seç.", MessageType.Info);
        }
    }

    private void ProcessSelection()
    {
        var gos = Selection.gameObjects;
        if (gos.Length == 0) return;

        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        int fixedCount = 0;

        foreach (var go in gos)
        {
            if (!go) continue;

            bool ok = false;
            switch (mode)
            {
                case Mode.MoveParentToChildrenBoundsCenter:
                    ok = MoveParentToChildrenBoundsCenter(go);
                    if (ok)
                        ok = AfterPivotReady(go); // pivot = go (kendi)
                    break;

                case Mode.WrapWithNewPivotAtBoundsCenter:
                    ok = WrapWithPivot(go, out var pivot);
                    if (ok && pivot != null)
                    {
                        // --- WRAP MODE EK İŞLEMLER ---
                        if (moveRotatorPlatformToPivot)
                            CopyThenRemove<RotatorPlatform>(go, pivot);

                        if (moveMotionClockRotateToPivot)
                            CopyThenRemoveByTypeName(go, pivot, "MotionClockRotate");

                        if (stripChildNetworkObjectsOnWrap)
                            StripChildNetworkObjects(pivot);

                        ok = AfterPivotReady(pivot); // pivot = yeni parent
                    }
                    break;
            }

            if (ok) fixedCount++;
        }

        Undo.CollapseUndoOperations(group);
        Debug.Log($"[PivotFixer] Done. Processed: {fixedCount}/{gos.Length}");
    }

    // --- MODE 1: Parent'ı çocukların bounds merkezine taşır; çocuklar world sabit kalır
    private bool MoveParentToChildrenBoundsCenter(GameObject root)
    {
        if (!TryComputeBoundsFromChildren(root, out var b))
        {
            Debug.LogWarning($"[PivotFixer] No child colliders/renderers for bounds: {root.name}");
            return false;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Move Parent To Bounds Center");

        Vector3 target = b.center;
        Vector3 delta = target - root.transform.position;

        foreach (Transform child in root.transform)
        {
            child.position -= delta; // çocukların world'ünü koru
        }
        root.transform.position = target;

        return true;
    }

    // --- MODE 2: Seçili objeyi yeni bir pivot parent içine alır (pivot world = bounds center)
    private bool WrapWithPivot(GameObject go, out GameObject pivot)
    {
        pivot = null;

        if (!TryComputeBoundsFromSelfOrChildren(go, out var b))
        {
            Debug.LogWarning($"[PivotFixer] No bounds (self/children): {go.name}");
            return false;
        }

        Undo.IncrementCurrentGroup();
        int grp = Undo.GetCurrentGroup();

        pivot = new GameObject(go.name + "_Pivot");
        Undo.RegisterCreatedObjectUndo(pivot, "Create Pivot");
        pivot.transform.SetPositionAndRotation(b.center, Quaternion.identity);
        pivot.transform.localScale = Vector3.one;

        var oldParent = go.transform.parent;
        int oldIndex = go.transform.GetSiblingIndex();

        if (oldParent) Undo.SetTransformParent(pivot.transform, oldParent, "Set Pivot Parent");
        Undo.SetTransformParent(go.transform, pivot.transform, "Wrap With Pivot");
        pivot.transform.SetSiblingIndex(oldIndex);

        Undo.CollapseUndoOperations(grp);
        return true;
    }

    // --- Wrap-only: pivot dışındaki çocuklardaki NetworkObject'leri kaldır
    private void StripChildNetworkObjects(GameObject pivot)
    {
        var allNOs = pivot.GetComponentsInChildren<NetworkObject>(true);
        foreach (var no in allNOs)
        {
            if (!no) continue;
            if (no.gameObject == pivot) continue; // pivot üzerindeki NO kalsın
            Undo.DestroyObjectImmediate(no);
        }
    }

    // --- Pivot hazırlandıktan sonra (her iki mod için ortak): RB/NO/child RB temizliği
    private bool AfterPivotReady(GameObject pivot)
    {
        if (ensureRigidbodyPresetOnPivot)
        {
            var rb = pivot.GetComponent<Rigidbody>();
            if (!rb)
            {
                Undo.AddComponent<Rigidbody>(pivot);
                rb = pivot.GetComponent<Rigidbody>();
            }

            Undo.RecordObject(rb, "Configure Rigidbody (Preset)");

            // ---- Preset ----
            rb.mass = 1f;
            rb.linearDamping = 0f;                 // Linear Damping
            rb.angularDamping = 0.05f;       // Angular Damping
            rb.useGravity = true;         // Use Gravity
            rb.isKinematic = true;        // Is Kinematic
            rb.interpolation = RigidbodyInterpolation.None; // Interpolate: None
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete; // Collision: Discrete
            rb.constraints = RigidbodyConstraints.None;     // Constraints: none

            // Automatic CoM / Tensor -> reset
            rb.ResetCenterOfMass();
            rb.ResetInertiaTensor();

#if UNITY_6000_0_OR_NEWER
            try
            {
                var includeProp = typeof(Rigidbody).GetProperty("includeLayers");
                var excludeProp = typeof(Rigidbody).GetProperty("excludeLayers");
                if (includeProp != null) includeProp.SetValue(rb, (LayerMask)0, null);
                if (excludeProp != null) excludeProp.SetValue(rb, (LayerMask)0, null);
            }
            catch { /* property yoksa sessiz geç */ }
#endif
        }

        if (ensureNetworkObjectOnPivot)
        {
            if (!pivot.GetComponent<NetworkObject>())
                Undo.AddComponent<NetworkObject>(pivot);
        }

        if (removeChildRigidbodies)
        {
            foreach (var crb in pivot.GetComponentsInChildren<Rigidbody>(true))
            {
                if (crb.gameObject == pivot) continue; // pivot RB kalsın
                Undo.DestroyObjectImmediate(crb);
            }
        }

        return true;
    }

    // --- Bounds hesapları
    private bool TryComputeBoundsFromChildren(GameObject root, out Bounds b)
    {
        b = new Bounds();
        bool inited = false;

        foreach (var c in root.GetComponentsInChildren<Collider>(true))
        {
            if (c.gameObject == root) continue;
            if (!inited) { b = c.bounds; inited = true; }
            else b.Encapsulate(c.bounds);
        }
        if (inited) return true;

        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject == root) continue;
            if (!inited) { b = r.bounds; inited = true; }
            else b.Encapsulate(r.bounds);
        }
        return inited;
    }

    private bool TryComputeBoundsFromSelfOrChildren(GameObject root, out Bounds b)
    {
        b = new Bounds();
        bool inited = false;

        // Önce self
        foreach (var c in root.GetComponents<Collider>())
        {
            if (!inited) { b = c.bounds; inited = true; }
            else b.Encapsulate(c.bounds);
        }
        foreach (var r in root.GetComponents<Renderer>())
        {
            if (!inited) { b = r.bounds; inited = true; }
            else b.Encapsulate(r.bounds);
        }
        if (inited) return true;

        // Sonra children
        return TryComputeBoundsFromChildren(root, out b);
    }

    // --- Component taşıma yardımcıları
    private void CopyThenRemove<T>(GameObject src, GameObject dst) where T : Component
    {
        var comp = src.GetComponent<T>();
        if (!comp) return;

        UnityEditorInternal.ComponentUtility.CopyComponent(comp);
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(dst);
        Undo.DestroyObjectImmediate(comp);
    }

    // Tip adıyla component taşı (derlemede tip yoksa da çalışır)
    private void CopyThenRemoveByTypeName(GameObject src, GameObject dst, string typeName)
    {
        var t = AppDomain.CurrentDomain.GetAssemblies()
            .Select(a => a.GetType(typeName, false))
            .FirstOrDefault(tt => tt != null && typeof(Component).IsAssignableFrom(tt));
        if (t == null) return;

        var comp = src.GetComponent(t) as Component;
        if (!comp) return;

        UnityEditorInternal.ComponentUtility.CopyComponent(comp);
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(dst);
        Undo.DestroyObjectImmediate(comp);
    }
}
#endif
