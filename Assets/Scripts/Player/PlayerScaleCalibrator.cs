using Cinemachine;
using UnityEngine;

/*
 * OVERVIEW
 *   Reads the Cinemachine Virtual Camera's orthographic size, computes a ratio against
 *   a baseline size, and scales all world-space values on SoftBodyPlayer and
 *   PlayerSplitController proportionally. This lets you freely adjust camera zoom
 *   without manually re-tuning every player value.
 *
 * SETUP
 *   1. Add this component to the same GameObject as SoftBodyPlayer.
 *   2. Assign the Cinemachine Virtual Camera in the Inspector.
 *   3. Set Baseline Ortho Size to the ortho size at which your Inspector values are correct (default 10).
 *   4. Enter Play mode — all values scale automatically.
 *
 * HOW IT WORKS
 *   Captures the raw Inspector values in Awake (before SoftBodyPlayer.Awake via
 *   [DefaultExecutionOrder]). On Start, reads the camera ortho size and applies
 *   ratio = orthoSize / baselineOrthoSize to every distance/force/velocity value.
 *   Dimensionless values (gravity scale, spring damping, fractions, durations) are left unchanged.
 *
 * TUNING
 *   All player values in the SoftBodyPlayer Inspector should be set as if the camera
 *   is at Baseline Ortho Size. The calibrator handles the rest at runtime.
 */
[DefaultExecutionOrder(-100)] // must run before SoftBodyPlayer (order 0) so Awake fires first
[RequireComponent(typeof(SoftBodyPlayer))]
public class PlayerScaleCalibrator : MonoBehaviour
{
    [Tooltip("The Cinemachine Virtual Camera whose orthographic size drives the scale ratio.")]
    public CinemachineVirtualCamera virtualCamera;
    [Tooltip("The orthographic size at which all SoftBodyPlayer Inspector values are correctly tuned.")]
    public float baselineOrthoSize = 10f;

    // ── Baseline values captured before SoftBodyPlayer.Awake ────────────────

    // SoftBodyPlayer
    private float   _bodyRadius;
    private float   _pointRadius;
    private float   _faceScale;
    private Vector2 _faceOffset;
    private float   _moveForce;
    private float   _maxMoveSpeed;
    private float   _moveDrag;
    private float   _jumpForce;
    private float   _restoreForce;
    private float   _pressureForce;
    private float   _boundaryForce;
    private float   _groundPoundDownForce;
    private float   _riseVelocityFull;
    private float   _fallVelocityFull;
    private float   _idleBobAmplitude;
    private float   _moveLeanAmount;
    private float   _moveBobAmplitude;
    private float   _riseStretchAmount;
    private float   _riseSqueezeAmount;
    private float   _fallSpreadAmount;
    private float   _fallFlattenAmount;
    private float   _landingSquashSpread;
    private float   _landingSquashFlatten;
    private float   _splitPinchInward;
    private float   _splitPinchVertical;
    private float   _splitSeparateAmount;
    private float   _mergePopOutward;
    private float   _wiggleAmplitude;
    private float   _activateSquashInward;
    private float   _activateSquashVertical;

    // PlayerSplitController (optional)
    private PlayerSplitController _splitController;
    private float _splitBurstX;
    private float _splitBurstY;
    private float _mergeProximityRadius;

    private SoftBodyPlayer _sp;

    private void Awake()
    {
        _sp = GetComponent<SoftBodyPlayer>();
        _splitController = GetComponent<PlayerSplitController>();
        CaptureBaseline();
        // Apply immediately in Awake so SoftBodyPlayer.Awake (which runs next)
        // reads the already-scaled bodyRadius and pointRadius when spawning ring points.
        ApplyScale();
    }

    private void Start()
    {
        // Re-apply in Start in case the virtual camera wasn't ready in Awake
        ApplyScale();
    }

