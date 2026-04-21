using UnityEngine;

/*
 *  Main Camera
 *  └── CameraFollow    (this script)
 *
 *  LevelBounds         (empty GameObject)
 *  └── BoxCollider2D   (defines the camera's clamping region — set Is Trigger: true)
 *
 *  Assign the player transform to "Target" and the LevelBounds collider to "Bounds Collider".
 *  The camera follows the player and stops at the edges of the bounds so the view
 *  never shows outside the defined play area.
 */
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The transform the camera follows — assign the Player.")]
    public Transform target;

    [Header("Smoothing")]
    [Tooltip("How quickly the camera catches up to the player. Higher = snappier. 0 = instant.")]
    public float smoothSpeed = 6f;

    [Header("Bounds")]
    [Tooltip("BoxCollider2D that defines the edges of the level. The camera will not pan beyond it.")]
    public BoxCollider2D boundsCollider;

    private Camera cam;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 desired = new Vector3(target.position.x, target.position.y, transform.position.z);

        Vector3 smoothed = smoothSpeed > 0f
            ? Vector3.Lerp(transform.position, desired, smoothSpeed * Time.deltaTime)
            : desired;

        transform.position = boundsCollider != null ? Clamp(smoothed) : smoothed;
    }

    private Vector3 Clamp(Vector3 position)
    {
        Bounds b = boundsCollider.bounds;

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float minX = b.min.x + halfW;
        float maxX = b.max.x - halfW;
        float minY = b.min.y + halfH;
        float maxY = b.max.y - halfH;

        // If the bounds are smaller than the camera view, centre inside the bounds instead of clamping
        float x = (minX < maxX) ? Mathf.Clamp(position.x, minX, maxX) : b.center.x;
        float y = (minY < maxY) ? Mathf.Clamp(position.y, minY, maxY) : b.center.y;

        return new Vector3(x, y, position.z);
    }
}
