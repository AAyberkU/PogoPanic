#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;

[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
public class DebugSaveTeleport : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode saveKey     = KeyCode.F7;
    [SerializeField] private KeyCode teleportKey = KeyCode.F9;

    [Header("What to save/apply")]
    [SerializeField] private bool applySavedScale = true;
    [SerializeField] private bool resetVelocities = true;

    NetworkTransform netTransform;
    Rigidbody rb;
    CharacterController cc;

    bool hasSaved;
    Vector3 savedPos;
    Quaternion savedRot;
    Vector3 savedScale = Vector3.one;

    void Awake()
    {
        netTransform = GetComponent<NetworkTransform>();
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();
    }

    void Update()
    {
        if (!IsClient || !IsOwner) return;

        if (Input.GetKeyDown(saveKey))
            SaveHere();

        if (Input.GetKeyDown(teleportKey))
        {
            if (!hasSaved)
            {
                Debug.Log("[DebugSaveTeleport] Kayıt yok (F7 ile kaydet).");
                return;
            }
            RequestTeleportToSavedServerRpc(savedPos, savedRot, savedScale,
                                            resetVelocities, applySavedScale);
        }
    }

    void SaveHere()
    {
        savedPos   = transform.position;
        savedRot   = transform.rotation;
        savedScale = transform.localScale;
        hasSaved   = true;

        Debug.Log($"[DebugSaveTeleport] Kayıt @ {savedPos} rot={savedRot.eulerAngles} scale={savedScale}");
    }

    //──────────────────────────────────────────────────────────────
    [ServerRpc(RequireOwnership = true)]
    void RequestTeleportToSavedServerRpc(Vector3 pos, Quaternion rot, Vector3 scale,
                                         bool zeroVel, bool applyScale, ServerRpcParams rpcParams = default)
    {
        bool serverHasAuthority = true;

        // Eğer NetworkTransform varsa otoriteyi kontrol et
        if (netTransform != null)
            serverHasAuthority = netTransform.CanCommitToTransform;

        if (serverHasAuthority)
        {
            // Server-authoritative durumda direkt uygula
            if (!TryApplyTeleportImmediate(pos, rot, scale, zeroVel, applyScale))
                SendTeleportToOwner(pos, rot, scale, zeroVel, applyScale);
        }
        else
        {
            // Owner-authoritative durumda owner uygular
            SendTeleportToOwner(pos, rot, scale, zeroVel, applyScale);
        }
    }

    void SendTeleportToOwner(Vector3 pos, Quaternion rot, Vector3 scale, bool zeroVel, bool applyScale)
    {
        var target = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { OwnerClientId } }
        };
        ApplyTeleportClientRpc(pos, rot, scale, zeroVel, applyScale, target);
    }

    [ClientRpc]
    void ApplyTeleportClientRpc(Vector3 pos, Quaternion rot, Vector3 scale,
                                bool zeroVel, bool applyScale, ClientRpcParams _ = default)
    {
        if (!IsOwner) return; // sadece hedef owner uygulasın
        TryApplyTeleportImmediate(pos, rot, scale, zeroVel, applyScale);
    }

    /// <summary>
    /// Bulunduğu tarafta anında uygular. Başarısız olursa false döner.
    /// </summary>
    bool TryApplyTeleportImmediate(Vector3 pos, Quaternion rot, Vector3 scale,
                                   bool zeroVel, bool applyScale)
    {
        bool ccWasEnabled = false;
        RigidbodyInterpolation rbPrevInterp = RigidbodyInterpolation.None;
        bool rbPrevDetect = false;

        if (cc != null) { ccWasEnabled = cc.enabled; if (ccWasEnabled) cc.enabled = false; }
        if (rb != null)
        {
            if (zeroVel) { rb.linearVelocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rbPrevDetect = rb.detectCollisions; rb.detectCollisions = false;
            rbPrevInterp = rb.interpolation; if (rbPrevInterp != RigidbodyInterpolation.None) rb.interpolation = RigidbodyInterpolation.None;
        }

        bool ok = true;

        if (netTransform != null)
        {
            try
            {
                var finalScale = applyScale ? scale : transform.localScale;
                netTransform.Teleport(pos, rot, finalScale); // yalnızca authoritative tarafta geçerli
            }
            catch
            {
                ok = false;
            }
        }
        else
        {
            if (rb != null && !rb.isKinematic)
            {
                rb.position = pos;
                rb.rotation = rot;
            }
            else
            {
                transform.SetPositionAndRotation(pos, rot);
            }

            if (applyScale) transform.localScale = scale;
        }

        Physics.SyncTransforms();

        if (rb != null) { rb.detectCollisions = rbPrevDetect; rb.interpolation = rbPrevInterp; }
        if (cc != null && ccWasEnabled) cc.enabled = true;

        return ok;
    }
}
#endif