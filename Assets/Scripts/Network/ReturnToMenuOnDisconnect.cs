using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Steamworks;
using Pogo.UI;

[DisallowMultipleComponent]
[AddComponentMenu("Network/Return To Menu On Disconnect (Verbose)")]
public class ReturnToMenuOnDisconnect : MonoBehaviour
{
    // ───────── Inspector ─────────
    [Header("Scene")]
    [Tooltip("Döneceğin ana menü sahnesinin adı")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Delays (seconds)")]
    [Tooltip("Host kapandığında clientların bekleyeceği süre")]
    [SerializeField] private float clientDelaySeconds = 3f;
    [Tooltip("Host'un kendi dönüş gecikmesi (genelde 0)")]
    [SerializeField] private float hostDelaySeconds   = 0f;

    [Header("Options")]
    [Tooltip("Disconnect'te aktif Steam lobby'den ayrılmayı dene")]
    [SerializeField] private bool leaveSteamLobby = true;
    [Tooltip("Menüye dönmeden önce NetworkManager'ı kapat")]
    [SerializeField] private bool shutdownNetwork = true;
    [Tooltip("Beklemeyi gerçek zamana göre yap (timeScale'den bağımsız)")]
    [SerializeField] private bool useRealtimeWait = true;
    [Tooltip("Host server'ı durdurduğunda (uygulama kapanmadan) host'u da menüye döndür")]
    [SerializeField] private bool returnHostOnServerStop = false;

    [Header("Debug")]
    [SerializeField] private bool verboseLogs = true;
    
    // DDOL & tekil koruma
    private static ReturnToMenuOnDisconnect _instance;

    // Watchdog durumu
    private bool _returnStarted = false;
    private bool _wasClientConnected = false;
    private bool _didSubscribe = false;

    private void Log(string msg)
    {
        if (verboseLogs) Debug.Log($"[ReturnToMenu] {msg}");
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Log("Duplicate detected → destroying myself (DDOL singleton).");
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);
        Log("Awake → DDOL set.");
    }

