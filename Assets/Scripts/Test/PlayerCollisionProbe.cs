using UnityEngine;

public class PlayerCollisionProbe : MonoBehaviour
{
    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
    }

    void OnCollisionEnter(Collision c)
    {
        Debug.Log($"[PLAYER PROBE] ENTER -> {c.collider.name}  trigger={c.collider.isTrigger}  otherRB={(c.rigidbody ? (c.rigidbody.isKinematic ? "Kinematic" : "Dynamic") : "null")}  contacts={c.contactCount}");
    }

    void OnCollisionStay(Collision c)
    {
        if (c.contactCount > 0)
        {
            var n = c.GetContact(0).normal;
            float dot = Vector3.Dot(n, Vector3.up);
            Debug.Log($"[PLAYER PROBE] STAY -> {c.collider.name}  firstNormalUpDot={dot:F2}  vel={rb.linearVelocity}");
        }
    }

    void OnCollisionExit(Collision c)
    {
        Debug.Log($"[PLAYER PROBE] EXIT -> {c.collider.name}");
    }
}