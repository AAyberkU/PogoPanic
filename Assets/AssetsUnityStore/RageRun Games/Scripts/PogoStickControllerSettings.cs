using System;
using UnityEngine;
using UnityEngine.Serialization;


namespace RageRunGames.PogostickController
{

    [CreateAssetMenu(fileName = "ControllerSettings", menuName = "ScriptableObjects/ControllerSettings", order = 1)]
    public class PogoStickControllerSettings : ScriptableObject
    {
        // ========================== JUMP SETTINGS ==========================
        [Header("Jump Settings")] public float upwardForceMultiplier = 400f;
        public float forwardForceMultiplier = 600f;
        public float maxAccumulatedForce = 3.25f;
        public float accumulatedForceSpeed = 6f;
        public float jumpBufferTimeMax = 0.75f;
        public float minJumpForce = 2f;
        public float clampedVelocity = 30f;
        public float linearDampingOnGround = 5f;
        public float linearDampingInAir = 2.5f;
        public float allowedJumpingAngle = 75f;

        // ========================== TILT SETTINGS ==========================
        [Header("Tilt Settings")] public float maxTiltAngle = 15f; // Maximum tilt angle in degrees
        public float minTiltAngle = 0f;
        public float tiltSmoothness = 5f; // Smoothness of the tilt transition
        public float characterMaxTiltAngle = 5f;

        // ========================== ROTATION SETTINGS ==========================
        [Header("Rotation Settings")] 
        public bool enableRotationLimits;
        public float pitchLimit = 45f;
        public float rollLimit = 45f;
        public float rotationSpeed = 10f;
        public float pitchSpeed = 25f;
        public float rollSpeed = 25f;
        

        // ========================== TORQUE & STUNT SETTINGS ==========================
        [Header("Torque Settings")] public bool useStunts = true;

        [FormerlySerializedAs("airStuntTorque")]
        public float airStuntTorqueXZ = 260f;

        public float airStuntTorqueY = 4f;

        // ========================== INPUT & CONTROL SETTINGS ==========================
        [Header("Input & Control")] public KeyCode jumpKey = KeyCode.Space;

        // ========================== RIGIDBODY SETTINGS ==========================
        [Header("Rigidbody Settings")] public RigidbodySettings rigidbodySettings;

        // ========================== IK SETTINGS ==========================
        [Header("IK Settings")] public IKSettings ikSettings;

        // ========================== SUSPENSION SETTINGS ==========================
        [Header("Suspension Settings")] public SuspensionSettings suspensionSettings;
        
        // INSIDE PogoStickControllerSettings
        [Header("Jetpack Settings")] public JetpackSettings jetpackSettings = new JetpackSettings();
    }

    [Serializable]
    public class RigidbodySettings
    {
        public float mass = 65f;
        public float drag = 2.5f;
        public float angularDrag = 2.5f;
        public CollisionDetectionMode collisionDetectionMode = CollisionDetectionMode.Continuous;
        public RigidbodyInterpolation interpolation = RigidbodyInterpolation.Interpolate;

        public Vector3 centerOfMassInAir = new Vector3(0, 0.957000017f, -0.349000007f);
        public Vector3 centerOfMassOnGround = Vector3.zero;
    }

    [Serializable]
    public class IKSettings
    {
        public Vector3 spineTargetMultiplier = new Vector3(30f, 10f, 0);
        public Vector3 headTargetMultiplier = new Vector3(10, 5f, 0);

        public float smoothLerpSpeed = 10f;
        public float rigidBodyWeight = 0.5f;
        public float rigidBodySmoothTime = 2f;
    }

    [Serializable]
    public class SuspensionSettings
    {
        public float pogoBottomSize = 0.3f;
        public float suspensionLength = 0.58f;
        public float suspensionStiffness = 20000f;
        public float damping = 200f;

    }
    
    [Serializable]
     public class JetpackSettings
     {
         [Header("Jetpack")]
         public float jetpackForce   = 650f;   // impulse applied every FixedUpdate
         public float maxFuel        = 3f;     // seconds of thrust when full
         public float burnRate       = 1f;     // fuel units consumed / sec while firing
         public float regenRate      = 0.35f;  // fuel units regained / sec while grounded
     }
}