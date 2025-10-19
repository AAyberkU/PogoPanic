using System;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// MotionClockRotate = rotator platformun kendi saati.
/// - Server-authoritative: Start/Pause/Resume/Reset sadece server yazar; NetVar'larla client'lara yayılır.
/// - Driver script (RotatorPlatform) her kare EffectiveTime (ET) okur ve hareketi LOKAL uygular.
/// - Pause sırasında ET sabit kalır (snapshot), Resume'da kaldığı yerden devam eder.
/// - Late-join: NetVar state'inden anında doğru zamana oturur.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(NetworkObject))]
[AddComponentMenu("Netcode/MotionClock (Rotate)")]
public class MotionClockRotate : NetworkBehaviour
{
    // -------- Inspector --------
    [Header("Startup")]
    [Tooltip("Server spawn olduğunda otomatik başlat.")]
    [SerializeField] private bool autoStartOnServer = true;

    [Tooltip("Saat aktifken global zaman çarpanı (server yazar). Driver içinde ayrıca scale uygulamak istersen 1 bırak.")]
    [SerializeField] private float initialTimeScale = 1f;

    // -------- NetVars (yalnız server yazar) --------
    private readonly NetworkVariable<bool>   isActiveNV =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool>   isPausedNV =
        new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // ServerTime cinsinden epoch (başlangıç anı)
    private readonly NetworkVariable<double> t0NV =
        new(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Toplam duraklama (o ana kadar biriken; CURRENT pause dahil DEĞİL)
    private readonly NetworkVariable<double> pausedAccumNV =
        new(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Pause'a girdiği andaki ET snapshot (client'ta ET'yi dondurmak için)
    private readonly NetworkVariable<double> pausedETSnapshotNV =
        new(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // İsteğe bağlı: global zaman çarpanı
    private readonly NetworkVariable<float>  timeScaleNV =
        new(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // -------- Server-only geçici alan --------
    private double pausedStartServer = 0.0;

    // -------- Public API (okunur) --------
    public bool   IsActive     => isActiveNV.Value;
    public bool   IsPaused     => isPausedNV.Value;
    public double T0           => t0NV.Value;
    public double PausedAccum  => pausedAccumNV.Value;
    public float  TimeScale    => timeScaleNV.Value;

    /// <summary> Şu anki server zamanı (double). Online değilse editor/test için Time.time kullanır. </summary>
    public static double Now
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && (nm.IsServer || nm.IsClient))
                return nm.ServerTime.Time;          // NGO'da doğru property
#if UNITY_EDITOR
            return (double)Time.time;               // Offline PlayMode kolaylığı
#else
            return 0.0;
#endif
        }
    }

    /// <summary>
    /// Driver'ların her kare okuyacağı zaman (ET).
    /// Aktif değilse 0. Paused ise snapshot sabit kalır. TimeScale uygulanmış hâli döner.
    /// </summary>
    public double EffectiveTime
    {
        get
        {
            if (!IsActive) return 0.0;
            double et = IsPaused
                ? Math.Max(0.0, pausedETSnapshotNV.Value)
                : Math.Max(0.0, Now - t0NV.Value - pausedAccumNV.Value);
            return et * Mathf.Max(0f, TimeScale);
        }
    }

    /// <summary> ET'nin scale uygulanmamış (ham) değeri gerekir ise. </summary>
    public double EffectiveTimeUnscaled
    {
        get
        {
            if (!IsActive) return 0.0;
            return IsPaused
                ? Math.Max(0.0, pausedETSnapshotNV.Value)
                : Math.Max(0.0, Now - t0NV.Value - pausedAccumNV.Value);
        }
    }

    // -------- Server API (durum değiştiren) --------

    /// <summary> Saati başlat (server). </summary>
    [ContextMenu("Server/StartMotion")]
    public void StartMotion()
    {
        if (!IsServer) return;

        isActiveNV.Value          = true;
        isPausedNV.Value          = false;
        t0NV.Value                = Now;
        pausedAccumNV.Value       = 0.0;
        pausedETSnapshotNV.Value  = 0.0;
        timeScaleNV.Value         = Mathf.Max(0f, initialTimeScale);
        pausedStartServer         = 0.0;
    }

    /// <summary> Saati duraklat (server). </summary>
    [ContextMenu("Server/Pause")]
    public void Pause()
    {
        if (!IsServer) return;
        if (!isActiveNV.Value || isPausedNV.Value) return;

        pausedStartServer          = Now;
        pausedETSnapshotNV.Value   = Math.Max(0.0, pausedStartServer - t0NV.Value - pausedAccumNV.Value);
        isPausedNV.Value           = true;
    }

    /// <summary> Saati devam ettir (server). </summary>
    [ContextMenu("Server/Resume")]
    public void Resume()
    {
        if (!IsServer) return;
        if (!isActiveNV.Value || !isPausedNV.Value) return;

        double dt = Now - pausedStartServer;
        if (dt < 0) dt = 0;
        pausedAccumNV.Value += dt;

        isPausedNV.Value    = false;
        pausedStartServer   = 0.0;
    }

    /// <summary> Tam sıfırlama (server). keepActive=false ise saat pasif olur (ET -> 0). </summary>
    [ContextMenu("Server/ResetClock")]
    public void ResetClock(bool keepActive = true)
    {
        if (!IsServer) return;

        isActiveNV.Value          = keepActive;
        isPausedNV.Value          = false;
        t0NV.Value                = Now;
        pausedAccumNV.Value       = 0.0;
        pausedETSnapshotNV.Value  = 0.0;
        pausedStartServer         = 0.0;
    }

    /// <summary> Aktifliği değiştir (server). Aktif yapılınca epoch tazelenir. </summary>
    public void SetActive(bool active)
    {
        if (!IsServer) return;

        isActiveNV.Value = active;
        if (active)
        {
            isPausedNV.Value          = false;
            t0NV.Value                = Now;
            pausedAccumNV.Value       = 0.0;
            pausedETSnapshotNV.Value  = 0.0;
            pausedStartServer         = 0.0;
        }
    }

    /// <summary> Global zaman çarpanı (server). 0 → akış dursa da pause değildir. </summary>
    public void SetTimeScale(float scale)
    {
        if (!IsServer) return;
        timeScaleNV.Value = Mathf.Max(0f, scale);
    }

    // -------- Client convenience (server'a istek iletmek istersen) --------

    public void RequestStart()  { StartMotionServerRpc(); }
    public void RequestPause()  { PauseServerRpc(); }
    public void RequestResume() { ResumeServerRpc(); }
    public void RequestReset(bool keepActive = true) { ResetClockServerRpc(keepActive); }
    public void RequestSetTimeScale(float s) { SetTimeScaleServerRpc(s); }

    [ServerRpc(RequireOwnership = false)] private void StartMotionServerRpc()        => StartMotion();
    [ServerRpc(RequireOwnership = false)] private void PauseServerRpc()              => Pause();
    [ServerRpc(RequireOwnership = false)] private void ResumeServerRpc()             => Resume();
    [ServerRpc(RequireOwnership = false)] private void ResetClockServerRpc(bool k)   => ResetClock(k);
    [ServerRpc(RequireOwnership = false)] private void SetTimeScaleServerRpc(float s)=> SetTimeScale(s);

    // -------- Lifecycle --------

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Auto-start yalnız server'da uygulanır
        if (IsServer && autoStartOnServer)
        {
            if (!isActiveNV.Value) // yeniden spawn'da iki kez çağrılmasın
            {
                timeScaleNV.Value = Mathf.Max(0f, initialTimeScale);
                StartMotion();
            }
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.45f,
            $"[MotionClockRotate]\nActive:{IsActive}  Paused:{IsPaused}\nT0:{T0:F2}  Acc:{PausedAccum:F2}\nET:{EffectiveTime:F2}  x{TimeScale:F2}");
    }
#endif
}
