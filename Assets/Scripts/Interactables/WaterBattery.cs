using System.Collections.Generic;
using UnityEngine;

public class WaterBattery : MonoBehaviour
{
    [Header("Player")] public List<GameObject> Players;
    public LayerMask PlayerSoftBodyLayer;

    /*
     * If Player or a layer attached to softbody that is attached to a player (See how collision is handelled for ref) collides with this obj, call the function
     * applyVaccum on SoftBodyPlayer as per comment, parameters are true and position of this obj. Once the Player leaves call the func again with false and 0,0 Vec2 parameters AI!
     */

    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {

    }

    private void OnTriggerEnter2D(Collider2D collision) {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            SoftBodyPlayer player = null;
            Debug.Log("Hello");

            // Check for direct SoftBodyPlayer component
            if (collision.TryGetComponent<SoftBodyPlayer>(out var softBodyPlayer)) {
                player = softBodyPlayer;
            }
            // Check for SoftBodyPointRef to find owner
            else if (collision.TryGetComponent<SoftBodyPointRef>(out var softBodyPointRef)) {
                var ownerObj = softBodyPointRef.owner.transform.gameObject;
                if (ownerObj.TryGetComponent<SoftBodyPlayer>(out var ownerPlayer)) {
                    player = ownerPlayer;
                }
            }

            if (player != null) {
                // Avoid duplicate entries if already tracked
                if (!Players.Contains(player.gameObject)) {
                    Players.Add(player.gameObject);
                    player.applyVaccum(true, transform.position);
                }
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if ((1 << collision.gameObject.layer) == PlayerSoftBodyLayer.value)
        {
            SoftBodyPlayer player = null;

            // Check for direct SoftBodyPlayer component
            if (collision.TryGetComponent<SoftBodyPlayer>(out var softBodyPlayer)) {
                player = softBodyPlayer;
            }
            // Check for SoftBodyPointRef to find owner
            else if (collision.TryGetComponent<SoftBodyPointRef>(out var softBodyPointRef)) {
                var ownerObj = softBodyPointRef.owner.transform.gameObject;
                if (ownerObj.TryGetComponent<SoftBodyPlayer>(out var ownerPlayer)) {
                    player = ownerPlayer;
                }
            }

            if (player != null) {
                // Remove from list and stop vacuum effect
                Players.Remove(player.gameObject);
                player.applyVaccum(false, Vector2.zero);
            }
        }
    }


}

