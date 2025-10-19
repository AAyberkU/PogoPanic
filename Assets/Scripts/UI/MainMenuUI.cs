using DG.Tweening;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.SceneManagement;   // ⬅️ eklendi

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Pogo.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        //───────────────────────────────────────────────────────────────
        [Header("Scene to load on Play")]
        [SerializeField] private string playSceneName = "FlippedDemo"; // ⬅️ varsayılanı FlippedDemo yaptım (Inspector’dan değiştirebilirsin)

        [Header("Buttons  (Unity UI – not Hex)")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button leaderboardButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels")]
        [SerializeField] private GameObject leaderboardPanel;   // may be left empty
        [SerializeField] private GameObject settingsPanel;      // may be left empty

        [Header("Input")]
        [Tooltip("UI/Cancel action (Esc, game-pad B, etc.)")]
        [SerializeField] private InputActionReference cancelAction;

        // cached animators
        private PanelScaleAnimator leaderboardAnim;
        private PanelScaleAnimator settingsAnim;

        //───────────────────────────────────────────────────────────────
        #region Unity lifecycle
        private void Awake()
        {
            WireButtons();   // just hook up button delegates here
        }

        private void Start()
        {
            EnsurePanels();  // run once when every object is alive (incl. DDOL)
            CloseAllPanels();
        }

        private void OnEnable()
        {
            if (cancelAction != null)
            {
                cancelAction.action.performed += OnCancel;
                cancelAction.action.Enable();
            }
        }

        private void OnDisable()
        {
            if (cancelAction != null)
            {
                cancelAction.action.performed -= OnCancel;
                cancelAction.action.Disable();
            }
        }
        #endregion
        //───────────────────────────────────────────────────────────────
        // Button handlers
        //───────────────────────────────────────────────────────────────
        public void Play()
        {
            CloseAllPanels();
            GameStateManager.Instance?.SetState(GameState.Gameplay);

            if (string.IsNullOrWhiteSpace(playSceneName))
            {
                Debug.LogWarning("[MainMenuUI] Play scene name is empty!");
                return;
            }

            // ⬇️ Sahneyi aç, aktif olunca host+lobby başlat (SteamNGOBootstrap)
            StartCoroutine(PlayFlow());
        }

        private System.Collections.IEnumerator PlayFlow()
        {
            string target = playSceneName;

            // 1) Sahneyi yükle (görsel geçiş varsa onu kullan)
            if (CircleTransitionManager.Instance)
                CircleTransitionManager.Instance.LoadScene(target);
            else
                SceneManager.LoadScene(target);

            // 2) Sahne gerçekten aktif olana kadar bekle
            while (SceneManager.GetActiveScene().name != target)
                yield return null;

            // 3) Sahne aktif: şimdi Steam lobby + host başlat
            SteamNGOBootstrap.Instance?.HostWithLobbyOnly();
        }

        public void ContinueGame() => Debug.Log("[MainMenuUI] Continue pressed (not implemented yet)");

        public void OpenLeaderboard() => SwitchPanels(leaderboardAnim, settingsAnim);

        public void OpenSettings()    => SwitchPanels(settingsAnim,    leaderboardAnim);

        public void QuitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
#endif
        }
        //───────────────────────────────────────────────────────────────
        // Input handler
        //───────────────────────────────────────────────────────────────
        private void OnCancel(InputAction.CallbackContext _) => CloseAllPanels();

        //───────────────────────────────────────────────────────────────
        // Helpers
        //───────────────────────────────────────────────────────────────
        private void SwitchPanels(PanelScaleAnimator toOpenOrToggle, PanelScaleAnimator otherPanel)
        {
            EnsurePanels();
            if (toOpenOrToggle == null) return;

            // toggle behaviour
            if (toOpenOrToggle.IsOpen)
            {
                toOpenOrToggle.Close();
                return;
            }

            // close the other first, then open
            if (otherPanel != null && otherPanel.IsOpen)
            {
                Sequence s = otherPanel.Close();
                s?.OnComplete(() => toOpenOrToggle.Open());
            }
            else
            {
                toOpenOrToggle.Open();
            }
        }

        private void CloseAllPanels()
        {
            EnsurePanels();
            leaderboardAnim?.Close();
            settingsAnim   ?.Close();
        }

        /// <summary>
        /// Make sure panel references & animators are cached.
        /// Call this any time before you use them.
        /// </summary>
        private void EnsurePanels()
        {
            if (!settingsPanel)
                settingsPanel = FindPanelContaining("settingspaneel");

            if (!leaderboardPanel)
                leaderboardPanel = FindPanelContaining("leaderboard");

            if (settingsPanel && !settingsAnim)
                settingsAnim = settingsPanel.GetComponent<PanelScaleAnimator>();

            if (leaderboardPanel && !leaderboardAnim)
                leaderboardAnim = leaderboardPanel.GetComponent<PanelScaleAnimator>();
        }

        /// <summary>
        /// Looks through **all** loaded scenes, including the DontDestroyOnLoad
        /// scene, and returns the first GameObject (even inactive) whose name
        /// contains the given keyword (case-insensitive).
        /// </summary>
        private static GameObject FindPanelContaining(string keyword)
        {
            keyword = keyword.ToLowerInvariant();

            foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
            {
                if (!go.scene.isLoaded)       continue;               // skip prefab assets
                if (go.hideFlags != 0)        continue;               // skip hidden/editor stuff

                if (go.name.ToLowerInvariant().Contains(keyword))
                    return go;
            }
            return null; // nothing matched
        }

        private void WireButtons()
        {
            playButton       ?.onClick.AddListener(Play);
            continueButton   ?.onClick.AddListener(ContinueGame);
            leaderboardButton?.onClick.AddListener(OpenLeaderboard);
            settingsButton   ?.onClick.AddListener(OpenSettings);
            quitButton       ?.onClick.AddListener(QuitGame);
        }
    }
}
