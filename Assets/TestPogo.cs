using System;
using DG.Tweening;
using UnityEngine;

namespace RageRunGames.PogostickController
{
    public class TestPogo : MonoBehaviour
    {

        #region Serialized Fields

        public enum ControllerType
        {
            KeyPressedBased, // Only jumps when key is pressed
            Continuous // Automatically jumps with constant force when grounded
        }

        public enum ControllerPerspective
        {
            ThreeDimensional,
            TwoDimensional
        }

        [Header("Controller Mode")] [SerializeField]
        private ControllerPerspective controllerPerspective = ControllerPerspective.ThreeDimensional;

        [SerializeField] private ControllerType controllerType = ControllerType.KeyPressedBased;
        [SerializeField] private float continuousJumpForce = 15f;
        [SerializeField] private float continuousJumpDelay = 0.2f;
        private float continuousJumpTimer;
        private float previousConinuousTimer;

        [Header("Stunt Settings")] [SerializeField]
        private bool lockStuntOnXAxis;

        [SerializeField] private bool lockStuntOnYAxis;
        [SerializeField] private bool lockStuntOnZAxis;

        [Header("Controller Settings")] [SerializeField]
        private float autobalancingTimer = 0.15f;

        [SerializeField] private float duration = 0.15f;
        [SerializeField] private PogoStickControllerSettings settings;

        [Header("Physics & Gravity")] [SerializeField]
        private bool enableFallGravity = true;

        [SerializeField] private float fallGravity = 35f;
        [SerializeField] private float gravityLerpSpeed = 5f;

        [Header("Input Buffer")] [SerializeField]
        private float timeToJumpAfterBuffer = 0.1f;

        [Header("References")] [SerializeField]
        private Transform cog;

        [SerializeField] private Transform pogostickModelTransform;
        [SerializeField] private Transform characterModelTransform;
        [SerializeField] private Transform[] otherTransformsToMove;
        [SerializeField] private CapsuleCollider characterCollider;

        [Header("SFX & Effects")] [SerializeField]
        private PogoStickBounceEffectHandler[] pogoStickBounceEffectHandler;

        #endregion

        #region Private Variables

        // Core Components
        private Rigidbody rb;
        private Spring spring;

        // Input & State
        private float horizontalInput;
        private float verticalInput;
        private bool isJumpKeyPressed;
        private bool restrictControls;
        private bool isGrounded;
        private bool jumpBuffered;
        private float jumpBufferTimer;
        private float jumpTimer;
        private bool isDead;

        // Jump Forces
        private float accumulatedForce;
        private float currentAccumulatedForce;

        // Rotation
        private float xRotation;
        private float yRotation;
        private float zRotation;
        private Quaternion targetRotation;

        // Gravity
        private float currentGravity;

        // Auto-balance
        private bool isAutoBalancing;
        private bool allowedToJumpBasedOnCurrentAngle;

        // Audio & FX Triggers
        private bool runOnce;
        private bool playEffect;

        // Timing
        private float timer;

        // Transforms
        private float[] initialYPositions;
        private float initialPlayerCharacterTransformPosY;

        #endregion

        #region Properties

        public bool OnSlidingPlatform { get; set; }
        public bool HoldingJumpKey { get; set; }
        public bool IsPerformingStunt { get; set; }

        public float HorizontalInput => horizontalInput;
        public float VerticalInput => verticalInput;
        public bool IsJumpPressed => isJumpKeyPressed;
        public bool RestrictControls => restrictControls;
        public bool IsGrounded => isGrounded;
        public Rigidbody Rb => rb;
        public PogoStickControllerSettings PogoStickControllerSettings => settings;

        public Action OnReset;

        #endregion

        #region Nested Types

        [Serializable]
        public class PogoStickBounceEffectHandler
        {
            public int layer;
            public AudioClip clip;
            public bool ignoreImpactEffect;
            public ParticleSystem impactEffect;
            public Color impactColor;
        }

        #endregion

        #region Unity Lifecycle Methods

        private void Awake()
        {
            InitializeComponents();
            InitializeSettings();
            CacheInitialPositions();
        }

        private void Update()
        {
            if (restrictControls) return;

            UpdateJumpAngleCheck();
            HandleResetInput();
            HandleAllInputs();
            HandleAutoBalance();
            UpdateCharacterPosition();
        }

