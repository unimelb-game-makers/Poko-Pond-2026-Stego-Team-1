using System.Collections.Generic;
using UnityEngine;

// Walk-through exit mist. Placeholder artwork is independent of the mechanic.
[RequireComponent(typeof(BoxCollider2D))]
public class Humidifier : MonoBehaviour
{
    private BoxCollider2D mist;
    private readonly HashSet<SoftBodyPlayer> visitors = new();
    private void Awake() => mist = GetComponent<BoxCollider2D>();

    private void Update()
    {
        if (Time.timeScale == 0f) return;
        Bounds b = mist.bounds;
        visitors.Clear();
        // Resolve the contacted body, including split droplets, not a tagged
        // main player that might be hidden elsewhere in the scene.
        foreach (Collider2D hit in Physics2D.OverlapBoxAll(b.center, b.size, 0f,
                     LayerMask.GetMask("Player", "SoftBodyPoint")))
        {
            SoftBodyPlayer player = hit.GetComponent<SoftBodyPointRef>()?.owner;
            if (player == null) player = hit.GetComponentInParent<SoftBodyPlayer>();
            if (player == null || !visitors.Add(player)) continue;
            player.changeBodyStateInPlace(PlayerBodyState.Liquid);
        }
    }
}
