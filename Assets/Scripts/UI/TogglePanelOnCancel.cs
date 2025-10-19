using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;
using Pogo.UI;                     // brings InGamePauseMenu into scope

public class TogglePanelOnCancel : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] InputActionReference cancelAction;

    [Header("Panel (leave blank to auto-find InGamePauseMenu)")]
    [SerializeField] GameObject targetPanel;

    PanelScaleAnimator anim;

    // ────────────────────────────────────────────────────────────────
    void Awake()
    {
        if (!targetPanel)
        {
            var pause = FindObjectOfType<InGamePauseMenu>(includeInactive: true);
            if (pause) targetPanel = pause.gameObject;
        }

        if (!targetPanel)
        { Debug.LogError("[TogglePanelOnCancel] No panel found."); enabled = false; return; }

        anim = targetPanel.GetComponent<PanelScaleAnimator>();
        if (!anim)
        { Debug.LogError("[TogglePanelOnCancel] Missing PanelScaleAnimator."); enabled = false; }
    }

    void OnEnable()
    {
        if (cancelAction == null) { enabled = false; return; }
        cancelAction.action.performed += OnCancel;
        cancelAction.action.Enable();
    }

    void OnDisable()
    {
        if (cancelAction != null)
        {
            cancelAction.action.performed -= OnCancel;
            cancelAction.action.Disable();
        }
    }

    // ────────────────────────────────────────────────────────────────
    void OnCancel(InputAction.CallbackContext _)
    {
        var pause = targetPanel.GetComponent<InGamePauseMenu>(); // <— get the script once

        if (anim.IsOpen)
        {
            pause?.CloseAllPanels();          // <— NEW LINE
            anim.Close();
            HideCursor();
        }
        else
        {
            anim.Open();
            ShowCursor();
        }
    }

    // ────────────────────────────────────────────────────────────────
    static void ShowCursor()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible   = true;
    }

    static void HideCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible   = false;
    }
}
