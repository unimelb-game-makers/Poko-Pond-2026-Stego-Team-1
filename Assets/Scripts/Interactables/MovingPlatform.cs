using UnityEngine;
using UnityEngine.Events;

public class MovingPlatform : MonoBehaviour
{
    [Header("Movement Mode")]
    // Toggle between linear and circular motion modes.
    [SerializeField] private bool useCircularMotion = false; 

    [Header("Linear Collision Detection")]
    // Layer mask defining which layers act as walls for collision detection in linear mode.
    [SerializeField] private LayerMask wallLayer; 
    // Distance ahead of the platform to check for collisions in linear mode.
    [SerializeField] private float checkDistance = 0.5f; 
    // Number of frames to wait after hitting a wall before checking for collisions again in linear mode.
    [SerializeField] private int collisionCooldownFrames = 10; 
    private int cooldownCounter = 0;

    [Header("Linear Movement Settings")]
    // Direction vector for linear movement.
    [SerializeField] private Vector2 moveDirection = Vector2.right;
    // Speed of the platform in units per second.
    [SerializeField] private float speed = 2f;
    // Reference to the platform's collider, used for bounds calculation in linear mode.
    private Collider2D platformCollider;

    [Header("Circular Movement Settings")]
    // The center point around which the platform rotates in circular mode.
    [SerializeField] private Transform pivotPoint; 
    // Initial phase offset in degrees for circular motion, allowing platforms to start at different positions.
    [SerializeField] private float phaseOffset = 0f; 
    // Radius of the circular path from the pivot point.
    [SerializeField] private float radius = 2f;   
    // Angular velocity multiplier for circular motion, applied per fixed frame.
    [SerializeField] private float angleIncrementPerFrame = 10f; 

    [Header("Rider Settings (Player)")]
    [Header("Player")] public GameObject Player;

    private Rigidbody2D rb;
    
    // Current angle of rotation in degrees for circular motion.
    private float currentAngle = 0f;

    void Start()
    {
        // Cache the rigidbody component
        rb = GetComponent<Rigidbody2D>();
        
        // Fallback: Attempt to find player by tag if not assigned in inspector
        if (Player == null) 
            Player = GameObject.FindWithTag("Player");

        // Cache collider for bounds calculations in linear mode
        platformCollider = GetComponent<Collider2D>(); 
        
        // Ensure the platform is kinematic so physics doesn't interfere with scripted movement
        rb.isKinematic = true;
        
        cooldownCounter = 0; 

        // Validate required fields based on active mode
        if (useCircularMotion && pivotPoint == null)
        {
            Debug.LogError("Pivot Point not assigned on MovingPlatform");
            return; 
        }

        if (!useCircularMotion && moveDirection == Vector2.zero)
        {
             Debug.LogWarning("Move Direction not set for linear mode.");
        }

    }

	public Vector2 getUnifiedVelocity() {
		if (useCircularMotion)
		{
			// Calculate angular velocity in radians per second. 
			// Note: angleIncrementPerFrame is degrees/frame, so we convert to rad/s using Time.fixedDeltaTime.
            float angularVelocityRadPerSec = (angleIncrementPerFrame * Mathf.Deg2Rad) / Time.fixedDeltaTime;

            // Current total angle including phase offset
            float totalAngle = currentAngle + phaseOffset;
            
            // Tangent vector for counter-clockwise rotation: (-sin, cos)
            Vector2 tangent = new Vector2(-Mathf.Sin(totalAngle * Mathf.Deg2Rad), Mathf.Cos(totalAngle * Mathf.Deg2Rad));

			// Velocity is angular velocity (rad/s) * radius. Direction is tangent.
            return tangent * (angularVelocityRadPerSec * radius);
        }
		else 
		{
			return rb.linearVelocity;
		}
    }

    void FixedUpdate()
    {		
    	if (useCircularMotion)
    	{	
    		// Update angle based on fixed timestep            
            currentAngle += angleIncrementPerFrame * Time.fixedDeltaTime; 
            
            // Calculate total angle including phase offset, then convert to radians
            float totalAngle = currentAngle + phaseOffset;
            float radian = totalAngle * Mathf.Deg2Rad;
          	
            Vector2 offset = new Vector2(Mathf.Cos(radian), Mathf.Sin(radian)) * radius;
            
            // Calculate target position relative to pivot, ensuring Z-axis remains unchanged
            Vector3 targetPosition = pivotPoint.position + new Vector3(offset.x, offset.y, 0f);
            
            // Move the rigidbody to the target position smoothly within the physics step
            rb.MovePosition(targetPosition);

        } 
    	else 
    	{	
    		if (rb != null)
    		{
    			// Decrement cooldown counter if active
    			if (cooldownCounter > 0)
                {
                    cooldownCounter--; 
                }

        		// Determine the edge of the platform based on current direction
        		float boundsEdge = moveDirection.x > 0 ? platformCollider.bounds.max.x : platformCollider.bounds.min.x;
        		Vector2 rayOrigin = new Vector2(boundsEdge, transform.position.y);
                
                // Cast a ray ahead to detect walls
                RaycastHit2D hit = Physics2D.Raycast(rayOrigin, moveDirection, checkDistance, wallLayer);
                
                if (hit.collider != null)
                {
                    // Ignore collisions with self; allow interaction with other platforms                 
                	if (hit.collider.gameObject != gameObject)                    
                        {                    	
                        	// Reverse direction and reset cooldown to prevent immediate re-collision                        
                            moveDirection = -moveDirection;                            
                            cooldownCounter = collisionCooldownFrames;

                            // Nudge position slightly to prevent sticking into the wall
                            transform.position += new Vector3(-moveDirection.x * 0.01f, -moveDirection.y * 0.01f, 0);                        
                        }                    
                }
                
                // Apply velocity based on current direction and speed
                rb.linearVelocity = moveDirection * speed;

            }
        }
    }
}