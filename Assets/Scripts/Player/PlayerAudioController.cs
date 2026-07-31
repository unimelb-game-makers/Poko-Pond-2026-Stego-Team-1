using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerAudioController : MonoBehaviour
{
    [Header("Audio Sources")]
    [Tooltip("Used for one-shot sounds like jumping and landing")]
    public AudioSource sfxSource;
    [Tooltip("Used for looping sounds like background music")]
    public AudioSource loopSource;

    [Header("Audio Clips")]
    public AudioClip jumpClip;
    public AudioClip jumpAltClip;
    public AudioClip landClip;
    public AudioClip backgroundClip;

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
        
        // Ensure the audio sources are properly configured
        if (loopSource != null)
        {
            loopSource.clip = backgroundClip;
            loopSource.loop = true;
            loopSource.Play();
        }
        if (sfxSource != null)
        {
            sfxSource.loop = false;
        }
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
            else
            {
                // Randomly pick one of two jump clips and apply a slight random pitch change, huge variety
                if (Random.value > 0.5f) {
                    sfxSource.pitch = Random.Range(0.9f, 1.1f);
                    sfxSource.clip = jumpClip;
                } else {
                    sfxSource.pitch = Random.Range(0.9f, 1.1f);
                    sfxSource.clip = jumpAltClip;
                }
            }
            
            sfxSource.Play();
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

    // 3. JUMP TRIGGER: To be called when jumping
    public void TriggerJump()
    {
        if (sfxSource != null && jumpClip != null && jumpAltClip != null)
        {
            if (Random.value > 0.5f) {
            	sfxSource.clip = jumpClip;
            	sfxSource.PlayOneShot(jumpClip);
            } else {
            	sfxSource.clip = jumpAltClip;
            	sfxSource.PlayOneShot(jumpAltClip);
            }
        }
    }

    private void TriggerLanding()
    {
        if (sfxSource != null && landClip != null)
        {
            if (!sfxSource.isPlaying || sfxSource.clip.name != landClip.name)
            {
                // Pitch randomization adds variety so the landing doesn't sound repetitive
                sfxSource.pitch = Random.Range(0.9f, 1.1f);
                sfxSource.PlayOneShot(landClip);
                sfxSource.pitch = 1f; // reset pitch   
            }
        }
        
    }
}
