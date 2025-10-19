using System.Collections;
using RageRunGames.PogostickController;
using UnityEngine;
using Unity.Netcode;  // ← eklendi

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(NetworkObject))] // ← FX yaymak için gerekli
public class JumpPad : NetworkBehaviour   // ← MonoBehaviour → NetworkBehaviour
{
    [Header("Jump Settings")]
    [SerializeField] private float jumpForce = 20f;
    [SerializeField] private ForceMode forceMode = ForceMode.VelocityChange;
    [SerializeField] private bool useWorldUp = false;     // NEW

    [Header("Filtering")]
    [SerializeField] private string playerTag = "Player";

    [Header("FX (optional)")]
    [SerializeField] private AudioClip jumpSfx;
    [SerializeField] private ParticleSystem jumpVfx;

    //--------------------------------------------------------------------
    private void Reset()
    {
        // Make collider a Trigger for classic “blue portal” behaviour,
        // but the script also works if you uncheck this and rely on collisions.
        GetComponent<Collider>().isTrigger = true;

        // Guarantee a kinematic RB so callbacks fire even if the player's
        // collider lives on a child without a rigidbody.
        EnsureKinematicRigidbody();
    }

    private void Awake() => EnsureKinematicRigidbody();

    private void EnsureKinematicRigidbody()
    {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;               // anchor the pad
        }
        else
        {
            rb.isKinematic = true;
        }
    }

    //--------------------------------------------------------------------
    // Works for Trigger OR Collision (use whichever you prefer)
    // OnTriggerEnter will still activate regardless of direction
    void OnTriggerEnter(Collider other)
    {
        // If you want directional sensitivity for triggers too,
        // you'd need a more complex solution (e.g., raycasting from player).
        // For now, it will launch even from bottom if it's a trigger.
        // If you primarily use collision, this is fine.
        TryLaunch(other.attachedRigidbody);
    }

    void OnCollisionEnter(Collision collision) // Changed 'other' to 'collision' for clarity
    {
        Rigidbody playerRb = collision.rigidbody; // Get rigidbody from Collision object

        if (playerRb == null || !playerRb.CompareTag(playerTag)) return;

        // Calculate the average normal of the contact points.
        // This will tell us the direction the collision came from relative to the jump pad.
        Vector3 averageNormal = Vector3.zero;
        foreach (ContactPoint contact in collision.contacts)
        {
            averageNormal += contact.normal;
        }
        averageNormal /= collision.contacts.Length;

        // Check if the collision normal is pointing mostly upwards (relative to the jump pad's local up).
        // This means the player landed on top.
        // Adjust the 0.5f threshold if needed for sensitivity.
        if (Vector3.Dot(averageNormal, transform.up) < 0.5f)
        {
            TryLaunch(playerRb);
        }
        // If the dot product is not positive and above the threshold,
        // it means the player hit from the side or below, so we do nothing.
    }

    private void TryLaunch(Rigidbody playerRb)
    {
        if (playerRb == null || !playerRb.CompareTag(playerTag)) return;

        // 1) Tell the spring not to fight us
        Spring spring = playerRb.GetComponentInChildren<Spring>();
        if (spring != null) spring.enableSuspensionForce = false;   // let it float

        // 2) Clear downward velocity and boost
        Vector3 vel = playerRb.linearVelocity;          // linearVelocity == velocity
        if (vel.y < 0f) vel.y = 0f;
        playerRb.linearVelocity = vel;                  // overwrite to be safe
        playerRb.AddForce((useWorldUp ? Vector3.up : transform.up) * jumpForce,
            forceMode);

        // 3) Re-enable the suspension after a tiny delay
        if (spring != null)
            StartCoroutine(EnableSuspensionNextFixed(spring));

        // 4) Play FX (optional) — önce yerelde çal
        if (jumpSfx != null)
        {
            AudioSource.PlayClipAtPoint(jumpSfx, transform.position);
        }
        if (jumpVfx != null)
        {
            Instantiate(jumpVfx, transform.position, Quaternion.identity); // Or pool it
        }

        // 4-b) FX’i diğer client’lara yayınla (sadece FX, fizik değil)
        // Network kapalıysa sessizce atlar.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Pozisyonu gönder; asset referanslarını her client kendi komponentinden kullanır.
            RequestFxServerRpc(transform.position);
        }
    }

    private IEnumerator EnableSuspensionNextFixed(Spring spring)
    {
        yield return new WaitForFixedUpdate();
        spring.enableSuspensionForce = true;
    }

    // ------------------ NGO: FX Relay ------------------

    [ServerRpc(RequireOwnership = false)]
    private void RequestFxServerRpc(Vector3 worldPos, ServerRpcParams serverRpcParams = default)
    {
        // Gönderen client’ı hariç tutarak yayınla (owner’da çift çalmasın)
        ulong senderId = serverRpcParams.Receive.SenderClientId;

        // Hedef listesi: tüm client’lar - sender
        if (NetworkManager == null) return;
        var ids = NetworkManager.ConnectedClientsIds;
        // Eğer tek başınaysa veya host tek client ise, gerek yok:
        if (ids == null || ids.Count <= 1) return;

        // Target listesi oluştur
        var targets = new System.Collections.Generic.List<ulong>(ids.Count);
        for (int i = 0; i < ids.Count; i++)
        {
            if (ids[i] != senderId)
                targets.Add(ids[i]);
        }

        if (targets.Count == 0) return;

        var sendParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = targets.ToArray() }
        };
        PlayFxClientRpc(worldPos, sendParams);
    }

    [ClientRpc]
    private void PlayFxClientRpc(Vector3 worldPos, ClientRpcParams clientRpcParams = default)
    {
        // Tüm hedef client’larda SFX/VFX’i çal
        if (jumpSfx != null)
            AudioSource.PlayClipAtPoint(jumpSfx, worldPos);

        if (jumpVfx != null)
            Instantiate(jumpVfx, worldPos, Quaternion.identity); // Pool önerilir
    }
}
