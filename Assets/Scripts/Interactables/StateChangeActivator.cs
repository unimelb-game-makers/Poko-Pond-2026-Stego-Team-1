using UnityEngine;

public class StateChangeActivator : MonoBehaviour
{
    private GameObject Player;
    public LayerMask PlayerSoftBodyLayer;
    public PlayerBodyState valueToChangeTo;
    void Start()
    {
        Player = GameObject.FindWithTag("Player");
    }
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            Player.GetComponent<SoftBodyPlayer>().changeBodyState(valueToChangeTo, new Vector2(transform.position.x, transform.position.y+0.5f));
        }
    }
}
