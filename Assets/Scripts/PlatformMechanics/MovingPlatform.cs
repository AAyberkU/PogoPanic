using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using DG.Tweening; // yalnız Ease için

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(NetworkObject), typeof(MotionClockMove))]
public class MovingPlatform : NetworkBehaviour
{
    public enum MoveStyle { Triggered, AutoLoop }

    [Header("Points")]
    public Transform targetPoint;

    [Header("Settings")]
    public float speed = 2f;
    public float waitTime = 1f;
    public Ease easeType = Ease.InOutSine;

    [Header("Behavior")]
    public MoveStyle moveStyle = MoveStyle.Triggered;

    [Header("Player Tag")]
    public string playerTag = "Player";

    [Header("Robustness")]
    [Tooltip("Kısa süreli temas kopmalarında tetikleme/hâlâ temas var sayma toleransı")]
    [SerializeField] private float coyoteTime = 0.15f;

    // ─── internals ─────────────────────────────────────────────────────
    private Rigidbody rb;
    private MotionClockMove clock;

    private Vector3 startPos;
    private Vector3 lastPlatformPos; // yalnız çizim/inspect için

    private float legDuration;
    private float singleRunDuration;
    private float loopCycleDuration;

    // Son görülen temas zamanları (rider başına)
    private readonly Dictionary<Rigidbody, float> lastSeenContactTime = new();

    // Gate: Triggered modda tetiklenene kadar hareket etme
    private readonly NetworkVariable<bool> allowMoveNV =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private bool RunActive => clock.IsActive && !clock.IsPaused;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;

