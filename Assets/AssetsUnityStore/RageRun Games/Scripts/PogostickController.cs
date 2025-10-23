using System;
using System.Collections;
using System.Collections.Generic; // ★ NEW
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

        // ★ NEW: Pogo’nun collider’ını inspector’dan atamak için
        [Header("Extra Colliders")]
        [SerializeField] private Collider pogoCollider;

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

        // ★ NEW (Inspector): Upright davranışı
        [Header("Upright (R) Settings")]
        [SerializeField] private float uprightCooldown = 1f;            // R spam engeli (default 1s)
        [SerializeField] private float uprightDurationGrounded = 0.75f;  // yerdeyken slerp
        [SerializeField] private float uprightDurationAir = 0.25f;       // havadayken slerp
        [SerializeField] private bool  uprightYawToCamera = true;        // bitişte kameraya hizala mı

        // ★ NEW (Inspector): Unstuck pipeline
        [Header("Unstuck Settings")]
        [SerializeField] private bool useDepenetration = true;
        [SerializeField] private int  maxDepenetrationIterations = 3;
        [SerializeField] private float maxTotalDepenetrationDistance = 0.6f;
        [SerializeField] private bool useUpwardScan = true;
        [SerializeField] private int  upwardScanSteps = 6;
        [SerializeField] private float upwardScanStepHeight = 0.2f;
        [SerializeField] private bool useLastSafeRewind = true;
        [SerializeField] private float rewindSeconds = 1.0f;
        [SerializeField] private float safePoseRecordInterval = 0.1f;
        [SerializeField] private LayerMask solidLayers = ~0; // tüm katılar (Trigger’lar ignore edilir)

        private float _lastUprightTime = -999f; // cooldown takibi
        private Tween _uprightTween;            // aktif tween referansı

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

        // override sırasında geçici constraint saklama
        private RigidbodyConstraints _prevConstraints;

        // ★ NEW: Last safe pose buffer
        private struct SafePose { public Vector3 pos; public Quaternion rot; public float t; }
        private const int SafePoseCapacity = 30; // ~3 sn @0.1s
        private readonly SafePose[] _safePoses = new SafePose[SafePoseCapacity];
        private int _safePoseIndex = 0;
        private float _safePoseTimer = 0f;
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
                    && !IsPerformingStunt
                    && !isAutoBalancing) // ★ override sırasında kameraya hizalama yapma
                {
                    AlignYawToCamera();
                }
            }

            // ★ ESKİ mimari: burada grounded rotasyon uygulanmaz (Update’te uygulanır)

            // Last safe pose kaydı
            SafePoseRecordingTick();
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
                // ESKİ mimari: hedef-rot cache/iptal yok
            }
        }

        private void HandleGroundedRotation()
        {
            // ESKİ: hedef açıyı hesapla ve BU KAREDE uygula (Update bağlamında)
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

            // KRİTİK: eski mimari — Update’te fizik rotasyonu uygula
            rb.MoveRotation(Quaternion.Slerp(
                rb.rotation, 
                targetRotation,
                Time.fixedDeltaTime * settings.rotationSpeed
            ));
        }

        private void HandleTorqueRotation()
        {
            if (!isGrounded && settings.useStunts && !isAutoBalancing) // ★ override sırasında stunt torque yok
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
            if (!alignYawInAir || isGrounded || IsPerformingStunt || isAutoBalancing) return; // ★ NEW

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
            // ★★ CHANGED: R her koşulda çalışır + cooldown
            if (Input.GetKeyDown(KeyCode.R))
            {
                TryForceUpright();
            }
        }

        private void TryForceUpright()
        {
            if (Time.time - _lastUprightTime < Mathf.Max(0f, uprightCooldown))
                return;

            ForceUpright();
            _lastUprightTime = Time.time;
        }

        private void ForceUpright()
        {
            // aktif tween varsa iptal
            if (_uprightTween != null && _uprightTween.IsActive())
                _uprightTween.Kill();

            isAutoBalancing = true;
            timer = autobalancingTimer;

            // geçici constraint gevşet (rotasyona engel olmasın), sonra geri yüklenecek
            _prevConstraints = rb.constraints;
            rb.freezeRotation = false;
            rb.constraints = RigidbodyConstraints.None;

            // hedef 1: pitch=0, roll=0, mevcut yaw korunur (görsel olarak dikleşme)
            Quaternion firstTarget = Quaternion.Euler(0f, rb.rotation.eulerAngles.y, 0f);

            float dur = isGrounded ? Mathf.Max(0.01f, uprightDurationGrounded)
                                   : Mathf.Max(0.01f, uprightDurationAir);

            _uprightTween = transform
                .DORotateQuaternion(firstTarget, dur)
                // ESKİ: SetUpdate(UpdateType.Fixed) YOK
                .OnComplete(() =>
                {
                    // bittiğinde isteğe göre yaw'ı kameraya hizala
                    float finalYaw = uprightYawToCamera ? Camera.main.transform.eulerAngles.y
                                                        : transform.rotation.eulerAngles.y;

                    xRotation = 0f;
                    zRotation = 0f;
                    yRotation = finalYaw;

                    // (ESKİ) hedef cache güncelleme/bastırma yok

                    // ★★ NEW (kalsın): Kurtarma pipeline (3 aşama)
                    UnstuckPipeline();

                    // constraint’leri geri yükle
                    rb.constraints = _prevConstraints;

                    isAutoBalancing = false;
                });
        }

        private void StartAutoBalance()
        {
            // (legacy) artık kullanılmıyor, TryForceUpright kullanılıyor.
            ForceUpright();
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

        #region Utility Methods (RPC & Effects)
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
        #endregion

        #region Jetpack Methods
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
        #endregion

        #region Reset & Stunt Lock
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
        #endregion

        #region Public Getters
        public float GetCurrentFuel()    => jetpackFuel;
        public float GetNormalizedFuel() => jetpackFuel / settings.jetpackSettings.maxFuel;
        
        public float GetNormalizedAccumulatedJump()
        {
            return currentAccumulatedForce / settings.maxAccumulatedForce;
        }
        #endregion

        #region Unstuck Pipeline (Depenetration → UpwardScan → LastSafe)
        private void UnstuckPipeline()
        {
            // Hızları kesmeden: sadece açısalı kestik; linearVelocity korunuyor.

            // 1) Depenetration
            if (useDepenetration && TryDepenetrate())
                return;

            // 2) Upward Scan
            if (useUpwardScan && TryUpwardScan())
                return;

            // 3) Last Safe Rewind
            if (useLastSafeRewind)
                TryRewindToLastSafe();
        }

        // ★★ NEW: İki collider içinden EN AZ biri sıkışıksa true
        private bool AnyColliderOverlapping(Vector3 offset)
        {
            bool stuck = false;

            // Character (Capsule) için
            if (characterCollider != null)
            {
                Vector3 p0, p1; float r;
                GetCapsuleWorldPoints(characterCollider, out p0, out p1, out r);
                if (IsCapsuleOverlapping(p0 + offset, p1 + offset, r))
                    stuck = true;
            }

            // Pogo collider (genel tip) için
            if (pogoCollider != null)
            {
                if (IsGenericColliderOverlapping(pogoCollider, offset))
                    stuck = true;
            }

            return stuck;
        }

        private bool TryDepenetrate()
        {
            // Hangi collider’lar devrede?
            List<Collider> testColliders = new List<Collider>(2);
            if (characterCollider != null) testColliders.Add(characterCollider);
            if (pogoCollider      != null) testColliders.Add(pogoCollider);

            if (testColliders.Count == 0) return false;

            // Hızlı çıkış: zaten temiz mi?
            if (!AnyColliderOverlapping(Vector3.zero)) return true;

            Vector3 totalOffset = Vector3.zero;

            for (int iter = 0; iter < Mathf.Max(1, maxDepenetrationIterations); iter++)
            {
                Vector3 accum = Vector3.zero;

                foreach (var col in testColliders)
                {
                    // Aday çakışanları topla
                    var candidates = GetOverlapCandidates(col, Vector3.zero);

                    foreach (var other in candidates)
                    {
                        if (other == null) continue;
                        if (other.attachedRigidbody == rb) continue; // kendi RB’ni sayma

                        Vector3 dir; float dist;

                        // Kendi collider’ımızı (col) "offset" olmadan test ediyoruz (rb.position zaten gerçek)
                        if (Physics.ComputePenetration(
                                col, col.transform.position, col.transform.rotation,
                                other, other.transform.position, other.transform.rotation,
                                out dir, out dist))
                        {
                            accum += dir * dist; // MTV topla
                        }
                    }
                }

                if (accum.sqrMagnitude < 1e-8f)
                    break;

                // Toplam hareket clamp
                Vector3 proposed = totalOffset + accum;
                if (proposed.magnitude > maxTotalDepenetrationDistance)
                {
                    accum = proposed.normalized * (maxTotalDepenetrationDistance - totalOffset.magnitude);
                }

                rb.position += accum;
                totalOffset += accum;

                // Yeniden kontrol: tertemiz mi?
                if (!AnyColliderOverlapping(Vector3.zero))
                    return true;
            }

            return !AnyColliderOverlapping(Vector3.zero);
        }

        private bool TryUpwardScan()
        {
            // Zaten temizse gerek yok
            if (!AnyColliderOverlapping(Vector3.zero)) return true;

            Vector3 basePos = rb.position;
            float step  = Mathf.Max(0.01f, upwardScanStepHeight);
            int   steps = Mathf.Max(1,     upwardScanSteps);

            for (int i = 1; i <= steps; i++)
            {
                Vector3 candidate = basePos + Vector3.up * (step * i);
                Vector3 offset    = candidate - rb.position;

                if (!AnyColliderOverlapping(offset))
                {
                    rb.position = candidate;
                    return true;
                }
            }

            return false;
        }

        private void TryRewindToLastSafe()
        {
            // hedef: rewindSeconds kadar geçmişteki ilk güvenli poza dön
            float targetT = Time.time - Mathf.Max(0.05f, rewindSeconds);

            // en yakın (ama targetT'den büyük olmayan) pozu bul
            SafePose best = default;
            bool found = false;

            for (int i = 0; i < SafePoseCapacity; i++)
            {
                var sp = _safePoses[i];
                if (sp.t <= 0f) continue;
                if (sp.t >= targetT)
                {
                    best = sp;
                    found = true;
                }
            }

            if (!found)
            {
                // buffer boş olabilir; en eski geçerli pozu bul
                float latest = -1f;
                for (int i = 0; i < SafePoseCapacity; i++)
                {
                    var sp = _safePoses[i];
                    if (sp.t > latest)
                    {
                        latest = sp.t;
                        best = sp;
                        found = true;
                    }
                }
            }

            if (found)
            {
                rb.position        = best.pos + Vector3.up * 0.03f; // küçük skin
                rb.rotation        = best.rot;
                rb.linearVelocity  = rb.linearVelocity;              // lineari KORU (istersen clamp’la)
                rb.angularVelocity = Vector3.zero;                   // yalnızca açısal sıfırla
            }
        }

        private void SafePoseRecordingTick()
        {
            _safePoseTimer += Time.fixedDeltaTime;
            if (_safePoseTimer < Mathf.Max(0.02f, safePoseRecordInterval)) return;
            _safePoseTimer = 0f;

            // KAYIT KRİTERİ: Her iki collider da temiz olmalı
            if (!AnyColliderOverlapping(Vector3.zero))
            {
                _safePoses[_safePoseIndex] = new SafePose
                {
                    pos = rb.position,
                    rot = rb.rotation,
                    t = Time.time
                };
                _safePoseIndex = (_safePoseIndex + 1) % SafePoseCapacity;
            }
        }

        // --- Overlap yardımcıları ---

        // Capsule özel overlap (mevcut fonksiyonun offset’li hali için aşırı yükleme kullanalım)
        private bool IsCapsuleOverlapping(Vector3 p0, Vector3 p1, float radius)
        {
            var cols = Physics.OverlapCapsule(p0, p1, radius, solidLayers, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
            {
                if (c == null) continue;
                if (c.attachedRigidbody == rb) continue; // kendini sayma
                return true;
            }
            return false;
        }

        // Generic collider için: OverlapSphere ile adayları al, sonra ComputePenetration ile kesin kontrol
        private bool IsGenericColliderOverlapping(Collider col, Vector3 offset)
        {
            // Kaba yarıçap olarak bounds extents magnitute kullanıyoruz
            Vector3 center = col.bounds.center + offset;
            float radius = col.bounds.extents.magnitude;

            var candidates = Physics.OverlapSphere(center, radius, solidLayers, QueryTriggerInteraction.Ignore);
            foreach (var other in candidates)
            {
                if (other == null) continue;
                if (other.attachedRigidbody == rb) continue;

                Vector3 dir; float dist;
                if (Physics.ComputePenetration(
                        col, col.transform.position + offset, col.transform.rotation,
                        other, other.transform.position, other.transform.rotation,
                        out dir, out dist))
                {
                    return true;
                }
            }
            return false;
        }

        // Depenetration sırasında adayları toplamak için ortak getter
        private Collider[] GetOverlapCandidates(Collider col, Vector3 offset)
        {
            if (col is CapsuleCollider cc)
            {
                Vector3 p0, p1; float r;
                GetCapsuleWorldPoints(cc, out p0, out p1, out r);
                p0 += offset; p1 += offset;
                return Physics.OverlapCapsule(p0, p1, r, solidLayers, QueryTriggerInteraction.Ignore);
            }
            else
            {
                Vector3 center = col.bounds.center + offset;
                float radius = col.bounds.extents.magnitude;
                return Physics.OverlapSphere(center, radius, solidLayers, QueryTriggerInteraction.Ignore);
            }
        }

        private void GetCapsuleWorldPoints(CapsuleCollider col, out Vector3 p0, out Vector3 p1, out float radius)
        {
            // world center
            Vector3 center = col.transform.TransformPoint(col.center);

            // scale’lı yarıçap & yükseklik
            Vector3 lossy = col.transform.lossyScale;
            float rScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.z));
            float hScale = Mathf.Abs(lossy.y);

            radius = col.radius * rScale;
            float height = Mathf.Max(col.height * hScale, radius * 2f);
            float half = (height * 0.5f) - radius;

            Vector3 up = col.transform.up;

            switch (col.direction)
            {
                case 0: // X
                    up = col.transform.right;
                    rScale = Mathf.Max(Mathf.Abs(lossy.y), Mathf.Abs(lossy.z));
                    radius = col.radius * rScale;
                    height = Mathf.Max(col.height * Mathf.Abs(lossy.x), radius * 2f);
                    half = (height * 0.5f) - radius;
                    break;
                case 1: // Y (default)
                    break;
                case 2: // Z
                    up = col.transform.forward;
                    rScale = Mathf.Max(Mathf.Abs(lossy.x), Mathf.Abs(lossy.y));
                    radius = col.radius * rScale;
                    height = Mathf.Max(col.height * Mathf.Abs(lossy.z), radius * 2f);
                    half = (height * 0.5f) - radius;
                    break;
            }

            p0 = center + up * half;
            p1 = center - up * half;
        }
        #endregion
    }
}
