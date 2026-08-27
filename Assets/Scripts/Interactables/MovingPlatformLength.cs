using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/*
 * Lets a moving platform's tile count be resized from the Inspector instead of building
 * left/middle/right sprites by hand. The platform's own SpriteRenderer/transform is treated
 * as the fixed centre tile so widening the platform never shifts its origin (and therefore
 * never disturbs waypoint-based movement); extra middle tiles are added symmetrically on
 * either side, and left/right edges plus colliders are repositioned to match.
 */
[ExecuteAlways]
public class MovingPlatformLength : MonoBehaviour
{
    [Header("Length")]
    // Total tile count: left edge + middle tiles + right edge. Must be odd (edges + a
    // symmetric run of middles) and at least 3; even values are rounded up automatically.
    [Min(3)]
    [SerializeField] private int length = 3;
    // World-space width of one tile; edge and middle sprites must all share this width.
    [SerializeField] private float tileWidth = 1f;

    [Header("Tile References")]
    [SerializeField] private Transform leftEdge;
    [SerializeField] private Transform rightEdge;
    [SerializeField] private Sprite middleSprite;

    [Header("Collider References")]
    // Main platform collider (on this GameObject), resized to span the full length.
    [SerializeField] private BoxCollider2D platformCollider;
    // Optional rider-detection trigger (e.g. the "Trigger" child), resized to match.
    [SerializeField] private BoxCollider2D riderTriggerCollider;

    private SpriteRenderer centreSpriteRenderer;

    void OnEnable()
    {
        centreSpriteRenderer = GetComponent<SpriteRenderer>();
        Rebuild();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        // Hierarchy edits (creating/destroying tiles) aren't allowed from inside OnValidate,
        // so defer the rebuild to the next editor tick.
        EditorApplication.delayCall += () =>
        {
            if (this == null) return;
            if (centreSpriteRenderer == null) centreSpriteRenderer = GetComponent<SpriteRenderer>();
            Rebuild();
        };
    }
#endif

    public void Rebuild()
    {
        if (length < 3) length = 3;
        if (length % 2 == 0) length++;

        // Middle tiles excluding the centre one (this GameObject's own sprite), split evenly
        // either side of centre.
        int middleCount = length - 2;
        int sideExtraCount = (middleCount - 1) / 2;

        // Rediscover generated tiles from the hierarchy itself (rather than trusting a cached
        // list) and rebuild them from scratch each time. A cached reference list can desync
        // from the real hierarchy on a prefab instance, leaving orphaned tiles behind that
        // silently pile up across editor sessions.
        List<Transform> existingTiles = new List<Transform>();
        foreach (Transform child in transform)
        {
            if (child.GetComponent<MovingPlatformMiddleTile>() != null)
                existingTiles.Add(child);
        }
        foreach (Transform tile in existingTiles)
        {
            if (Application.isPlaying) Destroy(tile.gameObject);
            else DestroyImmediate(tile.gameObject);
        }

        List<Transform> extraMiddleTiles = new List<Transform>(sideExtraCount * 2);
        for (int i = 0; i < sideExtraCount * 2; i++)
        {
            GameObject tile = new GameObject($"MiddleTile_{i}");
            tile.transform.SetParent(transform, false);
            tile.AddComponent<MovingPlatformMiddleTile>();
            var sr = tile.AddComponent<SpriteRenderer>();
            sr.sprite = middleSprite;
            if (centreSpriteRenderer != null)
            {
                sr.sortingLayerID = centreSpriteRenderer.sortingLayerID;
                sr.sortingOrder = centreSpriteRenderer.sortingOrder;
                sr.sharedMaterial = centreSpriteRenderer.sharedMaterial;
            }
            extraMiddleTiles.Add(tile.transform);
        }

        // Centre tile (this object's own sprite/transform) always sits at local x = 0.
        for (int side = 0; side < sideExtraCount; side++)
        {
            float x = tileWidth * (side + 1);
            SetLocalX(extraMiddleTiles[side * 2], -x);
            SetLocalX(extraMiddleTiles[side * 2 + 1], x);
        }

        float edgeX = tileWidth * (sideExtraCount + 1);
        if (leftEdge != null) SetLocalX(leftEdge, -edgeX);
        if (rightEdge != null) SetLocalX(rightEdge, edgeX);

        float totalWidth = length * tileWidth;
        if (platformCollider != null)
        {
            platformCollider.size = new Vector2(totalWidth, platformCollider.size.y);
        }
        if (riderTriggerCollider != null)
        {
            riderTriggerCollider.size = new Vector2(totalWidth, riderTriggerCollider.size.y);
        }
    }

    private static void SetLocalX(Transform t, float x)
    {
        Vector3 p = t.localPosition;
        p.x = x;
        t.localPosition = p;
    }
}
