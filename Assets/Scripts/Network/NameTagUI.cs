using UnityEngine;
using TMPro;
using Unity.Collections;

[DisallowMultipleComponent]
public class NameTagUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerNameData nameData;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private Transform followTarget;    // HeadAnchor

    [Header("Placement")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0, 0.45f, 0);

    [Header("Visibility")]
    [SerializeField] private float maxVisibleDistance = 35f;
    [SerializeField] private bool hideForLocalPlayer = true;

    private Camera _cam;
    private Transform _root;
    private bool _isLocalPlayer;

    private void Awake()
    {
        _cam = Camera.main;
        if (!nameData) nameData = GetComponentInParent<PlayerNameData>();
        _root = nameData ? nameData.transform : transform.parent;

        var nb = nameData ? nameData.GetComponent<Unity.Netcode.NetworkBehaviour>() : null;
        _isLocalPlayer = nb && nb.IsOwner;

        if (nameText) nameText.raycastTarget = false; // tıklanmasın
    }

    private void OnEnable()
    {
        if (nameData != null)
            nameData.DisplayName.OnValueChanged += OnNameChanged;

        if (nameData != null && nameText != null)
            nameText.text = nameData.DisplayName.Value.ToString();
    }

    private void OnDisable()
    {
        if (nameData != null)
            nameData.DisplayName.OnValueChanged -= OnNameChanged;
    }

    private void OnNameChanged(FixedString64Bytes oldV, FixedString64Bytes newV)
    {
        if (nameText) nameText.text = newV.ToString();
    }

    private void LateUpdate()
    {
        if (!_cam) { _cam = Camera.main; if (!_cam) return; }
        if (!_root) return;

        var anchor = followTarget ? followTarget.position : _root.position;
        transform.position = anchor + worldOffset;

        // billboard
        transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

        // visibility
        bool visible = !(_isLocalPlayer && hideForLocalPlayer);
        if (visible)
        {
            float dist = Vector3.Distance(_cam.transform.position, transform.position);
            if (dist > maxVisibleDistance) visible = false;
        }
        if (nameText && nameText.enabled != visible) nameText.enabled = visible;
    }
}
