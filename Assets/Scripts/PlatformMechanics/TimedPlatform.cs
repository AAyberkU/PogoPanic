using System.Collections;
using UnityEngine;
using Unity.Netcode;

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
public class TimedPlatform : NetworkBehaviour
{
    public enum TimedPlatformType { Triggered, Interval }
    private enum Phase { Open, Warning, Closed }

    [Header("General")]
    public TimedPlatformType platformType = TimedPlatformType.Triggered;

    [Header("References (auto-assign if blank)")]
    public Collider[] platformColliders;
    public Renderer[] platformRenderers;

    [Header("Triggered Mode")]
    public float delayBeforeClose = 2f;
    public float disabledDuration = 2f;
    public float blinkInterval = 0.2f;
    public float minBlinkInterval = 0.05f;

    [Header("Interval Mode")]
    public float initialDelay = 0f;
    public float openDuration = 2f;
    public float closeDuration = 2f;

    [Header("Filtering")]
    public string playerTag = "Player";

    // --- Net state ---
    private readonly NetworkVariable<Phase> _phase =
        new NetworkVariable<Phase>(Phase.Open, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<double> _blinkStartServerTime =
        new NetworkVariable<double>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // basit anti-spam/cooldown
    private double _lastTriggerServerTime;
    [SerializeField] private double triggerCooldownSec = 0.15;

    // local
    private Coroutine _serverRoutine;
    private Coroutine _clientBlinkRoutine;
    private bool _isProcessingTriggered;
    
    private Rigidbody      rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.isKinematic   = true;
        rb.interpolation = RigidbodyInterpolation.None;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        
        if (platformColliders == null || platformColliders.Length == 0)
            platformColliders = GetComponentsInChildren<Collider>(includeInactive: true);
        if (platformRenderers == null || platformRenderers.Length == 0)
            platformRenderers = GetComponentsInChildren<Renderer>(includeInactive: true);
    }

    public override void OnNetworkSpawn()
    {
        ApplyPhaseLocally(_phase.Value);
        _phase.OnValueChanged += OnPhaseChanged;

        if (IsServer && platformType == TimedPlatformType.Interval)
            _serverRoutine = StartCoroutine(ServerIntervalLoop());
    }

    public override void OnNetworkDespawn()
    {
        _phase.OnValueChanged -= OnPhaseChanged;
        if (_serverRoutine != null) StopCoroutine(_serverRoutine);
        if (_clientBlinkRoutine != null) StopCoroutine(_clientBlinkRoutine);
    }

    // -------------------- TEMAS ALGILAMA --------------------
    // Not: Client-auth hareket varsa server her zaman çarpışmayı görmeyebilir.
    // Bu yüzden client'ta algılayıp ServerRpc ile istek yolluyoruz.
    private void OnCollisionEnter(Collision col)
    {
        if (platformType != TimedPlatformType.Triggered)
            return;

        if (!col.gameObject.CompareTag(playerTag))
            return;

        // "üstten iniş" kontrolü (hem server hem client için aynı eşik)
        bool landedFromAbove = false;
        foreach (var c in col.contacts)
        {
            if (Vector3.Dot(c.normal, Vector3.up) < 0.5f)
            {
                landedFromAbove = true;
                break;
            }
        }
        if (!landedFromAbove) return;

        if (IsServer)
        {
            TryStartTriggeredServer();
        }
        else if (IsClient) // client'ta algılandı -> server'a iste
        {
            RequestTriggerServerRpc();
        }
    }

    // İstersen daha güvenli olsun diye, client tarafında OnCollisionStay de ekleyebilirsin
    // ben gerek görmedim.

    // -------------------- SERVER TARAFI AKIŞ --------------------
    private void TryStartTriggeredServer()
    {
        if (_isProcessingTriggered) return;

        double now = NetworkManager.ServerTime.Time;
        if (now - _lastTriggerServerTime < triggerCooldownSec)
            return;

        _lastTriggerServerTime = now;
        _isProcessingTriggered = true;

        if (_serverRoutine != null) StopCoroutine(_serverRoutine);
        _serverRoutine = StartCoroutine(ServerTriggeredRoutine());
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestTriggerServerRpc(ServerRpcParams rpcParams = default)
    {
        // İsteği alan yine server; tek yerden karar veriyoruz
        TryStartTriggeredServer();
    }

    private IEnumerator ServerTriggeredRoutine()
    {
        SetPhase(Phase.Warning);
        _blinkStartServerTime.Value = NetworkManager.ServerTime.Time;

        yield return new WaitForSeconds(delayBeforeClose);

        SetPhase(Phase.Closed);
        SetCollidersAndRenderers(false);

        yield return new WaitForSeconds(disabledDuration);

        SetPhase(Phase.Open);
        SetCollidersAndRenderers(true);

        _isProcessingTriggered = false;
    }

    private IEnumerator ServerIntervalLoop()
    {
        if (initialDelay > 0f)
            yield return new WaitForSeconds(initialDelay);

        while (true)
        {
            SetPhase(Phase.Open);
            SetCollidersAndRenderers(true);
            yield return new WaitForSeconds(openDuration);

            SetPhase(Phase.Closed);
            SetCollidersAndRenderers(false);
            yield return new WaitForSeconds(closeDuration);
        }
    }

    private void SetPhase(Phase p)
    {
        if (_phase.Value == p) return;
        _phase.Value = p;
    }

    // -------------------- CLIENT GÖRSEL UYGULAMA --------------------
    private void OnPhaseChanged(Phase prev, Phase next) => ApplyPhaseLocally(next);

    private void ApplyPhaseLocally(Phase p)
    {
        if (_clientBlinkRoutine != null) { StopCoroutine(_clientBlinkRoutine); _clientBlinkRoutine = null; }

        switch (p)
        {
            case Phase.Open:
                SetColliders(true);
                SetRenderers(true);
                break;

            case Phase.Warning:
                SetColliders(true);
                _clientBlinkRoutine = StartCoroutine(ClientBlinkWarning());
                break;

            case Phase.Closed:
                SetColliders(false);
                SetRenderers(false);
                break;
        }
    }

    private IEnumerator ClientBlinkWarning()
    {
        while (_phase.Value == Phase.Warning)
        {
            double serverNow = NetworkManager.Singleton.ServerTime.Time;
            float elapsed = (float)(serverNow - _blinkStartServerTime.Value);
            float t = Mathf.Clamp01(elapsed / Mathf.Max(0.0001f, delayBeforeClose));
            float currentInterval = Mathf.Lerp(blinkInterval, minBlinkInterval, t);

            ToggleRenderers();
            yield return new WaitForSeconds(currentInterval);
        }
    }

    // -------------------- Yardımcılar --------------------
    private void SetCollidersAndRenderers(bool on) { SetColliders(on); SetRenderers(on); }

    private void SetColliders(bool on)
    {
        for (int i = 0; i < platformColliders.Length; i++)
            if (platformColliders[i]) platformColliders[i].enabled = on;
    }

    private void SetRenderers(bool on)
    {
        for (int i = 0; i < platformRenderers.Length; i++)
            if (platformRenderers[i]) platformRenderers[i].enabled = on;
    }

    private void ToggleRenderers()
    {
        if (platformRenderers == null || platformRenderers.Length == 0) return;
        bool next = !platformRenderers[0].enabled;
        for (int i = 0; i < platformRenderers.Length; i++)
            if (platformRenderers[i]) platformRenderers[i].enabled = next;
    }
}