    private void CaptureBaseline()
    {
        _bodyRadius           = _sp.bodyRadius;
        _pointRadius          = _sp.pointRadius;
        _faceScale            = _sp.faceScale;
        _faceOffset           = _sp.faceOffset;
        _moveForce            = _sp.moveForce;
        _maxMoveSpeed         = _sp.maxMoveSpeed;
        _moveDrag             = _sp.moveDrag;
        _jumpForce            = _sp.jumpForce;
        _restoreForce         = _sp.restoreForce;
        _pressureForce        = _sp.pressureForce;
        _boundaryForce        = _sp.boundaryForce;
        _groundPoundDownForce = _sp.groundPoundDownForce;
        _riseVelocityFull     = _sp.riseVelocityFull;
        _fallVelocityFull     = _sp.fallVelocityFull;
        _idleBobAmplitude     = _sp.idleBobAmplitude;
        _moveLeanAmount       = _sp.moveLeanAmount;
        _moveBobAmplitude     = _sp.moveBobAmplitude;
        _riseStretchAmount    = _sp.riseStretchAmount;
        _riseSqueezeAmount    = _sp.riseSqueezeAmount;
        _fallSpreadAmount     = _sp.fallSpreadAmount;
        _fallFlattenAmount    = _sp.fallFlattenAmount;
        _landingSquashSpread  = _sp.landingSquashSpread;
        _landingSquashFlatten = _sp.landingSquashFlatten;
        _splitPinchInward     = _sp.splitPinchInward;
        _splitPinchVertical   = _sp.splitPinchVertical;
        _splitSeparateAmount  = _sp.splitSeparateAmount;
        _mergePopOutward      = _sp.mergePopOutward;
        _wiggleAmplitude      = _sp.wiggleAmplitude;
        _activateSquashInward  = _sp.activateSquashInward;
        _activateSquashVertical = _sp.activateSquashVertical;

        if (_splitController != null)
        {
            _splitBurstX          = _splitController.splitBurstX;
            _splitBurstY          = _splitController.splitBurstY;
            _mergeProximityRadius = _splitController.mergeProximityRadius;
        }
    }

    private void ApplyScale()
    {
        float ortho = virtualCamera != null
            ? virtualCamera.m_Lens.OrthographicSize
            : baselineOrthoSize;

        float r = ortho / baselineOrthoSize;

        // ── Distances and radii ──────────────────────────────────────────────
        _sp.bodyRadius  = _bodyRadius  * r;
        _sp.pointRadius = _pointRadius * r;

        // ── Face ────────────────────────────────────────────────────────────
        _sp.faceScale  = _faceScale  * r;
        _sp.faceOffset = _faceOffset * r;

        // ── Movement forces / speeds ─────────────────────────────────────────
        _sp.moveForce    = _moveForce    * r;
        _sp.maxMoveSpeed = _maxMoveSpeed * r;
        _sp.moveDrag     = _moveDrag     * r;
        _sp.jumpForce    = _jumpForce    * r;

        // ── Spring / restore forces ──────────────────────────────────────────
        _sp.restoreForce = _restoreForce * r;
        _sp.pressureForce = _pressureForce * r;
        _sp.boundaryForce = _boundaryForce * r;

        // ── Ground pound ─────────────────────────────────────────────────────
        _sp.groundPoundDownForce = _groundPoundDownForce * r;

        // ── Velocity thresholds ──────────────────────────────────────────────
        _sp.riseVelocityFull = _riseVelocityFull * r;
        _sp.fallVelocityFull = _fallVelocityFull * r;

        // ── Animation amplitudes (world-space deformation offsets) ───────────
        _sp.idleBobAmplitude     = _idleBobAmplitude     * r;
        _sp.moveLeanAmount       = _moveLeanAmount       * r;
        _sp.moveBobAmplitude     = _moveBobAmplitude     * r;
        _sp.riseStretchAmount    = _riseStretchAmount    * r;
        _sp.riseSqueezeAmount    = _riseSqueezeAmount    * r;
        _sp.fallSpreadAmount     = _fallSpreadAmount     * r;
        _sp.fallFlattenAmount    = _fallFlattenAmount    * r;
        _sp.landingSquashSpread  = _landingSquashSpread  * r;
        _sp.landingSquashFlatten = _landingSquashFlatten * r;
        _sp.splitPinchInward     = _splitPinchInward     * r;
        _sp.splitPinchVertical   = _splitPinchVertical   * r;
        _sp.splitSeparateAmount  = _splitSeparateAmount  * r;
        _sp.mergePopOutward      = _mergePopOutward      * r;
        _sp.wiggleAmplitude      = _wiggleAmplitude      * r;
        _sp.activateSquashInward  = _activateSquashInward  * r;
        _sp.activateSquashVertical = _activateSquashVertical * r;

        // ── PlayerSplitController ────────────────────────────────────────────
        if (_splitController != null)
        {
            _splitController.splitBurstX         = _splitBurstX         * r;
            _splitController.splitBurstY         = _splitBurstY         * r;
            _splitController.mergeProximityRadius = _mergeProximityRadius * r;
        }
    }
}
