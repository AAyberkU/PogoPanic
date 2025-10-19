using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections;

/// <summary>
/// Oyuncu kendi başlangıç konumuna T ile ışınlar.
/// - Player prefab'ına ekle.
/// - Prefab'ta NetworkObject (+ tercihen NetworkTransform) olmalı.
/// - NetworkTransform otoritesi Server ya da Owner olabilir; ikisiyle de çalışır.
/// </summary>
public class TeleportToStart : NetworkBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode teleportKey = KeyCode.T;
    [Tooltip("Tuşa basılı tutulması gereken süre (saniye). 0 = anında.")]
    [Min(0f)]
    [SerializeField] private float holdToTeleportSeconds = 0f;

    [Header("Start Position")]
    [Tooltip("Boş bırakılırsa spawn anındaki konum başlangıç kabul edilir.")]
    [SerializeField] private Transform customStartPoint;

    [Header("VFX")]
    [Tooltip("Teleport öncesi/sonrası oynatılacak smoke partikül prefabi (NetworkObject değil, sadece görsel)")]
    [SerializeField] private ParticleSystem smokeEffectPrefab;
    [Tooltip("Görsel amaçlı: Teleporttan sonra hedefte dumanı gecikmeli oynatır (fiziksel teleportu bekletmez).")]
    [SerializeField] private float preTeleportDelay = 0.5f;

    // Server writes, clients read
    private readonly NetworkVariable<Vector3> startPos =
        new NetworkVariable<Vector3>(writePerm: NetworkVariableWritePermission.Server);

    private NetworkTransform netTransform;
    private Rigidbody rb;
    private CharacterController cc;

    // Hold-to-teleport state
    private float holdTimer;
    private bool holdTriggered;

    public override void OnNetworkSpawn()
    {
        netTransform = GetComponent<NetworkTransform>();
        rb = GetComponent<Rigidbody>();
        cc = GetComponent<CharacterController>();

        if (IsServer)
        {
            var initial = customStartPoint ? customStartPoint.position : transform.position;
            startPos.Value = initial;
        }
    }

    private void Update()
    {
        if (!IsOwner || !IsClient) return;

        if (holdToTeleportSeconds <= 0f)
        {
            if (Input.GetKeyDown(teleportKey))
            {
                TriggerTeleport();
            }
            return;
        }

        if (Input.GetKey(teleportKey))
        {
            holdTimer += Time.unscaledDeltaTime;
            if (!holdTriggered && holdTimer >= holdToTeleportSeconds)
            {
                holdTriggered = true;
                TriggerTeleport();
            }
        }
        else
        {
            if (holdTimer > 0f || holdTriggered)
            {
                holdTimer = 0f;
                holdTriggered = false;
            }
        }
    }

    private bool HasTransformAuthority()
    {
        if (netTransform != null) return netTransform.CanCommitToTransform;
        // NetTransform yoksa server güvenli kaynak olsun
        return IsServer;
    }

    private void TriggerTeleport()
    {
        Vector3 target = startPos.Value;

        if (HasTransformAuthority())
        {
            // VFX yayınını server yaptır (client ise bildir)
            if (IsServer)
            {
                PlaySmokeEffectForAllClientRpc(transform.position);
                if (preTeleportDelay > 0f)
                    StartCoroutine(PlayDestinationSmokeDelayed(target, preTeleportDelay));
            }
            else
            {
                RequestVfxBroadcastServerRpc(transform.position, target, preTeleportDelay);
            }

            // Asıl teleportu otoritenin olduğu tarafta uygula (owner-auth ise local, server-auth ise server zaten burası)
            StartCoroutine(AuthorityTeleportRoutine(target));
        }
        else
        {
            // Otorite bizde değil → server uygulasın
            RequestTeleportServerRpc();
        }
    }

    //──────────────────────────────────────────────────────────────
    [ServerRpc]
    private void RequestTeleportServerRpc(ServerRpcParams rpcParams = default)
    {
        // Server-authority için: VFX + server tarafında teleport
        PlaySmokeEffectForAllClientRpc(transform.position);
        StartCoroutine(AuthorityTeleportRoutine(startPos.Value));

        if (preTeleportDelay > 0f)
            StartCoroutine(PlayDestinationSmokeDelayed(startPos.Value, preTeleportDelay));
    }

    [ServerRpc]
    private void RequestVfxBroadcastServerRpc(Vector3 current, Vector3 dest, float delay, ServerRpcParams rpcParams = default)
    {
        // Owner-authority durumda client isteğiyle sadece VFX yayınla (hareketi client zaten yapacak)
        PlaySmokeEffectForAllClientRpc(current);
        if (delay > 0f)
            StartCoroutine(PlayDestinationSmokeDelayed(dest, delay));
    }

    private IEnumerator PlayDestinationSmokeDelayed(Vector3 dest, float delay)
    {
        yield return new WaitForSeconds(delay);
        PlaySmokeEffectForAllClientRpc(dest);
    }

    //──────────────────────────────────────────────────────────────
    // Bu rutin hem server-authority hem owner-authority (local owner) için aynıdır.
    private IEnumerator AuthorityTeleportRoutine(Vector3 target)
    {
        if (cc != null)
        {
            bool wasEnabled = cc.enabled;
            if (wasEnabled) cc.enabled = false;

            ApplyTeleportPosition(target);
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            if (wasEnabled) cc.enabled = true;
            yield break;
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            bool prevDetect = rb.detectCollisions;
            var prevInterp = rb.interpolation;
            rb.detectCollisions = false;
            if (prevInterp != RigidbodyInterpolation.None)
                rb.interpolation = RigidbodyInterpolation.None;

            ApplyTeleportPosition(target);

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();

            rb.detectCollisions = prevDetect;
            rb.interpolation = prevInterp;
            yield break;
        }

        ApplyTeleportPosition(target);
        Physics.SyncTransforms();
        yield return null;
    }

    /// <summary>
    /// Varsa NetworkTransform.Teleport kullan; yoksa mevcut güvenli atama yoluyla konumu ver.
    /// Owner-auth’ta owner tarafında çağrıldığında state server’a çoğaltılır.
    /// </summary>
    private void ApplyTeleportPosition(Vector3 pos)
    {
        if (netTransform != null)
        {
            try
            {
                netTransform.Teleport(pos, transform.rotation, transform.localScale);
                return;
            }
            catch
            {
                // Teleport API yoksa fallback'e düş
            }
        }

        if (cc != null)
        {
            transform.position = pos;
            return;
        }

        if (rb != null)
        {
            if (rb.isKinematic)
                transform.position = pos;
            else
                rb.position = pos;
            return;
        }

        transform.position = pos;
    }

    //──────────────────────────────────────────────────────────────
    [ClientRpc]
    private void PlaySmokeEffectForAllClientRpc(Vector3 pos)
    {
        PlaySmokeEffectAt(pos);
    }

    private void PlaySmokeEffectAt(Vector3 pos)
    {
        if (!smokeEffectPrefab) return;

        ParticleSystem smoke = Instantiate(smokeEffectPrefab, pos, Quaternion.identity);
        smoke.Play();
        Destroy(smoke.gameObject, smoke.main.duration + 1f);
    }

    //──────────────────────────────────────────────────────────────
    [ServerRpc(RequireOwnership = true)]
    public void SetCurrentAsStartServerRpc()
    {
        startPos.Value = transform.position;
    }
}