        private void FixedUpdate()
        {
            if (restrictControls) return;

            HandlePhysics();
            HandleVelocityClamping();
            HandleFallGravity();

            if (controllerPerspective == ControllerPerspective.TwoDimensional)
            {
                Quaternion currentRot = rb.rotation;
                Vector3 euler = currentRot.eulerAngles;
                euler.y = 90f;
                rb.MoveRotation(Quaternion.Euler(euler));
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (restrictControls) return;
            // Collision logic can be added here
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.gameObject.layer == 4) // Water layer
            {
                // Water entry logic
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.layer == 4) // Water layer
            {
                // Water exit logic
            }
        }

        #endregion

        #region Initialization Methods

        private void InitializeComponents()
        {
            spring = GetComponentInChildren<Spring>();
            rb = GetComponent<Rigidbody>();

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void InitializeSettings()
        {
            rb.mass = settings.rigidbodySettings.mass;
            rb.linearDamping = settings.rigidbodySettings.drag;
            rb.angularDamping = settings.rigidbodySettings.angularDrag;
            rb.collisionDetectionMode = settings.rigidbodySettings.collisionDetectionMode;
            rb.interpolation = settings.rigidbodySettings.interpolation;

            jumpBufferTimer = -1f;
        }

        private void CacheInitialPositions()
        {
            initialYPositions = new float[otherTransformsToMove.Length];
            for (int i = 0; i < otherTransformsToMove.Length; i++)
            {
                initialYPositions[i] = otherTransformsToMove[i].localPosition.y;
            }

            initialPlayerCharacterTransformPosY = characterModelTransform.GetChild(0).localPosition.y;
        }

        #endregion

        #region Input Handling Methods

        private void HandleAllInputs()
        {
            HandleMovementInput();
            HandleJumpInput();
            HandleRotationInput();
            HandleCharacterTilt();
        }

        private void HandleResetInput()
        {
            if (Input.GetMouseButtonDown(1))
            {
                HandleReset();
            }
        }

        private void HandleMovementInput()
        {
            horizontalInput = Input.GetAxis("Horizontal");
            verticalInput = Input.GetAxis("Vertical");
        }

        private void HandleJumpInput()
        {
            UpdateGroundedState();

            if (isGrounded && spring.isGrounded && controllerType == ControllerType.Continuous &&
                Mathf.Approximately(previousConinuousTimer, continuousJumpTimer) && Input.GetKeyDown(settings.jumpKey))
            {
                jumpBuffered = true;
                isJumpKeyPressed = true;
                continuousJumpTimer = continuousJumpDelay;
            }

            if (controllerType == ControllerType.KeyPressedBased)
            {
                HandleJumpKeyPress();
                HandleJumpKeyHold();
                HandleJumpKeyRelease();
            }
        }

        #endregion

        #region Jump Methods

        private void UpdateJumpAngleCheck()
        {
            allowedToJumpBasedOnCurrentAngle =
                (transform.eulerAngles.x > settings.allowedJumpingAngle &&
                 transform.eulerAngles.x < 360f - settings.allowedJumpingAngle) ||
                (transform.eulerAngles.z > settings.allowedJumpingAngle &&
                 transform.eulerAngles.z < 360f - settings.allowedJumpingAngle);
        }

        private void UpdateGroundedState()
        {
            if (!isGrounded && spring.isGrounded)
            {
                PlayBounceEffects();

                if (controllerType == ControllerType.Continuous)
                {
                    if (continuousJumpTimer <= 0)
                    {
                        jumpBuffered = true;
                        isJumpKeyPressed = true;
                        continuousJumpTimer = continuousJumpDelay;
                    }
                }
            }
            else if (!spring.isGrounded)
            {
                continuousJumpTimer -= Time.deltaTime;
                previousConinuousTimer = continuousJumpTimer;
            }

            isGrounded = spring.isGrounded;
            UpdateCenterOfMass();
            UpdateLinearDamping();
        }

        private void PlayBounceEffects()
        {
            if (!playEffect)
            {
                foreach (var effectHandler in pogoStickBounceEffectHandler)
                {
                    if (effectHandler.layer == spring.collidedLayer)
                    {
                        AudioSource.PlayClipAtPoint(effectHandler.clip, transform.position);

                        if (!effectHandler.ignoreImpactEffect)
                        {
                            var main = effectHandler.impactEffect.main;
                            main.startColor = effectHandler.impactColor;
                            effectHandler.impactEffect.Play();
                        }
                    }
                }

                playEffect = true;
            }
        }

        private void UpdateCenterOfMass()
        {
            rb.centerOfMass = isGrounded && !isAutoBalancing && !isDead
                ? settings.rigidbodySettings.centerOfMassOnGround
                : settings.rigidbodySettings.centerOfMassInAir;
        }

        private void UpdateLinearDamping()
        {
            if (isGrounded && !isAutoBalancing && !isDead)
            {
                if (!isJumpKeyPressed && !jumpBuffered && jumpBufferTimer <= 0 && !OnSlidingPlatform)
                {
                    rb.linearDamping = settings.linearDampingOnGround;
                }
            }
            else
            {
                rb.linearDamping = settings.linearDampingInAir;
                HandleStuntLockSettings();
            }
        }

        private void HandleJumpKeyPress()
        {
            if (Input.GetKeyDown(settings.jumpKey))
            {
                if (isGrounded)
                {
                    spring.enableSuspensionForce = false;
                }
                else
                {
                    jumpBufferTimer = settings.jumpBufferTimeMax;
                    jumpTimer = timeToJumpAfterBuffer;
                }

                OnSlidingPlatform = false;
                rb.linearDamping = settings.linearDampingInAir;
                HoldingJumpKey = true;
            }
        }

        private void HandleJumpKeyHold()
        {
            if (jumpBufferTimer > 0)
            {
                jumpBufferTimer -= Time.deltaTime;

                if (isGrounded)
                {
                    HoldingJumpKey = true;
                    HandleJumpBuffer();
                }
            }

            if (Input.GetKey(settings.jumpKey) && HoldingJumpKey && !isJumpKeyPressed)
            {
                AccumulateJumpForce();
                UpdateCharacterPositionsDuringJumpAccumulation();
            }
        }

        private void HandleJumpBuffer()
        {
            if (!runOnce)
            {
                runOnce = true;
                characterModelTransform.GetChild(0).DOLocalMoveY(-0.2f, 0.125f);

                for (int i = 0; i < otherTransformsToMove.Length; i++)
                {
                    otherTransformsToMove[i].DOLocalMoveY(initialYPositions[i] - 0.125f, duration);
                }
            }

            jumpTimer -= Time.deltaTime;

            if (jumpTimer <= 0f)
            {
                runOnce = false;
                jumpBuffered = true;
                spring.enableSuspensionForce = true;
                isJumpKeyPressed = true;
                HoldingJumpKey = false;
                jumpBufferTimer = -1f;
            }
        }

        private void AccumulateJumpForce()
        {
            currentAccumulatedForce += Time.deltaTime * settings.accumulatedForceSpeed;
            currentAccumulatedForce = Mathf.Clamp(currentAccumulatedForce, 0f, settings.maxAccumulatedForce);
            accumulatedForce = Mathf.Max(settings.minJumpForce, currentAccumulatedForce);
        }

        private void UpdateCharacterPositionsDuringJumpAccumulation()
        {
            if (isGrounded)
            {
                var currentCharacterPos = characterModelTransform.GetChild(0).localPosition;
                currentCharacterPos.y = Mathf.Lerp(
                    currentCharacterPos.y,
                    -0.4f,
                    currentAccumulatedForce / settings.maxAccumulatedForce
                );
                characterModelTransform.GetChild(0).localPosition = currentCharacterPos;

                for (int i = 0; i < otherTransformsToMove.Length; i++)
                {
                    var otherTransformPosition = otherTransformsToMove[i].localPosition;
                    otherTransformPosition.y = Mathf.Lerp(
                        otherTransformPosition.y,
                        initialYPositions[i] - 0.15f,
                        currentAccumulatedForce / settings.maxAccumulatedForce
                    );
                    otherTransformsToMove[i].localPosition = otherTransformPosition;
                }
            }
        }

        private void HandleJumpKeyRelease()
        {
            if (Input.GetKeyUp(settings.jumpKey) && isGrounded && HoldingJumpKey)
            {
                spring.enableSuspensionForce = true;
                isJumpKeyPressed = true;
                HoldingJumpKey = false;
            }
        }

        private void ApplyJump(float forceMultiplier)
        {
            for (int i = 0; i < otherTransformsToMove.Length; i++)
            {
                otherTransformsToMove[i].DOLocalMoveY(initialYPositions[i], duration);
            }

            isJumpKeyPressed = false;
            accumulatedForce = 0f;
            currentAccumulatedForce = 0f;
            isGrounded = false;
            spring.isGrounded = false;

            var upVector = transform.up;
            spring.ApplyForceAtSuspensionPoint(settings.upwardForceMultiplier * forceMultiplier * upVector);

            HandleStuntLockSettings();
        }

        #endregion

        #region Physics Methods

        private void HandlePhysics()
        {
            HandleJumpPhysics();
            HandleTorqueRotation();
        }

        private void HandleJumpPhysics()
        {
            if (isJumpKeyPressed)
            {
                jumpBuffered = false;
                float force = controllerType == ControllerType.Continuous
                    ? continuousJumpForce
                    : accumulatedForce;

                ApplyJump(force);
                OnReset?.Invoke();
            }

            if (!isGrounded)
            {
                ApplyAirMovement();
            }
        }

        private void ApplyAirMovement()
        {
            Vector3 localForward = transform.forward;
            Vector3 localRight = transform.right;

            bool isUpsideDown = Vector3.Dot(transform.up, Vector3.up) < 0;

            if (isUpsideDown)
            {
                localForward = -localForward;
                localRight = -localRight;
            }

            localForward.y = 0f;
            localRight.y = 0f;

            localForward.Normalize();
            localRight.Normalize();

            rb.AddForce(localForward * settings.forwardForceMultiplier * verticalInput, ForceMode.Force);
            rb.AddForce(localRight * settings.forwardForceMultiplier * horizontalInput, ForceMode.Force);
        }

        private void HandleVelocityClamping()
        {
            if (rb.linearVelocity.magnitude > settings.clampedVelocity)
            {
                rb.linearVelocity = rb.linearVelocity.normalized * settings.clampedVelocity;
            }
        }

        private void HandleFallGravity()
        {
            if (!enableFallGravity) return;

            if (!isGrounded)
            {
                currentGravity = Mathf.Lerp(currentGravity, fallGravity, Time.fixedDeltaTime * gravityLerpSpeed);
                rb.AddForce(Vector3.down * currentGravity, ForceMode.Acceleration);
            }
            else if (!Mathf.Approximately(currentGravity, 0))
            {
                currentGravity = 0f;
            }
        }

        #endregion

        #region Rotation Methods

        private void HandleRotationInput()
        {
            if (isGrounded)
            {
                HandleGroundedRotation();
            }
            else
            {
                playEffect = false;
            }
        }

        private void HandleGroundedRotation()
        {
            xRotation = Mathf.Lerp(
                xRotation,
                xRotation + (verticalInput * settings.pitchSpeed),
                Time.deltaTime * 5f
            );


            if (controllerPerspective != ControllerPerspective.TwoDimensional)
            {
                yRotation = Mathf.LerpAngle(
                    yRotation,
                    Camera.main.transform.eulerAngles.y,
                    Time.deltaTime * 20
                );

                zRotation = Mathf.Lerp(
                    zRotation,
                    zRotation + (-horizontalInput * settings.rollSpeed),
                    Time.deltaTime * 5f
                );
            }



            targetRotation = Quaternion.Euler(xRotation, yRotation, zRotation);

            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                Time.fixedDeltaTime * settings.rotationSpeed
            ));
        }

