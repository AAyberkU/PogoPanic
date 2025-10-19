using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using DG.Tweening; // sadece DOVirtual.EasedValue için

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

    // ---- internals ----
    private Rigidbody      rb;
    private MotionClockMove clock;

    private Vector3 startPos;
    private Vector3 lastPlatformPos;

    private float legDuration;
    private float singleRunDuration;
    private float loopCycleDuration;

    private readonly Dictionary<Rigidbody, Vector3> riderLocalPos = new();

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
                // Gate kapalı başlasın
                allowMoveNV.Value = false;
                rb.MovePosition(startPos);
                lastPlatformPos = startPos;
            }
            else if (moveStyle == MoveStyle.AutoLoop && !clock.IsActive)
            {
                clock.SetTimeScale(1f);
                clock.StartMotion();
                allowMoveNV.Value = true; // AutoLoop açık
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
        // --- SERVER: Triggered koşusu bittiyse gate'i kapat ve clock'u durdur ---
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

        // --- Pozisyonu uygula ---
        Vector3 newPos;
        if (moveStyle == MoveStyle.Triggered && !allowMoveNV.Value)
        {
            newPos = startPos; // saat çalışsa da gate kapalıyken hareket etme
        }
        else
        {
            newPos = ComputePositionFromET(clock.EffectiveTime);
        }

        rb.MovePosition(newPos);

        // --- Rider taşıma ---
        float deltaY = newPos.y - lastPlatformPos.y;

        foreach (var kvp in riderLocalPos)
        {
            var rider = kvp.Key;
            if (rider == null) continue;

            Vector3 worldOffset = transform.TransformPoint(kvp.Value);

            float desiredY = (deltaY < 0f)
                ? Mathf.Max(rider.position.y, worldOffset.y)
                : rider.position.y;

            Vector3 riderTarget = new Vector3(worldOffset.x, desiredY, worldOffset.z);
            rider.MovePosition(riderTarget);
        }

        lastPlatformPos = newPos;
    }

    void OnCollisionEnter(Collision col)
    {
        TryHandleRiderAndTrigger(col);
    }

    void OnCollisionStay(Collision col)
    {
        // Bazen Enter frame'inde hız/normal koşulları kaçabilir; Stay'de de dene.
        TryHandleRiderAndTrigger(col);
    }

    private void TryHandleRiderAndTrigger(Collision col)
    {
        var riderRb = col.collider.attachedRigidbody;
        if (riderRb == null) return;
        if (!riderRb.CompareTag(playerTag)) return;

        // local offset’i bir kere kaydet
        if (!riderLocalPos.ContainsKey(riderRb))
            riderLocalPos[riderRb] = transform.InverseTransformPoint(riderRb.position);

        if (moveStyle == MoveStyle.Triggered && !allowMoveNV.Value)
        {
            if (IsTopContact(col, riderRb))
            {
#if UNITY_EDITOR
                Debug.Log($"[Platform:{name}] Top contact detected -> request trigger");
#endif
                RequestTriggerServerRpc();
            }
        }
    }

    // ---- daha sağlam üstten iniş tespiti ----
    private bool IsTopContact(Collision col, Rigidbody rider)
    {
        // 1) Rider yukarıda mı?
        bool above = rider.worldCenterOfMass.y >= (transform.position.y + 0.05f);

        // 2) İniş hızı var mı?
        bool falling = col.relativeVelocity.y < -0.05f;

        // 3) En az bir contact normal yukarı bakıyor mu?
        bool normalUp = false;
        foreach (var c in col.contacts)
        {
            if (Vector3.Dot(c.normal, Vector3.up) > 0.25f) // toleransı genişlettik
            {
                normalUp = true;
                break;
            }
        }

#if UNITY_EDITOR
        if (!(above && (falling || normalUp)))
        {
            // Gözlemlemek istersen uncomment et
            // Debug.Log($"[Platform:{name}] above:{above} falling:{falling} normalUp:{normalUp}");
        }
#endif

        return above && (falling || normalUp);
    }

    void OnCollisionExit(Collision col)
    {
        var riderRb = col.collider.attachedRigidbody;
        if (riderRb != null)
            riderLocalPos.Remove(riderRb);
    }

    // --------- Trigger akışı (server-authoritative) ---------
    [ServerRpc(RequireOwnership = false)]
    private void RequestTriggerServerRpc(ServerRpcParams rpc = default)
    {
        if (moveStyle != MoveStyle.Triggered) return;

        allowMoveNV.Value = true;
        clock.ResetClock(true); // aktif + ET=0
        clock.StartMotion();

#if UNITY_EDITOR
        Debug.Log($"[Platform:{name}] TRIGGERED by client -> gate ON, clock START");
#endif
    }

    // ----------------- Yardımcılar -----------------
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
