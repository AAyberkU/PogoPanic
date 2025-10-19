using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody), typeof(NetworkObject))]
public class Projectile : NetworkBehaviour
{
    private float lifeTime;
    private float timer;
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    // Server tarafından çağrılır, clientlere RPC atar
    public void Init(float life, Vector3 initialVelocity)
    {
        lifeTime = life;

        if (IsServer)
        {
#if UNITY_6000_0_OR_NEWER
            rb.linearVelocity = initialVelocity;
#else
            rb.velocity = initialVelocity;
#endif
            InitClientRpc(life, initialVelocity);
        }
    }

    [ClientRpc]
    void InitClientRpc(float life, Vector3 initialVelocity)
    {
        if (IsServer) return; // host zaten ayarladı
        lifeTime = life;
#if UNITY_6000_0_OR_NEWER
        rb.linearVelocity = initialVelocity;
#else
        rb.velocity = initialVelocity;
#endif
    }

    void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;
        if (timer >= lifeTime)
        {
            var no = GetComponent<NetworkObject>();
            if (no && no.IsSpawned)
                no.Despawn(true);
            else
                Destroy(gameObject);
        }
    }
}