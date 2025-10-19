using UnityEngine;
using TMPro;
using Unity.Netcode;
using System.Collections;

public class WorldYDisplay : MonoBehaviour
{
    [Header("Target to watch")]
    [SerializeField] Transform target;

    [Header("UI – Current & Max")]
    [SerializeField] TextMeshProUGUI currentReadout; // auto-filled if blank
    [SerializeField] TextMeshProUGUI maxReadout;     // optional

    [Header("Display")]
    [Tooltip("Numeric format, e.g. \"F1\" → 12.3   |  \"F0\" → 12")]
    [SerializeField] string format = "F2";
    [Tooltip("Unit to append after the number")]
    [SerializeField] string unit = " m";
    [Tooltip("Seconds between updates; 0 = every frame")]
    [SerializeField] float refreshRate = 0f;

    [Header("Milestone Popup")]
    [Tooltip("Panel to show when the player first reaches the milestone")]
    [SerializeField] GameObject milestonePanel;
    [Tooltip("CanvasGroup used to fade the milestone panel in/out")]
    [SerializeField] CanvasGroup milestoneCanvasGroup;
    [Tooltip("Height (Y) that triggers the milestone popup")]
    [SerializeField] float milestoneHeight = 257f;
    [Tooltip("How long the milestone panel stays visible before fading out")]
    [SerializeField] float milestoneVisibleDuration = 5f;
    [Tooltip("Fade-in/out duration (seconds)")]
    [SerializeField] float fadeDuration = 1f;

    float timer;
    float maxY = float.NegativeInfinity;
    bool milestoneTriggered;

    //──────────────────────────────────────────────────────────────
    void Awake()
    {
        if (!currentReadout)
            currentReadout = GetComponent<TextMeshProUGUI>();

        // If target already set manually, we’re done
        if (target) return;

        // Otherwise, try to hook into NetworkManager to find player later
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
        else
            Debug.LogWarning("[WorldYDisplay] No NetworkManager found yet — will retry next frame.");
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
    }

    //──────────────────────────────────────────────────────────────
    void HandleClientConnected(ulong clientId)
    {
        if (NetworkManager.Singleton.IsClient && clientId == NetworkManager.Singleton.LocalClientId)
            FindLocalPlayer();
    }

    void Start()
    {
        if (!target)
            FindLocalPlayer();

        if (milestonePanel)
            milestonePanel.SetActive(false);

        if (milestoneCanvasGroup)
            milestoneCanvasGroup.alpha = 0f;
    }

    void FindLocalPlayer()
    {
        foreach (var obj in FindObjectsOfType<NetworkBehaviour>())
        {
            if (obj.IsOwner)
            {
                target = obj.transform;
                Debug.Log($"[WorldYDisplay] Target assigned to local player '{obj.name}'");
                return;
            }
        }

        Debug.LogWarning("[WorldYDisplay] Could not find local player yet — will retry.");
    }

    //──────────────────────────────────────────────────────────────
    void Update()
    {
        if (!target || !currentReadout) return;

        if (refreshRate > 0f)
        {
            timer -= Time.deltaTime;
            if (timer > 0f) return;
            timer = refreshRate;
        }

        float y = target.position.y;
        currentReadout.text = y.ToString(format) + unit;

        if (y > maxY)
        {
            maxY = y;
            if (maxReadout)
                maxReadout.text = maxY.ToString(format) + unit;
        }

        // --- Milestone check ---
        if (!milestoneTriggered && y >= milestoneHeight)
        {
            milestoneTriggered = true;
            Debug.Log($"[WorldYDisplay] Milestone reached at {y:F1}m — starting fade sequence.");
            if (milestonePanel && milestoneCanvasGroup)
                StartCoroutine(FadeMilestoneSequence());
        }
    }

    IEnumerator FadeMilestoneSequence()
    {
        milestonePanel.SetActive(true);

        // Fade in
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            milestoneCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t / fadeDuration);
            yield return null;
        }
        milestoneCanvasGroup.alpha = 1f;

        // Stay visible
        yield return new WaitForSeconds(milestoneVisibleDuration);

        // Fade out
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            milestoneCanvasGroup.alpha = Mathf.Lerp(1f, 0f, t / fadeDuration);
            yield return null;
        }
        milestoneCanvasGroup.alpha = 0f;

        milestonePanel.SetActive(false);
    }

    /// <summary>Clear the stored maximum and reset milestone.</summary>
    public void ResetMax()
    {
        maxY = float.NegativeInfinity;
        if (maxReadout) maxReadout.text = "—";
        milestoneTriggered = false;

        if (milestonePanel)
            milestonePanel.SetActive(false);

        if (milestoneCanvasGroup)
            milestoneCanvasGroup.alpha = 0f;
    }
}
