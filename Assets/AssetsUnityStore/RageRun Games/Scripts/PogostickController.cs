using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.Netcode;   // ★ NEW

namespace RageRunGames.PogostickController
{
    [RequireComponent(typeof(NetworkObject))] // ★ NEW
    public class PogostickController : NetworkBehaviour   // ★ CHANGED
    {
        #region Serialized Fields
        [Header("Stunt Settings")] 
        [SerializeField] private bool lockStuntOnXAxis;
        [SerializeField] private bool lockStuntOnYAxis;
        [SerializeField] private bool lockStuntOnZAxis;

        [Header("Controller Settings")] 
        [SerializeField] private float autobalancingTimer = 0.15f;
        [SerializeField] private float duration = 0.15f;
        [SerializeField] private PogoStickControllerSettings settings;

        [Header("Physics & Gravity")] 
        [SerializeField] private bool enableFallGravity = true;
        [SerializeField] private float fallGravity = 35f;
        [SerializeField] private float gravityLerpSpeed = 5f;

        [Header("Input Buffer")] 
        [SerializeField] private float timeToJumpAfterBuffer = 0.1f;

        [Header("References")] 
        [SerializeField] private Transform cog;
        [SerializeField] private Transform pogostickModelTransform;
        [SerializeField] private Transform characterModelTransform;
        [SerializeField] private Transform[] otherTransformsToMove;
        [SerializeField] private CapsuleCollider characterCollider;

        [Header("SFX & Effects")] 
        [SerializeField] private PogoStickBounceEffectHandler[] pogoStickBounceEffectHandler;
        [SerializeField] private AudioClip jumpSound;
        [SerializeField] private AudioSource jumpSource;
        [SerializeField] private AudioSource jetpackAudio;
        [SerializeField] private AudioClip   jetpackClip;
        [SerializeField] private ParticleSystem jetpackFx;
        [SerializeField] private KeyCode jetpackKey = KeyCode.LeftShift;
        
        // Suspension (yay) görselini aynalamak için
        [SerializeField] private Transform suspensionTarget;
        [SerializeField] private float suspensionSendThreshold = 0.0005f;
        [SerializeField] private float suspensionSmoothLerp = 25f;

        private NetworkVariable<float> nvSuspensionPosY = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private float _suspLastSentY;


        [Header("Air-Rotation")]
        public bool  alignYawInAir = true;
        public float airYawLerpSpeed = 6f;

        [Header("Hit Effect")]
        [SerializeField] private ParticleSystem hitEffect;
        private Coroutine hitEffectCoroutine;

        private float jetpackFuel;
        private bool  jetpackActive;

        // Chargelanma şiddetini (owner yazar) herkes okusun
        private NetworkVariable<float> nvAccumulatedForce = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Jetpack state'i networkte tutmak için
        private NetworkVariable<bool> nvJetpackActive = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


        [SerializeField] private float holdJumpDuration = 1f;
        private float groundHoldTimer;          
        #endregion

        #region Private Variables
        private Rigidbody rb;
        private Spring spring;

        private float horizontalInput;
        private float verticalInput;
        private bool isJumpKeyPressed;
        private bool restrictControls;
        private bool isGrounded;
        private bool jumpBuffered;
        private float jumpBufferTimer;
        private float jumpTimer;
        private bool isDead;

        private float accumulatedForce;
        private float currentAccumulatedForce;

        private float xRotation;
        private float yRotation;
        private float zRotation;
        private Quaternion targetRotation;

        private float currentGravity;

        private bool isAutoBalancing;
        private bool allowedToJumpBasedOnCurrentAngle;

        private bool runOnce;
        private bool playEffect;

        private float timer;

        private float[] initialYPositions;
        private float initialPlayerCharacterTransformPosY;

        private RotatorPlatform currentPlatform;
        private const float detachAirGap = 0.05f;

        // ★★★ NEW: Fixed için hedef rotasyon cache + bastırma bayrağı
        private Quaternion _groundedTargetRot;
        private bool _hasGroundedTarget;
        private bool _suppressGroundedRotation;
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
        bool IsDoingFlip => Mathf.Abs(verticalInput) > 0.01f || Mathf.Abs(horizontalInput) > 0.01f;
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

        private void PlayJumpSound()
        {
            if (jumpSource != null && jumpSound != null)
                jumpSource.PlayOneShot(jumpSound);
        }
        #endregion

