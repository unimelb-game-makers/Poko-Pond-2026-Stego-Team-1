using UnityEngine;

// Procedural spark/chain-lightning visual across a platform's top surface while
// MovingPlatform.IsElectrifying is true. MovingPlatformTrigger handles the actual kill on
// contact — this is cosmetic only.
//
// Needs no art assets: small jagged bolts are drawn with LineRenderers and snapped to a
// coarse grid so they read as chunky pixel-art sparks rather than smooth vector lines.
// Bolts flicker in and out at random along the surface width instead of forming one
// continuous "wire" across the platform.
[RequireComponent(typeof(MovingPlatform))]
public class ElectrifyingPlatformEffect : MonoBehaviour
{
    [Header("Surface")]
    [Tooltip("Fallback width sparks scatter across if no BoxCollider2D is found to read live.")]
    [SerializeField] private float surfaceWidth = 3f;
    [Tooltip("Extra lift above the platform's actual collider top, so sparks sit right at the surface.")]
    [SerializeField] private float surfaceHeightOffset = 0.02f;

    private BoxCollider2D platformBoxCollider;

    [Header("Sparks")]
    [SerializeField, Range(1, 8)] private int sparkCount = 4;
    [Tooltip("Bright white-blue core color for the neon look.")]
    [SerializeField] private Color coreColor = new Color(0.85f, 0.95f, 1f);
    [Tooltip("Wider, semi-transparent blue behind the core, faking a neon glow.")]
    [SerializeField] private Color glowColor = new Color(0.2f, 0.55f, 1f, 0.55f);
    [SerializeField, Min(0.01f)] private float coreWidth = 0.035f;
    [SerializeField, Min(0.01f)] private float glowWidth = 0.11f;
    [Tooltip("Grid size bolt points are snapped to, for a chunky pixel-art look.")]
    [SerializeField, Min(0.01f)] private float pixelSnap = 0.06f;
    [Tooltip("How many links each chained bolt has — more links reads as chainier lightning.")]
    [SerializeField] private Vector2Int linkCountRange = new Vector2Int(5, 8);
    [Tooltip("How tall each bolt reaches above the surface.")]
    [SerializeField] private Vector2 boltHeightRange = new Vector2(0.15f, 0.35f);
    [Tooltip("Seconds between a spark's redraws; also its flicker rate.")]
    [SerializeField] private Vector2 flickerIntervalRange = new Vector2(0.04f, 0.14f);
    [Range(0f, 1f)]
    [Tooltip("Chance a flicker tick leaves the spark dark instead of redrawing a bolt, so sparks read as intermittent.")]
    [SerializeField] private float skipChance = 0.15f;

    private MovingPlatform platform;
    private LineRenderer[] coreSparks;
    private LineRenderer[] glowSparks;
    private float[] nextFlickerTime;

    private void Awake()
    {
        platform = GetComponent<MovingPlatform>();
        platformBoxCollider = GetComponent<BoxCollider2D>();
        BuildSparks();
    }

    private void BuildSparks()
    {
        coreSparks = new LineRenderer[sparkCount];
        glowSparks = new LineRenderer[sparkCount];
        nextFlickerTime = new float[sparkCount];

        for (int i = 0; i < sparkCount; i++)
        {
            glowSparks[i] = CreateLine($"Spark_{i}_Glow", glowColor, glowWidth, 4);
            coreSparks[i] = CreateLine($"Spark_{i}_Core", coreColor, coreWidth, 5);
        }
    }

    private LineRenderer CreateLine(string name, Color color, float width, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);

        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.numCapVertices = 0;
        lr.numCornerVertices = 0;
        lr.startColor = lr.endColor = color;
        lr.startWidth = lr.endWidth = width;
        lr.sortingOrder = sortingOrder;
        lr.enabled = false;
        return lr;
    }

    private void Update()
    {
        bool active = platform.IsElectrifying;
        for (int i = 0; i < coreSparks.Length; i++)
        {
            if (!active)
            {
                coreSparks[i].enabled = false;
                glowSparks[i].enabled = false;
                continue;
            }

            if (Time.time < nextFlickerTime[i]) continue;

            RegenerateSpark(coreSparks[i], glowSparks[i]);
            nextFlickerTime[i] = Time.time + Random.Range(flickerIntervalRange.x, flickerIntervalRange.y);
        }
    }

    private void RegenerateSpark(LineRenderer core, LineRenderer glow)
    {
        if (Random.value < skipChance)
        {
            core.enabled = false;
            glow.enabled = false;
            return;
        }

        float width = platformBoxCollider != null ? platformBoxCollider.size.x : surfaceWidth;
        float x = Random.Range(-width / 2f, width / 2f);

        // Local-space top of the platform's own collider (offset + half its height) — the
        // same surface MovingPlatform's electrified kill zone straddles — plus a small lift.
        float surfaceTop = platformBoxCollider != null
            ? platformBoxCollider.offset.y + platformBoxCollider.size.y / 2f + surfaceHeightOffset
            : surfaceHeightOffset;

        // More links (jagged zigzag segments) reads as a chain of lightning rather than a
        // single spark flick.
        int links = Random.Range(linkCountRange.x, linkCountRange.y + 1);
        var points = new Vector3[links];
        for (int i = 0; i < links; i++)
        {
            float t = i / (float)(links - 1);
            float px = Snap(x + Random.Range(-0.12f, 0.12f));
            float py = Snap(surfaceTop + t * Random.Range(boltHeightRange.x, boltHeightRange.y));
            points[i] = new Vector3(px, py, 0f);
        }

        core.positionCount = links;
        core.SetPositions(points);
        core.enabled = true;

        glow.positionCount = links;
        glow.SetPositions(points);
        glow.enabled = true;
    }

    private float Snap(float value) => Mathf.Round(value / pixelSnap) * pixelSnap;
}
