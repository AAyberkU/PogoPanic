using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Child collider'ların fizik event'lerini HİYERARŞİDEKİ
/// EN YAKIN NetworkObject barındıran parent GameObject'e forward eder.
/// (RotatorPlatform / MovingPlatform / RotatingHazard parent'ta kaldığı için
/// OnCollision*/OnTrigger* event'leri güvenle parent'a taşınır.)
/// </summary>
[DisallowMultipleComponent]
public class PhysicsRelayToParent : MonoBehaviour
{
    [Tooltip("Event'lerin iletileceği hedef. Boş bırakılırsa, en yakın NetworkObject barındıran parent otomatik bulunur.")]
    public GameObject target;

    public enum ForwardMode { Auto, CollisionOnly, TriggerOnly }

    [Tooltip("Auto: hem collision hem trigger iletir. İstersen tek tipe sınırlayabilirsin.")]
    public ForwardMode mode = ForwardMode.Auto;

    // ------------------------------------------------------------
    // Target çözümleme
    // ------------------------------------------------------------
    private void Reset()
    {
        AutoResolveTarget();
    }

    private void Awake()
    {
        if (target == null) AutoResolveTarget();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying && target == null)
            AutoResolveTarget();
    }
#endif

    private void AutoResolveTarget()
    {
        // 1) Yukarı doğru en yakın NetworkObject barındıran parent'ı ara
        Transform t = transform.parent;
        while (t != null)
        {
            if (t.GetComponent<NetworkObject>() != null)
            {
                target = t.gameObject;
                return;
            }
            t = t.parent;
        }

        // 2) Bulunamazsa, bir üst parent; o da yoksa kendisi
        target = transform.parent ? transform.parent.gameObject : gameObject;
    }

    // ------------------------------------------------------------
    // Collision forward
    // ------------------------------------------------------------
    private void OnCollisionEnter(Collision c)
    {
        if (!CanForwardCollision()) return;
        EnsureTarget();
        target.SendMessage("OnCollisionEnter", c, SendMessageOptions.DontRequireReceiver);
    }

    private void OnCollisionStay(Collision c)
    {
        if (!CanForwardCollision()) return;
        EnsureTarget();
        target.SendMessage("OnCollisionStay", c, SendMessageOptions.DontRequireReceiver);
    }

    private void OnCollisionExit(Collision c)
    {
        if (!CanForwardCollision()) return;
        EnsureTarget();
        target.SendMessage("OnCollisionExit", c, SendMessageOptions.DontRequireReceiver);
    }

    // ------------------------------------------------------------
    // Trigger forward
    // ------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        if (!CanForwardTrigger()) return;
        EnsureTarget();
        target.SendMessage("OnTriggerEnter", other, SendMessageOptions.DontRequireReceiver);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!CanForwardTrigger()) return;
        EnsureTarget();
        target.SendMessage("OnTriggerStay", other, SendMessageOptions.DontRequireReceiver);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!CanForwardTrigger()) return;
        EnsureTarget();
        target.SendMessage("OnTriggerExit", other, SendMessageOptions.DontRequireReceiver);
    }

    // ------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------
    private bool CanForwardCollision() => mode != ForwardMode.TriggerOnly;
    private bool CanForwardTrigger()   => mode != ForwardMode.CollisionOnly;

    private void EnsureTarget()
    {
        if (target == null) AutoResolveTarget();
    }
}
