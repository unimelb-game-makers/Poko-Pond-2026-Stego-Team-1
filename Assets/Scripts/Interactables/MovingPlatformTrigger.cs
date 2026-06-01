using UnityEngine;

/*
 * This class encapsulates all Trigger based functionality of the moving collider. Which is used to keep
 * the player moving with the platform when on top of it. 
 */
public class MovingPlatformTrigger : MonoBehaviour
{
    [Header("Player")] public GameObject Player;
    public LayerMask PlayerSoftBodyLayer;
    private Rigidbody2D rb;
    private bool playerColliding = false;
    
    void Start()
    {
        rb = this.transform.parent.gameObject.GetComponent<Rigidbody2D>();
        Player = GameObject.FindWithTag("Player");
    }
    
    void FixedUpdate()
    {
        if (playerColliding)
        {
            Player.GetComponent<SoftBodyPlayer>().SetConstantForce(new Vector3(rb.linearVelocity.x, 0, 0));
        }
    }
    
    private void OnTriggerStay2D(Collider2D collision)
    {
        if ((1 << collision.gameObject.layer)  == PlayerSoftBodyLayer.value)
        {
            playerColliding = true;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            Player.GetComponent<SoftBodyPlayer>().SetConstantForce(new Vector3(0, 0, 0));
            playerColliding = false;
        }
    }
}
