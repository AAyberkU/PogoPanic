using UnityEngine;
using Unity.Netcode;
using Steamworks;
using Netcode.Transports;                 // SteamNetworkingSocketsTransport
using UnityEngine.SceneManagement;        // sadece LoadSceneMode enum'u için

/// <summary>
/// Plan A:
/// - Şu anki akış: Menü → (Unity SceneManager ile) FlippedDemo yüklenir → bu sınıftan HostWithLobbyOnly() çağrılır.
/// - HostWithLobbyOnly(): Friends-Only Lobby kurar, host'u başlatır; sahne zaten aktif olduğu için load etmez.
/// - Steam Overlay "Join Game": client lobby'e girer → host ID'yi LobbyData'dan alır → transport.ConnectToSteamID → StartClient.
/// Not: İleride ihtiyaç olursa PlayHostWithLobbyAndLoad(scene) ile "önce host, sonra networked scene load" da yapılabilir.
/// </summary>
public class SteamNGOBootstrap : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────────────
    // Singleton + DDOL
    public static SteamNGOBootstrap Instance { get; private set; }

    [Header("Default Lobby Settings")]
    [SerializeField] private ELobbyType defaultLobbyType = ELobbyType.k_ELobbyTypeFriendsOnly;
    [SerializeField] private int        defaultMaxMembers = 4;

    [Header("Optional")]
    [Tooltip("Host oluşturulunca Steam'in Invite penceresini otomatik aç.")]
    [SerializeField] private bool openInviteOverlayOnHost = false;

    private Callback<GameLobbyJoinRequested_t> cbJoinRequested;
    private Callback<LobbyCreated_t>           cbLobbyCreated;
    private Callback<LobbyEnter_t>             cbLobbyEnter;

    private CSteamID currentLobby;
    public CSteamID CurrentLobbyID => currentLobby;

    private string   pendingSceneToLoad;   // null ise sahne yüklenmez (mevcut akış)
    private bool     isStartingFlow;       // double-click guard
    

    private SteamNetworkingSocketsTransport Transport =>
        (SteamNetworkingSocketsTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

    // ─────────────────────────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!SteamManager.Initialized)
        {
            Debug.LogError("[Bootstrap] Steam not initialized! Put SteamManager in scene (above NetworkManager).");
            return;
        }

        try { SteamNetworkingUtils.InitRelayNetworkAccess(); } catch { }
    }

    private void OnEnable()
    {
        cbJoinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
        cbLobbyCreated  = Callback<LobbyCreated_t>.Create(OnLobbyCreated);
        cbLobbyEnter    = Callback<LobbyEnter_t>.Create(OnLobbyEnter);
    }

    private void OnDisable()
    {
        cbJoinRequested?.Dispose(); cbJoinRequested = null;
        cbLobbyCreated ?.Dispose(); cbLobbyCreated  = null;
        cbLobbyEnter   ?.Dispose(); cbLobbyEnter    = null;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // PUBLIC API — MainMenuUI.Play() sonrası kullanılacak
    // ─────────────────────────────────────────────────────────────────────────
    /// <summary>
    /// Mevcut akış: Sahne zaten yüklü → sadece lobby kur + host başlat.
    /// </summary>
    public void HostWithLobbyOnly(ELobbyType? lobbyType = null, int? maxMembers = null)
    {
        if (isStartingFlow) return;
        isStartingFlow = true;

        if (!CheckPrereqs()) { isStartingFlow = false; return; }

        // YENİ: yeni lobby kurmadan önce eski tur state'ini sıfırla
        ResetLobbyState();

        pendingSceneToLoad = null; // bu akışta sahne yüklenmeyecek

        var type = lobbyType ?? defaultLobbyType;
        var cap  = Mathf.Max(1, maxMembers ?? defaultMaxMembers);
        SteamMatchmaking.CreateLobby(type, cap);

        Debug.Log($"[Bootstrap] Creating lobby (no scene load)... type={type}, cap={cap}");
    }

    /// <summary>
    /// Alternatif akış (ileride gerekirse): Lobby kur + host başlat + NGO SceneManager ile sahne yükle.
    /// </summary>
    public void PlayHostWithLobbyAndLoad(string sceneName, ELobbyType? lobbyType = null, int? maxMembers = null)
    {
        if (isStartingFlow) return;
        isStartingFlow = true;

        if (!CheckPrereqs()) { isStartingFlow = false; return; }

        // YENİ: yeni lobby kurmadan önce eski tur state'ini sıfırla
        ResetLobbyState();

        pendingSceneToLoad = sceneName;

        var type = lobbyType ?? defaultLobbyType;
        var cap  = Mathf.Max(1, maxMembers ?? defaultMaxMembers);
        SteamMatchmaking.CreateLobby(type, cap);

        Debug.Log($"[Bootstrap] Creating lobby... type={type}, cap={cap}, nextScene='{pendingSceneToLoad}'");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Steam CALLBACKS
    // ─────────────────────────────────────────────────────────────────────────
    private void OnLobbyCreated(LobbyCreated_t e)
    {
        if (e.m_eResult != EResult.k_EResultOK)
        {
            Debug.LogError("[Bootstrap] Lobby create failed: " + e.m_eResult);
            isStartingFlow = false;
            return;
        }

        currentLobby = new CSteamID(e.m_ulSteamIDLobby);

        var ownerIdUlong = SteamUser.GetSteamID().m_SteamID;
        SteamMatchmaking.SetLobbyData(currentLobby, "host", ownerIdUlong.ToString());

        Debug.Log($"[Bootstrap] Lobby created: {currentLobby.m_SteamID}, owner={ownerIdUlong}");

        // Host'u başlat
        NetworkManager.Singleton.StartHost();
        Debug.Log("[Bootstrap] NGO Host started.");

        if (openInviteOverlayOnHost)
            SteamFriends.ActivateGameOverlayInviteDialog(currentLobby);

        // Eğer pendingSceneToLoad set edildiyse (alternatif akış), networked scene load yap
        if (!string.IsNullOrWhiteSpace(pendingSceneToLoad) && NetworkManager.Singleton.SceneManager != null)
        {
            var ok = NetworkManager.Singleton.SceneManager.LoadScene(pendingSceneToLoad, LoadSceneMode.Single);
            Debug.Log($"[Bootstrap] Networked scene load requested → '{pendingSceneToLoad}' (ok={ok})");
        }
        else
        {
            // Mevcut akışta burası normal: sahne zaten aktif.
            Debug.Log("[Bootstrap] No scene load requested; staying on current active scene.");
        }

        isStartingFlow = false;
    }

    private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t data)
    {
        Debug.Log("[Bootstrap] Join requested via overlay. Joining lobby...");
        SteamMatchmaking.JoinLobby(data.m_steamIDLobby);
    }

    private void OnLobbyEnter(LobbyEnter_t e)
    {
        var lobby = new CSteamID(e.m_ulSteamIDLobby);
        currentLobby = lobby;

        // Kim host zannediyoruz?
        bool steamThinksIAmOwner =
            (SteamMatchmaking.GetLobbyOwner(lobby) == SteamUser.GetSteamID());

        // Network tarafında gerçekten host muyuz?
        // (Eğer zaten host olarak dinliyorsak, client başlatmaya çalışmayalım.)
        bool actuallyRunningHost =
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsHost &&
            NetworkManager.Singleton.IsListening;

        // GÜNCELLEME:
        // Eski kod: "steamThinksIAmOwner == true ise direkt return"
        // Yeni kod: sadece gerçekten host olarak çalışıyorsak return et.
        if (steamThinksIAmOwner && actuallyRunningHost)
        {
            Debug.Log("[Bootstrap] Entered own lobby (host).");
            return;
        }

        // Buraya düşüyorsak:
        // - ya gerçekten host değiliz (yeni turda client'ız),
        // - ya da Steam yanlışlıkla 'sen ownersın' dedi ama biz aslında host modunda değiliz.
        // Bu durumda client flow'u çalıştırıyoruz.

        // Client: host ID'yi al
        var hostStr = SteamMatchmaking.GetLobbyData(lobby, "host");
        if (string.IsNullOrEmpty(hostStr) || !ulong.TryParse(hostStr, out var hostId))
        {
            Debug.LogError("[Bootstrap] Lobby has no valid 'host' data.");
            return;
        }

        // Transport hedefini ayarla ve client'ı başlat
        if (!TrySetTransportTarget(hostId)) return;

        var ok = NetworkManager.Singleton.StartClient();
        Debug.Log($"[Bootstrap] NGO Client start (ok={ok}) → target host={hostId}");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────
    private bool CheckPrereqs()
    {
        if (!SteamManager.Initialized)
        {
            Debug.LogError("[Bootstrap] Steam not initialized. Put SteamManager in scene (above NetworkManager).");
            return false;
        }

        if (NetworkManager.Singleton == null)
        {
            Debug.LogError("[Bootstrap] No NetworkManager in scene.");
            return false;
        }

        if (!(NetworkManager.Singleton.NetworkConfig.NetworkTransport is SteamNetworkingSocketsTransport))
        {
            Debug.LogError("[Bootstrap] Transport is not SteamNetworkingSocketsTransport. Set it on NetworkManager.");
            return false;
        }

        return true;
    }

    private bool TrySetTransportTarget(ulong hostId)
    {
        try
        {
            Transport.ConnectToSteamID = hostId;
            return true;
        }
        catch
        {
            Debug.LogError("[Bootstrap] Could not set ConnectToSteamID on transport.");
            return false;
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // YENİ: round bittiğinde/menüye dönerken çağrılacak state reset helper
    // Bu, eski lobby bilgisinin / eski host bilgisinin bir sonraki tura sızmasını engeller.
    // HostWithLobbyOnly() ve PlayHostWithLobbyAndLoad() başında da çağrılıyor.
    private void ResetLobbyState()
    {
        currentLobby = default;
        pendingSceneToLoad = null;
        isStartingFlow = false;
    }
}
