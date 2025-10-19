using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
[RequireComponent(typeof(MotionClockRotate), typeof(Rigidbody), typeof(NetworkObject))]
public class RotatorPlatform : NetworkBehaviour
{
    public enum Axis { X, Y, Z }

    #region Inspector
    [Header("Rotation Mode")]
    [SerializeField] private bool  useFixedRotation   = false;   // false: continuous, true: fixed ping-pong
    [SerializeField] private Axis  axis               = Axis.Y;
    [SerializeField] private float speedDegPerSec     = 60f;

    [Header("Fixed Ping-Pong")]
    [SerializeField] private float rotationAmountDeg  = 90f;     // ileri hedef açı (işaret yönü belirler)
    [SerializeField] private float waitAtStartSec     = 1f;
    [SerializeField] private float waitAtTargetSec    = 1f;

    [Header("Player Detection (Collision-based)")]
    [SerializeField] private string playerTag         = "Player";
    [SerializeField, Tooltip("Üstten temas filtresi için eşik. 0.7–0.85 aralığı iyi.")]
    private float topDotThreshold = 0.70f; // Dot(avgNormal, transform.up) > threshold => üstte

    [Header("Behaviour Toggles")]
    [SerializeField, Tooltip("Üstünde oyuncu varken dur.")]
    private bool pauseWhenPlayerOnTop      = false;

    [SerializeField, Tooltip("Yalnız üstünde oyuncu varken dön.")]
    private bool onlyRotateWhenPlayerOnTop = false;

    [Header("Stability")]
    [SerializeField, Tooltip("Kısa süreli temas kopmalarını yok saymak için tolerans (s).")]
    private float coyoteTime = 0.15f;
    
    [Header("Reset Settings (opsiyonel)")]
    [SerializeField] private bool  resetWhenEmptyAndFar = false;
    [SerializeField] private float resetDistance        = 5f;
    [SerializeField] private float distanceCheckPeriod  = 0.1f;
    #endregion

    // Components
    private Rigidbody           rb;
    private MotionClockRotate   rotateClock;

    // Base orientation & axis
    private Quaternion  baseRotation;
    private Vector3     worldAxis;

    // SERVER: rider set (clientId)
    private readonly HashSet<ulong> serverRiders = new();

    // Coyote-time takibi
    private readonly Dictionary<ulong, float>    lastTopTouchTime = new();
    private readonly Dictionary<ulong, Coroutine> removeRoutines  = new();

    private Coroutine resetRoutine;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rotateClock = GetComponent<MotionClockRotate>();

        rb.isKinematic   = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false; // root’ta collider varsa solid; yoksa sorun yok

