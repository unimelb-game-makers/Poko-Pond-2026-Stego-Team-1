using UnityEngine;

/*
 * OVERVIEW
 *   Softbody water-droplet player. A ring of N Rigidbody2D "points" is held
 *   together by a spring network; a fan mesh is rebuilt every frame from their
 *   interpolated positions, producing a squishy, physically-reactive silhouette.
 *
 * ONE-TIME PROJECT SETUP
 *   1. Create a layer named "SoftBodyPoint"  (Project Settings → Tags and Layers)
 *   2. Physics 2D → Layer Collision Matrix → uncheck SoftBodyPoint × SoftBodyPoint
 *   3. Assign "SoftBodyPoint" to the Soft Body Point Layer field in the Inspector
 *   4. Set Ground Layer to match your platform/terrain layers
 *
 * SCENE SETUP
 *   - Add this component to a GameObject that also has MeshFilter + MeshRenderer.
 *   - All N physics-point child GameObjects are spawned at runtime — do NOT add
 *     them manually in the Editor.  Rely on the public API (Points, Center, etc.)
 *     rather than caching child transforms directly in other scripts.
 *
 * SPLIT / MERGE  (managed externally by PlayerSplitController)
 *   Left Shift   → split into two half-size droplets
 *   Tab          → swap which droplet is active
 *   Auto         → droplets merge when their centres come within mergeProximityRadius
 *
 *   Key methods used by PlayerSplitController:
 *     Freeze() / Unfreeze()          — disable / re-enable physics simulation
 *     TeleportTo(center, velocity)   — reposition the entire ring (call AFTER Unfreeze)
 *     GetHalfState(...)              — read the visual centre + velocity of each half
 *     SetVisible(bool)               — show / hide all renderers
 *     SetBodyAlpha(float)            — fade the body, highlight, and face together
 *     SetSortingOrder(int)           — change render layer order at runtime
 *     InitFaceDirection(float)       — immediately set face sprite with no crossfade
 *     LastFaceDir                    — current intended face direction (+1 right, -1 left)
 *     InputEnabled                   — set false on the passive droplet to suppress input
 *
 * GROUND POUND  (C key while airborne)
 *   Drives the blob downward at groundPoundDownForce; squash-animates the landing.
 *   Fires OnGroundPoundLand(impactVel) on landing — PlayerSplitController listens to
 *   this on the active droplet to launch the passive one (pressure transfer).
 *
 * PRESSURE TRANSFER
 *   When the active droplet ground-pounds while split, the passive droplet is launched
 *   upward at its own jumpForce — always a consistent, predictable height.
 *   The passive droplet must be grounded for the transfer to trigger.
 *
 * CODING WITH THE SOFTBODY PLAYER
 *   Physics points are created at runtime in Awake — never rely on child GameObjects
 *   or their transforms before Awake has run.  Use the public properties:
 *
 *     sp.Points   → Rigidbody2D[] of all ring points
 *     sp.Center   → world-space centroid (updated every LateUpdate)
 *     sp.IsGrounded
 *     sp.IsGroundPounding
 *
 *   To apply a force to the whole body, iterate sp.Points and call rb.AddForce().
 *   To teleport, always call Unfreeze() first, then TeleportTo() — writing
 *   rb.position on a frozen (simulated=false) Rigidbody2D has no effect.
 *
 *   Listen to OnGroundPoundLand for impact events:
 *     sp.OnGroundPoundLand += vel => { ... };
 *
 *   Blend fields (SplitPinchBlend, MergePopBlend, etc.) are driven by coroutines
 *   in PlayerSplitController — do not set them from other scripts unless you are
 *   building a new controller that fully owns the animation pipeline.
 */
