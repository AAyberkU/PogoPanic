using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;
using Michsky.UI.Hex;
using UnityEngine.UI;
using Steamworks;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pogo.UI
{
    public class InGamePauseMenu : MonoBehaviour
    {
        [Header("Scene to load when returning to main menu")]
        [SerializeField] string mainMenuSceneName = "MainMenu";

        [Header("Buttons (Hex)")]
        [SerializeField] ButtonManager resumeButton;
        [SerializeField] ButtonManager settingsButton;
        [SerializeField] ButtonManager mainMenuButton;
        [SerializeField] ButtonManager quitButton;
        [SerializeField] ButtonManager inviteButton;

        [Header("Settings Panel")]
        [SerializeField] GameObject settingsPanel;

        private PanelScaleAnimator settingsAnim;
        private PanelScaleAnimator rootAnim;
        public static InGamePauseMenu Instance { get; private set; }

        private bool settingsBusy;

        // ────────────────────────────────────────────────────────────────
        void Awake()
        {
            Instance = this;
            settingsAnim = settingsPanel ? settingsPanel.GetComponent<PanelScaleAnimator>() : null;
            rootAnim     = GetComponent<PanelScaleAnimator>();

            resumeButton  ?.onClick.AddListener(ResumeGame);
            settingsButton?.onClick.AddListener(ToggleSettings);
            mainMenuButton?.onClick.AddListener(ReturnToMainMenu);
            quitButton    ?.onClick.AddListener(QuitGame);
            inviteButton  ?.onClick.AddListener(InviteSteam);

            CloseAllPanels();
        }

        // ────────────────────────────────────────────────────────────────
        public void ResumeGame()
        {
            HideCursor();
            rootAnim?.Close();
            CloseAllPanels();
        }

        public void ToggleSettings()
        {
            if (settingsAnim == null || settingsBusy) return;

            settingsBusy = true;
            Sequence seq = settingsAnim.IsOpen
                ? settingsAnim.Close()
                : settingsAnim.Open();

            if (seq != null)
                seq.OnComplete(() => settingsBusy = false);
            else
                settingsBusy = false;
        }

        // ────────────────────────────────────────────────────────────────
        public void ReturnToMainMenu()
        {
            ShowCursor();
            CloseAllPanels();
            GameStateManager.Instance?.SetState(GameState.MainMenu);

            // ✅ Cleanly end and destroy NetworkManager before loading menu
            EndNetworkSession();

            if (string.IsNullOrWhiteSpace(mainMenuSceneName))
            {
                Debug.LogWarning("[PauseMenu] Main-menu scene name empty");
                return;
            }

            Sequence seq = rootAnim ? rootAnim.Close() : null;

            void LoadMain()
            {
                if (CircleTransitionManager.Instance)
                    CircleTransitionManager.Instance.LoadScene(mainMenuSceneName);
                else
                    SceneManager.LoadScene(mainMenuSceneName);
            }

            if (seq != null)
                seq.OnComplete(LoadMain);
            else
                LoadMain();
        }

        // ────────────────────────────────────────────────────────────────
        public void QuitGame()
        {
            HideCursor();
            CloseAllPanels();

            // ✅ Also clean up network before quitting
            EndNetworkSession();

            StartCoroutine(QuitRoutine());
        }

        // ────────────────────────────────────────────────────────────────
        private IEnumerator QuitRoutine()
        {
            Debug.Log("[PauseMenu] QuitRoutine started.");

            var ctm = CircleTransitionManager.Instance;
            bool didTween = false;

            if (ctm != null)
            {
                var img = ctm.GetComponentInChildren<Image>(true);
                if (img != null && img.material != null && img.material.HasProperty("_Radius"))
                {
                    int id = Shader.PropertyToID("_Radius");
                    Tween tween = img.material
                        .DOFloat(-0.1f, id, 0.6f)
                        .SetEase(Ease.InCubic)
                        .SetUpdate(true);

                    didTween = true;
                    yield return tween.WaitForCompletion();
                }
                else
                {
                    Debug.LogWarning("[PauseMenu] CircleTransition image or material missing, skipping animation.");
                }
            }
            else
            {
                Debug.LogWarning("[PauseMenu] No CircleTransitionManager found, quitting directly.");
            }

            // Now safe to quit (after animation or immediately)
            Debug.Log("[PauseMenu] Quitting application now...");
            Application.Quit();

#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                Debug.Log("[PauseMenu] Stopping Play Mode (Editor only).");
                EditorApplication.isPlaying = false;
            }
#endif
            yield break;
        }

        // ────────────────────────────────────────────────────────────────
        public void InviteSteam()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogWarning("[PauseMenu] Steam not initialized — cannot open invite overlay.");
                return;
            }

            if (SteamNGOBootstrap.Instance == null)
            {
                Debug.LogWarning("[PauseMenu] SteamNGOBootstrap missing — cannot open invite overlay.");
                return;
            }

            try
            {
                SteamFriends.ActivateGameOverlayInviteDialog(
                    SteamNGOBootstrap.Instance.CurrentLobbyID
                );
                Debug.Log("[PauseMenu] Steam Friends Overlay opened for invites.");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[PauseMenu] Failed to open Steam overlay: {ex.Message}");
            }
        }

        // ────────────────────────────────────────────────────────────────
        public void CloseAllPanels() => settingsAnim?.Close();

        // ────────────────────────────────────────────────────────────────
        static void HideCursor()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible   = false;
        }

        static void ShowCursor()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible   = true;
        }

        // ────────────────────────────────────────────────────────────────
        public static void TryReturnToMainMenu()
        {
            var instance = FindObjectOfType<InGamePauseMenu>();
            if (instance != null)
            {
                instance.ReturnToMainMenu();
            }
            else
            {
                Debug.LogWarning("[InGamePauseMenu] No active instance found — loading MainMenu directly as fallback.");
                SceneManager.LoadScene("MainMenu");
            }
        }

        // ✅ Fully ends and destroys any lingering NetworkManager
        void EndNetworkSession()
        {
            var nm = Unity.Netcode.NetworkManager.Singleton;
            if (nm != null)
            {
                if (nm.IsListening)
                {
                    nm.Shutdown();
                    Debug.Log("[PauseMenu] Network session shutdown completed.");
                }

                // ✅ Destroy the persistent object so new scene can spawn fresh one
                Destroy(nm.gameObject);
                Debug.Log("[PauseMenu] NetworkManager destroyed before scene reload.");
            }
        }
    }
}
