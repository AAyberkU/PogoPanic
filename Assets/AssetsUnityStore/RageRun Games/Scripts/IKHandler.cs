using System;
using System.Collections;
using DG.Tweening;
using UnityEngine;
using Unity.Netcode;

namespace RageRunGames.PogostickController
{
    public class IKHandler : NetworkBehaviour
    {
        [SerializeField] private Transform pogoStickHolder;
        [SerializeField] private float pogoStickInputMultiplier;

        [SerializeField] private Transform spineTarget;
        [SerializeField] private Transform headTarget;

        [SerializeField] private Transform spineTwistTarget;
        [SerializeField] private Transform headTwistTarget;

        private Vector3 initialSpineTargetPosition;
        private Vector3 initialHeadTargetPosition;
        private Vector3 initialSpineTwistTargetRotation;
        private Vector3 initialHeadTwistTargetRotation;
        private PogostickController pogostickController;

        private Vector3 headTargetMultiplier;
        private Vector3 spineTargetMultiplier;

        private PogoStickControllerSettings pogoStickControllerSettings;

        private float yVelocity;
        private bool HasHit { get; set; }

        // ── Network state (owner yazar, herkes okur) ─────────────────────────────
        private NetworkVariable<Vector2> nvInput =
            new NetworkVariable<Vector2>(Vector2.zero, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> nvIsGrounded =
            new NetworkVariable<bool>(true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<float> nvYVelocity =
            new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);
        // ─────────────────────────────────────────────────────────────────────────

        private void Awake()
        {
            InitializeInitialPositions();
            pogostickController = GetComponent<PogostickController>();
            InitializeSettings(pogostickController.PogoStickControllerSettings);
        }

        private void InitializeSettings(PogoStickControllerSettings pogoStickControllerSettings)
        {
            this.pogoStickControllerSettings = pogoStickControllerSettings;
        }

        private void InitializeInitialPositions()
        {
            initialSpineTargetPosition = spineTarget.localPosition;
            initialHeadTargetPosition  = headTarget.localPosition;
            initialSpineTwistTargetRotation = spineTwistTarget.localEulerAngles;
            initialHeadTwistTargetRotation  = headTwistTarget.localEulerAngles;
        }

        private void Update()
        {
            // Owner input’u üretir ve yayınlar; proxy’ler NV’den okur
            if (IsOwner)
            {
                var localInput = new Vector2(pogostickController.HorizontalInput, pogostickController.VerticalInput);
                nvInput.Value      = localInput;
                nvIsGrounded.Value = pogostickController.IsGrounded;
            }

            Vector2 inputVector = IsOwner ? new Vector2(pogostickController.HorizontalInput, pogostickController.VerticalInput)
                                          : nvInput.Value;

            if (inputVector.magnitude != 0f)
            {
                UpdateTargetPositions(inputVector);
            }
            else
            {
                ResetTargetPositions(); // yVelocity içeride owner/proxy ayrımıyla set edilir
            }

            bool grounded = IsOwner ? pogostickController.IsGrounded : nvIsGrounded.Value;

            if (!grounded)
            {
                // orijinal formülü NV input ile sür
                pogoStickHolder.localEulerAngles = new Vector3(
                    inputVector.y * pogoStickInputMultiplier, 0f,
                    inputVector.x * pogoStickInputMultiplier);
            }
        }

        private void UpdateTargetPositions(Vector2 inputVector)
        {
            Vector3 desiredSpinePos = initialSpineTargetPosition + new Vector3(
                0f,
                -inputVector.y * pogoStickControllerSettings.ikSettings.spineTargetMultiplier.y,
                0f
            );
            spineTarget.localPosition = Vector3.Lerp(
                spineTarget.localPosition,
                desiredSpinePos,
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );

            Vector3 desiredHeadPos = initialHeadTargetPosition + new Vector3(
                0f,
                -inputVector.y * pogoStickControllerSettings.ikSettings.headTargetMultiplier.y,
                0f
            );
            headTarget.localPosition = Vector3.Lerp(
                headTarget.localPosition,
                desiredHeadPos,
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );

            Quaternion desiredSpineTwist = Quaternion.Euler(
                0f,
                Mathf.Clamp(
                    inputVector.x * pogoStickControllerSettings.ikSettings.spineTargetMultiplier.x,
                    -pogoStickControllerSettings.ikSettings.spineTargetMultiplier.x,
                    pogoStickControllerSettings.ikSettings.spineTargetMultiplier.x
                ),
                0f
            );
            spineTwistTarget.localRotation = Quaternion.Lerp(
                spineTwistTarget.localRotation,
                desiredSpineTwist,
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );

            Quaternion desiredHeadTwist = Quaternion.Euler(
                0f,
                Mathf.Clamp(
                    inputVector.x * pogoStickControllerSettings.ikSettings.headTargetMultiplier.x,
                    -pogoStickControllerSettings.ikSettings.headTargetMultiplier.x,
                    pogoStickControllerSettings.ikSettings.headTargetMultiplier.x
                ),
                0f
            );
            headTwistTarget.localRotation = Quaternion.Lerp(
                headTwistTarget.localRotation,
                desiredHeadTwist,
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );
        }

        private void ResetTargetPositions()
        {
            // Owner yVelocity'yi hesaplar ve yayınlar; proxy direkt NV’yi kullanır
            if (IsOwner)
            {
                var velocity = pogostickController.Rb.linearVelocity;
                yVelocity = Mathf.Lerp(
                    yVelocity,
                    velocity.y * pogoStickControllerSettings.ikSettings.rigidBodyWeight,
                    Time.deltaTime * pogoStickControllerSettings.ikSettings.rigidBodySmoothTime
                );
                nvYVelocity.Value = yVelocity;
            }
            else
            {
                yVelocity = nvYVelocity.Value;
            }

            spineTarget.localPosition = Vector3.Lerp(
                spineTarget.localPosition,
                initialSpineTargetPosition + Vector3.up * yVelocity,
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );

            headTarget.localPosition = Vector3.Lerp(
                headTarget.localPosition,
                initialHeadTargetPosition + Vector3.up * yVelocity,
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );

            spineTwistTarget.localRotation = Quaternion.Lerp(
                spineTwistTarget.localRotation,
                Quaternion.Euler(initialSpineTwistTargetRotation),
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );

            headTwistTarget.localRotation = Quaternion.Lerp(
                headTwistTarget.localRotation,
                Quaternion.Euler(initialHeadTwistTargetRotation),
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );
        }

        public void ReactToHit(Vector3 hitPoint, float reactionAmount = 1f)
        {
            // Owner local uygular ve herkese yayınlar
            if (IsOwner)
            {
                ReactToHitServerRpc(hitPoint, reactionAmount);
            }
            DoReactToHitLocal(hitPoint, reactionAmount);
        }

        public void ReactOnHit(Vector3 direction)
        {
            // Owner local uygular ve herkese yayınlar
            if (IsOwner)
            {
                ReactOnHitServerRpc(direction);
            }
            DoReactOnHitLocal(direction);
        }

        // ── Local uygulama (orijinal içeriği aynen korundu) ──────────────────────
        private void DoReactToHitLocal(Vector3 hitPoint, float reactionAmount = 1f)
        {
            Vector3 localHitDir = transform.InverseTransformDirection((hitPoint - spineTarget.position).normalized);

            float xRotation = Mathf.Clamp(
                -localHitDir.z * pogoStickControllerSettings.ikSettings.spineTargetMultiplier.x,
                -15f, 15f
            );
            float yRotation = Mathf.Clamp(
                localHitDir.x * pogoStickControllerSettings.ikSettings.spineTargetMultiplier.x,
                -15f, 15f
            );

            Quaternion desiredRotation = Quaternion.Euler(xRotation, yRotation, 0f);
            spineTwistTarget.localRotation = Quaternion.Lerp(
                spineTwistTarget.localRotation,
                desiredRotation,
                Time.deltaTime * pogoStickControllerSettings.ikSettings.smoothLerpSpeed
            );
        }

        private void DoReactOnHitLocal(Vector3 direction)
        {
            HasHit = true;

            direction.Normalize();

            float forwardDot = Vector3.Dot(transform.forward, direction);
            float rightDot   = Vector3.Dot(transform.right, direction);

            float swayAmountY = Mathf.Clamp(forwardDot, -1f, 1f) * 50f;
            float returnY = 0f;

            spineTarget.DOLocalMoveY(swayAmountY, 0.35f).SetEase(Ease.OutQuad);
            headTarget.DOLocalMoveY(swayAmountY, 0.35f).SetEase(Ease.OutQuad).OnComplete(() =>
            {
                spineTarget.DOLocalMoveY(returnY, 0.25f).SetEase(Ease.InOutQuad);
                headTarget.DOLocalMoveY(returnY, 0.25f).SetEase(Ease.InOutQuad);
            });

            float twistAmountY = Mathf.Clamp(rightDot, -1f, 1f) * 30f;
            float returnTwist = 0f;
            float twistDuration = 0.2f;

            spineTwistTarget.DOLocalRotate(new Vector3(0f, twistAmountY, 0f), twistDuration).SetEase(Ease.OutQuad);
            headTwistTarget.DOLocalRotate(new Vector3(0f, twistAmountY, 0f), twistDuration).SetEase(Ease.OutQuad)
                .OnComplete(() =>
                {
                    spineTwistTarget.DOLocalRotate(new Vector3(0f, returnTwist, 0f), 0.25f).SetEase(Ease.InOutQuad);
                    headTwistTarget.DOLocalRotate(new Vector3(0f, returnTwist, 0f), 0.25f).SetEase(Ease.InOutQuad);
                });

            StartCoroutine(ResetHasHit());
        }
        // ─────────────────────────────────────────────────────────────────────────

        private IEnumerator ResetHasHit()
        {
            yield return new WaitForSeconds(2f);
            HasHit = false;
        }

        // ── RPC’ler: hit reaksiyonlarını herkese yayınlar ───────────────────────
        [ServerRpc(RequireOwnership = true)]
        private void ReactToHitServerRpc(Vector3 hitPoint, float reactionAmount)
        {
            ReactToHitClientRpc(hitPoint, reactionAmount);
        }

        [ClientRpc]
        private void ReactToHitClientRpc(Vector3 hitPoint, float reactionAmount)
        {
            if (IsOwner) return; // owner zaten local uyguladı
            DoReactToHitLocal(hitPoint, reactionAmount);
        }

        [ServerRpc(RequireOwnership = true)]
        private void ReactOnHitServerRpc(Vector3 direction)
        {
            ReactOnHitClientRpc(direction);
        }

        [ClientRpc]
        private void ReactOnHitClientRpc(Vector3 direction)
        {
            if (IsOwner) return; // owner zaten local uyguladı
            DoReactOnHitLocal(direction);
        }
        // ─────────────────────────────────────────────────────────────────────────
    }
}