        private void HandleTorqueRotation()
        {
            if (!isGrounded && settings.useStunts)
            {
                ApplyStuntTorque();
                CacheCurrentRotation();
            }
        }

        private void ApplyStuntTorque()
        {
            Vector3 torque = new Vector3(
                verticalInput * settings.airStuntTorqueXZ,
                controllerPerspective == ControllerPerspective.TwoDimensional
                    ? 0f
                    : Input.GetAxis("Mouse X") * settings.airStuntTorqueY,
                controllerPerspective == ControllerPerspective.TwoDimensional
                    ? 0f
                    : -horizontalInput * settings.airStuntTorqueXZ
            );

            rb.AddRelativeTorque(torque, ForceMode.Force);
        }

        private void CacheCurrentRotation()
        {
            xRotation = transform.eulerAngles.x;
            yRotation = transform.eulerAngles.y;
            zRotation = transform.eulerAngles.z;
        }

        private void HandleCharacterTilt()
        {
            if (IsPerformingStunt) return;

            float verticalVelocity = rb.linearVelocity.y;

            UpdatePogostickTilt(verticalVelocity);
            UpdateCharacterTilt(verticalVelocity);
        }

        private void UpdatePogostickTilt(float verticalVelocity)
        {
            float targetAngle = Mathf.Clamp(
                verticalVelocity * settings.maxTiltAngle,
                settings.minTiltAngle,
                settings.maxTiltAngle
            );

            Quaternion targetRot = Quaternion.Euler(targetAngle, 0f, 0f);
            pogostickModelTransform.localRotation = Quaternion.Slerp(
                pogostickModelTransform.localRotation,
                targetRot,
                settings.tiltSmoothness * Time.deltaTime
            );
        }

