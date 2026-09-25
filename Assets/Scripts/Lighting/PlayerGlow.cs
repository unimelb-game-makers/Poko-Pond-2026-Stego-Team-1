using UnityEngine;
using UnityEngine.Rendering.Universal;

// Keeps a soft light on the blob's center so it never gets lost in dark rooms.
[RequireComponent(typeof(SoftBodyPlayer))]
public class PlayerGlow : MonoBehaviour
{
    public Light2D glow;

    private SoftBodyPlayer _body;

    private void Awake()
    {
        _body = GetComponent<SoftBodyPlayer>();
    }

    private void LateUpdate()
    {
        if (glow == null) return;
        var center = _body.Center;
        glow.transform.position = new Vector3(center.x, center.y, glow.transform.position.z);
    }
}
