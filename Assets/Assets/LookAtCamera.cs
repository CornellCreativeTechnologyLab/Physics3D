using UnityEngine;

public class LookAtCamera : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    [Tooltip("Keep the label pinned at its starting position; only rotation changes.")]
    [SerializeField] private bool lockPosition = true;

    [Tooltip("3D text faces +Z, so by default the label points AWAY from the camera to stay readable. Turn off if your letters face the other way.")]
    [SerializeField] private bool faceAway = true;

    private Vector3 lockedPosition;
    private Vector3 pivotOffset;   // letters' center relative to this transform

    void Start()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        lockedPosition = transform.position;
        pivotOffset = ComputeChildrenCenter() - transform.position;
    }

    void LateUpdate()
    {
        if (targetCamera == null) return;

        if (lockPosition)
            transform.position = lockedPosition;

        // The point that should face the camera = the letters' visual center.
        Vector3 visualCenter = transform.position + transform.rotation * pivotOffset;

        // Full billboard: aim at the camera's actual position so the label tilts
        // to face a high/low camera instead of only spinning around vertical.
        Vector3 toCamera = targetCamera.transform.position - visualCenter;
        if (toCamera.sqrMagnitude < 1e-4f) return;

        Vector3 forward = faceAway ? -toCamera : toCamera;
        Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);

        transform.rotation = targetRot;

        // Keep the visual center fixed in place while rotating about it.
        if (lockPosition)
            transform.position = lockedPosition + (pivotOffset - targetRot * pivotOffset);
    }

    private Vector3 ComputeChildrenCenter()
    {
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return transform.position;

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);
        return b.center;
    }
}