using System.Collections.Generic;
using UnityEngine;

/*
 *  Attach to any player body — the main SoftBodyPlayer or a split droplet.
 *  Hazards call Kill() directly, or use the static KillAllInBox() helper to
 *  hit any player body inside a world-space rectangle without needing to track
 *  whether the player is merged or split.  KillAllInBox remains unfiltered for
 *  generic hazards; callers that need a state-specific hazard can use the
 *  PlayerBodyState overload (or KillAllSolidInBox for crushers).
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
    // overlap the given world-space box and calls Kill() on each.  This is the
    // generic, state-agnostic API intended for future hazards.
    public static void KillAllInBox(Vector2 center, Vector2 size)
    {
        KillAllInBox(center, size, null);
    }

    // State-filtered variant used by hazards whose damage only applies to one
    // body state.  The unfiltered overload above intentionally remains intact
    // so new hazards do not need to opt into state filtering.
    public static void KillAllInBox(Vector2 center, Vector2 size, PlayerBodyState? requiredState)
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
            if (!TryResolveOwner(col, out SoftBodyPlayer owner)) continue;
            if (requiredState.HasValue && owner.getBodyState() != requiredState.Value) continue;

            PlayerLife life = owner.GetComponent<PlayerLife>();
            if (life != null && seen.Add(life))
                life.Kill();
        }
    }

    // Convenience API for the current crusher hazard.  Keeping the state
    // choice here means crusher implementations do not duplicate owner or
    // split-body filtering logic.
    public static void KillAllSolidInBox(Vector2 center, Vector2 size)
    {
        KillAllInBox(center, size, PlayerBodyState.Solid);
    }

    private static bool TryResolveOwner(Collider2D collider, out SoftBodyPlayer owner)
    {
        // Soft-body point GameObjects are spawned independently of the player
        // hierarchy, so SoftBodyPointRef.owner is the authoritative link.
        SoftBodyPointRef pointRef = collider.GetComponent<SoftBodyPointRef>();
        if (pointRef != null && pointRef.owner != null)
        {
            owner = pointRef.owner;
            return true;
        }

        // Keep direct/parent lookups as a fallback for player colliders and
        // future player-body implementations that do use a hierarchy.
        owner = collider.GetComponent<SoftBodyPlayer>();
        if (owner == null)
            owner = collider.GetComponentInParent<SoftBodyPlayer>();

        return owner != null;
    }
}