        #region Unity / Netcode Lifecycle
        private void Awake()
        {
            InitializeComponents();
            InitializeSettings();
            CacheInitialPositions();

            jetpackFuel = settings.jetpackSettings.maxFuel;   // start full
            if (suspensionTarget) _suspLastSentY = suspensionTarget.localPosition.y;
        }

        public override void OnNetworkSpawn() // ★ NEW
        {
            base.OnNetworkSpawn();
            ApplyAuthorityState(IsOwner);
            // Cursor kilitleme yalnızca owner’da
            if (IsOwner)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
           
            // Chargelanma değerindeki değişimleri proxy'lerde uygula
            nvAccumulatedForce.OnValueChanged += (oldV, newV) =>
            {
                if (IsOwner) return;  // owner zaten local uygular
                ApplyChargeVisual(newV);
            };
            
            // Jetpack state değişince diğer client'larda efektleri uygula
            nvJetpackActive.OnValueChanged += (oldV, newV) =>
            {
                if (IsOwner) return; // Owner kendi local efektini zaten oynatıyor
                ApplyJetpackFxSfx(newV);
            };

            // Proxy spawn olduğunda başlangıç durumu uygula
            if (!IsOwner)
                ApplyJetpackFxSfx(nvJetpackActive.Value);

            // Suspension posY değişince proxy'de uygula
            nvSuspensionPosY.OnValueChanged += (oldV, newV) =>
            {
                if (IsOwner || suspensionTarget == null) return;
                // anlık set yerine hafif yumuşatarak uygularız (Update'te de akacak)
                var lp = suspensionTarget.localPosition;
                lp.y = newV;
                suspensionTarget.localPosition = lp;
            };

            // Proxy ilk spawn olduğunda mevcut değeri uygula
            if (!IsOwner && suspensionTarget)
            {
                var lp = suspensionTarget.localPosition;
                lp.y = nvSuspensionPosY.Value;
                suspensionTarget.localPosition = lp;
            }

        }

        public override void OnGainedOwnership() // ★ NEW
        {
            base.OnGainedOwnership();
            ApplyAuthorityState(true);
        }

        public override void OnLostOwnership() // ★ NEW
        {
            base.OnLostOwnership();
            ApplyAuthorityState(false);
        }

        private void ApplyAuthorityState(bool isOwner)
        {
            // Input yine sadece owner’da
            restrictControls = !isOwner;

            if (rb != null)
            {
                // Owner fizik simüle eder; remote kopyalar kinematic kalır
                rb.isKinematic = !isOwner;

                // KRİTİK: Çarpışmalar her iki tarafta da AÇIK kalsın;
                // böylece remote kopya "katı engel" gibi davranır.
                rb.detectCollisions = true;

                // Stabilite için öneri (opsiyonel, sende settings'ten de geliyor olabilir):
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            }

            if (spring != null)
                spring.enabled = isOwner;   // hesap/kuvvet owner’da
        }

        
        private void ApplyJetpackFxSfx(bool active)
        {
            if (jetpackFx)
            {
                if (active && !jetpackFx.isPlaying) jetpackFx.Play(true);
                else if (!active && jetpackFx.isPlaying) jetpackFx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            }

            if (jetpackAudio && jetpackClip)
            {
                if (active && !jetpackAudio.isPlaying)
                {
                    jetpackAudio.clip = jetpackClip;
                    jetpackAudio.Play();
                }
                else if (!active && jetpackAudio.isPlaying)
                {
                    jetpackAudio.Stop();
                }
            }
        }
        #endregion

        #region Unity Lifecycle Methods
        private void Update()
        {
            if (!IsOwner)
            {
                // Charge görseli
                ApplyChargeVisual(nvAccumulatedForce.Value);

                // Suspension görselini yumuşak takip
                if (suspensionTarget)
                {
                    float a = 1f - Mathf.Exp(-suspensionSmoothLerp * Time.deltaTime);
                    var lp = suspensionTarget.localPosition;
                    lp.y = Mathf.Lerp(lp.y, nvSuspensionPosY.Value, a);
                    suspensionTarget.localPosition = lp;
                }
                return;
            }
            if (restrictControls) return;

            UpdateJumpAngleCheck();
            HandleResetInput();
            HandleAllInputs();
            
            // OWNER: suspension posY değiştiyse NV'ye yaz
            if (suspensionTarget)
            {
                float y = suspensionTarget.localPosition.y;
                if (Mathf.Abs(y - _suspLastSentY) > suspensionSendThreshold)
                {
                    nvSuspensionPosY.Value = y;
                    _suspLastSentY = y;
                }
            }

            HandleAutoBalance();
            UpdateCharacterPosition();
            HandleJetpackInput();
            HandleJetpackVfxAndSfx();
        }

