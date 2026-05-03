using System.Collections;
using UnityEngine;

/*
 * OVERVIEW
 *   Orchestrates the split, merge, and pressure-transfer mechanics for the
 *   softbody water-droplet player.  Attach to the same GameObject as the main
 *   SoftBodyPlayer (or any persistent manager GO in the scene).
 *
 * SCENE SETUP
 *   1. Assign Main Player  → the scene's SoftBodyPlayer GameObject
 *   2. Assign Camera Proxy → the CameraFollowProxy that Cinemachine follows
 *
 * KEY BINDINGS
 *   Left Shift   → split into two half-size droplets
 *   Tab          → swap which droplet is active (camera pans across)
 *   C (airborne) → ground pound on the active droplet
 *   Auto         → droplets merge when their centres come within mergeProximityRadius
 *
 * SPLIT BEHAVIOUR
 *   - The droplet that faces the same direction as the player was facing becomes active.
 *   - Both droplets inherit all physics and visual settings from mainPlayer, scaled to
 *     half mass / half area.
 *   - A short body-alpha fade-out plays before the split; a spawn-pop plays after.
 *
 * MERGE BEHAVIOUR
 *   - Proximity is polled every LateUpdate using freshly-interpolated ring-point positions.
 *   - The merged blob spawns at the active droplet's position the moment the threshold is
 *     crossed, then fades in.  The camera smooth-damps to avoid a hard snap.
 *   - The merged droplet inherits the active droplet's face direction.
 *
 * PRESSURE TRANSFER
 *   - When the active droplet ground-pounds, the passive droplet is launched upward
 *     at exactly its own jumpForce — a fixed, predictable height every time.
 *   - The passive droplet must be grounded; holding C cannot repeatedly air-launch it.
 *
 * EVENTS  (see EventManager)
 *   EventManager.OnPlayerSplit  — fired at the end of every successful split
 *   EventManager.OnPlayerMerge  — fired at the end of every merge
 */
