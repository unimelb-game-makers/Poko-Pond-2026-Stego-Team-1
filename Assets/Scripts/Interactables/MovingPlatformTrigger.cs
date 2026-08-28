using System.Collections.Generic;
using UnityEngine;

/*
 * This class encapsulates all Trigger based functionality of the moving collider. Which is used to keep
 * the player moving with the platform when on top of it. 
 */
public class MovingPlatformTrigger : MonoBehaviour
{
    [Header("Player")] public List<GameObject> Players;
    public LayerMask PlayerSoftBodyLayer;
    private Rigidbody2D rb;
    private bool playerColliding = false;
	private MovingPlatform parentPlatform;    

    void Start()
    {
        rb = this.transform.parent.gameObject.GetComponent<Rigidbody2D>();
		parentPlatform = transform.parent.gameObject.GetComponent<MovingPlatform>();
    }
    
    void FixedUpdate()
    {
		foreach (GameObject p in Players) {
			Vector2 parPlatVel = parentPlatform.getUnifiedVelocity();
			p.GetComponent<SoftBodyPlayer>().SetConstantForce(new Vector3(parPlatVel.x, parPlatVel.y, 0));
		}
    }

	private void OnTriggerEnter2D(Collider2D collision) {
		if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
			if (collision.gameObject.TryGetComponent<SoftBodyPointRef>(out var softBodyPointRef)) {
				Players.Add(softBodyPointRef.owner.transform.gameObject);
			} else if(collision.gameObject.TryGetComponent<SoftBodyPlayer>(out var softBodyPlayer)) {
				Players.Add(collision.gameObject);
			}
        }
	}

    private void OnTriggerExit2D(Collider2D collision)
    {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
			if (collision.gameObject.TryGetComponent<SoftBodyPointRef>(out var softBodyPointRef)) {
				int index = Players.FindIndex(obj => obj.GetInstanceID() == softBodyPointRef.owner.transform.gameObject.GetInstanceID());
				if(index != -1) {
					Players[index].GetComponent<SoftBodyPlayer>().SetConstantForce(new Vector3(0, 0, 0));
					Players.RemoveAt(index);
				}
			}
        }
    }
}
