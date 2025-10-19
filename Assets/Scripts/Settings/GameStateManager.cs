//----------------------------------------------
//  GameStateManager.cs
//----------------------------------------------
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace Pogo
{
    /// <summary>Only two states, exactly as requested.</summary>
    public enum GameState { MainMenu, Gameplay }

    public class GameStateManager : MonoBehaviour
    {
        // ──────────────────────────────────────────────
        #region Singleton boiler‑plate
        public static GameStateManager Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);                // OPTIONAL – remove if you reload the manager each scene
        }
        #endregion
        // ──────────────────────────────────────────────

        [Header("Main‑menu root panel (will be auto‑shown)")]
        [SerializeField] public GameObject mainMenuRoot;   // drag the whole main‑menu canvas here

        [Header("Events (optional)")]
        public UnityEvent onEnterMainMenu  = new();
        public UnityEvent onEnterGameplay  = new();

        public GameState CurrentState { get; private set; } = GameState.MainMenu;

        /// <summary>Call this from your UI or other code to change state.</summary>
        public void SetState(GameState newState)
        {
            if (newState == CurrentState) return;           // already there
            CurrentState = newState;

            StopAllCoroutines();                            // cancel any pending menu‑show

            switch (CurrentState)
            {
                case GameState.MainMenu:
                    StartCoroutine(ShowMainMenuAfterDelay());
                    onEnterMainMenu?.Invoke();
                    break;

                case GameState.Gameplay:
                    if (mainMenuRoot) mainMenuRoot.SetActive(false);
                    onEnterGameplay?.Invoke();
                    break;
            }
        }

        // ──────────────────────────────────────────────
        IEnumerator ShowMainMenuAfterDelay()
        {
            yield return new WaitForSeconds(1f);            // <‑‑ 1 second delay
            if (mainMenuRoot) mainMenuRoot.SetActive(true);
        }
    }
}
