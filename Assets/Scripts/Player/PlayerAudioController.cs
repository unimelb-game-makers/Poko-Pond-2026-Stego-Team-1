using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAudioController : MonoBehaviour
{

    [Header("Physics Triggers")]
    [Tooltip("Downward velocity required to trigger the falling sound")]
    public float fallSpeedThreshold = -5f;
    [Tooltip("What layers count as 'ground' for the landing sound?")]
    public LayerMask groundLayer;

    private Rigidbody2D rb;
    private bool isFalling = false;
    
    // Link to SoftBodyPlayer to access velocity calculations
    private SoftBodyPlayer softBodyPlayer;

    private Vector2 previousVelocity;

    void Awake()
    {
        // Access rigidbody for velocity tracking
        rb = GetComponent<Rigidbody2D>();
        softBodyPlayer = GetComponent<SoftBodyPlayer>();

        // Reset Fmod to liquid state
        AudioManager.SetParameter("State", 0);
        // Play blower sounds
        AudioManager.SFXOneShot("Blower");
    }

    void FixedUpdate()
    {
        Vector2 velocity = softBodyPlayer.CalculateAverageVelocity();
        
        // 1. FALLING TRIGGER: Check the physics velocity
        if (velocity.y < fallSpeedThreshold && !isFalling)
        {
            isFalling = true;
        }
        else if (velocity.y >= fallSpeedThreshold && isFalling)
        {
            isFalling = false;
            // Detect rapid change in velocity
            if (Mathf.Abs(previousVelocity.y) - Mathf.Abs(velocity.y) > 1.0f)
            {
                TriggerLanding();   
            }
        }

        previousVelocity = velocity;
    }

    // 2. LANDING TRIGGER: Listen for physics collisions
    void OnCollisionEnter2D(Collision2D collision)
    {
        // Check if the object we collided with is in the Ground layer mask
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            // Verify we actually landed ON TOP of the ground using collision normals
            foreach (ContactPoint2D contact in collision.contacts)
            {
                if (contact.normal.y > 0.5f) // Upward normal means ground is below us
                {
                    TriggerLanding();
                    break;
                }
            }
        }
    }

    private void TriggerLanding()
    {
        AudioManager.SFXOneShot("Land");
    }
}