        private void UpdateCharacterTilt(float verticalVelocity)
        {
            float characterTiltAngle = Mathf.Clamp(
                Mathf.Abs(verticalVelocity) * -settings.characterMaxTiltAngle,
                -settings.characterMaxTiltAngle,
                settings.characterMaxTiltAngle
            );

            Quaternion characterTargetRot = Quaternion.Euler(characterTiltAngle, 0f, 0f);
            characterModelTransform.localRotation = Quaternion.Slerp(
                characterModelTransform.localRotation,
                characterTargetRot,
                settings.tiltSmoothness * Time.deltaTime
            );
        }

        #endregion

        #region Auto-balance Methods

        private void HandleAutoBalance()
        {
            HandleAutoBalanceInput();
            UpdateAutoBalanceTimer();
        }

        private void HandleAutoBalanceInput()
        {
            if (Input.GetKeyDown(KeyCode.R) && allowedToJumpBasedOnCurrentAngle && !isAutoBalancing)
            {
                StartAutoBalance();
            }
        }

        private void StartAutoBalance()
        {
            isAutoBalancing = true;
            timer = autobalancingTimer;

            transform.DORotateQuaternion(
                Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f),
                0.75f
            ).OnComplete(() =>
            {
                xRotation = 0f;
                zRotation = 0f;
                isAutoBalancing = false;
            });
        }

        private void UpdateAutoBalanceTimer()
        {
            if (timer > 0)
            {
                timer -= Time.deltaTime;
                if (timer <= 0) isAutoBalancing = false;
            }
        }

        #endregion

        #region Character Position Methods

        private void UpdateCharacterPosition()
        {
            var playerCharacterTransform = characterModelTransform.GetChild(0).localPosition;

            playerCharacterTransform.y = isGrounded
                ? Mathf.Lerp(
                    playerCharacterTransform.y,
                    initialPlayerCharacterTransformPosY,
                    Time.deltaTime * 5f
                )
                : Mathf.Lerp(
                    playerCharacterTransform.y,
                    rb.linearVelocity.y * 0.0125f,
                    Time.deltaTime * 5f
                );

            characterModelTransform.GetChild(0).localPosition = playerCharacterTransform;
        }

        #endregion

        #region Utility Methods

        public void HandleReset()
        {
            HoldingJumpKey = false;
            isJumpKeyPressed = false;
            jumpBuffered = false;
            jumpBufferTimer = -1f;
            spring.enableSuspensionForce = true;
            currentAccumulatedForce = 0f;
            accumulatedForce = 0f;
            OnReset?.Invoke();
        }

        private void HandleStuntLockSettings()
        {
            rb.freezeRotation = false;

            if (controllerPerspective == ControllerPerspective.TwoDimensional)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationY | RigidbodyConstraints.FreezeRotationZ;
            }
            else
            {
                if (lockStuntOnYAxis)
                {
                    rb.constraints |= RigidbodyConstraints.FreezeRotationY;
                }

                if (lockStuntOnXAxis)
                {
                    rb.constraints |= RigidbodyConstraints.FreezeRotationX;
                }

                if (lockStuntOnZAxis)
                {
                    rb.constraints |= RigidbodyConstraints.FreezeRotationZ;
                }
            }
        }


        public float GetNormalizedAccumulatedJump()
        {
            return currentAccumulatedForce / settings.maxAccumulatedForce;
        }

        #endregion


    }
}