using UnityEngine;

/*
 *  Player                  (GameObject — tag must be set to "Player")
 *  ├── PlayerMovement      (this script)
 *  ├── CapsuleCollider2D   (used to locate foot/head origins for raycasts)
 *  └── SpriteRenderer      (visual representation)
 *
 *  Tilemap                 (must have TilemapCollider2D + CompositeCollider2D, layer set to "Ground")
 *  — OR —
 *  Platforms               (parent GameObject)
 *  └── Platform (×N)       (each must have a Collider2D and be assigned the "Ground" layer)
 *
 *  Assign the "Ground" layer to all ground/platform surfaces, and set the "Ground Layer" field of this script to match.
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

    [Header("Ceiling Check")]
    [Tooltip("Skin thickness kept between the player's head and the ceiling surface. " +
             "Platforms are fully solid — the player cannot jump through them from below.")]
    public float ceilingSkin = 0.05f;

    [Header("Wall Check")]
    [Tooltip("Skin thickness used for horizontal wall raycasts. 0.05–0.1 works well for most setups.")]
    public float wallSkin = 0.05f;

    [Tooltip("Layer mask that identifies ground surfaces. Must match the layer assigned to all platform/tilemap GameObjects.")]
    public LayerMask groundLayer;

    [Header("Level Bounds")]
    [Tooltip("The PolygonCollider2D on the Background that defines the level edges. The player cannot move outside it.")]
    public PolygonCollider2D levelBounds;

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

        // Block horizontal movement into a wall and snap flush to the surface
        if (moveInput != 0f)
        {
            float moveAmount = moveInput * moveSpeed * Time.deltaTime;
            RaycastHit2D wallHit = CastWallRay(Mathf.Sign(moveInput), wallSkin + Mathf.Abs(moveAmount));
            if (wallHit.collider != null)
                moveInput = 0f;
        }

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
            // Moving upward — check for a solid ceiling to prevent jumping through platforms
            float dynamicCeilLen = ceilingSkin + verticalVelocity * Time.deltaTime;
            RaycastHit2D ceilHit = CastCeilingRay(dynamicCeilLen);

            if (ceilHit.collider != null)
            {
                // Hit the underside of a platform; kill upward velocity and snap head flush
                verticalVelocity = 0f;
                verticalMove = ceilHit.distance;
            }
            else
            {
                isGrounded = false;
                verticalMove = verticalVelocity * Time.deltaTime;
            }
        }

        // Jump: override verticalMove for this frame so the first frame of a jump moves immediately
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            verticalVelocity = jumpForce;
            verticalMove = verticalVelocity * Time.deltaTime;
            isGrounded = false;
        }

        transform.Translate(new Vector3(moveInput * moveSpeed * Time.deltaTime, verticalMove, 0f));

        if (levelBounds != null)
            ClampToBounds();
    }

    private void ClampToBounds()
    {
        Bounds b = levelBounds.bounds;
        float halfW = capsule.size.x * 0.5f * transform.localScale.x;

        float clampedX = Mathf.Clamp(transform.position.x, b.min.x + halfW, b.max.x - halfW);
        transform.position = new Vector3(clampedX, transform.position.y, transform.position.z);
    }

    // Returns the world-space position of the bottom-centre of the CapsuleCollider2D.
    private Vector2 GetFootOrigin()
    {
        Vector2 origin = (Vector2)transform.position + capsule.offset;
        origin.y -= capsule.size.y * 0.5f * transform.localScale.y;
        return origin;
    }

    // Returns the world-space position of the top-centre of the CapsuleCollider2D.
    private Vector2 GetHeadOrigin()
    {
        Vector2 origin = (Vector2)transform.position + capsule.offset;
        origin.y += capsule.size.y * 0.5f * transform.localScale.y;
        return origin;
    }

    // Casts a ray straight down from the player's feet with the given length.
    private RaycastHit2D CastGroundRay(float length)
    {
        return Physics2D.Raycast(GetFootOrigin(), Vector2.down, length, groundLayer);
    }

    // Casts a ray straight up from the player's head with the given length.
    private RaycastHit2D CastCeilingRay(float length)
    {
        return Physics2D.Raycast(GetHeadOrigin(), Vector2.up, length, groundLayer);
    }

    // Casts three horizontal rays (low, mid, high) and returns the closest hit.
    // Three rays ensure the player can't clip into a platform at any height along the capsule.
    private RaycastHit2D CastWallRay(float direction, float length)
    {
        Vector2 center = (Vector2)transform.position + capsule.offset;
        float halfH = capsule.size.y * 0.5f * transform.localScale.y;
        float margin = 0.1f;
        Vector2 dir = new Vector2(direction, 0f);

        RaycastHit2D hitLow  = Physics2D.Raycast(center + Vector2.up * (-halfH + margin), dir, length, groundLayer);
        RaycastHit2D hitMid  = Physics2D.Raycast(center, dir, length, groundLayer);
        RaycastHit2D hitHigh = Physics2D.Raycast(center + Vector2.up * (halfH - margin), dir, length, groundLayer);

        RaycastHit2D closest = default;
        foreach (RaycastHit2D hit in new[] { hitLow, hitMid, hitHigh })
        {
            if (hit.collider != null && (closest.collider == null || hit.distance < closest.distance))
                closest = hit;
        }
        return closest;
    }

    // Draws the ground and ceiling check rays in the Scene view when this GameObject is selected.
    // Ground ray: green = grounded, red = airborne. Ceiling ray: always cyan.
    private void OnDrawGizmosSelected()
    {
        if (capsule == null)
            capsule = GetComponent<CapsuleCollider2D>();
        if (capsule == null) return;

        Gizmos.color = isGrounded ? Color.green : Color.red;
        Gizmos.DrawLine(GetFootOrigin(), GetFootOrigin() + Vector2.down * groundSkin);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(GetHeadOrigin(), GetHeadOrigin() + Vector2.up * ceilingSkin);

        Vector2 mid = (Vector2)transform.position + capsule.offset;
        float halfH = capsule.size.y * 0.5f * transform.localScale.y;
        float margin = 0.1f;
        Gizmos.color = Color.yellow;
        foreach (Vector2 origin in new[] {
            mid + Vector2.up * (-halfH + margin),
            mid,
            mid + Vector2.up * (halfH - margin) })
        {
            Gizmos.DrawLine(origin, origin + Vector2.right * wallSkin);
            Gizmos.DrawLine(origin, origin + Vector2.left * wallSkin);
        }
    }
}
