using UnityEngine;
using Unity.Netcode;
using System.Collections;

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
            // YENİ: ilk fizik adımına kadar interpolation kapat
            StartInterpolationFix();

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
        // YENİ: client instance'ında da aynı tek-frame fix
        StartInterpolationFix();
    }

    // --- YENİ: tek-frame interpolation kapatma/açma yardımcıları ---
    private void StartInterpolationFix()
    {
        var prev = rb.interpolation;
        rb.interpolation = RigidbodyInterpolation.None;
        StartCoroutine(RestoreInterpolationNextFixed(prev));
    }

    private IEnumerator RestoreInterpolationNextFixed(RigidbodyInterpolation restoreTo)
    {
        yield return new WaitForFixedUpdate(); // ilk physics step
        rb.interpolation = restoreTo;
    }
    // ----------------------------------------------------------------

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
