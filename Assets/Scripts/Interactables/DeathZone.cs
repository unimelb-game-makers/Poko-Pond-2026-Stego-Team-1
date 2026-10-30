using UnityEngine;

public class DeathZone : MonoBehaviour
{
    [SerializeField, Min(0.02f)] private float contactHeight = 0.25f;
    private BoxCollider2D surface;
    private void Awake() => surface = GetComponent<BoxCollider2D>();

    private void FixedUpdate()
    {
        Vector2 top = transform.TransformPoint(surface.offset + Vector2.up * surface.size.y * 0.5f);
        PlayerLife.KillAllInBox(top, new Vector2(surface.size.x * Mathf.Abs(transform.lossyScale.x), contactHeight));
    }
}
