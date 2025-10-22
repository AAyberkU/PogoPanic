using UnityEngine;
using Unity.Netcode;
using Unity.Collections;
#if STEAMWORKS_NET
using Steamworks;
#endif
using System.Text.RegularExpressions;
using System.Collections;

[DisallowMultipleComponent]
public class PlayerNameData : NetworkBehaviour
{
    public readonly NetworkVariable<FixedString64Bytes> DisplayName =
        new NetworkVariable<FixedString64Bytes>(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

    [SerializeField] private string fallbackName = "Player";

    public override void OnNetworkSpawn()
    {
        if (IsOwner && IsClient)
            StartCoroutine(SendNameOnceReady());
    }

    private IEnumerator SendNameOnceReady()
    {
        // SteamManager hazır değilse kısa bekle
#if STEAMWORKS_NET
        float t = 0f;
        while (t < 2f && (!SteamManager.Initialized))
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }
#endif
        string localName = GetLocalPersonaName();
        SubmitNameServerRpc(localName);
    }

    [ServerRpc(RequireOwnership = true)]
    private void SubmitNameServerRpc(string proposed)
    {
        string clean = Sanitize(proposed);
        if (string.IsNullOrEmpty(clean)) clean = fallbackName;
        if (clean.Length > 64) clean = clean.Substring(0, 64);
        DisplayName.Value = clean;
        gameObject.name = $"Player[{clean}]";
    }

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        s = s.Trim();
        s = Regex.Replace(s, "<.*?>", string.Empty);
        s = Regex.Replace(s, @"\p{C}+", "");
        return s;
    }

    private string GetLocalPersonaName()
    {
#if STEAMWORKS_NET
        if (SteamManager.Initialized)
        {
            try
            {
                string n = SteamFriends.GetPersonaName();
                if (!string.IsNullOrWhiteSpace(n)) return n;
            } catch {}
        }
#endif
        return !string.IsNullOrWhiteSpace(fallbackName) ? fallbackName : System.Environment.UserName;
    }
}
