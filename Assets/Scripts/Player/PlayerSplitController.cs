using System.Collections;
using UnityEngine;

public class PlayerSplitController : MonoBehaviour
{
    [Header("References")]
    public SoftBodyPlayer   mainPlayer;
    public CameraFollowProxy cameraProxy;

    [Header("Split")]
    [Tooltip("Horizontal burst speed given to each droplet on split.")]
    public float splitBurstX        = 1.5f;
    [Tooltip("Upward burst speed given to each droplet on split.")]
    public float splitBurstY        = 1.0f;
    [Tooltip("Duration of the pinch animation before the split fires (seconds).")]
    public float splitPinchDuration = 0.15f;

    [Header("Merge")]
    [Tooltip("Distance between droplet centres that triggers auto-merge (world units).")]
    public float mergeProximityRadius = 0.4f;
    [Tooltip("Duration of the merge pop animation (seconds).")]
    public float mergePopDuration     = 0.35f;

    [Header("Pressure Transfer")]
    [Tooltip("Impact velocity is multiplied by this to determine the passive droplet's launch speed.")]
    public float pressureTransferScale  = 0.9f;
    [Tooltip("Minimum upward launch speed on pressure transfer even for a light hit.")]
    public float pressureLaunchMinY     = 2f;
    [Tooltip("Duration of the extra squash on the active droplet after a pressure transfer (seconds).")]
    public float pressureSquashDuration = 0.28f;

    // ── Runtime state ────────────────────────────────────────────────────
    private SoftBodyPlayer[] _droplets  = new SoftBodyPlayer[2];
    private int              _activeIdx = 0;
    private bool             _isSplit;
    private Coroutine        _splitCoroutine;
    private Coroutine        _mergeCoroutine;
    private float            _mergeCooldown;

