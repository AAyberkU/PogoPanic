using UnityEngine;
using Unity.Netcode;

/// <summary>
/// RotatingHazard (ServerTime-synced, client-auth physics)
///  • Kökte sabit, yalnız döner (NetworkTransform yok)
///  • Dönüş herkeste ServerTime'a göre deterministik hesaplanır
///  • Çarpışmada itme kuvveti owner client tarafından lokal uygulanır
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class RotatingHazard : NetworkBehaviour
{
    public enum Axis { X, Y, Z }

    [Header("Rotation")]
    [SerializeField] private Axis  axis           = Axis.Y;
    [SerializeField] private float speedDegPerSec = 90f;
    [SerializeField] private bool  clockwise      = true;

    [Header("Hit Impulse")]
    [SerializeField] private float     hitForce    = 15f;
    [SerializeField] private float     upwardForce = 5f;
    [SerializeField] private ForceMode forceMode   = ForceMode.Impulse;

    [Header("Filtering")]
    [SerializeField] private string playerTag = "Player";

    // internals -----------------------------------------------------------
    private Rigidbody rb;               // kendi RB (kinematic, yerinde)
    private Vector3   axisVector;
    private float     dirSign;
    private Quaternion initialRot;
    private double    t0;               // faz başlangıcı (server-time)
    private bool      initialized;

    // --------------------------------------------------------------------
    public override void OnNetworkSpawn()
    {
        SetupIfNeeded();

        // Herkes aynı fazdan başlasın
        t0 = GetServerTime();
    }

    void Awake()
    {
        // Editor'de play-in-editor/local testte de çalışsın diye
        // (Host olmadan da düzgün ayarlansın)
        SetupIfNeeded();
    }

    private void SetupIfNeeded()
    {
        if (initialized) return;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic   = true;  // fizik kuvvetlerinden etkilenmesin
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.constraints   = RigidbodyConstraints.FreezePositionX
                         | RigidbodyConstraints.FreezePositionY
                         | RigidbodyConstraints.FreezePositionZ; // asla translasyon yok

        var col = GetComponent<Collider>();
        if (col) col.isTrigger = false;    // varsa solid; yoksa child collider’lar iş görür

        axisVector = (axis == Axis.X) ? Vector3.right
                   : (axis == Axis.Y) ? Vector3.up
                                      : Vector3.forward;

        // Sağ el kuralı: saat yönünde görünmesi için -1
        dirSign = clockwise ? -1f : 1f;

        initialRot = transform.rotation;

        initialized = true;
    }

    // --------------------------------------------------------------------
    void Update()
    {
        // İnkremental biriktirme yok; mutlak zaman -> mutlak açı
        double t = GetServerTime();
        float angle = (float)(dirSign * speedDegPerSec * (t - t0));

        // Transform üzerinden döndür (NetworkTransform kullanılmıyor)
        transform.rotation = initialRot * Quaternion.AngleAxis(angle, axisVector);
    }

    // --------------------------------------------------------------------
    void OnCollisionEnter(Collision col)
    {
        Rigidbody other = col.rigidbody;
        if (other == null) return;

        // Sadece hedef tag
        if (!other.CompareTag(playerTag)) return;

        // Client-auth: Yalnızca owner kendi rigidbody'sine kuvvet uygular
        // (Diğer clientlarda o rigidbody ya kinematic ya da authority yok)
        var no = other.GetComponent<NetworkObject>() ?? other.GetComponentInParent<NetworkObject>();
        if (no != null && !no.IsOwner) return;

        // Tanjant yönü = ω × r mantığı
        Vector3 radial = other.position - transform.position;
        radial -= Vector3.Project(radial, axisVector);               // eksen düzlemine indir
        if (radial.sqrMagnitude < 1e-6f) radial = transform.forward; // emniyet

        Vector3 tangent = Vector3.Cross(dirSign * axisVector, radial).normalized;
        Vector3 impulse = tangent * hitForce + Vector3.up * upwardForce;

        other.AddForce(impulse, forceMode);
    }

    // --------------------------------------------------------------------
    private static double GetServerTime()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsConnectedClient)
            return nm.ServerTime.Time; // server clock
        else
            return Time.time;          // offline/standalone fallback
    }
}