        baseRotation = transform.rotation;
        worldAxis = axis == Axis.X ? Vector3.right :
                    axis == Axis.Y ? Vector3.up    : Vector3.forward;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            // “Yalnız üstteyken dön” modunda saat başlangıçta kapalı dursun.
            if (onlyRotateWhenPlayerOnTop)
                rotateClock.SetActive(false);
            else if (!rotateClock.IsActive)
                rotateClock.StartMotion();
        }
    }

    private void FixedUpdate()
    {
        // 1) HAREKETİ LOKALDE UYGULA (tüm client'lar + host)
        ApplyLocalRotationFromClock();

        // 2) Sticky/local offset kompanzasyonu KALDIRILDI (sürtünme ile taşınma denemesi)
    }

    // ===================== Local Motion (Clock-driven) =====================

    private void ApplyLocalRotationFromClock()
    {
        // Saat kapalıysa başlangıç pozunda tut
        if (!rotateClock.IsActive)
        {
            rb.MoveRotation(baseRotation);
            return;
        }

        double t = rotateClock.EffectiveTime; // pause iken sabit kalır

        float angle;
        if (!useFixedRotation)
        {
            // Continuous: açı = hız * t
            angle = speedDegPerSec * (float)t;
        }
        else
        {
            // Fixed ping-pong: ET tabanlı deterministik hesap
            float absSpeed = Mathf.Max(0.0001f, Mathf.Abs(speedDegPerSec));
            float absAngle = Mathf.Abs(rotationAmountDeg);
            float rotDur   = absAngle / absSpeed; // ileri/geri süre
            float period   = waitAtStartSec + rotDur + waitAtTargetSec + rotDur;

            float localT   = (float)(t % Mathf.Max(0.0001f, period));
            float sign     = Mathf.Sign(rotationAmountDeg);
            angle = 0f;

            float t0 = waitAtStartSec;
            float t1 = t0 + rotDur;
            float t2 = t1 + waitAtTargetSec;
            float t3 = t2 + rotDur; // = period

            if (localT < t0)
            {
                angle = 0f; // start bekleme
            }
            else if (localT < t1)
            {
                float f = (localT - t0) / rotDur; // 0→1
                angle = sign * Mathf.Lerp(0f, absAngle, f);
            }
            else if (localT < t2)
            {
                angle = sign * absAngle; // target bekleme
            }
            else // [t2, t3)
            {
                float f = (localT - t2) / rotDur; // 0→1
                angle = sign * Mathf.Lerp(absAngle, 0f, f);
            }
        }

        // World-axis etrafında baseRotation'dan hesaplanan hedef oryantasyon
        Quaternion target = Quaternion.AngleAxis(angle, worldAxis) * baseRotation;
        rb.MoveRotation(target);
    }

    // ===================== Collision / Rider Tracking =====================

    private void OnCollisionEnter(Collision c)
    {
        var rider = GetRootRigidbody(c.collider);
        if (!rider || !rider.CompareTag(playerTag)) return;

        // “Üstten mi?” filtresi (avg normal)
        Vector3 avgNormal = Vector3.zero;
        for (int i = 0; i < c.contactCount; i++) avgNormal += c.GetContact(i).normal;
        avgNormal /= Mathf.Max(1, c.contactCount);

        // (SENİN ORİJİNALİNDEKİ KARŞILAŞTIRMA — DEĞİŞTİRMEDİM)
        if (Vector3.Dot(avgNormal, transform.up) < topDotThreshold)
        {
            if (IsServer)
            {
                var noS = rider.GetComponentInParent<NetworkObject>();
                if (noS != null)
                {
                    ulong id = noS.OwnerClientId;
                    serverRiders.Add(id);
                    lastTopTouchTime[id] = Time.time;
                    CancelCoyoteRemove(id);

                    if (onlyRotateWhenPlayerOnTop && !rotateClock.IsActive)
                        rotateClock.StartMotion();

                    UpdatePauseStateServer();
                    CancelResetIfRunning();
                }
            }
            else
            {
                var noC = rider.GetComponentInParent<NetworkObject>();
                if (noC != null && noC.IsOwner)
                    NotifyLatchServerRpc(true);
            }
        }
    }

    // <<<< EKLENDİ: Her kare üsttemas kontrolü (avg normal ile, aynen Enter’daki gibi) >>>>
    private void OnCollisionStay(Collision c)
    {
        var rider = GetRootRigidbody(c.collider);
        if (!rider || !rider.CompareTag(playerTag)) return;

        // “Üstten mi?” filtresi (avg normal) — mantık aynı, değişmedi
        Vector3 avgNormal = Vector3.zero;
        for (int i = 0; i < c.contactCount; i++) avgNormal += c.GetContact(i).normal;
        avgNormal /= Mathf.Max(1, c.contactCount);

        if (Vector3.Dot(avgNormal, transform.up) < topDotThreshold)
        {
            if (IsServer)
            {
                var noS = rider.GetComponentInParent<NetworkObject>();
                if (noS == null) return;

                ulong id = noS.OwnerClientId;
                serverRiders.Add(id);
                lastTopTouchTime[id] = Time.time;
                CancelCoyoteRemove(id);

                if (onlyRotateWhenPlayerOnTop && !rotateClock.IsActive)
                    rotateClock.StartMotion();

                // Pause state’i sık güncellemek zararsız
                UpdatePauseStateServer();
            }
            else
            {
                var noC = rider.GetComponentInParent<NetworkObject>();
                if (noC != null && noC.IsOwner)
                    NotifyLatchServerRpc(true);
            }
        }
    }

    private void OnCollisionExit(Collision c)
    {
        var rider = GetRootRigidbody(c.collider);

        if (IsServer)
        {
            var noS = rider ? rider.GetComponentInParent<NetworkObject>() : null;
            if (noS != null)
            {
                ulong id = noS.OwnerClientId;

                // HEMEN SİLME — COYOTE TIME BEKLE
                lastTopTouchTime[id] = Time.time;
                StartCoyoteRemove(id);

                // Pause kararını şimdilik bozma; coyote süresini bekle
            }
        }
        else
        {
            var noC = rider ? rider.GetComponentInParent<NetworkObject>() : null;
            if (noC != null && noC.IsOwner)
                NotifyLatchServerRpc(false);
        }
    }

    public void ForceRemoveRider(Rigidbody rider)
    {
        var no = rider ? rider.GetComponentInParent<NetworkObject>() : null;

        if (IsServer)
        {
            if (no != null) 
            {
                ulong id = no.OwnerClientId;
                serverRiders.Remove(id);
                lastTopTouchTime.Remove(id);
                CancelCoyoteRemove(id);
            }
            UpdatePauseStateServer();
            TryScheduleReset();
        }
        else
        {
            if (no != null && no.IsOwner)
                NotifyLatchServerRpc(false);
        }
    }

    // Owner client bildirir; server rider set tutar
    [ServerRpc(RequireOwnership = false)]
    private void NotifyLatchServerRpc(bool latched, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (latched)
        {
            serverRiders.Add(sender);
            lastTopTouchTime[sender] = Time.time;
            CancelCoyoteRemove(sender);

            if (onlyRotateWhenPlayerOnTop && !rotateClock.IsActive)
                rotateClock.StartMotion();

            UpdatePauseStateServer();
            CancelResetIfRunning();
        }
        else
        {
            // Hemen silme — coyote süresi kadar beklemek için zaman damgası bırak
            lastTopTouchTime[sender] = Time.time;
            StartCoyoteRemove(sender);
        }
    }

    // ===================== Pause/Resume & Reset =====================

    private void UpdatePauseStateServer()
    {
        if (!IsServer) return;

        bool hasRider = serverRiders.Count > 0;
        bool shouldPause =
            (pauseWhenPlayerOnTop      && hasRider) ||
            (onlyRotateWhenPlayerOnTop && !hasRider);

        if (!rotateClock.IsActive && !onlyRotateWhenPlayerOnTop)
            rotateClock.StartMotion();

        if (shouldPause && !rotateClock.IsPaused)      rotateClock.Pause();
        else if (!shouldPause && rotateClock.IsPaused) rotateClock.Resume();
    }

    private void TryScheduleReset()
    {
        if (!IsServer || !resetWhenEmptyAndFar) return;

        if (serverRiders.Count == 0 && resetRoutine == null)
            resetRoutine = StartCoroutine(ResetWhenPlayersFar());
    }

    private IEnumerator ResetWhenPlayersFar()
    {
        while (!AllPlayersBeyondDistance())
            yield return new WaitForSeconds(distanceCheckPeriod);

        // Clock'u sıfırla ve base oryantasyona çek
        rotateClock.ResetClock(keepActive: !onlyRotateWhenPlayerOnTop);
        rb.MoveRotation(baseRotation);

        resetRoutine = null;
    }

    private bool AllPlayersBeyondDistance()
    {
        if (!IsServer) return true;

        var players = GameObject.FindGameObjectsWithTag(playerTag);
        foreach (var p in players)
        {
            if (Vector3.Distance(p.transform.position, transform.position) < resetDistance)
                return false;
        }
        return true;
    }

    private void CancelResetIfRunning()
    {
        if (resetRoutine != null)
        {
            StopCoroutine(resetRoutine);
            resetRoutine = null;
        }
    }

    // ===================== Helpers =====================

    private Rigidbody GetRootRigidbody(Collider col)
    {
        return col.attachedRigidbody
            ? col.attachedRigidbody
            : col.GetComponentInParent<Rigidbody>();
    }

    private void StartCoyoteRemove(ulong id)
    {
        CancelCoyoteRemove(id);
        removeRoutines[id] = StartCoroutine(CoyoteRemoveRoutine(id));
    }

    private void CancelCoyoteRemove(ulong id)
    {
        if (removeRoutines.TryGetValue(id, out var co) && co != null)
        {
            StopCoroutine(co);
            removeRoutines.Remove(id);
        }
    }


    private IEnumerator CoyoteRemoveRoutine(ulong id)
    {
        // Mantık: lastTopTouchTime[id] güncellendikçe beklemeyi uzat.
    // lastTopTouchTime üzerinden geçen süre coyoteTime'ı aşana kadar bekle,
    // aştığı an rider'ı düşür.
        while (true)
        {
            // Kayıt yoksa zaten düşürülmüştür/çıkılmıştır
            if (!lastTopTouchTime.TryGetValue(id, out float tLast))
                yield break;

            // Yeterince uzun süredir temas yok mu?
            if (Time.time - tLast >= coyoteTime)
                break; // artık düşür

            yield return null; // bir sonraki frame'e kadar bekle
        }

        serverRiders.Remove(id);
        lastTopTouchTime.Remove(id);
        removeRoutines.Remove(id);

        UpdatePauseStateServer();
        TryScheduleReset(); 
    }

}