    // ─────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        if (mainPlayer == null)
            Debug.LogError("[PlayerSplitController] mainPlayer is not assigned.", this);
    }

    private void Update()
    {
        if (mainPlayer == null) return;

        if (!_isSplit && _splitCoroutine == null && _mergeCoroutine == null)
        {
            if (Input.GetKeyDown(KeyCode.LeftShift) && !mainPlayer.IsGroundPounding)
                _splitCoroutine = StartCoroutine(SplitCoroutine());
        }

        if (_isSplit && Input.GetKeyDown(KeyCode.Tab))
            SetActiveDroplet(1 - _activeIdx);
    }

    private void LateUpdate()
    {
        if (cameraProxy == null) return;

        if (_isSplit)
        {
            // Normal split state: follow the active droplet
            var active = _droplets[_activeIdx];
            if (active != null)
                cameraProxy.UpdateTarget(active.Center);
        }
        else if (_mergeCoroutine != null)
        {
            // Mid-merge animation: keep following the active droplet while it still exists.
            // Once DestroyDroplets() runs it becomes null and we fall through to mainPlayer,
            // which TeleportTo() has already placed at the correct merge position.
            var active = _droplets[_activeIdx];
            if (active != null)
                cameraProxy.UpdateTarget(active.Center);
            else if (mainPlayer != null)
                cameraProxy.UpdateTarget(mainPlayer.Center);
        }
        else
        {
            // Fully merged: follow the main player
            if (mainPlayer != null)
                cameraProxy.UpdateTarget(mainPlayer.Center);
        }
    }

    private void FixedUpdate()
    {
        if (_mergeCooldown > 0f) _mergeCooldown -= Time.fixedDeltaTime;

        if (!_isSplit || _mergeCoroutine != null) return;
        if (_droplets[0] == null || _droplets[1] == null) return;
        if (_mergeCooldown > 0f) return;

        if (Vector2.Distance(_droplets[0].Center, _droplets[1].Center) < mergeProximityRadius)
            _mergeCoroutine = StartCoroutine(MergeCoroutine());
    }

    // ── Split ─────────────────────────────────────────────────────────────

    private IEnumerator SplitCoroutine()
    {
        // Phase 1 — ease-in rise to full tear (80% of duration)
        float riseDur = splitPinchDuration * 0.80f;
        float t = 0f;
        while (t < riseDur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / riseDur);
            // Ease-in quad: quick build, slows as it reaches the peak
            mainPlayer.SplitPinchBlend = 1f - (1f - n) * (1f - n);
            yield return null;
        }
        mainPlayer.SplitPinchBlend = 1f;

        // Phase 2 — hold at peak so the tear is clearly visible (20% of duration)
        t = 0f;
        float holdDur = splitPinchDuration * 0.20f;
        while (t < holdDur)
        {
            t += Time.deltaTime;
            yield return null;
        }

        // Sample the actual visual half-positions at peak animation for seamless spawn
        mainPlayer.GetHalfState(
            out Vector2 leftCenter,  out Vector2 rightCenter,
            out Vector2 leftVel,     out Vector2 rightVel);

        // Particles burst sideways from the seam before the droplets spawn
        WaterParticleEffect.PlaySplit(
            mainPlayer.Center,
            mainPlayer.bodyOuterColor,
            mainPlayer.sortingLayerName,
            mainPlayer.sortingOrder + 1);

        mainPlayer.SplitPinchBlend = 0f;
        mainPlayer.Freeze();
        mainPlayer.SetVisible(false);

        // Spawn both half-size droplets at their exact visual half-positions
        _droplets[0] = SpawnDroplet(leftCenter,  leftVel  + new Vector2(-splitBurstX, splitBurstY));
        _droplets[1] = SpawnDroplet(rightCenter, rightVel + new Vector2( splitBurstX, splitBurstY));

        _isSplit        = true;
        _activeIdx      = 0;
        _mergeCooldown  = 0.75f;
        _splitCoroutine = null;
        SetActiveDroplet(0);

        // Birth pop then tension wiggle — each droplet bursts out then jiggles
        StartCoroutine(DriveSpawnPop(_droplets[0], 0.28f));
        StartCoroutine(DriveSpawnPop(_droplets[1], 0.28f));
        StartCoroutine(DriveWiggle(_droplets[0], 0.50f));
        StartCoroutine(DriveWiggle(_droplets[1], 0.50f));

        EventManager.PlayerSplit();
    }

    private SoftBodyPlayer SpawnDroplet(Vector2 center, Vector2 initialVelocity)
    {
        // SetActive(false) before AddComponent so Awake doesn't fire until fields are set.
        // MeshFilter and MeshRenderer must be added manually — [RequireComponent] is editor-only
        // and does not auto-add dependencies when using AddComponent at runtime.
        var go = new GameObject("SplitDroplet");
        go.SetActive(false);
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();

        var sp = go.AddComponent<SoftBodyPlayer>();

        sp.pointCount          = mainPlayer.pointCount;
        sp.bodyRadius          = mainPlayer.bodyRadius / Mathf.Sqrt(2f);
        sp.domeHeightScale     = mainPlayer.domeHeightScale;
        sp.pointMass           = mainPlayer.pointMass * 0.5f;
        sp.pointRadius         = mainPlayer.pointRadius;
        sp.softBodyPointLayer  = mainPlayer.softBodyPointLayer;
        sp.springFrequency     = mainPlayer.springFrequency;
        sp.springDamping       = mainPlayer.springDamping;
        sp.restoreForce        = mainPlayer.restoreForce  * 0.5f;  // scale with halved pointMass
        sp.pressureForce       = mainPlayer.pressureForce * 0.5f;  // scale with halved pointMass
        sp.moveForce           = mainPlayer.moveForce * 0.8f;
        sp.maxMoveSpeed        = mainPlayer.maxMoveSpeed;
        sp.moveDrag            = mainPlayer.moveDrag * 0.5f;       // scale with halved pointMass
        sp.airControlFraction  = mainPlayer.airControlFraction;
        sp.jumpForce           = mainPlayer.jumpForce * 0.85f;
        sp.baseGravityScale    = mainPlayer.baseGravityScale;
        sp.maxGravityScale     = mainPlayer.maxGravityScale;
        sp.gravityRampRate     = mainPlayer.gravityRampRate;
        sp.groundLayer         = mainPlayer.groundLayer;
        sp.groundCheckFraction = mainPlayer.groundCheckFraction;
        sp.levelBounds         = mainPlayer.levelBounds;
        sp.boundaryForce       = mainPlayer.boundaryForce * 0.5f;  // scale with halved pointMass

        // Anim params
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

        // Rendering
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

        // Face — half scale to match the smaller body
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
        go.SetActive(true);  // Awake fires here with all fields correctly set

        foreach (var rb in sp.Points)
            rb.linearVelocity = initialVelocity;

        return sp;
    }

    // ── Merge ─────────────────────────────────────────────────────────────

    private IEnumerator MergeCoroutine()
    {
        _isSplit = false;

        _droplets[0].InputEnabled = false;
        _droplets[1].InputEnabled = false;
        _droplets[_activeIdx].OnGroundPoundLand -= OnActiveGroundPound;

        // Capture merge midpoint for particles (before any position changes)
        Vector2 particlePos = Vector2.zero;
        int pCount = 0;
        for (int i = 0; i < 2; i++)
            if (_droplets[i] != null) { particlePos += _droplets[i].Center; pCount++; }
        if (pCount > 0) particlePos /= pCount;

        // Phase 1 — brief inward pre-squeeze: each droplet compresses as if inhaling
        float squeezeDur = mergePopDuration * 0.25f;
        float t = 0f;
        while (t < squeezeDur)
        {
            t += Time.deltaTime;
            float sq = Mathf.Sin((t / squeezeDur) * Mathf.PI * 0.5f); // 0 → 1
            if (_droplets[0] != null) _droplets[0].SplitPinchBlend = sq * 0.4f;
            if (_droplets[1] != null) _droplets[1].SplitPinchBlend = sq * 0.4f;
            yield return null;
        }

        // Reset squeeze and fire particles + pop simultaneously
        if (_droplets[0] != null) _droplets[0].SplitPinchBlend = 0f;
        if (_droplets[1] != null) _droplets[1].SplitPinchBlend = 0f;

        WaterParticleEffect.PlayMerge(
            particlePos,
            mainPlayer.bodyOuterColor,
            mainPlayer.sortingLayerName,
            mainPlayer.sortingOrder + 1);

        // Phase 2 — radial pop on both droplets
        t = 0f;
        while (t < mergePopDuration)
        {
            t += Time.deltaTime;
            float pop = Mathf.Sin((t / mergePopDuration) * Mathf.PI);
            if (_droplets[0] != null) _droplets[0].MergePopBlend = pop;
            if (_droplets[1] != null) _droplets[1].MergePopBlend = pop;
            yield return null;
        }

        if (_droplets[0] != null) _droplets[0].MergePopBlend = 0f;
        if (_droplets[1] != null) _droplets[1].MergePopBlend = 0f;

        // Merge position = passive droplet's center (it stayed put; active moved to it)
        int passiveForMerge = 1 - _activeIdx;
        Vector2 mergePos    = _droplets[passiveForMerge] != null
                              ? _droplets[passiveForMerge].Center
                              : (_droplets[0] != null ? _droplets[0].Center : _droplets[1].Center);
        Vector2 combinedVel = Vector2.zero;
        int     velCount    = 0;
        for (int i = 0; i < 2; i++)
        {
            if (_droplets[i] == null) continue;
            combinedVel += AverageVelocity(_droplets[i]);
            velCount++;
        }
        if (velCount > 0) combinedVel /= velCount;

        DestroyDroplets();

        // Unfreeze FIRST so rb.position writes are respected, then teleport the ring
        // to the merge location in its natural resting shape and show it.
        mainPlayer.Unfreeze();
        mainPlayer.TeleportTo(mergePos, combinedVel);
        mainPlayer.SetVisible(true);

        // Elastic arrival pop on the reformed main player
        StartCoroutine(DriveMergeArrivalPop(mainPlayer, 0.38f));
        StartCoroutine(DriveWiggle(mainPlayer, 0.55f));

        _mergeCoroutine = null;
        EventManager.PlayerMerge();
    }

    // ── Pressure transfer ─────────────────────────────────────────────────

    private void OnActiveGroundPound(float impactVel)
    {
        if (!_isSplit) return;

        int passiveIdx = 1 - _activeIdx;
        if (_droplets[passiveIdx] == null) return;

        float launchVY = Mathf.Max(impactVel * pressureTransferScale, pressureLaunchMinY);
        foreach (var rb in _droplets[passiveIdx].Points)
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, launchVY);

        StartCoroutine(DrivePressureSquash(_droplets[_activeIdx], pressureSquashDuration));
    }

    private IEnumerator DrivePressureSquash(SoftBodyPlayer droplet, float duration)
    {
        float t = 0f;
        while (t < duration && droplet != null)
        {
            t                        += Time.deltaTime;
            droplet.PressureSquashBlend = 1f - (t / duration);
            yield return null;
        }
        if (droplet != null)
            droplet.PressureSquashBlend = 0f;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private void SetActiveDroplet(int index)
    {
        if (_droplets[_activeIdx] != null)
            _droplets[_activeIdx].OnGroundPoundLand -= OnActiveGroundPound;

        _activeIdx = index;
        for (int i = 0; i < 2; i++)
        {
            if (_droplets[i] == null) continue;
            _droplets[i].InputEnabled = (i == _activeIdx);
            _droplets[i].SetFaceVisible(i == _activeIdx);
        }

        if (_droplets[_activeIdx] != null)
        {
            _droplets[_activeIdx].OnGroundPoundLand += OnActiveGroundPound;

            // Squeeze-in feedback on newly active droplet
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

    private Vector2 AverageVelocity(SoftBodyPlayer sp)
    {
        if (sp == null || sp.Points == null) return Vector2.zero;
        Vector2 sum = Vector2.zero;
        foreach (var rb in sp.Points) sum += rb.linearVelocity;
        return sum / sp.Points.Length;
    }

    // Radial burst 0→1→0 reusing MergePopBlend — used for birth pop on split
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

    // Elastic overshoot pop on main player after merge — peaks above 1.0 then settles
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
