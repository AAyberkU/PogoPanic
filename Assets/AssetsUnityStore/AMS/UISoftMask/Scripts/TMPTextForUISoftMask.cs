using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace AMS.UI.SoftMask
{
    [AddComponentMenu("UI/TextMeshPro - Text (UI) + AMS UI Soft Mask")]
    public class TMPTextForUISoftMask : TextMeshProUGUI
    {
        internal UnityAction AfterGenerateTextMesh;

        internal new void Awake()
        {
            base.Awake();
        }

        protected override void GenerateTextMesh()
        {
            base.GenerateTextMesh();
            
            AfterGenerateTextMesh?.Invoke();
        }
    }
}