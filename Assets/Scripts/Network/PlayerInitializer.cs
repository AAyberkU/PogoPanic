using Unity.Netcode;
using UnityEngine;

public class PlayerInitializer : NetworkBehaviour
{
    [Header("Owner Only Objects")]
    [SerializeField] private GameObject[] ownerOnlyObjects; // Kamera, UI, vb.

    [Header("Camera")]
    [SerializeField] private Camera playerCamera; // Prefab içindeki Main Camera
    [SerializeField] private AudioListener audioListener; // Prefab içindeki AudioListener

    public override void OnNetworkSpawn()
    {
        bool isOwnerLocal = IsOwner;

        // Owner değilse owner-only objeleri kapat
        foreach (var go in ownerOnlyObjects)
        {
            if (go != null) go.SetActive(isOwnerLocal);
        }

        // Kamera ve AudioListener ayarı
        if (playerCamera != null)
            playerCamera.enabled = isOwnerLocal;

        if (audioListener != null)
            audioListener.enabled = isOwnerLocal;
    }
}