public class PlayerSplitController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The main SoftBodyPlayer — hidden while split, restored on merge.")]
    public SoftBodyPlayer    mainPlayer;
    [Tooltip("CameraFollowProxy that Cinemachine tracks. Receives UpdateTarget / SwitchTarget calls.")]
    public CameraFollowProxy cameraProxy;

    [Header("Split")]
    [Tooltip("Horizontal burst speed given to each droplet on split.")]
    public float splitBurstX        = 1.5f;
    [Tooltip("Upward burst speed given to each droplet on split.")]
    public float splitBurstY        = 1.0f;
    [Tooltip("How much of the pre-split horizontal velocity is reflected onto the passive droplet in the opposite direction. 1 = full speed reversal, 0 = no repel.")]
    public float splitPassiveVelocityScale = 1.0f;
    [Range(0f, 1f)]
    [Tooltip("Fraction of moveDrag applied to the passive droplet. Lets repel velocity coast naturally before decelerating. 0 = no drag (slides forever), 1 = full drag (stops instantly).")]
    public float splitPassiveDragFraction = 0.04f;

    [Header("Merge")]
    [Tooltip("Distance between droplet centres that triggers auto-merge (world units).")]
    public float mergeProximityRadius = 0.4f;
    [Tooltip("Seconds after a split before merging is allowed again.")]
    public float mergeCooldownDuration = 0.75f;

    [Header("Pressure Transfer")]
    [Tooltip("Duration of the extra squash on the active droplet after a pressure transfer (seconds).")]
    public float pressureSquashDuration = 0.28f;

    // ── Runtime state ────────────────────────────────────────────────────

    private SoftBodyPlayer[] _droplets  = new SoftBodyPlayer[2]; // [0] = left, [1] = right
    private int              _activeIdx = 0;
    private bool             _isSplit;
    private Coroutine        _splitCoroutine;
    private bool             _isMerging;     // true while MergeCoroutine is executing
    private float            _mergeCooldown; // prevents immediate re-merge after split

    private Vector2 _capturedMergePos; // active droplet position at the moment proximity triggers
    private Vector2 _capturedMergeVel; // average ring-point velocity of both droplets

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (mainPlayer == null)
            Debug.LogError("[PlayerSplitController] mainPlayer is not assigned.", this);
    }

    private void Update()
    {
        if (mainPlayer == null) return;

        if (!_isSplit && _splitCoroutine == null && !_isMerging)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) && !mainPlayer.IsGroundPounding)
                _splitCoroutine = StartCoroutine(SplitCoroutine());
        }

        if (_isSplit && Input.GetKeyDown(KeyCode.Tab))
            SetActiveDroplet(1 - _activeIdx);
    }

    private void LateUpdate()
    {
        // Proximity check in LateUpdate so ring-point transform.positions are freshly
        // interpolated for this render frame — more current than rb.position in FixedUpdate.
        if (_isSplit && !_isMerging && _mergeCooldown <= 0f &&
            _droplets[0] != null && _droplets[1] != null)
        {
            Vector2 c0 = RenderCenter(_droplets[0]);
            Vector2 c1 = RenderCenter(_droplets[1]);

            if (Vector2.Distance(c0, c1) < mergeProximityRadius)
            {
                _capturedMergePos = (c0 + c1) * 0.5f;

                Vector2 velSum = Vector2.zero; int vn = 0;
                foreach (var d in _droplets)
                    if (d != null)
                        foreach (var rb in d.Points) { velSum += rb.linearVelocity; vn++; }
                _capturedMergeVel = vn > 0 ? velSum / vn : Vector2.zero;

                StartCoroutine(MergeCoroutine());
            }
        }

        if (cameraProxy == null) return;

        if (_isSplit)
        {
            var active = _droplets[_activeIdx];
            Vector2 target = active != null ? active.Center : (Vector2)mainPlayer.transform.position;
            cameraProxy.UpdateTarget(target);
        }
        else if (_isMerging)
        {
            var active = _droplets[_activeIdx];
            if (active != null) cameraProxy.UpdateTarget(active.Center);
            else if (mainPlayer != null) cameraProxy.UpdateTarget(mainPlayer.Center);
        }
        else if (mainPlayer != null)
        {
            cameraProxy.UpdateTarget(mainPlayer.Center);
        }
    }

    private void FixedUpdate()
    {
        if (_mergeCooldown > 0f) _mergeCooldown -= Time.fixedDeltaTime;
    }

    // Centroid from ring-point transform.positions — valid at any point in LateUpdate
    // regardless of script execution order, since it reads directly from the GOs.
    private static Vector2 RenderCenter(SoftBodyPlayer d)
    {
        var pts = d.Points;
        Vector2 sum = Vector2.zero;
        foreach (var rb in pts) sum += (Vector2)rb.transform.position;
        return sum / pts.Length;
    }

    // ── Split ─────────────────────────────────────────────────────────────

    private IEnumerator SplitCoroutine()
    {
        // Fade out the main player before splitting for a smooth visual transition.
        yield return StartCoroutine(DriveBodyFade(mainPlayer, 1f, 0f, 0.06f));

        // Sample the visual half-positions at this exact moment so the spawned
        // droplets appear exactly where the two halves of the body currently are.
        mainPlayer.GetHalfState(
            out Vector2 leftCenter,  out Vector2 rightCenter,
            out Vector2 leftVel,     out Vector2 rightVel);

        // Active droplet matches the direction the player was facing:
        // facing right → right droplet (index 1), facing left → left droplet (index 0).
        float preSplitFaceDir = mainPlayer.LastFaceDir;
        int   activeIdx       = preSplitFaceDir > 0f ? 1 : 0;

        mainPlayer.SplitPinchBlend = 0f;
        mainPlayer.SetBodyAlpha(1f); // reset alpha so mainPlayer reappears correctly on merge
        mainPlayer.Freeze();
        mainPlayer.SetVisible(false);

        // Average pre-split horizontal velocity — used to repel the passive droplet.
        float avgVelX = (leftVel.x + rightVel.x) * 0.5f;

        Vector2 vel0 = leftVel  + new Vector2(-splitBurstX, splitBurstY);
        Vector2 vel1 = rightVel + new Vector2( splitBurstX, splitBurstY);

        // Override only the passive droplet's horizontal velocity.
        // No burst X on the passive — when still, it should receive zero horizontal force.
        // When moving, it gets the mirrored speed so it flies in the opposite direction.
        // Drag is never applied to inactive droplets (InputEnabled = false), so this
        // velocity coasts naturally under gravity until the player takes control.
        int passiveIdx = 1 - activeIdx;
        if (passiveIdx == 0) vel0.x = -avgVelX * splitPassiveVelocityScale;
        else                 vel1.x = -avgVelX * splitPassiveVelocityScale;

        _droplets[0] = SpawnDroplet(leftCenter,  vel0);
        _droplets[1] = SpawnDroplet(rightCenter, vel1);

        _isSplit        = true;
        _activeIdx      = 0;
        _mergeCooldown  = mergeCooldownDuration;
        _splitCoroutine = null;
        SetActiveDroplet(activeIdx);

        // Push the pre-split face direction into the active droplet immediately so the
        // sprite is correct on the first frame before UpdateFace has a chance to run.
        _droplets[activeIdx].InitFaceDirection(preSplitFaceDir);

        // Birth pop (radial expansion) then dampened wiggle on both droplets.
        StartCoroutine(DriveSpawnPop(_droplets[0], 0.28f));
        StartCoroutine(DriveSpawnPop(_droplets[1], 0.28f));
        StartCoroutine(DriveWiggle(_droplets[0], 0.50f));
        StartCoroutine(DriveWiggle(_droplets[1], 0.50f));

        EventManager.PlayerSplit();
        yield break;
    }

    // Creates a half-size SoftBodyPlayer at runtime, copying all settings from mainPlayer.
    // go.SetActive(false) before AddComponent so Awake does not fire until all fields are
    // assigned.  MeshFilter and MeshRenderer must be added manually because [RequireComponent]
    // only auto-adds in the Editor — it has no effect during runtime AddComponent calls.
    private SoftBodyPlayer SpawnDroplet(Vector2 center, Vector2 initialVelocity)
    {
        var go = new GameObject("SplitDroplet");
        go.SetActive(false);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();

        // Important to keep visuals tracking the right objects
        go.tag = "Player";
        go.layer = LayerMask.NameToLayer("Player");

        var sp = go.AddComponent<SoftBodyPlayer>();

        // ── Physics — scaled to half mass / half area ─────────────────────
        sp.pointCount          = mainPlayer.pointCount;
        sp.bodyRadius          = mainPlayer.bodyRadius / Mathf.Sqrt(2f); // half area = radius / √2
        sp.domeHeightScale     = mainPlayer.domeHeightScale;
        sp.pointMass           = mainPlayer.pointMass * 0.5f;
        sp.pointRadius         = mainPlayer.pointRadius;
        sp.softBodyPointLayer  = mainPlayer.softBodyPointLayer;
        sp.springFrequency     = mainPlayer.springFrequency;
        sp.springDamping       = mainPlayer.springDamping;
        sp.restoreForce        = mainPlayer.restoreForce  * 0.5f; // proportional to pointMass
        sp.pressureForce       = mainPlayer.pressureForce * 0.5f;
        sp.moveForce           = mainPlayer.moveForce * 0.8f;
        sp.maxMoveSpeed        = mainPlayer.maxMoveSpeed;
        sp.moveDrag            = mainPlayer.moveDrag * 0.5f;
        sp.passiveDragFraction = splitPassiveDragFraction;
        sp.airControlFraction  = mainPlayer.airControlFraction;
        sp.jumpForce           = mainPlayer.jumpForce * 0.85f;
        sp.baseGravityScale    = mainPlayer.baseGravityScale;
        sp.maxGravityScale     = mainPlayer.maxGravityScale;
        sp.gravityRampRate     = mainPlayer.gravityRampRate;
        sp.groundLayer         = mainPlayer.groundLayer;
        sp.groundCheckFraction = mainPlayer.groundCheckFraction;
        sp.levelBounds         = mainPlayer.levelBounds;
        sp.boundaryForce       = mainPlayer.boundaryForce * 0.5f;

        // ── Animation ─────────────────────────────────────────────────────
        sp.idleBobAmplitude        = mainPlayer.idleBobAmplitude;
        sp.idleBobFrequency        = mainPlayer.idleBobFrequency;
        sp.moveLeanAmount          = mainPlayer.moveLeanAmount;
        sp.moveBobAmplitude        = mainPlayer.moveBobAmplitude;
        sp.moveBobFrequency        = mainPlayer.moveBobFrequency;
        sp.riseStretchAmount       = mainPlayer.riseStretchAmount;
        sp.riseSqueezeAmount       = mainPlayer.riseSqueezeAmount;
        sp.riseVelocityFull        = mainPlayer.riseVelocityFull;
        sp.fallSpreadAmount        = mainPlayer.fallSpreadAmount;
        sp.fallFlattenAmount       = mainPlayer.fallFlattenAmount;
        sp.fallVelocityFull        = mainPlayer.fallVelocityFull;
        sp.landingSquashSpread     = mainPlayer.landingSquashSpread;
        sp.landingSquashFlatten    = mainPlayer.landingSquashFlatten;
        sp.landingDuration         = mainPlayer.landingDuration;
        sp.animBlendSpeed          = mainPlayer.animBlendSpeed;
        sp.landingPressureScale    = mainPlayer.landingPressureScale;
        sp.landingRestoreScale     = mainPlayer.landingRestoreScale;
        sp.groundPoundDownForce    = mainPlayer.groundPoundDownForce * 0.6f;
        sp.groundPoundGravityBoost = mainPlayer.groundPoundGravityBoost;
        sp.groundPoundDiveCompress = mainPlayer.groundPoundDiveCompress;
        sp.groundPoundDiveSpread   = mainPlayer.groundPoundDiveSpread;
        sp.groundPoundSquashMult   = mainPlayer.groundPoundSquashMult;
        sp.groundPoundSquashDuration = mainPlayer.groundPoundSquashDuration;
        sp.splitPinchInward        = mainPlayer.splitPinchInward;
        sp.splitPinchVertical      = mainPlayer.splitPinchVertical;
        sp.mergePopOutward         = mainPlayer.mergePopOutward;
        sp.mergePopFalloff         = mainPlayer.mergePopFalloff;

        // ── Rendering ─────────────────────────────────────────────────────
        sp.bodyMaterial           = mainPlayer.bodyMaterial;
        sp.sortingLayerName       = mainPlayer.sortingLayerName;
        sp.sortingOrder           = mainPlayer.sortingOrder;
        sp.subdivisionsPerSegment = mainPlayer.subdivisionsPerSegment;
        sp.meshSmoothingPasses    = mainPlayer.meshSmoothingPasses;
        sp.bodyInnerColor         = mainPlayer.bodyInnerColor;
        sp.bodyOuterColor         = mainPlayer.bodyOuterColor;
        sp.highlightColor         = mainPlayer.highlightColor;
        sp.highlightScale         = mainPlayer.highlightScale;
        sp.highlightOffset        = mainPlayer.highlightOffset;

        // ── Face — half scale to match the smaller body ───────────────────
        sp.faceRightSprite  = mainPlayer.faceRightSprite;
        sp.faceLeftSprite   = mainPlayer.faceLeftSprite;
        sp.faceBias         = mainPlayer.faceBias;
        sp.faceOffset       = mainPlayer.faceOffset;
        sp.faceScale        = mainPlayer.faceScale * 0.625f;
        sp.faceSortingOrder = mainPlayer.faceSortingOrder;
        sp.faceFadeDuration = mainPlayer.faceFadeDuration;
        sp.wiggleAmplitude  = mainPlayer.wiggleAmplitude;
        sp.wiggleFrequency  = mainPlayer.wiggleFrequency;

        go.transform.position = center;
        go.SetActive(true); // Awake fires here — all fields are set, so SpawnPoints runs correctly

        foreach (var rb in sp.Points)
            rb.linearVelocity = initialVelocity;

        return sp;
    }

    // ── Merge ─────────────────────────────────────────────────────────────

    private IEnumerator MergeCoroutine()
    {
        _isMerging = true;
        _isSplit = false;

        // Disable input on both droplets and detach the ground-pound listener so
        // no further pressure transfers can fire mid-merge.
        _droplets[0].InputEnabled = false;
        _droplets[1].InputEnabled = false;
        _droplets[_activeIdx].OnGroundPoundLand -= OnActiveGroundPound;

        Vector2 mergePos    = _capturedMergePos;
        Vector2 combinedVel = _capturedMergeVel;

        // Capture face direction BEFORE destroying droplets — after DestroyDroplets() the
        // array entries are null and LastFaceDir would always fall back to 1f.
        float rawH = Input.GetAxisRaw("Horizontal");
        float activeFaceDir = rawH > 0.05f  ?  1f
                            : rawH < -0.05f ? -1f
                            : (_droplets[_activeIdx] != null ? _droplets[_activeIdx].LastFaceDir : 1f);

        DestroyDroplets();

        // Unfreeze BEFORE writing rb.position — position writes on a frozen (simulated=false)
        // Rigidbody2D are silently discarded by the physics engine.
        mainPlayer.Unfreeze();
        mainPlayer.TeleportTo(mergePos, combinedVel);
        mainPlayer.DepenetrateFromGround();
        mainPlayer.InitFaceDirection(activeFaceDir);
        mainPlayer.SetBodyAlpha(0f);
        mainPlayer.SetVisible(true);
        StartCoroutine(DriveBodyFade(mainPlayer, 0f, 1f, 0.06f));

        // SwitchTarget triggers a SmoothDamp pan in CameraFollowProxy instead of a
        // hard snap, preventing the 1-frame camera jump on merge.
        if (cameraProxy != null)
            cameraProxy.SwitchTarget(mergePos);

        StartCoroutine(DriveMergeArrivalPop(mainPlayer, 0.38f));
        StartCoroutine(DriveWiggle(mainPlayer, 0.55f));

        _isMerging = false;
        EventManager.PlayerMerge();
        yield break;
    }

    // ── Pressure transfer ─────────────────────────────────────────────────

    // Callback subscribed to the active droplet's OnGroundPoundLand event.
    // Launches the passive droplet at a fixed, consistent height equal to its
    // own jump force — not scaled from impact velocity, which varied with fall height.
    private void OnActiveGroundPound(float impactVel)
    {
        if (!_isSplit) return;

        int passiveIdx = 1 - _activeIdx;
        var passive = _droplets[passiveIdx];
        if (passive == null) return;

        // Guard: passive must be grounded — prevents chained air-launches from rapid pounding.
        if (!passive.IsGrounded) return;

        foreach (var rb in passive.Points)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, passive.jumpForce);

        StartCoroutine(DrivePressureSquash(_droplets[_activeIdx], pressureSquashDuration));
    }

    // Drives the active droplet's PressureSquashBlend from 1 → 0, amplifying the
    // landing-squash animation to give tactile feedback on a successful transfer.
    private IEnumerator DrivePressureSquash(SoftBodyPlayer droplet, float duration)
    {
        float t = 0f;
        while (t < duration && droplet != null)
        {
            t += Time.deltaTime;
            droplet.PressureSquashBlend = 1f - (t / duration);
            yield return null;
        }
        if (droplet != null)
            droplet.PressureSquashBlend = 0f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    // Switches which droplet receives player input, updates face visibility,
    // sorting order (active draws in front), and pans the camera to the new target.
    private void SetActiveDroplet(int index)
    {
        if (_droplets[_activeIdx] != null)
            _droplets[_activeIdx].OnGroundPoundLand -= OnActiveGroundPound;

        _activeIdx = index;
        int baseOrder = mainPlayer.sortingOrder;

        for (int i = 0; i < 2; i++)
        {
            if (_droplets[i] == null) continue;
            bool isActive = (i == _activeIdx);
            _droplets[i].InputEnabled    = isActive;
            _droplets[i].SetFaceVisible(isActive);
            _droplets[i].SetSortingOrder(isActive ? baseOrder + 2 : baseOrder);
        }

        if (_droplets[_activeIdx] != null)
        {
            _droplets[_activeIdx].OnGroundPoundLand += OnActiveGroundPound;
            StartCoroutine(DriveActivateSquash(_droplets[_activeIdx], 0.18f));
            if (cameraProxy != null)
                cameraProxy.SwitchTarget(_droplets[_activeIdx].Center);
        }
    }

    private void DestroyDroplets()
    {
        for (int i = 0; i < 2; i++)
        {
            if (_droplets[i] != null)
            {
                Destroy(_droplets[i].gameObject);
                _droplets[i] = null;
            }
        }
    }

    // Radial outward expansion using MergePopBlend — gives each newly spawned droplet
    // a brief pop so the split doesn't look like a hard cut.
    private IEnumerator DriveSpawnPop(SoftBodyPlayer droplet, float duration)
    {
        float t = 0f;
        while (t < duration && droplet != null)
        {
            t += Time.deltaTime;
            droplet.MergePopBlend = Mathf.Sin((t / duration) * Mathf.PI);
            yield return null;
        }
        if (droplet != null) droplet.MergePopBlend = 0f;
    }

    // Inward squeeze 0→1→0 on Tab switch to new active droplet
    private IEnumerator DriveActivateSquash(SoftBodyPlayer droplet, float duration)
    {
        float t = 0f;
        while (t < duration && droplet != null)
        {
            t += Time.deltaTime;
            droplet.ActivateSquashBlend = Mathf.Sin((t / duration) * Mathf.PI);
            yield return null;
        }
        if (droplet != null) droplet.ActivateSquashBlend = 0f;
    }

    // Elastic overshoot pop on the reformed main player after merge.  The double-sine
    // formula creates a peak above 1.0 that settles back, reading as a satisfying "thwump".
    private IEnumerator DriveMergeArrivalPop(SoftBodyPlayer player, float duration)
    {
        float t = 0f;
        while (t < duration && player != null)
        {
            t += Time.deltaTime;
            float n = t / duration;
            player.MergePopBlend = Mathf.Sin(n * Mathf.PI) + 0.25f * Mathf.Sin(n * Mathf.PI * 2f);
            yield return null;
        }
        if (player != null) player.MergePopBlend = 0f;
    }

    // Linearly fades the body, highlight, and face alpha together.  Used for the
    // subtle ease-in/out on split (fade out) and merge (fade in).
    private IEnumerator DriveBodyFade(SoftBodyPlayer player, float from, float to, float duration)
    {
        float t = 0f;
        while (t < duration && player != null)
        {
            t += Time.deltaTime;
            player.SetBodyAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
            yield return null;
        }
        if (player != null) player.SetBodyAlpha(to);
    }

    // Dampened horizontal tension wiggle — WiggleBlend decays linearly so oscillations
    // fade out naturally. Used after both split (on each droplet) and merge (on main player).
    private IEnumerator DriveWiggle(SoftBodyPlayer droplet, float duration)
    {
        float t = 0f;
        while (t < duration && droplet != null)
        {
            t += Time.deltaTime;
            droplet.WiggleBlend = 1f - (t / duration); // linear decay 1 → 0
            yield return null;
        }
        if (droplet != null) droplet.WiggleBlend = 0f;
    }

}
