using UnityEngine;
using UnityEngine.UI;


namespace RageRunGames.PogostickController
{
    public class PogoStickControllerUI : MonoBehaviour
    {
        [SerializeField] private Image jumpForceIndicator;
        [SerializeField] private Gradient jumpForceGradient;

        private PogostickController pogostickController;

        private void Awake()
        {
            pogostickController = GetComponent<PogostickController>();
        }

        private void OnEnable()
        {
            pogostickController.OnReset += ResetJumpForceIndicator;
        }

        private void OnDisable()
        {
            pogostickController.OnReset -= ResetJumpForceIndicator;
        }

        private void ResetJumpForceIndicator()
        {
            jumpForceIndicator.fillAmount = 0;
            jumpForceIndicator.color = jumpForceGradient.Evaluate(0);
        }

        private void Update()
        {
            if (pogostickController.HoldingJumpKey)
            {
                float normalizedJumpForce = pogostickController.GetNormalizedAccumulatedJump();
                jumpForceIndicator.fillAmount = normalizedJumpForce;
                jumpForceIndicator.color = jumpForceGradient.Evaluate(normalizedJumpForce);
            }

            if (pogostickController.IsJumpPressed)
            {
                jumpForceIndicator.fillAmount = 0;
                jumpForceIndicator.color = jumpForceGradient.Evaluate(0);
            }

        }
    }
}