        private void FixedUpdate()
        {
            // ★ Client-auth: Sadece owner fizik çalıştırır
            if (!IsOwner) return;
            if (restrictControls) return;

            HandlePhysics();
            HandleVelocityClamping();
            HandleFallGravity();
            HandleJetpackPhysics();

            if (!isGrounded)
            {
                if (Mathf.Abs(horizontalInput) < 0.01f 
                    && Mathf.Abs(verticalInput)   < 0.01f
                    && !IsPerformingStunt)
                {
                    AlignYawToCamera();
                }
            }

            // ★★★ NEW: Yerdeyken hedef rotasyona Fixed’te yaklaş ve uygula
            if (isGrounded && _hasGroundedTarget && !_suppressGroundedRotation)
            {
                var next = Quaternion.Slerp(
                    rb.rotation,
                    _groundedTargetRot,
                    settings.rotationSpeed * Time.fixedDeltaTime
                );
                rb.MoveRotation(next);
            }
        }

        void OnCollisionEnter(Collision c)
        {
            if (!IsOwner) return;  // ★ only owner handles contacts

            var platform = c.collider.GetComponentInParent<RotatorPlatform>();
            if (platform != null)
                currentPlatform = platform;

            if (c.collider.GetComponent<Projectile>() != null)
            {
                PlayHitEffect();
            }
        }

        void OnCollisionExit(Collision c)
        {
            if (!IsOwner) return;  // ★ only owner handles contacts

            var platform = c.collider.GetComponentInParent<RotatorPlatform>();
            if (platform == currentPlatform)
                currentPlatform = null;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner) return;
            if (other.gameObject.layer == 4)
            {
                // your logic
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsOwner) return;
            if (other.gameObject.layer == 4)
            {
                // your logic
            }
        }
        #endregion

        #region Initialization Methods
        private void InitializeComponents()
        {
            spring = GetComponentInChildren<Spring>();
            rb = GetComponent<Rigidbody>();
            // Cursor lock burada değil; owner bilgisi Awake’te hazır değil.
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
                initialYPositions[i] = otherTransformsToMove[i].localPosition.y;

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
            HandleJumpKeyPress();
            HandleJumpKeyHold();
            HandleJumpKeyRelease();
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
            }

