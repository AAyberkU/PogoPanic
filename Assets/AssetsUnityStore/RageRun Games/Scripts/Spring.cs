using UnityEngine;

namespace RageRunGames.PogostickController
{
    public class Spring : MonoBehaviour
    {
        // public GameObject platformDetector;
        [SerializeField] private Transform characterTransform;
        [SerializeField] private float characterSpringForceMultiplier = 1f;

        [SerializeField] private Transform pogoSpringTransform;
        [SerializeField] private float currentSuspensionLength;
        [SerializeField] private float prevoiusSuspensionLength;

        public bool isGrounded;

        [SerializeField] private Rigidbody rb;

        [SerializeField] private LayerMask groundLayer;

        public bool enableSuspensionForce = true;

        private Vector3 currentHitPoint;

        private PogostickController pogostickController;

        [SerializeField] private float pogoBottomSize;
        [SerializeField] private float suspensionLength;
        private float suspensionStiffness;
        private float damping;

        private float intiialCharacterPositionY;

        private float groundAngle;

        private bool isOnMovingPlatform;

        public int collidedLayer;

        private void Start()
        {
            pogostickController = GetComponentInParent<PogostickController>();

            InitializeSettings(pogostickController.PogoStickControllerSettings);

            prevoiusSuspensionLength = suspensionLength;
            currentSuspensionLength = suspensionLength;

            intiialCharacterPositionY = characterTransform.localPosition.y;
        }

        private void InitializeSettings(PogoStickControllerSettings pogoStickControllerPogoStickControllerSettings)
        {
            pogoBottomSize = pogoStickControllerPogoStickControllerSettings.suspensionSettings.pogoBottomSize;
            suspensionLength = pogoStickControllerPogoStickControllerSettings.suspensionSettings.suspensionLength;
            suspensionStiffness = pogoStickControllerPogoStickControllerSettings.suspensionSettings.suspensionStiffness;
            damping = pogoStickControllerPogoStickControllerSettings.suspensionSettings.damping;
        }

        private void FixedUpdate()
        {
            if (pogostickController.RestrictControls) return;

            UpdateSpring();
        }

        public void UpdateSpring()
        {
            if (rb)
            {
                HandleSuspension();
                PlaceSpring();
            }
        }

        private void HandleSuspension()
        {
            RaycastHit hit;
            Ray ray = new Ray(transform.position, -transform.up);

            if (Physics.Raycast(ray, out hit, suspensionLength + pogoBottomSize, groundLayer))
            {
                collidedLayer = hit.collider.gameObject.layer;



                groundAngle = Vector3.Angle(Vector3.up, hit.normal);
                currentSuspensionLength = hit.distance - pogoBottomSize;

                float force = suspensionStiffness * (suspensionLength - currentSuspensionLength) +
                              damping * (prevoiusSuspensionLength - currentSuspensionLength) / Time.deltaTime;
                Vector3 upVec = transform.up;

                upVec.z = 0f;
                upVec.x = 0f;
                Vector3 finalForce = (upVec * force);

                if (!isOnMovingPlatform && groundAngle < 75f)
                {
                    rb.AddForceAtPosition(finalForce, hit.point);
                }

                currentHitPoint = hit.point;

                isGrounded = true;

                prevoiusSuspensionLength = currentSuspensionLength;

            }
            else
            {
                currentSuspensionLength = Mathf.Lerp(currentSuspensionLength, suspensionLength, Time.deltaTime * 5f);
                isGrounded = false;
                enableSuspensionForce = true;
                isOnMovingPlatform = false;
                pogostickController.transform.parent = null;
            }
        }

        public float smoothTime = 0.2f;
        private float refVel;

        private void PlaceSpring()
        {
            if (pogoSpringTransform)
            {
                Vector3 wheelGeoPos = transform.position + (-transform.up * currentSuspensionLength);
                pogoSpringTransform.position = wheelGeoPos;

                var currentLocalPos = characterTransform.localPosition;
                currentLocalPos.y = Mathf.Lerp(currentLocalPos.y,
                    intiialCharacterPositionY + (currentSuspensionLength * characterSpringForceMultiplier),
                    smoothTime * Time.deltaTime);
                characterTransform.localPosition = currentLocalPos;
            }
        }

        public void ApplyForceAtSuspensionPoint(Vector3 force)
        {
            rb.AddForceAtPosition(force, currentHitPoint, ForceMode.Impulse);
        }

        private void OnDrawGizmos()
        {
            Vector3 suspensionPos = transform.position + (-transform.up * suspensionLength);
            Vector3 currSuspensionPos = transform.position + (-transform.up * currentSuspensionLength);

            if (Application.isPlaying && isGrounded)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, currSuspensionPos);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(currSuspensionPos, currSuspensionPos + (-transform.up * pogoBottomSize));
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(currSuspensionPos, 0.05f);
                Gizmos.DrawWireSphere(currSuspensionPos + (-transform.up * pogoBottomSize), 0.05f);
            }
            else
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, suspensionPos);
                Gizmos.color = Color.blue;
                Gizmos.DrawLine(suspensionPos, suspensionPos + (-transform.up * pogoBottomSize));
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(suspensionPos, 0.05f);
                Gizmos.DrawWireSphere(suspensionPos + (-transform.up * pogoBottomSize), 0.05f);
            }
        }
    }


}