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

        // Kamera
        _camT = mainCamOverride ? mainCamOverride.transform : (Camera.main ? Camera.main.transform : null);

        // Local gizleme
        if (IsOwner && hideForLocalPlayer && worldCanvas)
            worldCanvas.enabled = false;

        // UI güncelleme aboneliği
        _playerName.OnValueChanged += OnNameChanged;

        // Sadece owner kendi ismini yazar → tüm client'lara yayılır
        if (IsOwner)
        {
            string steamName = TryGetSteamPersonaName();
            _playerName.Value = !string.IsNullOrWhiteSpace(steamName) ? steamName : fallbackName;
        }

        // Spawn anında da güncelle
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
        if (IsOwner && hideForLocalPlayer && !worldCanvas.enabled) return;

        // Pozisyon + offset
        var basePos = _targetT ? _targetT.position : transform.position;
        worldCanvas.transform.position = basePos + worldOffset;

        // Kameraya bak
        if (faceCamera && _camT)
        {
            var dir = worldCanvas.transform.position - _camT.position;
            if (dir.sqrMagnitude > 0.0001f)
                worldCanvas.transform.rotation = Quaternion.LookRotation(dir);
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
