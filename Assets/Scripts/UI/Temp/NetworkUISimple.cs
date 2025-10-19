using UnityEngine;
using Unity.Netcode;
using TMPro;
using Unity.Netcode.Transports.UTP;
using Unity.Networking.Transport; // NetworkEndpoint için

public class NetworkUISimple : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TMP_InputField ipInputField;   // Local IP (örn: 192.168.1.42)
    [SerializeField] private GameObject panelToHide;        // UI panel

    private const ushort DEFAULT_PORT = 7777;

    private UnityTransport Utp
        => (UnityTransport)NetworkManager.Singleton.NetworkConfig.NetworkTransport;

    public void OnHostClicked()
    {
        if (NetworkManager.Singleton.IsListening) return;

        // Host/Server: tüm arayüzlerde dinle
        var listen = NetworkEndpoint.AnyIpv4.WithPort(DEFAULT_PORT);

        // Bazı sürümlerde 2 parametreli overload var (serverEndPoint, listenEndPoint).
        // Güvenli olması için ikisine de aynı endpoint'i veriyoruz.
        Utp.SetConnectionData(listen, listen);

        var ok = NetworkManager.Singleton.StartHost();
        if (ok && panelToHide) panelToHide.SetActive(false);
    }

    public void OnClientClicked()
    {
        if (NetworkManager.Singleton.IsListening) return;
        if (!ValidateIp()) return;

        var ip = ipInputField.text.Trim();

        // Client: server endpoint
        var server = NetworkEndpoint.Parse(ip, DEFAULT_PORT);
        Utp.SetConnectionData(server);

        var ok = NetworkManager.Singleton.StartClient();
        if (ok && panelToHide) panelToHide.SetActive(false);
    }

    // — yardımcı —
    private bool ValidateIp()
    {
        if (ipInputField == null)
        {
            Debug.LogError("[UI] IP InputField atanmadı!");
            return false;
        }
        var ip = ipInputField.text;
        if (string.IsNullOrWhiteSpace(ip))
        {
            Debug.LogWarning("IP alanı boş bırakılamaz!");
            return false;
        }
        return true;
    }
}