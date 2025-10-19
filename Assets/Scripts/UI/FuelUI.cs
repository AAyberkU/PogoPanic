using UnityEngine;
using UnityEngine.UI;
using Michsky.UI.Hex;
using System.Collections;
using Unity.Netcode;

namespace RageRunGames.PogostickController
{
    /// <summary>
    /// Keeps a HexUI SliderManager in sync with the player's jet-pack fuel,
    /// and pulses the fuelImage when fuel is being used.
    /// </summary>
    public class FuelUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SliderManager fuelSlider;    // HexUI slider
        [SerializeField] private PogostickController pogo;    // player controller
        [SerializeField] private Image fuelImage;             // the icon to pulse

        [Header("Pulse Settings")]
        [Tooltip("How much larger the image gets when fuel is used.")]
        [SerializeField] private float scaleFactor = 1.2f;
        [Tooltip("How long it takes to scale up or down.")]
        [SerializeField] private float scaleDuration = 0.5f;

        // runtime
        private Vector3 _normalScale;
        private Vector3 _pulsedScale;
        private Coroutine _scaleCoroutine;
        private float _lastFuel;

        //──────────────────────────────────────────────────────────────
        private void Awake()
        {
            if (fuelSlider == null)
                fuelSlider = GetComponentInChildren<SliderManager>();

            // Subscribe to NGO player spawn events
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            else
                Debug.LogWarning("[FuelUI] No NetworkManager found in scene.");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
        }

        //──────────────────────────────────────────────────────────────
        private void Start()
        {
            TryAssignPlayer(); // In case player is already spawned (host)
        }

        private void HandleClientConnected(ulong clientId)
        {
            // only assign for our own local player
            if (NetworkManager.Singleton.IsClient && 
                clientId == NetworkManager.Singleton.LocalClientId)
            {
                TryAssignPlayer();
            }
        }

        private void TryAssignPlayer()
        {
            if (pogo != null) return;

            // Find the local player
            foreach (var pc in FindObjectsOfType<PogostickController>())
            {
                var nb = pc.GetComponent<NetworkBehaviour>();
                if (nb != null && nb.IsOwner)
                {
                    pogo = pc;
                    SetupFuelUI();
                    Debug.Log($"[FuelUI] Assigned local player: {pc.name}");
                    return;
                }
            }

            Debug.LogWarning("[FuelUI] Could not find local PogostickController yet, will retry.");
        }

        //──────────────────────────────────────────────────────────────
        private void SetupFuelUI()
        {
            if (pogo == null || fuelSlider == null || fuelImage == null)
                return;

            // match slider range
            float maxFuel = pogo.PogoStickControllerSettings.jetpackSettings.maxFuel;
            fuelSlider.mainSlider.minValue = 0f;
            fuelSlider.mainSlider.maxValue = maxFuel;

            // cache scales
            _normalScale = fuelImage.rectTransform.localScale;
            _pulsedScale = _normalScale * scaleFactor;
            _lastFuel = pogo.GetCurrentFuel();
        }

        //──────────────────────────────────────────────────────────────
        private void Update()
        {
            if (pogo == null)
            {
                // keep retrying until player spawns
                TryAssignPlayer();
                return;
            }

            if (fuelSlider == null || fuelImage == null)
                return;

            float currentFuel = pogo.GetCurrentFuel();
            fuelSlider.mainSlider.value = currentFuel;
            fuelSlider.UpdateUI();

            // detect burning: fuel dropped since last frame
            bool isBurning = currentFuel < _lastFuel;
            _lastFuel = currentFuel;

            // launch scale coroutine if needed
            Vector3 target = isBurning ? _pulsedScale : _normalScale;
            if (_scaleCoroutine == null || fuelImage.rectTransform.localScale != target)
            {
                if (_scaleCoroutine != null) StopCoroutine(_scaleCoroutine);
                _scaleCoroutine = StartCoroutine(ScaleTo(target));
            }
        }

        private IEnumerator ScaleTo(Vector3 targetScale)
        {
            Vector3 start = fuelImage.rectTransform.localScale;
            float t = 0f;

            while (t < scaleDuration)
            {
                t += Time.deltaTime;
                float u = t / scaleDuration;
                fuelImage.rectTransform.localScale = Vector3.Lerp(start, targetScale, u);
                yield return null;
            }

            fuelImage.rectTransform.localScale = targetScale;
            _scaleCoroutine = null;
        }
    }
}
