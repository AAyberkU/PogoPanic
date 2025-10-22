using UnityEngine;
using Unity.Netcode;
using Unity.Collections;   // FixedString64Bytes
using TMPro;
using Steamworks;          // Steamworks.NET

[DisallowMultipleComponent]
public class NameTag : NetworkBehaviour
{
    [Header("Refs")]
    [SerializeField] private Canvas worldCanvas;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Transform headAnchor;
    [SerializeField] private Camera mainCamOverride;

    [Header("Behaviour")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.4f, 0f);
    [SerializeField] private bool faceCamera = true;
    [SerializeField] private bool hideForLocalPlayer = true;
    [SerializeField] private string fallbackName = "Player";

    [Header("Distance Fade (optional)")]
    [SerializeField] private bool enableDistanceFade = true;
    [SerializeField] private float visibleDistance = 30f;
    [SerializeField] private float minScale = 0.75f;
    [SerializeField] private float maxScale = 1.25f;

    // Everyone read, only Owner write
    private readonly NetworkVariable<FixedString64Bytes> _playerName =
        new NetworkVariable<FixedString64Bytes>(
            readPerm: NetworkVariableReadPermission.Everyone,
            writePerm: NetworkVariableWritePermission.Owner);

    private Transform _camT;
    private Transform _targetT;
    private bool _initialized;
    private float _nextCamSearchTime; // lazy kamera bulma için

    // ──────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (!worldCanvas) worldCanvas = GetComponentInChildren<Canvas>(true);
        if (!nameText)    nameText    = GetComponentInChildren<TextMeshProUGUI>(true);
        _targetT = headAnchor ? headAnchor : transform;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // İlk deneme: o anda varsa al
        var cam = FindBestCamera();
        _camT = cam ? cam.transform : null;

        // Local gizleme sadece owner için
        if (IsOwner && hideForLocalPlayer && worldCanvas)
            worldCanvas.enabled = false;

        // UI güncelleme aboneliği
        _playerName.OnValueChanged += OnNameChanged;

        // Sadece owner bir kere set eder → tüm peer’lara yayılır
        if (IsOwner)
        {
            string steamName = TryGetSteamPersonaName();
            _playerName.Value = !string.IsNullOrWhiteSpace(steamName) ? steamName : fallbackName;
        }

        // Spawn anında UI senkronla
        OnNameChanged(default, _playerName.Value);
        _initialized = true;
    }

    public override void OnNetworkDespawn()
    {
        _playerName.OnValueChanged -= OnNameChanged;
        base.OnNetworkDespawn();
    }

    private void LateUpdate()
    {
        if (!_initialized || !worldCanvas) return;

        // Kamera yoksa periyodik tekrar dene (Cinemachine/scene transition sonrası)
        EnsureCamera();

        if (IsOwner && hideForLocalPlayer && !worldCanvas.enabled) return;

        // Pozisyon + offset
        var basePos = _targetT ? _targetT.position : transform.position;
        worldCanvas.transform.position = basePos + worldOffset;

        // Kameraya bak (billboard)
        if (faceCamera && _camT)
        {
            var dir = worldCanvas.transform.position - _camT.position;
            if (dir.sqrMagnitude > 0.0001f)
                worldCanvas.transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
        }

        // Mesafe bazlı görünürlük/ölçek
        if (enableDistanceFade && _camT)
        {
            float d = Vector3.Distance(_camT.position, worldCanvas.transform.position);
            bool visible = d <= visibleDistance;
            if (nameText) nameText.alpha = visible ? 1f : 0f;

            float t = Mathf.InverseLerp(visibleDistance, 0f, d); // yakına geldikçe 1
            float scale = Mathf.Lerp(minScale, maxScale, t);
            worldCanvas.transform.localScale = Vector3.one * scale;
        }
    }

    private void OnNameChanged(FixedString64Bytes oldName, FixedString64Bytes newName)
    {
        if (nameText)
            nameText.text = newName.IsEmpty ? fallbackName : newName.ToString();
    }

    // ──────────────────────────────────────────────────────────────
    // Kamera yardımcıları

    private void EnsureCamera()
    {
        if (_camT) return;

        // Performansı korumak için yarım saniyede bir ara
        if (Time.unscaledTime < _nextCamSearchTime) return;

        var cam = FindBestCamera();
        if (cam) _camT = cam.transform;

        _nextCamSearchTime = Time.unscaledTime + 0.5f;
    }

    private Camera FindBestCamera()
    {
        if (mainCamOverride) return mainCamOverride;

        // Önce Main Camera tag’li olan
        var cam = Camera.main;
        if (cam && cam.isActiveAndEnabled) return cam;

        // Değilse aktif herhangi bir kamera
        var all = Camera.allCameras;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] && all[i].isActiveAndEnabled) return all[i];
        }

        return null;
    }

    // ──────────────────────────────────────────────────────────────
    // Steam adı

    private string TryGetSteamPersonaName()
    {
        try
        {
            if (SteamManager.Initialized)
                return SteamFriends.GetPersonaName();
        }
        catch { /* ignore */ }
        return null;
    }
}
