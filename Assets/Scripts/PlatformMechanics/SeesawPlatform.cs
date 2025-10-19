using UnityEngine;

/// <summary>
/// Turns any thin plank into a playground-style seesaw:
///  • Adds a HingeJoint at runtime (or re-uses one if you already placed it)
///  • Lets the board tilt around its local Z axis (like a real seesaw)
///  • Springs gently back to horizontal when nobody is on it
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class SeesawPlatform : MonoBehaviour
{
    [Header("Joint Settings")]
    [Tooltip("Degrees the board is allowed to tilt each way.")]
    [SerializeField] private float maxAngle = 30f;

    [Tooltip("Strength of the spring that returns the board to 0°.")]
    [SerializeField] private float spring = 20f;

    [Tooltip("How quickly the spring motion is damped.")]
    [SerializeField] private float damper = 2f;

    [Header("Pivot")]
    [Tooltip("Leave empty to pivot around this object’s own origin, " +
             "or drop in another Rigidbody (e.g. a post) to act as the fulcrum.")]
    [SerializeField] private Rigidbody pivotRb = null;

    //--------------------------------------------------------------------
    private void Awake()
    {
        SetupRigidbodies();
        SetupHingeJoint();
    }

    //--------------------------------------------------------------------
    private void SetupRigidbodies()
    {
        // Board should be dynamic
        Rigidbody boardRb = GetComponent<Rigidbody>();
        boardRb.mass = 5f;                 // tweak as you like
        boardRb.interpolation = RigidbodyInterpolation.Interpolate;

        // If user gave no pivot, create a hidden one in place
        if (pivotRb == null)
        {
            GameObject pivot = new GameObject("SeesawPivot");
            pivot.transform.position = transform.position;
            pivot.transform.rotation = transform.rotation;
            pivotRb = pivot.AddComponent<Rigidbody>();
            pivotRb.isKinematic = true;    // anchor
        }
    }

    //--------------------------------------------------------------------
    private void SetupHingeJoint()
    {
        HingeJoint hinge = GetComponent<HingeJoint>();
        if (hinge == null) hinge = gameObject.AddComponent<HingeJoint>();

        hinge.connectedBody = pivotRb;
        hinge.axis          = Vector3.forward;   // tilt around local Z (X for side-to-side)

        // Angle limits
        JointLimits limits  = hinge.limits;
        limits.min          = -maxAngle;
        limits.max          =  maxAngle;
        hinge.limits        = limits;
        hinge.useLimits     = true;

        // Spring that pulls the seesaw back to level
        JointSpring js      = hinge.spring;
        js.spring           = spring;
        js.damper           = damper;
        js.targetPosition   = 0f;                // aim for horizontal
        hinge.spring        = js;
        hinge.useSpring     = true;
    }
}