            isGrounded = spring.isGrounded;
            UpdateCenterOfMass();
            UpdateLinearDamping();
        }

        private void PlayBounceEffects()
        {
            if (playEffect) return;

            foreach (var eh in pogoStickBounceEffectHandler)
            {
                if (eh.layer == spring.collidedLayer)
                {
                    // 1) Owner: lokal çal
                    PlayBounceEffects_Internal(eh.layer, eh.impactColor);
                    // 2) Herkese yayınla
                    BounceEffectsServerRpc(eh.layer, eh.impactColor);
                    break;
                }
            }
            playEffect = true;
        }

        private void PlayBounceEffects_Internal(int layer, Color color)
        {
            foreach (var eh in pogoStickBounceEffectHandler)
            {
                if (eh.layer != layer) continue;

                if (eh.clip) AudioSource.PlayClipAtPoint(eh.clip, transform.position);

                if (!eh.ignoreImpactEffect && eh.impactEffect)
                {
                    var main = eh.impactEffect.main;
                    main.startColor = color;
                    eh.impactEffect.Play();
                }
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

            bool jumpKeyHeld = Input.GetKey(settings.jumpKey);

            if (jumpKeyHeld && HoldingJumpKey && !isJumpKeyPressed)
            {
                AccumulateJumpForce();
                UpdateCharacterPositionsDuringJumpAccumulation();
            }

            if (jumpKeyHeld && isGrounded && HoldingJumpKey && !isJumpKeyPressed)
            {
                groundHoldTimer += Time.deltaTime;

                if (groundHoldTimer >= holdJumpDuration)
                {
                    spring.enableSuspensionForce = true;
                    isJumpKeyPressed            = true;
                    HoldingJumpKey              = false;
                    groundHoldTimer             = 0f;
                }
            }
            else
            {
                groundHoldTimer = 0f;
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
            
            // Owner: değeri network'e yaz
            nvAccumulatedForce.Value = currentAccumulatedForce;
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
            
            // Owner local uygular; proxy'ler NV'den ApplyChargeVisual ile aynı efekti alır
            ApplyChargeVisual(currentAccumulatedForce);     
        }

        private void ApplyChargeVisual(float forceValue)
        {
            float t = Mathf.Clamp01(forceValue / settings.maxAccumulatedForce);

            // t ~ 0 ise ANINDA başlangıç pozisyonlarına dön (snap)
            if (t <= 0.0001f)
            {
                var snapChar = characterModelTransform.GetChild(0).localPosition;
                snapChar.y = initialPlayerCharacterTransformPosY;
                characterModelTransform.GetChild(0).localPosition = snapChar;

                for (int i = 0; i < otherTransformsToMove.Length; i++)
                {
                    var p = otherTransformsToMove[i].localPosition;
                    p.y = initialYPositions[i];
                    otherTransformsToMove[i].localPosition = p;
                }
                return;
            }

            // t > 0 iken ESKİ relative Lerp (hız hissi için)
            var currentCharacterPos = characterModelTransform.GetChild(0).localPosition;
            currentCharacterPos.y = Mathf.Lerp(
                currentCharacterPos.y,
                -0.4f,
                t
            );
            characterModelTransform.GetChild(0).localPosition = currentCharacterPos;

            for (int i = 0; i < otherTransformsToMove.Length; i++)
            {
                var otherTransformPosition = otherTransformsToMove[i].localPosition;
                otherTransformPosition.y = Mathf.Lerp(
                    otherTransformPosition.y,
                    initialYPositions[i] - 0.15f,
                    t
                );
                otherTransformsToMove[i].localPosition = otherTransformPosition;
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
            
            // proxy'lere "artık charge yok" de
            nvAccumulatedForce.Value = 0f;

            isGrounded = false;
            spring.isGrounded = false;
            
            PlayJumpSound();   

            var upVector = transform.up;
            if (currentPlatform != null)
            {
                currentPlatform.ForceRemoveRider(rb);
                currentPlatform = null;
            }
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
                ApplyJump(accumulatedForce);
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
                _hasGroundedTarget = false; // ★ NEW: havada hedefi iptal et
            }
        }

        private void HandleGroundedRotation()
        {
            // ★ Update bağlamı: SADECE hedef açıyı hesapla ve cache et
            xRotation = Mathf.Lerp(
                xRotation, 
                xRotation + (verticalInput * settings.pitchSpeed),
                Time.deltaTime * 5f
            );
            
            yRotation = Mathf.LerpAngle(
                yRotation, 
                Camera.main.transform.eulerAngles.y, 
                Time.deltaTime * 20f
            );

            zRotation = Mathf.Lerp(
                zRotation, 
                zRotation + (-horizontalInput * settings.rollSpeed),
                Time.deltaTime * 5f
            );

            if (settings.enableRotationLimits)
            {
                xRotation = Mathf.Clamp(xRotation, -settings.pitchLimit, settings.pitchLimit);
                zRotation = Mathf.Clamp(zRotation, -settings.rollLimit, settings.rollLimit);
            }
            
            targetRotation = Quaternion.Euler(xRotation, yRotation, zRotation);

            // ★★ ÖNEMLİ: Update’te fizik çağrısı YOK. Sadece hedefi cache et.
            _groundedTargetRot = targetRotation;
            _hasGroundedTarget = true;
        }

        private void HandleTorqueRotation()
        {
            if (!isGrounded && settings.useStunts)
            {
                ApplyAirTorque();
                CacheCurrentRotation();
            }
        }

        private void ApplyAirTorque()
        {
            if (Mathf.Abs(verticalInput) > 0.01f)
            {
                Vector3 camRight = Vector3
                    .ProjectOnPlane(Camera.main.transform.right, Vector3.up)
                    .normalized;

                rb.AddTorque(camRight * verticalInput * settings.airStuntTorqueXZ,
                    ForceMode.Force);
            }

            if (Mathf.Abs(horizontalInput) > 0.01f)
            {
                Vector3 camFwd = Vector3
                    .ProjectOnPlane(Camera.main.transform.forward, Vector3.up)
                    .normalized;

                rb.AddTorque(-camFwd * horizontalInput * settings.airStuntTorqueXZ,
                    ForceMode.Force);
            }
        }
     
        private void AlignYawToCamera()
        {
            if (!alignYawInAir || isGrounded || IsPerformingStunt) return;

            float targetYaw = Camera.main.transform.eulerAngles.y;

            Quaternion now   = rb.rotation;
            Quaternion want  = Quaternion.Euler(now.eulerAngles.x, targetYaw, now.eulerAngles.z);

            rb.MoveRotation(Quaternion.Slerp(
                now, want,
                Time.fixedDeltaTime * airYawLerpSpeed));
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

            // ★ NEW: tween boyunca fizik rotasyonunu bastır
            _suppressGroundedRotation = true;
            
            transform
                .DORotateQuaternion(
                    Quaternion.Euler(0f, transform.rotation.eulerAngles.y, 0f), 
                    0.75f
                )
                .SetUpdate(UpdateType.Fixed) // ★ NEW: Update yerine Fixed
                .OnComplete(() =>
                {
                    xRotation = 0f;
                    zRotation = 0f;
                    yRotation = Camera.main.transform.eulerAngles.y;
                    isAutoBalancing = false;

                    // tween biter bitmez cache’i güncelle, bastırmayı kaldır
                    _groundedTargetRot = Quaternion.Euler(xRotation, yRotation, zRotation);
                    _hasGroundedTarget = true;
                    _suppressGroundedRotation = false;
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
        [ServerRpc(RequireOwnership = true)]
        private void BounceEffectsServerRpc(int layer, Color color)
        {
            BounceEffectsClientRpc(layer, color);
        }

        [ClientRpc]
        private void BounceEffectsClientRpc(int layer, Color color)
        {
            if (IsOwner) return; // Owner zaten lokal oynattı
            PlayBounceEffects_Internal(layer, color);
        }
        
        [ServerRpc(RequireOwnership = true)]
        private void HitEffectServerRpc()
        {
            HitEffectClientRpc();
        }

        [ClientRpc]
        private void HitEffectClientRpc()
        {
            if (IsOwner) return; // Owner zaten local oynattı
            PlayHitEffect_Internal();
        }

        private void PlayHitEffect()
        {
            if (hitEffect == null) return;

            // 1) Owner local oynatır
            PlayHitEffect_Internal();

            // 2) Herkese yayınlar
            HitEffectServerRpc();
        }
       
        private void PlayHitEffect_Internal()
        {
            hitEffect.Play();
            if (hitEffectCoroutine != null)
                StopCoroutine(hitEffectCoroutine);
            hitEffectCoroutine = StartCoroutine(StopHitEffectAfterDelay());
        }

        private IEnumerator StopHitEffectAfterDelay()
        {
            yield return new WaitForSeconds(1f);
            hitEffect.Stop();
            hitEffectCoroutine = null;
        }

        private void HandleJetpackPhysics()
        {
            var jp = settings.jetpackSettings;

            if (isGrounded && jetpackFuel < jp.maxFuel)
                jetpackFuel = Mathf.Min(jetpackFuel + jp.regenRate * Time.fixedDeltaTime, jp.maxFuel);

            if (!jetpackActive) return;

            jetpackFuel = Mathf.Max(jetpackFuel - jp.burnRate * Time.fixedDeltaTime, 0f);
            rb.AddForce(transform.up * jp.jetpackForce, ForceMode.Force);

            if (jetpackFuel <= 0f)
                jetpackActive = false;
        }

        private void HandleJetpackInput()
        {
            bool wantsJetpack = Input.GetKey(jetpackKey) && !isGrounded && jetpackFuel > 0f;
            jetpackActive = wantsJetpack;
            
            // Owner jetpack durumunu network'e yazar
            nvJetpackActive.Value = jetpackActive;
        }

        private void HandleJetpackVfxAndSfx()
        {
            // Owner local uygular
            ApplyJetpackFxSfx(jetpackActive);
        }


        public void HandleReset()
        {
            HoldingJumpKey = false;
            isJumpKeyPressed = false;
            jumpBuffered = false;
            jumpBufferTimer = -1f;
            spring.enableSuspensionForce = true;
            currentAccumulatedForce = 0f;
            accumulatedForce = 0f;
            nvAccumulatedForce.Value = 0f;
            OnReset?.Invoke();
            groundHoldTimer = 0f;
        }

        private void HandleStuntLockSettings()
        {
            rb.freezeRotation = false;
            
            if (lockStuntOnYAxis)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationY;
            }

            if (lockStuntOnXAxis)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationX;
            }

            if (lockStuntOnZAxis)
            {
                rb.constraints = RigidbodyConstraints.FreezeRotationZ;
            }
        }

        public float GetCurrentFuel()    => jetpackFuel;
        public float GetNormalizedFuel() => jetpackFuel / settings.jetpackSettings.maxFuel;
        
        public float GetNormalizedAccumulatedJump()
        {
            return currentAccumulatedForce / settings.maxAccumulatedForce;
        }
        #endregion
    }
}
