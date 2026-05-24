using System.Collections.Generic;
using UnityEngine;

/*
 *  Attach to any player body — the main SoftBodyPlayer or a split droplet.
 *  Hazards call Kill() directly, or use the static KillAllInBox() helper to
 *  hit any player body inside a world-space rectangle without needing to track
 *  whether the player is merged or split.
 *
 *  Kill() fires EventManager.OnPlayerKilled; GameStateManager listens to that
 *  event and handles the Game Over transition.  Hazards have no dependency on
 *  GameStateManager — new hazard types just call Kill() or KillAllInBox() and
 *  everything else is handled automatically.
 */
public class PlayerLife : MonoBehaviour
{
    private bool _dead;

    public void Kill()
    {
        if (_dead) return;
        _dead = true;
        EventManager.PlayerKilled();
    }

    // Finds every player body (main or split droplet) whose softbody points
    // overlap the given world-space box and calls Kill() on each.
    // Uses the SoftBodyPoint physics layer so the check works regardless of
    // split state — callers never need to know about PlayerSplitController.
    public static void KillAllInBox(Vector2 center, Vector2 size)
    {
        int layer = LayerMask.NameToLayer("SoftBodyPoint");
        if (layer < 0)
        {
            Debug.LogWarning("[PlayerLife] 'SoftBodyPoint' layer not found — KillAllInBox had no effect.");
            return;
        }

        Collider2D[] hits = Physics2D.OverlapBoxAll(center, size, 0f, 1 << layer);
        var seen = new HashSet<PlayerLife>();
        foreach (Collider2D col in hits)
        {
            SoftBodyPointRef pointRef = col.GetComponent<SoftBodyPointRef>();
            if (pointRef == null) continue;
            PlayerLife life = pointRef.owner.GetComponent<PlayerLife>();
            if (life != null && seen.Add(life))
                life.Kill();
        }
    }
}
