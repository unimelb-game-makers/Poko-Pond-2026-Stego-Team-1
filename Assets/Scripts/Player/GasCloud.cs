using System.Collections;
using UnityEngine;

/*
 * OVERVIEW
 *   Represents a SoftBodyPlayer that has been evaporated. Spawned and destroyed
 *   exclusively by PlayerSplitController — never place in the scene directly.
 *
 * PHYSICS
 *   Reduced gravity + terminal fall-speed cap gives the drifting-gas feel.
 *   Horizontal air control is near-full so the player can steer while floating.
 *   Up input provides a gentle upward drift assist; the cloud still descends without it.
 *
 * VISUALS
 *   Procedural fan-mesh circle with per-vertex sinusoidal noise and a global pulse,
 *   producing a soft, breathing cloud silhouette. The outer ring uses a lower-alpha
 *   colour to fade the edge — requires the body material to support vertex-colour
 *   alpha blending (the same material used by SoftBodyPlayer works if it is set up
 *   for transparency). The face sprite is a child SpriteRenderer, copied from the
 *   linked SoftBodyPlayer when spawned.
 *
 * LAYER
 *   Added to the "GasCloud" layer in Awake. Create this layer in
 *   Project Settings → Tags and Layers before entering Play mode.
 *   In Physics 2D → Layer Collision Matrix: enable GasCloud × Ground so the cloud
 *   collides with terrain. Disable GasCloud × Player and GasCloud × SoftBodyPoint.
 *
 * LINKED DROPLET
 *   LinkedDroplet   — the SoftBodyPlayer that was evaporated (frozen + hidden).
 *   LinkedDropletIndex — -1 for the un-split mainPlayer; 0 or 1 for a split slot.
 *   These are read by PlayerSplitController.TryCondense to restore the correct body.
 */
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class GasCloud : MonoBehaviour
{
    [Header("Physics")]
    [Tooltip("Gravity scale while in gas form. Lower = floatier drift.")]
    public float gravityScale  = 0.35f;
    [Tooltip("Horizontal force applied each FixedUpdate when input is held.")]
    public float moveForce     = 3.5f;
    [Tooltip("Maximum horizontal speed in gas form.")]
    public float maxMoveSpeed  = 4f;
    [Tooltip("Horizontal braking force per second when no input is held.")]
    public float moveDrag      = 2.5f;
    [Tooltip("Maximum upward speed granted by the drift assist (Up input).")]
    public float verticalDrift = 2f;
    [Tooltip("Maximum downward fall speed — keeps the cloud from plummeting.")]
    public float maxFallSpeed  = 2f;

    [Header("Collider")]
    [Tooltip("Radius of the CircleCollider2D added at runtime.")]
    public float colliderRadius = 0.42f;

    [Header("Body Shape")]
    [Tooltip("Number of ring vertices around the cloud mesh.")]
    public int   vertexCount = 28;
    [Tooltip("Base radius of the cloud mesh in world units.")]
    public float bodyRadius  = 0.52f;

    [Header("Noise")]
    [Tooltip("Per-vertex radial noise amplitude — controls how jagged the cloud edge looks.")]
    public float noiseAmplitude = 0.07f;
    [Tooltip("Speed of the per-vertex noise oscillation (cycles per second).")]
    public float noiseSpeed     = 1.2f;
    [Tooltip("Global pulse amplitude — the whole cloud breathes in and out.")]
    public float pulseAmplitude = 0.04f;
    [Tooltip("Speed of the global breathing pulse (cycles per second).")]
    public float pulseSpeed     = 1.8f;

    [Header("Rendering")]
    [Tooltip("The same vertex-colour material used by SoftBodyPlayer. Must support alpha blending.")]
    public Material bodyMaterial;
    [Tooltip("Centre vertex colour — translucent blue-white for a gaseous look.")]
    public Color innerColor       = new Color(0.82f, 0.94f, 1.00f, 0.70f);
    [Tooltip("Edge vertex colour — more transparent to soften the silhouette.")]
    public Color outerColor       = new Color(0.60f, 0.84f, 1.00f, 0.20f);
    public string sortingLayerName = "Default";
    public int    sortingOrder     = 0;

    [Header("Face")]
    public Sprite faceRightSprite;
    public Sprite faceLeftSprite;
    public float  faceScale       = 1f;
    public int    faceSortingOrder = 1;

    // ── Public API ──────────────────────────────────────────────────────────

    // Set false on the passive slot in a split to suppress player input (mirrors SoftBodyPlayer.InputEnabled).
    public bool    InputEnabled = true;
    // World-space centre — read by PlayerSplitController for proximity checks and camera targeting.
    public Vector2 Center       => Rb != null ? Rb.position : (Vector2)transform.position;
    // Exposed so PlayerSplitController can apply burst velocity immediately after spawning.
    public Rigidbody2D Rb       { get; private set; }

    // Set by PlayerSplitController immediately after constructing this object.
    [HideInInspector] public SoftBodyPlayer LinkedDroplet;
    // -1 = mainPlayer evaporated (not split); 0 or 1 = which split droplet slot this cloud represents.
    [HideInInspector] public int            LinkedDropletIndex = -1;

    // ── Private ─────────────────────────────────────────────────────────────

    private Mesh           _mesh;
    private Vector3[]      _verts;
    private Color[]        _colors;
    private float[]        _noisePhase; // per-vertex random phase offset for independent wobble

    private SpriteRenderer _faceRenderer;
    private float          _lastFaceDir = 1f;

    // ─────────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        gameObject.layer = LayerMask.NameToLayer("GasCloud");

        Rb                         = gameObject.AddComponent<Rigidbody2D>();
        Rb.gravityScale            = gravityScale;
        Rb.constraints             = RigidbodyConstraints2D.FreezeRotation;
        Rb.collisionDetectionMode  = CollisionDetectionMode2D.Continuous;
        Rb.interpolation           = RigidbodyInterpolation2D.Interpolate;

        var col    = gameObject.AddComponent<CircleCollider2D>();
        col.radius = colliderRadius;

        BuildMesh();
        SpawnFaceRenderer();
    }

    private void BuildMesh()
    {
        _mesh = new Mesh { name = "GasCloudMesh" };
        GetComponent<MeshFilter>().mesh = _mesh;

        var mr              = GetComponent<MeshRenderer>();
        if (bodyMaterial == null)
            Debug.LogError("[GasCloud] bodyMaterial is null — assign the player body material on SoftBodyPlayer in the Inspector.", this);
        mr.material         = bodyMaterial;
        mr.sortingLayerName = sortingLayerName;
        mr.sortingOrder     = sortingOrder;

        int total   = vertexCount + 1; // centre vertex + ring
        _verts      = new Vector3[total];
        _colors     = new Color[total];
        _noisePhase = new float[vertexCount];
        for (int i = 0; i < vertexCount; i++)
            _noisePhase[i] = Random.Range(0f, Mathf.PI * 2f);

        // Fan triangles from centre (index 0) to consecutive ring pairs
        var tris = new int[vertexCount * 3];
        for (int i = 0; i < vertexCount; i++)
        {
            tris[i * 3]     = 0;
            tris[i * 3 + 1] = i + 1;
            tris[i * 3 + 2] = (i + 1) % vertexCount + 1; // wraps last vertex back to index 1
        }
        // Set vertices before triangles so mesh bounds are valid from frame 0
        _mesh.vertices  = _verts;
        _mesh.colors    = _colors;
        _mesh.triangles = tris;
        _mesh.RecalculateBounds();
    }

    private void SpawnFaceRenderer()
    {
        var faceGO                     = new GameObject("Face");
        faceGO.transform.SetParent(transform);
        faceGO.transform.localPosition = Vector3.zero;
        faceGO.transform.localScale    = Vector3.one * faceScale;
        _faceRenderer                  = faceGO.AddComponent<SpriteRenderer>();
        _faceRenderer.sprite           = faceRightSprite;
        _faceRenderer.sortingLayerName = sortingLayerName;
        _faceRenderer.sortingOrder     = faceSortingOrder;
    }

    private void Update()
    {
        if (_faceRenderer == null || !InputEnabled) return;
        float h = Input.GetAxisRaw("Horizontal");
        if      (h >  0.05f) _lastFaceDir =  1f;
        else if (h < -0.05f) _lastFaceDir = -1f;
        _faceRenderer.sprite = _lastFaceDir > 0f ? faceRightSprite : faceLeftSprite;
    }

    private void FixedUpdate()
    {
        if (!InputEnabled)
        {
            // Gently brake passive cloud — it shouldn't drift forever while uncontrolled
            Rb.linearVelocity = Vector2.Lerp(Rb.linearVelocity, Vector2.zero, moveDrag * 0.05f);
            return;
        }

        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Horizontal movement with speed cap
        if (Mathf.Abs(h) > 0.05f)
        {
            Rb.AddForce(new Vector2(h * moveForce, 0f), ForceMode2D.Force);
            if (Mathf.Abs(Rb.linearVelocity.x) > maxMoveSpeed)
                Rb.linearVelocity = new Vector2(Mathf.Sign(Rb.linearVelocity.x) * maxMoveSpeed, Rb.linearVelocity.y);
        }
        else
        {
            Rb.linearVelocity = new Vector2(
                Mathf.MoveTowards(Rb.linearVelocity.x, 0f, moveDrag * Time.fixedDeltaTime),
                Rb.linearVelocity.y);
        }

        // Gentle upward drift assist — pressing up counteracts gravity without full anti-gravity
        if (v > 0.05f && Rb.linearVelocity.y < verticalDrift)
            Rb.AddForce(new Vector2(0f, moveForce * 0.45f), ForceMode2D.Force);

        // Terminal fall speed cap — cloud sinks slowly, never plummets
        if (Rb.linearVelocity.y < -maxFallSpeed)
            Rb.linearVelocity = new Vector2(Rb.linearVelocity.x, -maxFallSpeed);
    }

    private void LateUpdate() => RebuildMesh();

    private void RebuildMesh()
    {
        float t     = Time.time;
        float pulse = pulseAmplitude * Mathf.Sin(t * pulseSpeed * Mathf.PI * 2f);

        _verts[0]  = Vector3.zero;
        _colors[0] = innerColor;

        for (int i = 0; i < vertexCount; i++)
        {
            float angle = (float)i / vertexCount * Mathf.PI * 2f;
            float noise = noiseAmplitude * Mathf.Sin(t * noiseSpeed * Mathf.PI * 2f + _noisePhase[i]);
            float r     = bodyRadius + pulse + noise;
            _verts[i + 1]  = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r, 0f);
            _colors[i + 1] = outerColor;
        }

        _mesh.vertices = _verts;
        _mesh.colors   = _colors;
        _mesh.RecalculateBounds();
    }

    // Outward-then-inward pulse to signal a rejected merge (mismatched liquid/gas states).
    public void PlayMismatchPulse() => StartCoroutine(MismatchPulseCoroutine());

    private IEnumerator MismatchPulseCoroutine()
    {
        float t = 0f, duration = 0.20f;
        float baseRadius = bodyRadius;
        while (t < duration)
        {
            t += Time.deltaTime;
            bodyRadius = baseRadius + 0.10f * Mathf.Sin(t / duration * Mathf.PI);
            yield return null;
        }
        bodyRadius = baseRadius;
    }

    // Called by PlayerSplitController — hides the face on the passive (non-controlled) slot.
    public void SetFaceVisible(bool visible)
    {
        if (_faceRenderer != null) _faceRenderer.enabled = visible;
    }

    // Called by PlayerSplitController — ensures active cloud renders in front of passive.
    public void SetSortingOrder(int order)
    {
        var mr = GetComponent<MeshRenderer>();
        if (mr != null) mr.sortingOrder = order;
        if (_faceRenderer != null)
            _faceRenderer.sortingOrder = faceSortingOrder + (order - sortingOrder);
    }
}