        clock = GetComponent<MotionClockMove>();
        BindStartAndRecompute();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            if (moveStyle == MoveStyle.Triggered)
            {
                allowMoveNV.Value = false;     // kapalı başla
                rb.MovePosition(startPos);
                lastPlatformPos = startPos;
            }
            else if (moveStyle == MoveStyle.AutoLoop && !clock.IsActive)
            {
                clock.SetTimeScale(1f);
                clock.StartMotion();
                allowMoveNV.Value = true;      // AutoLoop açık
            }
        }
    }

    void OnValidate()
    {
        if (Application.isPlaying) return;
        if (tryGetRb(out var _))
            startPos = transform.position;
        RecomputeDurations();
    }

    void OnEnable()
    {
        lastPlatformPos = transform.position;
        RecomputeDurations();
    }

    void FixedUpdate()
    {
        // ── SERVER: Triggered tek tur bitince kapat ─────────────────────
        if (IsServer && moveStyle == MoveStyle.Triggered && allowMoveNV.Value && RunActive)
        {
            if (clock.EffectiveTimeUnscaled >= singleRunDuration)
            {
                clock.Pause();
                allowMoveNV.Value = false;
#if UNITY_EDITOR
                Debug.Log($"[Platform:{name}] Run finished -> gate OFF");
#endif
            }
        }

        // ── Coyote fallback: kısa kopma olduysa hâlâ temas say ──────────
        if (IsServer && moveStyle == MoveStyle.Triggered && !allowMoveNV.Value && coyoteTime > 0f)
        {
            float now = Time.time;
            foreach (var kvp in lastSeenContactTime)
            {
                if (kvp.Key == null) continue;
                if (now - kvp.Value <= coyoteTime)
                {
                    // Temas yakın geçmişte vardı → tetikle
                    RequestTriggerServerRpc();
                    break;
                }
            }
        }

        // ── Pozisyonu uygula ────────────────────────────────────────────
        Vector3 newPos;
        if (moveStyle == MoveStyle.Triggered && !allowMoveNV.Value)
        {
            newPos = startPos; // clock aksa bile gate kapalı iken hareket yok
        }
        else
        {
            newPos = ComputePositionFromET(clock.EffectiveTime);
        }

        rb.MovePosition(newPos);
        lastPlatformPos = newPos;
    }

    // ─── Temas olayları (ÜSTTE/YANDA/ALTTAN ayrımı YOK) ────────────────
    void OnCollisionEnter(Collision col)
    {
        HandleContact(col);
    }

    void OnCollisionStay(Collision col)
    {
        HandleContact(col);
    }

    void OnCollisionExit(Collision col)
    {
        var riderRb = col.collider.attachedRigidbody;
        if (riderRb == null) return;
        if (!riderRb.CompareTag(playerTag)) return;

        // Çıkış anını kaydet (coyote için)
        lastSeenContactTime[riderRb] = Time.time;
    }

    private void HandleContact(Collision col)
    {
        var riderRb = col.collider.attachedRigidbody;
        if (riderRb == null) return;
        if (!riderRb.CompareTag(playerTag)) return;

        // Son görülen zamanı sürekli güncelle (Enter/Stay)
        lastSeenContactTime[riderRb] = Time.time;

        // Triggered modda gate kapalıysa: ANINDA tetikle
        if (moveStyle == MoveStyle.Triggered && !allowMoveNV.Value)
        {
#if UNITY_EDITOR
            Debug.Log($"[Platform:{name}] Contact -> request trigger");
#endif
            RequestTriggerServerRpc();
        }
    }

    // ─── Trigger akışı (server-authoritative) ──────────────────────────
    [ServerRpc(RequireOwnership = false)]
    private void RequestTriggerServerRpc(ServerRpcParams rpc = default)
    {
        if (moveStyle != MoveStyle.Triggered) return;
        if (allowMoveNV.Value) return; // zaten açık

        allowMoveNV.Value = true;
        clock.ResetClock(true); // aktif + ET=0
        clock.StartMotion();

#if UNITY_EDITOR
        Debug.Log($"[Platform:{name}] TRIGGERED -> gate ON, clock START");
#endif
    }

    // ─── Yardımcılar ───────────────────────────────────────────────────
    private void BindStartAndRecompute()
    {
        startPos = transform.position;
        RecomputeDurations();
    }

    private void RecomputeDurations()
    {
        if (targetPoint == null || speed <= 0f)
        {
            legDuration = 0f;
            singleRunDuration = 0f;
            loopCycleDuration = 0f;
            return;
        }

        float dist = Vector3.Distance(startPos, targetPoint.position);
        legDuration = dist / Mathf.Max(speed, 0.0001f);
        singleRunDuration = legDuration + waitTime + legDuration;
        loopCycleDuration = legDuration + waitTime + legDuration + waitTime;
    }

    private Vector3 ComputePositionFromET(double effectiveTime)
    {
        if (targetPoint == null || legDuration <= 0f)
            return startPos;

        float t = (float)effectiveTime;

        if (moveStyle == MoveStyle.AutoLoop && loopCycleDuration > 0f)
            t = Mathf.Repeat(t, loopCycleDuration);
        else
            t = Mathf.Clamp(t, 0f, singleRunDuration);

        if (t <= legDuration)
        {
            float u = Ease01(t / legDuration, easeType);
            return Vector3.LerpUnclamped(startPos, targetPoint.position, u);
        }

        if (t <= legDuration + waitTime)
            return targetPoint.position;

        float t2 = t - (legDuration + waitTime);
        if (t2 <= legDuration)
        {
            float u = Ease01(t2 / legDuration, easeType);
            return Vector3.LerpUnclamped(targetPoint.position, startPos, u);
        }

        return startPos;
    }

    private static float Ease01(float u, Ease ease)
    {
        return DOVirtual.EasedValue(0f, 1f, Mathf.Clamp01(u), ease);
    }

    private bool tryGetRb(out Rigidbody body)
    {
        body = rb != null ? rb : GetComponent<Rigidbody>();
        return body != null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (targetPoint != null)
        {
            Gizmos.DrawLine(startPos, targetPoint.position);
            Gizmos.DrawSphere(startPos, 0.06f);
            Gizmos.DrawSphere(targetPoint.position, 0.06f);
        } 
    }
#endif
}