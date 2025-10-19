using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class LeaveGameBasic : MonoBehaviour
{
    [Header("Optional – boş bırak sahne değiştirmez")]
    [SerializeField] private string returnScene = "";

    [Header("UI Paneli (Disconnect’ten sonra geri açılacak)")]
    [SerializeField] private GameObject networkUI;   // NetworkUISimple içeren panel

    public void OnDisconnectClicked()
    {
        if (NetworkManager.Singleton == null)
        {
            Debug.LogWarning("[LeaveGame] NetworkManager yok!");
            return;
        }

        // 1) Bağlantıyı kapat
        if (NetworkManager.Singleton.IsListening)
            NetworkManager.Singleton.Shutdown();

        // 2) UI Panelini yeniden etkinleştir (isteğe bağlı)
        if (networkUI != null)
            networkUI.SetActive(true);

        // 3) Sahne değiştirmek istiyorsan
        if (!string.IsNullOrEmpty(returnScene))
            SceneManager.LoadScene(returnScene);
    }
}