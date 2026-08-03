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
    
        if (hit.collider != null)
        {
            FlipDirection();
        }
    }

    private void FlipDirection()
    {
        moveDirection = -moveDirection;
    }
}