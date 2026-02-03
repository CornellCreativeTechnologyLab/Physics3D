using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool flipRotation = true;

    private Vector3 lockedPosition;

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;

        // Record exactly where the object is when the game starts
        lockedPosition = transform.position;
    }

    void LateUpdate()
    {
        if (targetCamera != null)
        {
            // Force the position to stay where it started
            transform.position = lockedPosition;

            // Match rotation
            transform.rotation = targetCamera.transform.rotation;

            if (flipRotation)
            {
                transform.Rotate(0, 180, 0);
            }
        }
    }
}