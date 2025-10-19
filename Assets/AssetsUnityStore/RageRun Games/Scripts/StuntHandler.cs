using System.Collections;
using UnityEngine;
using UnityEngine.Animations.Rigging;
using Unity.Netcode;

namespace RageRunGames.PogostickController
{
    public class StuntHandler : NetworkBehaviour
    {
        [SerializeField] private Transform pogoStickTransform;
        [SerializeField] private Vector3 stuntPosition;
        [SerializeField] private Vector3 stuntRotation;

        [SerializeField] private float stuntDuration = 0.25f;

        [Header("IK Constraints")] [SerializeField]
        private TwoBoneIKConstraint leftHandIK;
        [SerializeField] private TwoBoneIKConstraint rightHandIK;
        [SerializeField] private TwoBoneIKConstraint leftFootIK;
        [SerializeField] private TwoBoneIKConstraint rightFootIK;

        [Header("Spine & Head IK")] [SerializeField]
        private Transform spineTarget;
        [SerializeField] private Transform headTarget;
        [SerializeField] private Vector3 spineTwistRotation;
        [SerializeField] private Vector3 headTwistRotation;

        private Vector3 spineTargetOriginalRotation;
        private Vector3 headTargetOriginalRotation;

        [Header("Hand Targets")] [SerializeField]
        private Transform rightHandTarget;
        [SerializeField] private Vector3 rightHandTargetStuntPosition;
        [SerializeField] private Vector3 rightHandEulerRotation;

        [SerializeField] private Transform leftHandTarget;
        [SerializeField] private Vector3 leftHandTargetStuntPosition;
        [SerializeField] private Vector3 leftHandEulerRotation;

        private Vector3 rightHandTargetOriginalPosition;
        private Vector3 rightHandOriginalRotation;

        private Vector3 leftHandTargetOriginalPosition;
        private Vector3 leftHandOriginalRotation;

        private PogostickController pogostickController;

        // Hangi stunt tetiklendiğini küçük bir enum ile taşırız
        private enum StuntOp : byte
        {
            LeftHand,
            RightHand,
            FeetLoop,
            RightHandTarget,
            LeftHandTarget,
            SpineHeadTwist,
            PogoStick
        }

        private void Start()
        {
            pogostickController = GetComponent<PogostickController>();

            // Store original positions and rotations
            rightHandTargetOriginalPosition = rightHandTarget.localPosition;
            rightHandOriginalRotation       = rightHandTarget.localEulerAngles;

            leftHandTargetOriginalPosition  = leftHandTarget.localPosition;
            leftHandOriginalRotation        = leftHandTarget.localEulerAngles;

            spineTargetOriginalRotation     = spineTarget.localEulerAngles;
            headTargetOriginalRotation      = headTarget.localEulerAngles;
        }

        private void Update()
        {
            // Input sadece owner'da alınır
            if (!IsOwner) return;

            if (pogostickController.IsGrounded) return;

            if (Input.GetKeyDown(KeyCode.K)) // Left Hand Stunt
            {
                TriggerLocal(StuntOp.LeftHand, stuntDuration, 0);
                SendStuntServerRpc(StuntOp.LeftHand, stuntDuration, 0);
            }

            if (Input.GetKeyDown(KeyCode.L)) // Right Hand Stunt
            {
                TriggerLocal(StuntOp.RightHand, stuntDuration, 0);
                SendStuntServerRpc(StuntOp.RightHand, stuntDuration, 0);
            }

            if (Input.GetKeyDown(KeyCode.N)) // Foot Twisting Loop Stunt
            {
                TriggerLocal(StuntOp.FeetLoop, stuntDuration, 2);
                SendStuntServerRpc(StuntOp.FeetLoop, stuntDuration, 2);
            }

            if (Input.GetKeyDown(KeyCode.M)) // Right Hand Target Stunt
            {
                TriggerLocal(StuntOp.RightHandTarget, stuntDuration, 0);
                SendStuntServerRpc(StuntOp.RightHandTarget, stuntDuration, 0);
            }

            if (Input.GetKeyDown(KeyCode.B)) // Left Hand Target Stunt
            {
                TriggerLocal(StuntOp.LeftHandTarget, stuntDuration, 0);
                SendStuntServerRpc(StuntOp.LeftHandTarget, stuntDuration, 0);
            }

            if (Input.GetKeyDown(KeyCode.G)) // Spine & Head Twist Stunt
            {
                TriggerLocal(StuntOp.SpineHeadTwist, stuntDuration, 0);
                SendStuntServerRpc(StuntOp.SpineHeadTwist, stuntDuration, 0);
            }

            if (Input.GetKeyDown(KeyCode.F)) // Pogostick Stunt
            {
                TriggerLocal(StuntOp.PogoStick, stuntDuration, 0);
                SendStuntServerRpc(StuntOp.PogoStick, stuntDuration, 0);
            }
        }

