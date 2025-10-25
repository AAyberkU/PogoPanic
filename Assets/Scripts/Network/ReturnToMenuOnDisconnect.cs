using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Steamworks;
using Pogo.UI;

/// <summary>
/// Client-side safety watchdog.
/// Gameplay sahnesine koy.
/// - Eğer host herhangi bir şekilde giderse (quit, alt+F4, main menu'ye dönme, crash),
///   client bunu fark eder ve anında main menu sahnesine döner.
/// - Host'u ASLA etkilemez.
/// - Delay yok.
/// - DDOL yok; main menüye dönünce bu component zaten yok olur (sahne unload olur).
/// </summary>
[DisallowMultipleComponent]
public class ReturnToMenuOnDisconnect : MonoBehaviour
{
    [Header("Main menu scene name")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // guard: aynı frame içinde birden fazla tetik gelirse tekrar çalışmasın
    private bool _returning;

    // önceki frame bağlantı durumu (sadece saf client için anlamlı)
    private bool _wasConnectedClient;

    // ─────────────────────────────────────────────────────
    // lifecycle
    void Awake()
    {
        var nm = NetworkManager.Singleton;

        // Başta bağlantı durumunu cachele
        _wasConnectedClient = (nm != null && nm.IsClient && nm.IsConnectedClient);

        // NGO callbacklerine abone ol
        if (nm != null)
        {
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            nm.OnServerStopped            += OnServerStopped;
        }
    }

    void OnDestroy()
    {
        // Unsubscribe (defansif)
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
            nm.OnServerStopped            -= OnServerStopped;
        }
    }

    void Update()
    {
        if (_returning) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // Host asla bu script tarafından zorlanmamalı
        // Host = IsHost == true (IsServer && IsClient aynı anda)
        if (nm.IsHost)
            return;

        // Sadece saf client için çalış
        if (!nm.IsClient || nm.IsServer)
            return;

        // Watchdog: bağlantı durumu frame'ten frame'e değişti mi?
        bool isNowConnected = nm.IsConnectedClient;

        // önce bağlıydık, şimdi bağlı değiliz → host kayboldu demektir
        if (_wasConnectedClient && !isNowConnected)
        {
            TriggerReturnToMenu();
            return;
        }

        _wasConnectedClient = isNowConnected;
    }

    // ─────────────────────────────────────────────────────
    // NGO callbacks
    // ─────────────────────────────────────────────────────

    // herhangi bir client disconnect ettiğinde çağrılır (biz dahil)
    private void OnClientDisconnected(ulong disconnectedClientId)
    {
        if (_returning) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // hostu ASLA dokunma
        if (nm.IsHost)
            return;

        // sadece saf client
        if (!nm.IsClient || nm.IsServer)
            return;

        // Aşağıdaki üç durumdan biri doğruysa: bizim için match bitti say
        bool serverDownById = (disconnectedClientId == NetworkManager.ServerClientId); // genelde 0 = host
        bool selfKicked     = (nm.LocalClientId == disconnectedClientId);
        bool lostLinkNow    = !nm.IsConnectedClient;

        if (serverDownById || selfKicked || lostLinkNow)
        {
            TriggerReturnToMenu();
        }
    }

    // server kapandı diye haber geldiğinde
    private void OnServerStopped(bool _ignored)
    {
        if (_returning) return;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        // host ise bu onun kendi kararı -> karışma
        if (nm.IsHost)
            return;

        // sadece saf client
        if (!nm.IsClient || nm.IsServer)
            return;

        TriggerReturnToMenu();
    }

    // ─────────────────────────────────────────────────────
    // Core action
    // ─────────────────────────────────────────────────────
    private void TriggerReturnToMenu()
    {
        if (_returning) return;
        _returning = true;

        // 1) Steam lobby'den ayrılmaya çalış (sessiz fail)
        TryLeaveLobby();

        // 2) Main menu'ye dön
        // Tercih: pause menu varsa onun yolunu kullan (o zaten network shutdown mantığını biliyor)
        if (InGamePauseMenu.Instance != null)
        {
            InGamePauseMenu.Instance.ReturnToMainMenu();
            return;
        }

        // Fallback: doğrudan sahneyi yükle
        if (!string.IsNullOrEmpty(mainMenuSceneName))
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        else
        {
            Debug.LogWarning("[ReturnToMenuOnDisconnect] mainMenuSceneName boş, sahne yüklenemiyor.");
        }
    }

    private void TryLeaveLobby()
    {
        try
        {
            if (SteamManager.Initialized && SteamNGOBootstrap.Instance != null)
            {
                var lobbyId = SteamNGOBootstrap.Instance.CurrentLobbyID;
                if (lobbyId.m_SteamID != 0)
                {
                    SteamMatchmaking.LeaveLobby(lobbyId);
                }
            }
        }
        catch
        {
            // sessiz geç
        }
    }
}
