using UnityEngine;

/*
 *  Background
 *  └── ParallaxBackground    (this script)
 *
 *  Assign the Main Camera to "Cam". The background moves at a fraction of the
 *  camera's movement each frame, creating a parallax depth effect.
 *  A factor of 0 = background is fixed. A factor of 1 = background moves with the camera.
 */
public class ParallaxBackground : MonoBehaviour
{
    [Tooltip("The main camera. Assign Main Camera.")]
    public Transform cam;

    [Tooltip("How much the background moves relative to the camera. 0.1–0.4 works well for a distant background.")]
    [Range(0f, 1f)]
    public float parallaxFactor = 0.2f;

    private Vector3 previousCamPosition;

    private void Start()
    {
        previousCamPosition = cam.position;
    }

    private void LateUpdate()
    {
        Vector3 delta = cam.position - previousCamPosition;
        transform.position += new Vector3(delta.x * parallaxFactor, 0f, 0f);
        previousCamPosition = cam.position;
    }
}
