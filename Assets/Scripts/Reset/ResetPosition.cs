using UnityEngine;

public class ResetPosition : MonoBehaviour
{
    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.M))
        {
            transform.position = startPosition;
        }
    }
}