[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class SoftBodyPlayer : MonoBehaviour
{
    [Header("Body Shape")]
    [Tooltip("Number of Rigidbody2D points around the circle outline.")]
    public int pointCount = 30;
    [Tooltip("Horizontal radius of the spawn ellipse in world units.")]
    public float bodyRadius = 0.55f;
    [Tooltip("Vertical scale of the spawn ellipse. <1 = wider than tall.")]
    [Range(0.5f, 2f)]
    public float domeHeightScale = 1f;

    [Header("Point Physics")]
    public float pointMass   = 0.1f;
    [Tooltip("Radius of each point's CircleCollider2D.")]
    public float pointRadius = 0.05f;
    [Tooltip("Leave empty — a zero-friction material is created automatically.")]
    public PhysicsMaterial2D pointMaterial;
    [Tooltip("Layer assigned to ring points. Must exist in Tags and Layers.")]
    public string softBodyPointLayer = "SoftBodyPoint";

    [Header("Spring Network  (~3×N joints)")]
    [Tooltip("Master spring stiffness. Skip-2 and quarter-span springs scale from this.")]
    public float springFrequency = 10f;
    [Range(0f, 1f)]
    [Tooltip("Master spring damping. Higher = less wobble.")]
    public float springDamping = 0.7f;

    [Header("Shape Restoration")]
    [Tooltip("Force pulling each point toward its spawn offset from the centroid.")]
    public float restoreForce = 60f;
    [Tooltip("Outward pressure force when the blob area drops below its rest area.")]
    public float pressureForce = 14f;

    [Header("Movement")]
    [Tooltip("Horizontal force per point when input is held.")]
    public float moveForce    = 4f;
    [Tooltip("Per-point horizontal speed cap.")]
    public float maxMoveSpeed = 7f;
    [Tooltip("Braking force per point when no input is held.")]
    public float moveDrag     = 6f;
    [Range(0f, 1f)]
    [Tooltip("Fraction of move force while airborne. Keeps mid-air steering without wall sticking.")]
    public float airControlFraction = 0.4f;

    [Header("Jump")]
    [Tooltip("Upward velocity (m/s) added to every ring point on jump.")]
    public float jumpForce = 13.5f;

    [Header("Gravity")]
    public float baseGravityScale = 3f;
    [Tooltip("Gravity scale while falling.")]
    public float maxGravityScale  = 7f;
    [Tooltip("How fast gravity transitions between base and max (scale/sec).")]
    public float gravityRampRate  = 4f;

    [Header("Ground Detection")]
    public LayerMask groundLayer;
    [Tooltip("Fraction of bottommost points used for ground checks.")]
    [Range(0.05f, 0.5f)]
    public float groundCheckFraction = 0.2f;

    [Header("Level Bounds  (optional)")]
    public PolygonCollider2D levelBounds;
    [Tooltip("Force pushing points back inside the level bounds.")]
    public float boundaryForce = 40f;

    [Header("Face")]
    [Tooltip("Sprite used when the player is facing right. Import: Texture Type = Sprite (2D and UI), Pixels Per Unit = match your game scale, Alpha Is Transparency = on.")]
    public Sprite faceRightSprite;
    [Tooltip("Sprite used when the player is facing left.")]
    public Sprite faceLeftSprite;
    [Tooltip("0 = face sits at blob centroid, 1 = face sits at the outer edge of the half. Tune live in Play mode.")]
    [Range(0f, 1f)]
    public float faceBias = 0.45f;
    [Tooltip("Extra offset applied on top of the computed position (local space).")]
    public Vector2 faceOffset = Vector2.zero;
    [Tooltip("Uniform scale of the face sprite.")]
    public float faceScale = 1f;
    [Tooltip("Sorting order for the face sprite. Should be higher than the body Sorting Order so it draws on top.")]
    public int faceSortingOrder = 1;
    [Tooltip("Duration of the crossfade when the face flips between left and right (seconds).")]
    public float faceFadeDuration = 0.07f;

    [Header("Rendering")]
    public Material bodyMaterial;
    public string   sortingLayerName = "Default";
    public int      sortingOrder     = 0;
    [Range(1, 8)]
    [Tooltip("Catmull-Rom subdivisions between physics points. 4 is a good balance.")]
    public int subdivisionsPerSegment = 4;
    [Range(0, 5)]
    [Tooltip("Laplacian smoothing passes before the spline. Softens spike artefacts on impact.")]
    public int meshSmoothingPasses = 4;

    [Header("Visuals — Body Gradient")]
    [Tooltip("Colour at the centre of the blob — lighter gives a rounded 3-D look.")]
    public Color bodyInnerColor = new Color(0.52f, 0.80f, 1.00f);
    [Tooltip("Colour at the outer edge of the blob.")]
    public Color bodyOuterColor = new Color(0.18f, 0.52f, 0.88f);

    [Header("Visuals — Highlight")]
    [Tooltip("Colour and opacity of the specular highlight. Alpha controls strength.")]
    public Color   highlightColor  = new Color(1f, 1f, 1f, 0.55f);
    [Range(0.05f, 0.85f)]
    [Tooltip("Size of the highlight relative to the blob body.")]
    public float   highlightScale  = 0.38f;
    [Tooltip("Offset of the highlight centre in local blob space (shift up-left for a natural light source).")]
    public Vector2 highlightOffset = new Vector2(-0.05f, 0.10f);

    // ── Animation — Idle ─────────────────────────────────────────────────
    [Header("Animation — Idle")]
    [Tooltip("Amplitude of the gentle radial breathing pulse (world units).")]
    public float idleBobAmplitude = 0.02f;
    [Tooltip("Breathing cycles per second.")]
    public float idleBobFrequency = 1.2f;

    // ── Animation — Move ─────────────────────────────────────────────────
    [Header("Animation — Move")]
    [Tooltip("How far the top of the body shears in the direction of travel (world units).")]
    public float moveLeanAmount   = 0.3f;
    [Tooltip("Squash/stretch amplitude per step cycle.")]
    public float moveBobAmplitude = 0.01f;
    [Tooltip("Bob cycles per second at full move speed.")]
    public float moveBobFrequency = 2f;

    // ── Animation — Rise ─────────────────────────────────────────────────
    [Header("Animation — Rise  (Airborne, moving up)")]
    [Tooltip("How much the body stretches vertically while rising.")]
    public float riseStretchAmount = 0.08f;
    [Tooltip("How much the sides squeeze inward while rising.")]
    public float riseSqueezeAmount = 0.055f;
    [Tooltip("Upward velocity at which rise blend reaches full strength.")]
    public float riseVelocityFull  = 3.5f;

    // ── Animation — Fall ─────────────────────────────────────────────────
    [Header("Animation — Fall  (Airborne, moving down)")]
    [Tooltip("How much the sides spread outward while falling.")]
    public float fallSpreadAmount  = 0.07f;
    [Tooltip("How much the body flattens vertically while falling.")]
    public float fallFlattenAmount = 0.042f;
    [Tooltip("Downward velocity at which fall blend reaches full strength.")]
    public float fallVelocityFull  = 4f;

    // ── Animation — Landing ──────────────────────────────────────────────
    [Header("Animation — Landing")]
    [Tooltip("How much the sides spread on impact.")]
    public float landingSquashSpread  = 0.13f;
    [Tooltip("How much the body flattens on impact.")]
    public float landingSquashFlatten = 0.09f;
    [Tooltip("Total duration of the landing squash animation (seconds).")]
    public float landingDuration      = 0.26f;

    // ── Animation — Blending ─────────────────────────────────────────────
    [Header("Animation — Blending")]
    [Tooltip("How fast idle/move/rise/fall blends transition (lerp rate per second).")]
    public float animBlendSpeed = 4f;
    [Tooltip("Pressure force scale during landing squash — lower lets the animation dominate.")]
    [Range(0f, 1f)]
    public float landingPressureScale = 0.2f;
    [Tooltip("Restore force multiplier during landing — higher snaps the squash in faster.")]
    [Range(1f, 3f)]
    public float landingRestoreScale  = 1.8f;

    // ── Ground Pound ──────────────────────────────────────────────────────
    [Header("Ground Pound")]
    [Tooltip("Downward velocity (m/s) set on every point when ground pound activates.")]
    public float groundPoundDownForce      = 16f;
    [Tooltip("Extra gravity scale added on top of maxGravityScale during the dive.")]
    public float groundPoundGravityBoost   = 1f;
    [Tooltip("Vertical flatten amplitude while diving.")]
    public float groundPoundDiveCompress   = 0.10f;
    [Tooltip("Horizontal spread amplitude while diving.")]
    public float groundPoundDiveSpread     = 0.06f;
    [Tooltip("Multiplier on landing squash amplitudes for a ground-pound impact.")]
    public float groundPoundSquashMult     = 2.2f;
    [Tooltip("Landing squash duration override for a ground-pound impact (seconds).")]
    public float groundPoundSquashDuration = 0.38f;

    // ── Animation — Split / Merge ─────────────────────────────────────────
    [Header("Animation — Split / Merge")]
    [Tooltip("How far equatorial points squeeze inward during the split pinch.")]
    public float splitPinchInward    = 0.35f;
    [Tooltip("How far top/bottom points stretch outward during the split pinch.")]
    public float splitPinchVertical  = 0.20f;
    [Tooltip("How far each half pushes outward once the seam opens.")]
    public float splitSeparateAmount = 0.22f;
    [Tooltip("Radial burst amplitude on merge / spawn pop.")]
    public float mergePopOutward     = 0.22f;
    [Tooltip("Power curve exponent on merge pop — higher = faster peak, softer tail.")]
    public float mergePopFalloff     = 2.5f;

    [Header("Animation — Activate")]
    [Tooltip("Horizontal inward squeeze when this droplet becomes the active one (Tab switch).")]
    public float activateSquashInward   = 0.08f;
    [Tooltip("Vertical inward squeeze on Tab switch.")]
    public float activateSquashVertical = 0.05f;

    [Header("Animation — Wiggle")]
    [Tooltip("Horizontal deformation amplitude of the post-split/merge tension wiggle.")]
    public float wiggleAmplitude  = 0.10f;
    [Tooltip("Oscillations per second for the tension wiggle.")]
    public float wiggleFrequency  = 9f;

    // ── Public API ───────────────────────────────────────────────────────
    public bool          IsGrounded      { get; private set; }
    public bool          IsGroundPounding { get; private set; }
    public Vector2       Center          => _center;
    public Rigidbody2D[] Points          => _rbs;

    // Set by PlayerSplitController — false suppresses all input on passive droplet
    public bool  InputEnabled        = true;
    // Driven externally by PlayerSplitController via coroutine
    public float SplitPinchBlend      = 0f;
    public float MergePopBlend        = 0f;
    public float PressureSquashBlend  = 0f;
    public float ActivateSquashBlend  = 0f;
    public float WiggleBlend          = 0f;

    // Fires with average |impact velocity| when a ground-pound landing registers
    public event System.Action<float> OnGroundPoundLand;

    // ── Private — Physics ────────────────────────────────────────────────
    private Rigidbody2D[]    _rbs;
    private GameObject[]     _pointGOs;
    private Vector2[]        _offsets;
    private int[]            _bottomIdx;

    private Vector2 _center;
    private float   _currentGravityScale;
    private float   _restArea;

    private bool  _jumpQueued;
    private float _jumpQueueWindow;
    private bool  _frozen;

    // ── Private — Mesh ───────────────────────────────────────────────────
    private Mesh      _mesh;
    private int[]     _triangles;
    private int       _subdivVerts;
    private Vector2[] _smoothRing;
    private Vector3[] _meshVerts;
    private Vector2[] _meshUVs;
    private Color[]   _meshColors;

    private Mesh         _highlightMesh;
    private MeshRenderer _highlightRenderer;
    private MeshRenderer _bodyRenderer;
    private Vector3[]    _highlightVerts;
    private float        _bodyAlpha = 1f;

    private CircleCollider2D[] _cols;
    private Vector2[]          _preSmoothA;
    private Vector2[]          _preSmoothB;
    private float[]            _neighborRestDist;
    private Vector2[]          _prevPositions;

    // ── Private — Animation ──────────────────────────────────────────────
    private Vector2[] _animOffsets;   // per-point bias added to rest targets each frame
    private float[]   _angles;        // spawn angle for each point (fixed)

    private float _idleBlend;
    private float _moveBlend;         // bob only — grounded + moving
    private float _leanBlend;         // lean only — any horizontal input, persists airborne
    private float _riseBlend;         // 0-1, driven by upward velocity
    private float _fallBlend;         // 0-1, driven by downward velocity
    private float _landingTimer;      // counts down from landingDuration to 0
    private float _landingSquashT;    // shaped squash weight (peaks mid-animation)

    private float _moveBobPhase;      // advances with horizontal speed
    private float _idlePhase;         // advances with time
    private float _leanDir;           // smoothed -1 to +1 lean direction
    private float _hInput;            // current horizontal input, read once per FixedUpdate

    private float _pressureMultiplier = 1f;
    private float _restoreMultiplier  = 1f;
    private bool  _wasGrounded;
    private bool  _groundPoundJustLanded;
    private float _wigglePhase;

    // ── Private — Face ───────────────────────────────────────────────────
    private SpriteRenderer _faceRenderer;
    private float          _rightRestAvgX;  // precomputed average rest X of right-half points
    private float          _leftRestAvgX;   // precomputed average rest X of left-half points
    private float          _lastFaceDir    = 1f;  // +1 right, -1 left
    private float          _pendingFaceDir = 1f;  // direction waiting to be shown after fade
    private float          _faceAlpha      = 1f;
    private int            _faceFadeState  = 0;   // 0 stable, -1 fading out, 1 fading in

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _currentGravityScale = baseGravityScale;
        _center = transform.position;
        SpawnPoints();
        SetupSprings();
        SetupMesh();
        SetupHighlight();
        SetupFace();
    }

    private void Update()
    {
        if (_frozen) return;
        if (!InputEnabled) return;
        if (Input.GetKeyDown(KeyCode.Space))
        {
            _jumpQueued      = true;
            _jumpQueueWindow = 0.12f;
        }
    }

    private void FixedUpdate()
    {
        if (_frozen) return;

        _hInput = InputEnabled ? Input.GetAxisRaw("Horizontal") : 0f;

        ResolveCollisions();
        UpdateCenter();
        UpdateGravity();
        ApplyRestoreForces();   // uses _animOffsets and _restoreMultiplier from previous tick
        ApplyPressureForces();  // uses _pressureMultiplier from previous tick
        DetectGround();
        UpdateAnimationState(); // computes new _animOffsets, _pressureMultiplier, _restoreMultiplier
        HandleMovement();
        HandleJump();
        HandleGroundPound();
        EnforceLevelBounds();
        EnforceNeighborConstraints();

        for (int i = 0; i < pointCount; i++)
            _prevPositions[i] = _rbs[i].position;
    }

    private void LateUpdate()
    {
        if (_frozen) return;
        // Recompute center from the interpolated transform positions so the camera
        // and mesh are driven by the same smooth value, not the FixedUpdate-stale _center.
        // (rb.position bypasses interpolation; _pointGOs[i].transform.position uses it.)
        Vector2 lateSum = Vector2.zero;
        for (int i = 0; i < pointCount; i++)
            lateSum += (Vector2)_pointGOs[i].transform.position;
        _center = lateSum / pointCount;
        transform.position = _center;
        RebuildMesh();
        RebuildHighlight();
        UpdateFace();
    }

    public void Freeze()
    {
        _frozen = true;
        foreach (var rb in _rbs) rb.simulated = false;
    }

    public void Unfreeze()
    {
        _frozen = false;
        foreach (var rb in _rbs) rb.simulated = true;
    }

    // Places the ring in its natural resting shape centered at newCenter and sets velocity.
    // Sets both rb.position and transform.position so the visual is immediately correct.
    // Must be called AFTER Unfreeze() so rb.position writes are respected by the physics engine.
    public void TeleportTo(Vector2 newCenter, Vector2 velocity)
    {
        for (int i = 0; i < pointCount; i++)
        {
            Vector2 p = newCenter + _offsets[i];
            _rbs[i].position      = p;
            _rbs[i].linearVelocity = velocity;
            _pointGOs[i].transform.position = p;
        }
        _center            = newCenter;
        transform.position = newCenter;
    }

    // Returns the visual centre and average velocity of each half at the moment of split.
    // Uses interpolated transform positions so the spawn location matches what's on screen.
    public void GetHalfState(
        out Vector2 leftCenter,  out Vector2 rightCenter,
        out Vector2 leftVelocity, out Vector2 rightVelocity)
    {
        Vector2 lc = Vector2.zero, rc = Vector2.zero;
        Vector2 lv = Vector2.zero, rv = Vector2.zero;
        int ln = 0, rn = 0;

        for (int i = 0; i < pointCount; i++)
        {
            Vector2 pos = _pointGOs[i].transform.position;
            Vector2 vel = _rbs[i].linearVelocity;
            if (pos.x <= _center.x) { lc += pos; lv += vel; ln++; }
            else                     { rc += pos; rv += vel; rn++; }
        }

        leftCenter    = ln > 0 ? lc / ln : _center + Vector2.left  * bodyRadius * 0.5f;
        rightCenter   = rn > 0 ? rc / rn : _center + Vector2.right * bodyRadius * 0.5f;
        leftVelocity  = ln > 0 ? lv / ln : Vector2.zero;
        rightVelocity = rn > 0 ? rv / rn : Vector2.zero;
    }

    public void SetVisible(bool visible)
    {
        GetComponent<MeshRenderer>().enabled = visible;
        if (_highlightRenderer != null) _highlightRenderer.enabled = visible;
        if (_faceRenderer      != null) _faceRenderer.enabled      = visible;
    }

    public void SetFaceVisible(bool visible)
    {
        if (_faceRenderer != null) _faceRenderer.enabled = visible;
    }

    // Intended face direction (+1 right, -1 left). Uses _pendingFaceDir so it reflects
    // the direction the player chose even if the crossfade sprite-swap hasn't completed yet.
    public float LastFaceDir => _pendingFaceDir;

    public void SetBodyAlpha(float alpha) => _bodyAlpha = Mathf.Clamp01(alpha);

    public void SetSortingOrder(int order)
    {
        sortingOrder = order;
        if (_bodyRenderer      != null) _bodyRenderer.sortingOrder      = order;
        if (_highlightRenderer != null) _highlightRenderer.sortingOrder = order + 1;
        if (_faceRenderer      != null) _faceRenderer.sortingOrder      = Mathf.Max(faceSortingOrder, order + 2);
    }

    // Immediately sets the face direction without a crossfade — used on split/merge
    // to carry the pre-split or active-droplet direction into the new body.
    public void InitFaceDirection(float dir)
    {
        _lastFaceDir    = dir >= 0f ? 1f : -1f;
        _pendingFaceDir = _lastFaceDir;
        _faceFadeState  = 0;
        _faceAlpha      = 1f;
        if (_faceRenderer == null) return;
        Sprite target = _lastFaceDir > 0f ? faceRightSprite : faceLeftSprite;
        if (target != null) _faceRenderer.sprite = target;
        _faceRenderer.color = Color.white;
    }

    // ── Spawn ─────────────────────────────────────────────────────────────

    private void SpawnPoints()
    {
        _rbs      = new Rigidbody2D[pointCount];
        _cols     = new CircleCollider2D[pointCount];
        _offsets  = new Vector2[pointCount];
        _pointGOs = new GameObject[pointCount];
        _angles   = new float[pointCount];

        int layer = LayerMask.NameToLayer(softBodyPointLayer);
        if (layer >= 0)
        {
            Physics2D.IgnoreLayerCollision(layer, layer, true);

            for (int l = 0; l < 32; l++)
            {
                if (l == layer) continue;
                bool isGround = ((1 << l) & groundLayer.value) != 0;
                Physics2D.IgnoreLayerCollision(layer, l, !isGround);
            }
        }

        if (pointMaterial == null)
        {
            pointMaterial            = new PhysicsMaterial2D("SoftBodyPoint_Mat");
            pointMaterial.friction   = 0f;
            pointMaterial.bounciness = 0.15f;
        }

        Vector2 spawnCenter = transform.position;

        for (int i = 0; i < pointCount; i++)
        {
            float   angle  = i / (float)pointCount * Mathf.PI * 2f;
            _angles[i] = angle;
            Vector2 offset = new Vector2(Mathf.Cos(angle) * bodyRadius,
                                         Mathf.Sin(angle) * bodyRadius * domeHeightScale);
            _offsets[i] = offset;

            var go = new GameObject($"SoftPoint_{i}");
            go.transform.position = spawnCenter + offset;
            if (layer >= 0) go.layer = layer;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.mass                   = pointMass;
            rb.gravityScale           = baseGravityScale;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation          = RigidbodyInterpolation2D.Interpolate;
            rb.constraints            = RigidbodyConstraints2D.FreezeRotation;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = pointRadius;
            if (pointMaterial != null) col.sharedMaterial = pointMaterial;

            _rbs[i]      = rb;
            _cols[i]     = col;
            _pointGOs[i] = go;
        }

        PrecomputeIndices();
        PrecomputeFaceHalves();
        _restArea = Mathf.PI * bodyRadius * bodyRadius * domeHeightScale;

        _neighborRestDist = new float[pointCount];
        for (int i = 0; i < pointCount; i++)
            _neighborRestDist[i] = Vector2.Distance(_offsets[i], _offsets[(i + 1) % pointCount]);

        _prevPositions = new Vector2[pointCount];
        for (int i = 0; i < pointCount; i++)
            _prevPositions[i] = _rbs[i].position;

        _animOffsets = new Vector2[pointCount];
    }

    private void OnDestroy()
    {
        if (_pointGOs != null)
            foreach (var go in _pointGOs)
                if (go != null) Destroy(go);
    }

    private void PrecomputeIndices()
    {
        int[] ranked = new int[pointCount];
        for (int i = 0; i < pointCount; i++) ranked[i] = i;
        System.Array.Sort(ranked, (a, b) => _offsets[a].y.CompareTo(_offsets[b].y));

        int bottomCount = Mathf.Max(1, Mathf.RoundToInt(pointCount * groundCheckFraction));
        _bottomIdx = new int[bottomCount];
        for (int i = 0; i < bottomCount; i++) _bottomIdx[i] = ranked[i];
    }

    // ── Face ─────────────────────────────────────────────────────────────

    private void PrecomputeFaceHalves()
    {
        float rightSum = 0f, leftSum = 0f;
        int   rightCnt = 0,  leftCnt = 0;
        for (int i = 0; i < pointCount; i++)
        {
            float x = _offsets[i].x;
            if (x > 0f)      { rightSum += x; rightCnt++; }
            else if (x < 0f) { leftSum  += x; leftCnt++;  }
        }
        _rightRestAvgX = rightCnt > 0 ? rightSum / rightCnt : 0f;
        _leftRestAvgX  = leftCnt  > 0 ? leftSum  / leftCnt  : 0f;
    }

    private void SetupFace()
    {
        if (faceRightSprite == null && faceLeftSprite == null) return;

        var go = new GameObject("PlayerFace");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = Vector3.one * faceScale;

        _faceRenderer                  = go.AddComponent<SpriteRenderer>();
        _faceRenderer.sprite           = faceRightSprite != null ? faceRightSprite : faceLeftSprite;
        _faceRenderer.sortingLayerName = sortingLayerName;
        // Highlight renders at sortingOrder+1 — face must be above it (at least +2)
        _faceRenderer.sortingOrder     = Mathf.Max(faceSortingOrder, sortingOrder + 2);
    }

    private void UpdateFace()
    {
        if (_faceRenderer == null) return;

        // ── Direction tracking ────────────────────────────────────────────
        float rawH = Input.GetAxisRaw("Horizontal");
        float desiredDir = rawH > 0.05f ? 1f : rawH < -0.05f ? -1f : _lastFaceDir;

        // When direction changes, kick off a fade-out → swap → fade-in sequence
        if (desiredDir != _lastFaceDir && _faceFadeState == 0)
        {
            _pendingFaceDir = desiredDir;
            _faceFadeState  = -1; // start fading out
        }

        // ── Crossfade ─────────────────────────────────────────────────────
        float fadeStep = faceFadeDuration > 0f ? Time.deltaTime / faceFadeDuration : 1f;
        if (_faceFadeState == -1)
        {
            _faceAlpha -= fadeStep;
            if (_faceAlpha <= 0f)
            {
                _faceAlpha     = 0f;
                _lastFaceDir   = _pendingFaceDir;
                _faceFadeState = 1; // fully hidden — swap sprite and fade back in
            }
        }
        else if (_faceFadeState == 1)
        {
            _faceAlpha += fadeStep;
            if (_faceAlpha >= 1f)
            {
                _faceAlpha     = 1f;
                _faceFadeState = 0;
            }
        }

        Sprite target = _lastFaceDir > 0f ? faceRightSprite : faceLeftSprite;
        if (target != null && _faceRenderer.sprite != target)
            _faceRenderer.sprite = target;
        _faceRenderer.color = new Color(1f, 1f, 1f, _faceAlpha * _bodyAlpha);

        // ── Position ──────────────────────────────────────────────────────
        // Use precomputed rest-offset averages for X so fall/rise/landing
        // deformation never moves the face outward. Lean shear is the only
        // visual-space transform we layer on top, using the same formula as
        // RebuildMesh so the face tracks the tilted body correctly.
        bool  wantRight  = _lastFaceDir > 0f;
        float restAvgX   = wantRight ? _rightRestAvgX : _leftRestAvgX;

        float leanX      = _leanDir * moveLeanAmount * _leanBlend;
        float halfHeight = bodyRadius * domeHeightScale;
        // shear at the face's Y position (faceOffset.y, typically 0)
        float shear      = (faceOffset.y + halfHeight) / (halfHeight * 2f);

        float localX     = restAvgX * faceBias + leanX * shear + faceOffset.x;
        _faceRenderer.transform.localPosition = new Vector3(localX, faceOffset.y, 0f);
    }

    // ── Springs ───────────────────────────────────────────────────────────

    private void SetupSprings()
    {
        int N       = pointCount;
        int quarter = N / 4;

        float skip2Freq   = springFrequency * 0.70f;
        float skip2Damp   = springDamping   * 0.79f;
        float quarterFreq = springFrequency * 0.50f;
        float quarterDamp = springDamping   * 0.64f;

        for (int i = 0; i < N; i++)
        {
            AddSpring(i, (i + 1) % N,       springFrequency, springDamping);
            AddSpring(i, (i + 2) % N,       skip2Freq,       skip2Damp);
            AddSpring(i, (i + quarter) % N, quarterFreq,     quarterDamp);
        }
    }

    private void AddSpring(int i, int j, float freq, float damp)
    {
        var joint                   = _rbs[i].gameObject.AddComponent<SpringJoint2D>();
        joint.connectedBody         = _rbs[j];
        joint.distance              = Vector2.Distance(_offsets[i], _offsets[j]);
        joint.frequency             = freq;
        joint.dampingRatio          = damp;
        joint.autoConfigureDistance = false;
        joint.enableCollision       = false;
    }

    // ── Physics ───────────────────────────────────────────────────────────

    private void UpdateCenter()
    {
        Vector2 sum = Vector2.zero;
        foreach (var rb in _rbs) sum += rb.position;
        _center = sum / pointCount;
    }

    private void UpdateGravity()
    {
        float avgVY = 0f;
        foreach (var rb in _rbs) avgVY += rb.linearVelocity.y;
        avgVY /= pointCount;

        float target = (avgVY < -0.5f) ? maxGravityScale : baseGravityScale;
        _currentGravityScale = Mathf.MoveTowards(
            _currentGravityScale, target, gravityRampRate * Time.fixedDeltaTime);

        foreach (var rb in _rbs) rb.gravityScale = _currentGravityScale;
    }

    private void ApplyRestoreForces()
    {
        float scale = restoreForce * _restoreMultiplier;
        for (int i = 0; i < pointCount; i++)
        {
            Vector2 desired      = _center + _offsets[i] + _animOffsets[i];
            Vector2 displacement = desired - _rbs[i].position;
            _rbs[i].AddForce(displacement * scale, ForceMode2D.Force);
        }
    }

    private void ApplyPressureForces()
    {
        int N = pointCount;

        float area = 0f;
        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N;
            Vector2 a = _rbs[i].position, b = _rbs[j].position;
            area += (a.x * b.y) - (b.x * a.y);
        }
        area = Mathf.Abs(area) * 0.5f;

        float safeArea    = Mathf.Max(area, _restArea * 0.01f);
        float pressureMag = Mathf.Clamp(
            (_restArea / safeArea - 1f) * pressureForce,
            -pressureForce, pressureForce * 4f);

        pressureMag *= _pressureMultiplier;

        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N;
            Vector2 posA = _rbs[i].position, posB = _rbs[j].position;
            Vector2 edge = posB - posA;
            float   len  = edge.magnitude;
            if (len < 0.0001f) continue;

            Vector2 normal = new Vector2(-edge.y, edge.x).normalized;
            if (Vector2.Dot(normal, posA - _center) < 0f) normal = -normal;

            Vector2 force = normal * pressureMag * len;
            _rbs[i].AddForce(force, ForceMode2D.Force);
            _rbs[j].AddForce(force, ForceMode2D.Force);
        }
    }

    private void DetectGround()
    {
        IsGrounded = false;
        foreach (int idx in _bottomIdx)
        {
            if (Physics2D.OverlapCircle(_rbs[idx].position, pointRadius + 0.06f, groundLayer))
            {
                IsGrounded = true;
                return;
            }
        }
    }

    private void HandleMovement()
    {
        float forceMult = IsGrounded ? 1f : airControlFraction;

        foreach (var rb in _rbs)
        {
            if (_hInput != 0f)
            {
                bool underLimit = Mathf.Abs(rb.linearVelocity.x) < maxMoveSpeed ||
                                  Mathf.Sign(rb.linearVelocity.x) != Mathf.Sign(_hInput);
                if (underLimit)
                    rb.AddForce(new Vector2(_hInput * moveForce * forceMult, 0f), ForceMode2D.Force);
            }
            else
            {
                rb.AddForce(new Vector2(-rb.linearVelocity.x * moveDrag, 0f), ForceMode2D.Force);
            }
        }
    }

    private void HandleJump()
    {
        if (_jumpQueueWindow > 0f) _jumpQueueWindow -= Time.fixedDeltaTime;
        else _jumpQueued = false;

        if (!_jumpQueued || !IsGrounded) return;

        _jumpQueued          = false;
        _currentGravityScale = baseGravityScale;

        // Uniform velocity delta across every point — identical Δvy means no
        // differential motion between points so no compression wave can form in the
        // spring network. Any per-point variation (top-only impulse, weighted impulse)
        // creates a velocity gradient that travels as a visible ripple.
        foreach (var rb in _rbs)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpForce);
    }

    private void HandleGroundPound()
    {
        if (!InputEnabled) return;

        if (!IsGrounded && Input.GetKey(KeyCode.C) && !IsGroundPounding)
        {
            IsGroundPounding     = true;
            _currentGravityScale = maxGravityScale + groundPoundGravityBoost;
            foreach (var rb in _rbs)
                rb.linearVelocity = new Vector2(rb.linearVelocity.x, -groundPoundDownForce);
        }

        if (IsGroundPounding && IsGrounded)
        {
            float impactVel = 0f;
            foreach (var rb in _rbs) impactVel += Mathf.Abs(rb.linearVelocity.y);
            impactVel /= pointCount;

            IsGroundPounding       = false;
            _groundPoundJustLanded = true;
            OnGroundPoundLand?.Invoke(impactVel);
        }
    }

    private void EnforceLevelBounds()
    {
        if (levelBounds == null) return;
        Bounds b = levelBounds.bounds;

        foreach (var rb in _rbs)
        {
            Vector2 f = Vector2.zero;
            if (rb.position.x < b.min.x) f.x =  (b.min.x - rb.position.x) * boundaryForce;
            if (rb.position.x > b.max.x) f.x = -(rb.position.x - b.max.x) * boundaryForce;
            if (f.x != 0f)
                rb.AddForce(f, ForceMode2D.Force);
        }
    }

    private void EnforceNeighborConstraints()
    {
        const float maxNeighborStretch = 1.5f;

        for (int i = 0; i < pointCount; i++)
        {
            int     j       = (i + 1) % pointCount;
            Vector2 delta   = _rbs[j].position - _rbs[i].position;
            float   dist    = delta.magnitude;
            float   maxDist = _neighborRestDist[i] * maxNeighborStretch;

            if (dist <= maxDist) continue;

            Vector2 dir    = delta / dist;
            float   excess = dist - maxDist;

            _rbs[i].position += dir * (excess * 0.5f);
            _rbs[j].position -= dir * (excess * 0.5f);

            float relVel = Vector2.Dot(_rbs[j].linearVelocity - _rbs[i].linearVelocity, dir);
            if (relVel > 0f)
            {
                _rbs[i].linearVelocity += dir * (relVel * 0.5f);
                _rbs[j].linearVelocity -= dir * (relVel * 0.5f);
            }
        }
    }

    private void ResolveCollisions()
    {
        float checkR = pointRadius + 0.04f;

        for (int i = 0; i < pointCount; i++)
        {
            Vector2 prev  = _prevPositions[i];
            Vector2 curr  = _rbs[i].position;
            Vector2 delta = curr - prev;
            float   dist  = delta.magnitude;

            if (dist >= pointRadius)
            {
                RaycastHit2D swept = Physics2D.CircleCast(prev, pointRadius, delta / dist, dist, groundLayer);
                if (swept.collider != null)
                {
                    curr             = swept.centroid + swept.normal * 0.005f;
                    _rbs[i].position = curr;
                    float vd = Vector2.Dot(_rbs[i].linearVelocity, swept.normal);
                    if (vd < 0f) _rbs[i].linearVelocity -= swept.normal * (vd * 0.5f);
                }
            }

            Collider2D hit = Physics2D.OverlapCircle(curr, checkR, groundLayer);
            if (hit == null) continue;

            ColliderDistance2D cd = _cols[i].Distance(hit);
            if (cd.distance > -0.01f) continue;

            _rbs[i].position += cd.normal * cd.distance;
            float vDot = Vector2.Dot(_rbs[i].linearVelocity, cd.normal);
            if (vDot > 0f) _rbs[i].linearVelocity -= cd.normal * (vDot * 0.5f);
        }
    }

    // ── Animation ─────────────────────────────────────────────────────────

    private void UpdateAnimationState()
    {
        float dt = Time.fixedDeltaTime;

        // Average vertical velocity for rise/fall detection
        float avgVY = 0f;
        foreach (var rb in _rbs) avgVY += rb.linearVelocity.y;
        avgVY /= pointCount;

        // Landing — trigger on the first grounded frame after being airborne
        if (IsGrounded && !_wasGrounded)
            _landingTimer = landingDuration;

        if (_landingTimer > 0f)
        {
            _landingTimer  -= dt;
            _landingSquashT = _landingTimer / landingDuration; // 1 at impact, linear decay to 0
        }
        else
        {
            _landingSquashT = 0f;
        }

        _wasGrounded = IsGrounded;

        // Ground-pound landing: override timer from previous tick's HandleGroundPound
        if (_groundPoundJustLanded)
        {
            _landingTimer          = groundPoundSquashDuration;
            _groundPoundJustLanded = false;
        }

        // Target blend values
        bool isMoving  = Mathf.Abs(_hInput) > 0.05f;
        bool isIdle    = IsGrounded && !isMoving;
        float riseTarget = IsGrounded ? 0f : Mathf.Clamp01(avgVY / Mathf.Max(riseVelocityFull, 0.01f));
        // Fall blend is replaced by dive compress anim while ground-pounding
        float fallTarget = (IsGrounded || IsGroundPounding) ? 0f : Mathf.Clamp01(-avgVY / Mathf.Max(fallVelocityFull, 0.01f));

        float blendStep = animBlendSpeed * dt;
        _idleBlend  = Mathf.MoveTowards(_idleBlend,  isIdle  ? 1f : 0f, blendStep);
        _moveBlend  = Mathf.MoveTowards(_moveBlend,  (IsGrounded && isMoving) ? 1f : 0f, blendStep);
        _leanBlend  = Mathf.MoveTowards(_leanBlend,  isMoving ? 1f : 0f, blendStep); // active grounded or airborne
        _riseBlend  = Mathf.MoveTowards(_riseBlend,  riseTarget, blendStep);
        _fallBlend  = Mathf.MoveTowards(_fallBlend,  fallTarget, blendStep);

        // Smooth lean direction — follows input with a slight lag for feel
        _leanDir = Mathf.MoveTowards(_leanDir, _hInput, animBlendSpeed * 0.8f * dt);

        // Advance phases — bob phase only advances on the ground so it can't
        // drive oscillations into the spring network during a jump.
        _idlePhase += idleBobFrequency * dt;
        if (IsGrounded)
            _moveBobPhase += Mathf.Abs(_hInput) * moveBobFrequency * dt;
        if (WiggleBlend > 0.001f)
            _wigglePhase += wiggleFrequency * dt;

        // Scale physics forces so animations aren't fought by pressure during landing.
        // Rise also relaxes pressure slightly to let the vertical stretch breathe.
        _pressureMultiplier = Mathf.Lerp(1f, landingPressureScale, _landingSquashT);
        _pressureMultiplier = Mathf.Min(_pressureMultiplier, Mathf.Lerp(1f, 0.65f, _riseBlend));
        _restoreMultiplier  = Mathf.Lerp(1f, landingRestoreScale,  _landingSquashT);

        ComputeAnimOffsets();
    }

    private void ComputeAnimOffsets()
    {
        // Shared oscillator values computed once
        float idleSin  = Mathf.Sin(_idlePhase * Mathf.PI * 2f);
        float bobSin   = Mathf.Sin(_moveBobPhase * Mathf.PI * 2f);

        for (int i = 0; i < pointCount; i++)
        {
            float angle = _angles[i];
            float cosA  = Mathf.Cos(angle);
            float sinA  = Mathf.Sin(angle);

            // Outward unit vector for this point (radial direction from centre)
            Vector2 radial = new Vector2(cosA, sinA);

            Vector2 anim = Vector2.zero;

            // ── Idle: uniform radial pulse — expands and contracts the whole body ──
            // Pure radial keeps the sum of offsets over all ring points at zero,
            // so no net force is introduced into the physics simulation.
            anim += radial * (idleSin * idleBobAmplitude * _idleBlend);

            // ── Move: alternating squash/stretch step cycle ──────────────────────
            // Strictly grounded-only — no bob contribution when airborne at all,
            // preventing the oscillating driver from rippling through the spring
            // network during a moving jump.
            if (_moveBlend > 0.001f && IsGrounded)
            {
                anim.x += cosA *  bobSin * moveBobAmplitude * _moveBlend;
                anim.y += sinA * -bobSin * moveBobAmplitude * _moveBlend;
            }

            // ── Rise: tall and narrow — classic jump stretch ─────────────────────
            if (_riseBlend > 0.001f)
            {
                anim.x += cosA * (-riseSqueezeAmount) * _riseBlend;
                anim.y += sinA *   riseStretchAmount  * _riseBlend;
            }

            // ── Fall: wide and flat — belly-down anticipation ────────────────────
            // Bottom points spread more than the top for a hanging-belly silhouette.
            if (_fallBlend > 0.001f)
            {
                float bottomBias = Mathf.Lerp(1f, 1.5f, Mathf.Max(0f, -sinA));
                anim.x += cosA *  fallSpreadAmount  * bottomBias * _fallBlend;
                anim.y += sinA * -fallFlattenAmount * _fallBlend;
            }

            // ── Landing squash: sudden wide-flat spike, then restore ──────────────
            if (_landingSquashT > 0.001f)
            {
                anim.x += cosA *  landingSquashSpread   * _landingSquashT;
                anim.y += sinA * -landingSquashFlatten  * _landingSquashT;
            }

            // ── Ground pound dive: flatten vertically, spread horizontally ────────
            if (IsGroundPounding)
            {
                anim.x += cosA *  groundPoundDiveSpread;
                anim.y += sinA * -groundPoundDiveCompress;
            }

            // ── Split: vertical seam forms top-to-bottom, then halves peel apart ──
            if (SplitPinchBlend > 0.001f)
            {
                float blend = SplitPinchBlend;

                // Squeeze the midline inward and stretch the poles — forms hourglass
                anim.x += cosA * -splitPinchInward   * blend;
                anim.y += sinA *  splitPinchVertical * blend;

                // Halves peel apart — upper points lead, lower points follow.
                // topBias: 1 at crown (sinA=1), 0.4 at base (sinA=-1), so the tear
                // starts at the top and cascades downward.
                float topBias  = Mathf.Lerp(0.4f, 1.0f, (sinA + 1f) * 0.5f);
                float sepBlend = Mathf.Clamp01((blend - 0.25f) / 0.75f) * topBias;
                float side     = cosA >= 0f ? 1f : -1f;
                anim.x += side * splitSeparateAmount * sepBlend;
            }

            // ── Merge pop: radial expansion burst ────────────────────────────────
            // Power curve gives a fast peak and a soft settling tail.
            if (MergePopBlend > 0.001f)
            {
                float popT = Mathf.Pow(MergePopBlend, 1f / mergePopFalloff);
                anim += radial * (mergePopOutward * popT);
            }

            // ── Pressure squash: amplified landing squash on active ground-pound ─
            // Accumulates on top of _landingSquashT so both can be active at once.
            if (PressureSquashBlend > 0.001f)
            {
                float extra = Mathf.Lerp(0f, groundPoundSquashMult - 1f, PressureSquashBlend);
                anim.x += cosA *  (landingSquashSpread  * extra * PressureSquashBlend);
                anim.y += sinA * -(landingSquashFlatten * extra * PressureSquashBlend);
            }

            // ── Activate squash: quick inward squeeze on Tab switch ────────────
            if (ActivateSquashBlend > 0.001f)
            {
                anim.x += cosA * -activateSquashInward   * ActivateSquashBlend;
                anim.y += sinA * -activateSquashVertical * ActivateSquashBlend;
            }

            // ── Tension wiggle: dampened horizontal oscillation post-split/merge ─
            // cosA * sin(phase) makes left/right halves oscillate out of phase,
            // producing a lateral jello-like squeeze that decays with WiggleBlend.
            if (WiggleBlend > 0.001f)
            {
                float wiggleSin = Mathf.Sin(_wigglePhase * Mathf.PI * 2f);
                anim.x += cosA * wiggleAmplitude * wiggleSin * WiggleBlend;
            }

            _animOffsets[i] = anim;
        }
    }

    // ── Mesh ──────────────────────────────────────────────────────────────

    private void SetupMesh()
    {
        _subdivVerts = pointCount * subdivisionsPerSegment;
        _smoothRing  = new Vector2[_subdivVerts];
        _meshVerts   = new Vector3[_subdivVerts + 1];
        _meshUVs     = new Vector2[_subdivVerts + 1];
        _meshColors  = new Color[_subdivVerts + 1];
        _preSmoothA  = new Vector2[pointCount];
        _preSmoothB  = new Vector2[pointCount];

        _mesh = new Mesh { name = "SoftBodyMesh" };
        GetComponent<MeshFilter>().mesh = _mesh;

        _triangles = new int[_subdivVerts * 3];
        for (int i = 0; i < _subdivVerts; i++)
        {
            int next              = (i + 1) % _subdivVerts;
            _triangles[i * 3]     = 0;
            _triangles[i * 3 + 1] = i + 1;
            _triangles[i * 3 + 2] = next + 1;
        }

        _mesh.vertices  = _meshVerts;
        _mesh.uv        = _meshUVs;
        _mesh.triangles = _triangles;

        _bodyRenderer = GetComponent<MeshRenderer>();
        if (bodyMaterial != null)
        {
            _bodyRenderer.sharedMaterial = bodyMaterial;
        }
        else
        {
            _bodyRenderer.material = new Material(Shader.Find("Sprites/Default")) { color = Color.white };
        }

        _bodyRenderer.sortingLayerName = sortingLayerName;
        _bodyRenderer.sortingOrder     = sortingOrder;
    }

    private void SetupHighlight()
    {
        _highlightVerts = new Vector3[_subdivVerts + 1];

        var go = new GameObject("BodyHighlight");
        go.transform.SetParent(transform);
        go.transform.localPosition = Vector3.zero;
        go.transform.localScale    = Vector3.one;

        _highlightMesh = new Mesh { name = "HighlightMesh" };
        go.AddComponent<MeshFilter>().mesh = _highlightMesh;

        _highlightRenderer                  = go.AddComponent<MeshRenderer>();
        _highlightRenderer.material         = new Material(Shader.Find("Sprites/Default")) { color = highlightColor };
        _highlightRenderer.sortingLayerName = sortingLayerName;
        _highlightRenderer.sortingOrder     = sortingOrder + 1;

        // Reuse the same fan topology as the main mesh
        _highlightMesh.vertices  = _highlightVerts;
        _highlightMesh.triangles = _triangles;
    }

    private void RebuildMesh()
    {
        Vector2 rootPos = transform.position;
        int N = pointCount, S = subdivisionsPerSegment;

        for (int i = 0; i < N; i++)
            _preSmoothA[i] = _pointGOs[i].transform.position;

        for (int pass = 0; pass < meshSmoothingPasses; pass++)
        {
            for (int i = 0; i < N; i++)
            {
                int prev = (i - 1 + N) % N;
                int next = (i + 1) % N;
                _preSmoothB[i] = (_preSmoothA[prev] + _preSmoothA[i] * 4f + _preSmoothA[next]) / 6f;
            }
            var tmp = _preSmoothA; _preSmoothA = _preSmoothB; _preSmoothB = tmp;
        }

        for (int i = 0; i < N; i++)
        {
            Vector2 p0 = _preSmoothA[(i - 1 + N) % N];
            Vector2 p1 = _preSmoothA[i];
            Vector2 p2 = _preSmoothA[(i + 1) % N];
            Vector2 p3 = _preSmoothA[(i + 2) % N];

            for (int s = 0; s < S; s++)
                _smoothRing[i * S + s] = CentripetalCatmullRom(p0, p1, p2, p3, s / (float)S);
        }

        _meshVerts[0] = (Vector3)(_center - rootPos);
        _meshUVs[0]   = new Vector2(0.5f, 0.5f);

        for (int i = 0; i < _subdivVerts; i++)
        {
            Vector2 lp        = _smoothRing[i] - rootPos;
            _meshVerts[i + 1] = lp;
            _meshUVs[i + 1]   = lp / bodyRadius * 0.5f + Vector2.one * 0.5f;
        }

        // Visual-only lean — shear transform: bottom stays planted, top shifts forward.
        // Scaling by normalised Y means bottom vertices get ~0 offset and top gets full
        // leanX, producing a distinct directional tilt rather than a whole-body slide.
        if (_leanBlend > 0.001f)
        {
            float leanX     = _leanDir * moveLeanAmount * _leanBlend;
            float halfHeight = bodyRadius * domeHeightScale;

            for (int i = 0; i <= _subdivVerts; i++)
            {
                // Map local y from [-halfHeight, +halfHeight] → shear scalar [0, 1]
                // Bottom of blob leans back slightly, top leans forward fully.
                float shear = (_meshVerts[i].y + halfHeight) / (halfHeight * 2f);
                _meshVerts[i].x += leanX * shear;
            }
        }

        _mesh.vertices = _meshVerts;
        _mesh.uv       = _meshUVs;
        UpdateBodyColors();
        _mesh.RecalculateBounds();
        _mesh.RecalculateNormals();
    }

    private void UpdateBodyColors()
    {
        var inner = new Color(bodyInnerColor.r, bodyInnerColor.g, bodyInnerColor.b, bodyInnerColor.a * _bodyAlpha);
        var outer = new Color(bodyOuterColor.r, bodyOuterColor.g, bodyOuterColor.b, bodyOuterColor.a * _bodyAlpha);

        _meshColors[0] = inner;
        for (int i = 1; i <= _subdivVerts; i++)
        {
            float dist = Vector2.Distance(_meshUVs[i], Vector2.one * 0.5f) * 2f;
            _meshColors[i] = Color.Lerp(inner, outer, Mathf.Clamp01(dist));
        }

        _mesh.colors = _meshColors;
    }

    private void RebuildHighlight()
    {
        if (_highlightMesh == null) return;

        // Highlight is a scaled-down, shifted copy of the deformed mesh verts,
        // so it bends with squash/stretch/lean exactly like the body.
        Vector3 offset = highlightOffset;
        for (int i = 0; i <= _subdivVerts; i++)
            _highlightVerts[i] = _meshVerts[i] * highlightScale + offset;

        _highlightMesh.vertices = _highlightVerts;
        _highlightMesh.triangles = _triangles;
        _highlightMesh.RecalculateBounds();

        _highlightRenderer.material.color = new Color(highlightColor.r, highlightColor.g, highlightColor.b, highlightColor.a * _bodyAlpha);
    }

    private static Vector2 CentripetalCatmullRom(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t0 = 0f;
        float t1 = CRKnot(t0, p0, p1);
        float t2 = CRKnot(t1, p1, p2);
        float t3 = CRKnot(t2, p2, p3);

        float u = Mathf.LerpUnclamped(t1, t2, t);

        Vector2 A1 = CRLerp(p0, p1, t0, t1, u);
        Vector2 A2 = CRLerp(p1, p2, t1, t2, u);
        Vector2 A3 = CRLerp(p2, p3, t2, t3, u);
        Vector2 B1 = CRLerp(A1, A2, t0, t2, u);
        Vector2 B2 = CRLerp(A2, A3, t1, t3, u);
        return     CRLerp(B1, B2, t1, t2, u);
    }

    private static float CRKnot(float t, Vector2 a, Vector2 b)
        => t + Mathf.Pow(Mathf.Max(Vector2.Distance(a, b), 0.0001f), 0.5f);

    private static Vector2 CRLerp(Vector2 a, Vector2 b, float t0, float t1, float t)
    {
        float span = t1 - t0;
        if (Mathf.Abs(span) < 0.0001f) return a;
        return Vector2.LerpUnclamped(a, b, (t - t0) / span);
    }

    // ── Gizmos ───────────────────────────────────────────────────────────

    private void OnDrawGizmos()
    {
        if (_rbs != null)
        {
            int N       = _rbs.Length;
            int quarter = N / 4;

            Gizmos.color = new Color(1f, 0.5f, 0f, 0.15f);
            for (int i = 0; i < N; i++)
                Gizmos.DrawLine(_rbs[i].position, _rbs[(i + quarter) % N].position);

            Gizmos.color = new Color(1f, 1f, 0f, 0.25f);
            for (int i = 0; i < N; i++)
                Gizmos.DrawLine(_rbs[i].position, _rbs[(i + 2) % N].position);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.6f);
            for (int i = 0; i < N; i++)
                Gizmos.DrawLine(_rbs[i].position, _rbs[(i + 1) % N].position);

            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.9f);
            for (int i = 0; i < N; i++)
                Gizmos.DrawWireSphere(_rbs[i].position, pointRadius);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_center, pointRadius * 1.5f);
        }
        else
        {
            Gizmos.color = new Color(0.2f, 0.9f, 1f, 0.5f);
            Vector2 origin = transform.position;
            for (int i = 0; i < pointCount; i++)
            {
                float   a1 = i       / (float)pointCount * Mathf.PI * 2f;
                float   a2 = (i + 1) / (float)pointCount * Mathf.PI * 2f;
                Vector2 p1 = origin + new Vector2(Mathf.Cos(a1) * bodyRadius, Mathf.Sin(a1) * bodyRadius * domeHeightScale);
                Vector2 p2 = origin + new Vector2(Mathf.Cos(a2) * bodyRadius, Mathf.Sin(a2) * bodyRadius * domeHeightScale);
                Gizmos.DrawLine(p1, p2);
            }
        }
    }
}
