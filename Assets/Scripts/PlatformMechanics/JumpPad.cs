using System.Collections;
using RageRunGames.PogostickController;
using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(Rigidbody))]          // YENİ: RB garanti
[RequireComponent(typeof(NetworkObject))]      // FX yaymak için gerekli
public class JumpPad : NetworkBehaviour
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
        // Trigger davranışı (orijinalinle aynı niyet)
        GetComponent<Collider>().isTrigger = true;
    }

    private void Awake()
    {
        // YENİ: EnsureKinematicRigidbody yerine burada ayarla
        var rb = GetComponent<Rigidbody>();
        rb.isKinematic = true;                                   // pad sabit
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // CCD
    }

    //--------------------------------------------------------------------
    // Works for Trigger OR Collision (use whichever you prefer)
    // OnTriggerEnter will still activate regardless of direction
    void OnTriggerEnter(Collider other)
    {
        // Orijinal davranış: yön ayırt etmeden fırlat
        TryLaunch(other.attachedRigidbody);
    }

    void OnCollisionEnter(Collision collision) // Changed 'other' to 'collision' for clarity
    {
        Rigidbody playerRb = collision.rigidbody; // Get rigidbody from Collision object

        if (playerRb == null || !playerRb.CompareTag(playerTag)) return;

        Vector3 averageNormal = Vector3.zero;
        foreach (ContactPoint contact in collision.contacts)
        {
            averageNormal += contact.normal;
        }
        averageNormal /= collision.contacts.Length;

        // İSTEDİĞİN GİBİ AYNI: < 0.5f (hiç dokunmadım)
        if (Vector3.Dot(averageNormal, transform.up) < 0.5f)
        {
            TryLaunch(playerRb);
        }
    }

    private void TryLaunch(Rigidbody playerRb)
    {
        if (playerRb == null || !playerRb.CompareTag(playerTag)) return;

        // 1) Tell the spring not to fight us
        Spring spring = playerRb.GetComponentInChildren<Spring>();
        if (spring != null) spring.enableSuspensionForce = false;

        // 2) Clear downward velocity and boost
        Vector3 vel = playerRb.linearVelocity; // linearVelocity == velocity (Unity 6)
        if (vel.y < 0f) vel.y = 0f;
        playerRb.linearVelocity = vel;
        playerRb.AddForce((useWorldUp ? Vector3.up : transform.up) * jumpForce, forceMode);

        // 3) Re-enable the suspension after a tiny delay
        if (spring != null)
            StartCoroutine(EnableSuspensionNextFixed(spring));

        // 4) FX (lokal)
        if (jumpSfx != null)
            AudioSource.PlayClipAtPoint(jumpSfx, transform.position);
        if (jumpVfx != null)
            Instantiate(jumpVfx, transform.position, Quaternion.identity);

        // 4-b) FX’i diğer client’lara yayınla (sadece FX)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
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
        ulong senderId = serverRpcParams.Receive.SenderClientId;

        if (NetworkManager == null) return;
        var ids = NetworkManager.ConnectedClientsIds;
        if (ids == null || ids.Count <= 1) return;

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
        if (jumpSfx != null)
            AudioSource.PlayClipAtPoint(jumpSfx, worldPos);

        if (jumpVfx != null)
            Instantiate(jumpVfx, worldPos, Quaternion.identity);
    }
}