        // ---- NETWORK ----
        [ServerRpc(RequireOwnership = true)]
        private void SendStuntServerRpc(StuntOp op, float duration, int loops)
        {
            // Owner localde oynattı; tüm client'lara (owner dahil) yay
            BroadcastStuntClientRpc(op, duration, loops);
        }

        [ClientRpc]
        private void BroadcastStuntClientRpc(StuntOp op, float duration, int loops)
        {
            // Owner zaten local tetikledi; double tetiklemeyi atla
            if (IsOwner) return;

            TriggerLocal(op, duration, loops);
        }

        // ---- LOCAL tetikleyici: her client kendi tarafında coroutine'leri çalıştırır ----
        private void TriggerLocal(StuntOp op, float duration, int loops)
        {
            float dur = (duration > 0f) ? duration : stuntDuration;

            switch (op)
            {
                case StuntOp.LeftHand:
                    StartStunt(leftHandIK, dur);
                    break;

                case StuntOp.RightHand:
                    StartStunt(rightHandIK, dur);
                    break;

                case StuntOp.FeetLoop:
                    StartLoopStunt(leftFootIK, rightFootIK, dur, Mathf.Max(1, loops));
                    break;

                case StuntOp.RightHandTarget:
                    StartTargetStunt(
                        rightHandTarget,
                        rightHandTargetStuntPosition,
                        rightHandTargetOriginalPosition,
                        rightHandEulerRotation,
                        rightHandOriginalRotation,
                        dur
                    );
                    break;

                case StuntOp.LeftHandTarget:
                    StartTargetStunt(
                        leftHandTarget,
                        leftHandTargetStuntPosition,
                        leftHandTargetOriginalPosition,
                        leftHandEulerRotation,
                        leftHandOriginalRotation,
                        dur
                    );
                    break;

                case StuntOp.SpineHeadTwist:
                    StartTwistStunt(dur);
                    break;

                case StuntOp.PogoStick:
                    StartCoroutine(PerformPogostickStunt(pogoStickTransform, dur));
                    break;
            }
        }

        //======================= STUNT METHODS =======================//

        private void StartStunt(TwoBoneIKConstraint ikConstraint, float duration)
        {
            StopCoroutine(StuntRoutine(ikConstraint, duration));
            StartCoroutine(StuntRoutine(ikConstraint, duration));
        }

        private void StartLoopStunt(TwoBoneIKConstraint ik1, TwoBoneIKConstraint ik2, float duration, int loops)
        {
            StopCoroutine(LoopStuntRoutine(ik1, ik2, duration, loops));
            StartCoroutine(LoopStuntRoutine(ik1, ik2, duration, loops));
        }

        private void StartTargetStunt(Transform target, Vector3 stuntPosition, Vector3 originalPosition,
            Vector3 stuntRotation, Vector3 originalRotation, float duration)
        {
            StopCoroutine(TargetStuntRoutine(target, stuntPosition, originalPosition, stuntRotation, originalRotation, duration));
            StartCoroutine(TargetStuntRoutine(target, stuntPosition, originalPosition, stuntRotation, originalRotation, duration));
        }

        private void StartTwistStunt(float duration)
        {
            StopCoroutine(SpineHeadTwistRoutine(duration));
            StartCoroutine(SpineHeadTwistRoutine(duration));
        }

        //======================= COROUTINES =======================//

        private IEnumerator StuntRoutine(TwoBoneIKConstraint ikConstraint, float duration)
        {
            pogostickController.IsPerformingStunt = true;
            float elapsedTime = 0;
            float initialWeight = ikConstraint.weight;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                ikConstraint.weight = Mathf.Lerp(initialWeight, 0f, elapsedTime / duration);
                yield return null;
            }