    private void OnEnable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Log("OnEnable → NetworkManager.Singleton == null (will try again in Update watchdog).");
            _didSubscribe = false;
            return;
        }

        nm.OnClientDisconnectCallback += OnClientDisconnected;
        nm.OnServerStopped            += OnServerStopped;
        _didSubscribe = true;

        Log($"OnEnable → subscribed. IsServer={nm.IsServer}, IsClient={nm.IsClient}, " +
            $"IsListening={nm.IsListening}, LocalClientId={(nm.IsClient ? nm.LocalClientId : 999999)}");
    }

    private void OnDisable()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Log("OnDisable → NetworkManager.Singleton == null (nothing to unsubscribe).");
            return;
        }

        nm.OnClientDisconnectCallback -= OnClientDisconnected;
        nm.OnServerStopped            -= OnServerStopped;
        _didSubscribe = false;
        Log("OnDisable → unsubscribed.");
    }

    private void Start()
    {
        // Başlangıç bağlantı durumunu kaydet
        var nm = NetworkManager.Singleton;
        _wasClientConnected = (nm != null && nm.IsClient && nm.IsConnectedClient);
        Log($"Start → wasClientConnected={_wasClientConnected}");
    }

    private void Update()
    {
        // Watchdog: callback kaçarsa burada bağlantı düşüşünü yakalarız
        if (_returnStarted) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Subscribeları kaçırdıysak (sonradan sahneye geldiysek) bir kez daha deneyelim
        if (!_didSubscribe && nm != null)
        {
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            nm.OnServerStopped            += OnServerStopped;
            _didSubscribe = true;
            Log("Update → (late) subscribed to NGO callbacks.");
        }

        // Sadece CLIENT için: bir önceki frame bağlıydık, şimdi değilsek → server düşmüş olabilir
        if (!nm.IsServer && nm.IsClient)
        {
            bool isNowConnected = nm.IsConnectedClient;
            if (_wasClientConnected && !isNowConnected)
            {
                Log("Update/Watchdog → Client connection lost detected (IsConnectedClient just turned false).");
                BeginReturn(clientDelaySeconds);
            }
            _wasClientConnected = isNowConnected;
        }
    }

    // ───────── Callbacks ─────────

    // Host oyunu/oturumu durdurduğunda (uygulama kapanmadan)
    private void OnServerStopped(bool _)
    {
        var nm = NetworkManager.Singleton;
        Log($"OnServerStopped → nm!=null:{nm!=null}, returnHostOnServerStop={returnHostOnServerStop}");

        if (!returnHostOnServerStop) return;
        if (nm != null && nm.IsHost)
        {
            Log("OnServerStopped → Host detected. Will return to menu.");
            BeginReturn(hostDelaySeconds);
        }
    }

    // Client tarafı: server/host kapanınca bu tetiklenir
    private void OnClientDisconnected(ulong disconnectedClientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Sadece CLIENT tarafında ilgilen
        if (!nm.IsServer)
        {
            // Üçlü güvenli tespit:
            bool serverDownById = (disconnectedClientId == NetworkManager.ServerClientId); // 0
            bool selfKicked     = (nm.IsClient && disconnectedClientId == nm.LocalClientId);
            bool lostLinkNow    = !nm.IsConnectedClient; // bazı transportlarda bir-iki frame gecikmeli düşer

            if (verboseLogs)
            {
                var localId = nm.IsClient ? nm.LocalClientId : ulong.MaxValue;
                Debug.Log($"[ReturnToMenu] OnClientDisconnected (client). " +
                          $"disconnected={disconnectedClientId}, serverId={NetworkManager.ServerClientId}, localId={localId}, " +
                          $"serverDownById={serverDownById}, selfKicked={selfKicked}, isConnectedClient={nm.IsConnectedClient}");
            }
            
            if (serverDownById || selfKicked || lostLinkNow)
            {
                BeginReturn(clientDelaySeconds);
            }
        }
    }


    // ───────── Core flow ─────────
    private void BeginReturn(float delay)
    {
        if (_returnStarted)
        {
            Log("BeginReturn ignored (already started).");
            return;
        }
        _returnStarted = true;
        Log($"BeginReturn → delay={delay}");
        StartCoroutine(ReturnAfterDelay(delay));
    }

    private IEnumerator ReturnAfterDelay(float seconds)
    {
        if (seconds > 0f)
        {
            Log(useRealtimeWait
                ? $"Waiting (Realtime) {seconds}s before return…"
                : $"Waiting (Scaled) {seconds}s before return…");

            if (useRealtimeWait) yield return new WaitForSecondsRealtime(seconds);
            else                 yield return new WaitForSeconds(seconds);
        }

        // 1) Steam lobby'den ayrıl (varsa ve isteniyorsa)
        if (leaveSteamLobby)
        {
            try
            {
                if (SteamManager.Initialized)
                {
                    var lobby = GetCurrentLobbySafe();
                    Log($"LeaveLobby? SteamInitialized=true, lobbyId={lobby.m_SteamID}");
                    if (lobby.m_SteamID != 0)
                    {
                        Log($"Leaving Steam lobby {lobby.m_SteamID}.");
                        SteamMatchmaking.LeaveLobby(lobby);
                    }
                }
                else Log("Steam not initialized, skipping LeaveLobby.");
            }
            catch (System.Exception ex)
            {
                Log("LeaveLobby threw: " + ex.Message);
            }
        }
        else Log("leaveSteamLobby=false, skipping LeaveLobby.");

        // 2) Network'ü kapat (isteniyorsa)
        if (shutdownNetwork)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                Log($"Shutdown? IsListening={nm.IsListening}, IsServer={nm.IsServer}, IsClient={nm.IsClient}");
                if (nm.IsListening)
                {
                    nm.Shutdown();
                    Log("NetworkManager.Shutdown() called.");
                }
            }
            else Log("NetworkManager is null at shutdown step, skipping.");
        }
        else Log("shutdownNetwork=false, skipping NetworkManager.Shutdown().");

        // 3) Menü sahnesine dön
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            Log("Calling InGamePauseMenu.TryReturnToMainMenu() for unified return flow.");
            InGamePauseMenu.TryReturnToMainMenu();
        }
        else
        {
            Log("mainMenuSceneName empty, skipping return.");
        }

    }

    // Aynı GO'de SteamNGOBootstrap varsa ondan lobby ID al
    private CSteamID GetCurrentLobbySafe()
    {
        var bootstrap = SteamNGOBootstrap.Instance;
        if (bootstrap != null)
        {
            var id = bootstrap.CurrentLobbyID; // yoksa default (0) döner
            Log($"GetCurrentLobbySafe → bootstrap present, lobbyId={id.m_SteamID}");
            return id;
        }
        Log("GetCurrentLobbySafe → no bootstrap found, returning default.");
        return default;
    }
}