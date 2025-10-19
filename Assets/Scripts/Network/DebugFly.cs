#if DEVELOPMENT_BUILD || UNITY_EDITOR
using UnityEngine;
#if UNITY_NETCODE
using Unity.Netcode;
#endif

[DisallowMultipleComponent]
public class DebugFly : MonoBehaviour
{
    [Header("Activation")]
    [SerializeField] KeyCode toggleKey = KeyCode.F1;
    [SerializeField] bool onlyInDevelopmentBuild = true; // Prod build’de çalışmasın

    [Header("Movement")]
    [SerializeField] float moveSpeed = 8f;
    [SerializeField] float sprintMultiplier = 2.5f;
    [SerializeField] float verticalSpeed = 8f;

    [Header("Camera/Look")]
    [SerializeField] bool inheritCameraAim = true; 
    // true: kamera nereye bakıyorsa player da oraya baksın
    // false: (gerekirse) kendi mouse-look’unu yazarsın

    Camera cam;
    Rigidbody rb;
    CharacterController cc;
    Collider[] allCols;

    bool flying;

    // Orijinal durumları saklamak için
    bool wasCCEnabled;
    bool wasRBKinematic;
    bool wasRBUseGravity;
    Vector3 storedRBVel;
    bool[] colWasEnabled;

#if UNITY_NETCODE
    NetworkObject netObj;
#endif

    void Awake()
    {
        cam = GetComponentInChildren<Camera>();
        rb  = GetComponent<Rigidbody>();
        cc  = GetComponent<CharacterController>();
#if UNITY_NETCODE
        netObj = GetComponent<NetworkObject>();
#endif
        allCols = GetComponentsInChildren<Collider>(true);
        colWasEnabled = new bool[allCols.Length];
    }

    void OnDisable()
    {
        if (flying) StopFly();
    }

    void Update()
    {
        if (onlyInDevelopmentBuild && !Application.isEditor && !Debug.isDebugBuild)
            return;

#if UNITY_NETCODE
        if (netObj && !netObj.IsOwner) return;
#endif

        if (Input.GetKeyDown(toggleKey))
        {
            if (flying) StopFly();
            else StartFly();
        }

        if (!flying) return;

        HandleMove();
        AlignToCamera();
    }

    void StartFly()
    {
        flying = true;

        // CC kapat
        if (cc)
        {
            wasCCEnabled = cc.enabled;
            cc.enabled = false;
        }

        // RB’yi dondur
        if (rb)
        {
            wasRBKinematic  = rb.isKinematic;
            wasRBUseGravity = rb.useGravity;
            storedRBVel     = rb.linearVelocity;

            rb.isKinematic  = true;
            rb.useGravity   = false;
            rb.linearVelocity     = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // Noclip: tüm collider’ları kapat
        for (int i = 0; i < allCols.Length; i++)
        {
            if (!allCols[i]) continue;
            colWasEnabled[i] = allCols[i].enabled;
            allCols[i].enabled = false;
        }

        Debug.Log("<color=#8EE887>[DebugFly]</color> Fly+NoClip <b>ENABLED</b>.");
    }

    void StopFly()
    {
        flying = false;

        // Collider’ları eski haline
        for (int i = 0; i < allCols.Length; i++)
            if (allCols[i]) allCols[i].enabled = colWasEnabled[i];

        // RB eski haline
        if (rb)
        {
            rb.isKinematic = wasRBKinematic;
            rb.useGravity  = wasRBUseGravity;
            rb.linearVelocity    = Vector3.zero; // storedRBVel geri verilebilir; genelde 0 daha güvenli
        }

        // CC eski haline
        if (cc) cc.enabled = wasCCEnabled;

        Debug.Log("<color=#F6B26B>[DebugFly]</color> Fly+NoClip <b>DISABLED</b>.");
    }

    void HandleMove()
    {
        float dt = Time.unscaledDeltaTime;

        Vector3 input =
            (Input.GetKey(KeyCode.W) ? Vector3.forward : Vector3.zero) +
            (Input.GetKey(KeyCode.S) ? Vector3.back    : Vector3.zero) +
            (Input.GetKey(KeyCode.A) ? Vector3.left    : Vector3.zero) +
            (Input.GetKey(KeyCode.D) ? Vector3.right   : Vector3.zero);

        input = input.normalized;

        float y = 0f;
        if (Input.GetKey(KeyCode.Space)) y += 1f;
        if (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C)) y -= 1f;

        float speed = moveSpeed * (Input.GetKey(KeyCode.LeftShift) ? sprintMultiplier : 1f);

        // Hareket yönünü kamera baz alarak hesapla
        Transform basis = cam ? cam.transform : transform;
        Vector3 move = (basis.forward * input.z + basis.right * input.x);
        move.y = 0f;
        move = move.normalized * speed;

        move += Vector3.up * (y * verticalSpeed);

        transform.position += move * dt;
    }

    void AlignToCamera()
    {
        if (!inheritCameraAim || !cam) return;

        // Player yönünü kameranın baktığı yöne eşitle (yaw/pitch’i istersen sadece yaw’a indir)
        Vector3 fwd = cam.transform.forward;
        if (fwd.sqrMagnitude < 1e-6f) return;

        // Genelde sadece yatay yönü eşitlemek daha stabil:
        Vector3 flat = new Vector3(fwd.x, 0f, fwd.z);
        if (flat.sqrMagnitude > 1e-6f)
            transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
        else
            transform.forward = fwd;
    }
}
#endif