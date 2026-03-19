using UnityEngine;

/*
 *  Player                  (GameObject — tag must be set to "Player")
 *  ├── PlayerMovement      (this script)
 *  ├── CapsuleCollider2D   (used to locate the foot origin for the ground raycast)
 *  └── SpriteRenderer      (visual representation)
 *
 *  Platforms               (parent GameObject)
 *  └── Platform (×N)       (each must have a Collider2D and be assigned the "Ground" layer)
 *
 *  Assign the "Ground" layer to all platform GameObjects, and set the "Ground Layer" field of this script to match.
 */
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Horizontal movement speed in units per second.")]
    public float moveSpeed = 8f;

    [Tooltip("Initial upward velocity applied when the player jumps.")]
    public float jumpForce = 14f;

    [Header("Gravity")]
    [Tooltip("Downward acceleration applied each second while airborne. Use a negative value.")]
    public float gravity = -30f;

    [Header("Ground Check")]
    [Tooltip("Skin thickness kept between the player's feet and the ground surface. " +
             "Also used as the base ray length when standing still. 0.05–0.1 works well for most setups.")]
    public float groundSkin = 0.05f;

    [Tooltip("Layer mask that identifies ground surfaces. Must match the layer assigned to all platform GameObjects.")]
    public LayerMask groundLayer;

    // Accumulated vertical velocity
    private float verticalVelocity;

    // Whether the player is currently touching the ground, updated each frame via raycast
    private bool isGrounded;

    // Cached reference to the CapsuleCollider2D
    private CapsuleCollider2D capsule;

    private void Start()
    {
        capsule = GetComponent<CapsuleCollider2D>();
    }

    private void Update()
    {
        float moveInput = Input.GetAxisRaw("Horizontal");

        // Accumulate gravity every frame regardless of grounded state;
        // zeroed out below when the player lands
        verticalVelocity += gravity * Time.deltaTime;

        float verticalMove;

        if (verticalVelocity <= 0f)
        {
            // Extend the ray to cover the full distance the player could fall this frame.
            float dynamicRayLen = groundSkin + Mathf.Abs(verticalVelocity * Time.deltaTime);
            RaycastHit2D hit = CastGroundRay(dynamicRayLen);

            if (hit.collider != null)
            {
                isGrounded = true;
                verticalVelocity = 0f;

                // Snap feet to the surface
                verticalMove = -hit.distance;
            }
            else
            {
                isGrounded = false;
                verticalMove = verticalVelocity * Time.deltaTime;
            }
        }
        else
        {
            // Moving upward, the player cannot be grounded
            isGrounded = false;
            verticalMove = verticalVelocity * Time.deltaTime;
        }

        // Jump: override verticalMove for this frame so the first frame of a jump
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalVelocity = jumpForce;
            verticalMove = verticalVelocity * Time.deltaTime;
            isGrounded = false;
        }

        transform.Translate(new Vector3(moveInput * moveSpeed * Time.deltaTime, verticalMove, 0f));
    }

    // Returns the world-space position of the bottom-centre of the CapsuleCollider2D,
    // used as the origin point for all ground raycasts.
    private Vector2 GetFootOrigin()
    {
        Vector2 origin = (Vector2)transform.position + capsule.offset;
        origin.y -= capsule.size.y * 0.5f * transform.localScale.y;
        return origin;
    }

    // Casts a ray straight down from the player's feet with the given length
    private RaycastHit2D CastGroundRay(float length)
    {
        return Physics2D.Raycast(GetFootOrigin(), Vector2.down, length, groundLayer);
    }

    // Draws the ground check ray in the Scene view when this GameObject is selected.
    // Green = grounded, Red = airborne.
    private void OnDrawGizmosSelected()
    {
        if (capsule == null)
            capsule = GetComponent<CapsuleCollider2D>();
        if (capsule == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(GetFootOrigin(), GetFootOrigin() + Vector2.down * groundSkin);
    }
}
