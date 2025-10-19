using UnityEngine;
using Unity.Netcode;

public class Thrower : NetworkBehaviour
{
    public enum AxisDirection { XPlus, XMinus, YPlus, YMinus, ZPlus, ZMinus }

    [Header("Projectile Prefab (root: Rigidbody + NetworkObject + Projectile)")]
    public Rigidbody projectilePrefab;   // Orijinaldeki gibi Rigidbody

    [Header("Spawn Settings")]
    public Transform spawnPoint;
    public AxisDirection throwDirection = AxisDirection.ZPlus;
    public float launchForce = 15f;
    public float fireInterval = 1f;

    [Header("Per-Shot Data")]
    public float projectileLifeTime = 5f;

    private double nextFireServerTime;

    void Awake()
    {
        if (!spawnPoint) spawnPoint = transform;
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
            nextFireServerTime = NetworkManager.ServerTime.Time;
    }

    void Update()
    {
        if (!IsServer) return;

        double now = NetworkManager.ServerTime.Time;
        if (now >= nextFireServerTime)
        {
            Shoot();
            nextFireServerTime += fireInterval;
        }
    }

    void Shoot()
    {
        Vector3 dir = GetDirectionVector();
        Rigidbody rb = Instantiate(projectilePrefab, spawnPoint.position, Quaternion.identity);

        var no   = rb.GetComponent<NetworkObject>();
        var proj = rb.GetComponent<Projectile>();

        if (!no || !proj)
        {
            Debug.LogError("[Thrower] Prefab’ta NetworkObject + Projectile yok!");
            Destroy(rb.gameObject);
            return;
        }

        no.Spawn();
        proj.Init(projectileLifeTime, dir * launchForce); // hız + lifetime birlikte set ediliyor
    }

    Vector3 GetDirectionVector()
    {
        switch (throwDirection)
        {
            case AxisDirection.XPlus:  return spawnPoint.right;
            case AxisDirection.XMinus: return -spawnPoint.right;
            case AxisDirection.YPlus:  return spawnPoint.up;
            case AxisDirection.YMinus: return -spawnPoint.up;
            case AxisDirection.ZPlus:  return spawnPoint.forward;
            case AxisDirection.ZMinus: return -spawnPoint.forward;
            default:                   return spawnPoint.forward;
        }
    }
}