            elapsedTime = 0;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                ikConstraint.weight = Mathf.Lerp(0f, 1f, elapsedTime / duration);
                yield return null;
            }

            pogostickController.IsPerformingStunt = false;
        }

        private IEnumerator LoopStuntRoutine(TwoBoneIKConstraint ikConstraint1, TwoBoneIKConstraint ikConstraint2,
            float duration, int loopCount)
        {
            pogostickController.IsPerformingStunt = true;
            for (int i = 0; i < loopCount; i++)
            {
                yield return LerpIKWeights(ikConstraint1, ikConstraint2, 1f, 0f, duration);
                yield return LerpIKWeights(ikConstraint1, ikConstraint2, 0f, 1f, duration);
            }

            yield return LerpIKWeights(ikConstraint1, ikConstraint2, 1f, 1f, duration);

            pogostickController.IsPerformingStunt = false;
        }

        private IEnumerator TargetStuntRoutine(Transform target, Vector3 targetPosition, Vector3 originalPosition,
            Vector3 targetRotation, Vector3 originalRotation, float duration)
        {
            float elapsedTime = 0;
            pogostickController.IsPerformingStunt = true;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                target.localPosition = Vector3.Lerp(originalPosition, targetPosition, elapsedTime / duration);
                target.localEulerAngles = Vector3.Lerp(originalRotation, targetRotation, elapsedTime / duration);
                yield return null;
            }

            elapsedTime = 0;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                target.localPosition = Vector3.Lerp(targetPosition, originalPosition, elapsedTime / duration);
                target.localEulerAngles = Vector3.Lerp(targetRotation, originalRotation, elapsedTime / duration);
                yield return null;
            }

            pogostickController.IsPerformingStunt = false;
        }

        private IEnumerator SpineHeadTwistRoutine(float duration)
        {
            pogostickController.IsPerformingStunt = true;
            float elapsedTime = 0;
            Vector3 initialSpineRotation = spineTarget.localEulerAngles;
            Vector3 initialHeadRotation  = headTarget.localEulerAngles;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                spineTarget.localRotation = Quaternion.Slerp(Quaternion.Euler(initialSpineRotation),
                    Quaternion.Euler(spineTwistRotation), elapsedTime / duration);
                headTarget.localRotation = Quaternion.Slerp(Quaternion.Euler(initialHeadRotation),
                    Quaternion.Euler(headTwistRotation), elapsedTime / duration);
                yield return null;
            }

            elapsedTime = 0;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                spineTarget.localRotation = Quaternion.Slerp(Quaternion.Euler(spineTwistRotation),
                    Quaternion.Euler(initialSpineRotation), elapsedTime / duration);
                headTarget.localRotation = Quaternion.Slerp(Quaternion.Euler(headTwistRotation),
                    Quaternion.Euler(initialHeadRotation), elapsedTime / duration);
                yield return null;
            }

            pogostickController.IsPerformingStunt = false;
        }

        private IEnumerator LerpIKWeights(TwoBoneIKConstraint ik1, TwoBoneIKConstraint ik2, float targetWeight1,
            float targetWeight2, float duration)
        {
            float elapsedTime = 0;
            float startWeight1 = ik1.weight;
            float startWeight2 = ik2.weight;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                ik1.weight = Mathf.Lerp(startWeight1, targetWeight1, elapsedTime / duration);
                ik2.weight = Mathf.Lerp(startWeight2, targetWeight2, elapsedTime / duration);
                yield return null;
            }
        }

        private IEnumerator PerformPogostickStunt(Transform target, float duration)
        {
            pogostickController.IsPerformingStunt = true;

            float elapsedTime = 0;
            Vector3 initialPosition = target.localPosition;
            Vector3 initialRotation = target.localEulerAngles;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                target.localPosition = Vector3.Lerp(initialPosition, stuntPosition, elapsedTime / duration);
                target.localEulerAngles = Vector3.Lerp(initialRotation, stuntRotation, elapsedTime / duration);
                yield return null;
            }

            elapsedTime = 0;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                target.localPosition = Vector3.Lerp(stuntPosition, Vector3.zero, elapsedTime / duration);
                target.localEulerAngles = Vector3.Lerp(stuntRotation, Vector3.zero, elapsedTime / duration);
                yield return null;
            }

            pogostickController.IsPerformingStunt = false;
        }
    }
}
