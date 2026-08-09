using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;


public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float speed = 3f;
    [SerializeField] private bool moveRightInitially = true;

    [Header("Rider Settings (Player)")]
    
    [Header("Wall Detection")]
    [SerializeField] public LayerMask wallLayer;
    [SerializeField] private float checkDistance = 0.1f;
    [SerializeField] private Collider2D platformCollider;

    [Header("Player")] public GameObject Player;

    private Rigidbody2D rb;
    private Vector2 moveDirection;
    
    // Cooldown tracking for collision detection to prevent rapid flipping or oscillation
    private int collisionCooldownFrames = 0;
    private const int COOLDOWN_FRAMES = 25;

    // Track the last hit collider to identify unique objects
    private Collider2D lastHitCollider;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        Player = GameObject.FindWithTag("Player");
        
        platformCollider = GetComponent<Collider2D>();

        rb.isKinematic = true;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        moveDirection = moveRightInitially ? Vector2.right : Vector2.left;
    }

    void FixedUpdate()
    {
        rb.linearVelocity = moveDirection * speed;

        // Determine the edge of the platform based on direction
        float boundsEdge = moveDirection.x > 0 ? platformCollider.bounds.max.x : platformCollider.bounds.min.x;
        Vector2 rayOrigin = new Vector2(boundsEdge, transform.position.y);

        // Cast a tiny raycast forward to see if a wall is immediately in front of it
        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, moveDirection, checkDistance, wallLayer);
    
        // If we hit a different object than last frame, reset cooldown
        if (hit.collider != null && hit.collider != lastHitCollider)
        {
            collisionCooldownFrames = 0;
        }

        if (collisionCooldownFrames > 0)
        {
            collisionCooldownFrames--;
        }
        else
        {
            if (hit.collider != null)
            {
                // Ignore collision with self only; allow interaction with other MovingPlatforms
                if (hit.collider.gameObject == gameObject)
                {
                    return;
                }

				FlipDirection();

                // Start cooldown to prevent immediate re-triggering or oscillation
                collisionCooldownFrames = COOLDOWN_FRAMES;
            }
        }
        
        lastHitCollider = hit.collider;
    }

    private void FlipDirection()
    {
        moveDirection = -moveDirection;
    }


}