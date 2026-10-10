using System.Collections.Generic;
using UnityEngine;

/*
 * OVERVIEW
 *   Hydroelectric water battery. Sucks in a liquid droplet, holds it, and powers any
 *   prop linked by connection id while it is running. The player presses left or right
 *   to be launched back out. See documentation/technical/water-battery.md.
 *
 * VARIANTS
 *   Small — takes half the player. A whole droplet is split automatically: one half
 *           stays inside, the other pops back out the side it came from and gets control.
 *           Stays idle (no suction) until a SplittingMachine has unlocked splitting.
 *   Big   — needs both halves. A whole droplet counts as both. Two halves that arrive
 *           separately are merged inside and leave together as one whole droplet.
 *
 * STATES
 *   Idle → Sucking → Holding (big battery with one half) → Running → Cooldown → Idle
 *   Only Liquid bodies are pulled in. After an eject the battery ignores everything for
 *   Cooldown Duration seconds so the launched droplet is not immediately re-captured.
 *
 * SIGNALLING
 *   Fires the same EventManager pressure-plate events as PressurePlate, using Battery Id,
 *   so doors, conveyors, crushers and grates react without changes.
 *     Hold     — Activated when it starts running, Deactivated when it stops.
 *     One Shot — Activated on the first run and never deactivated.
 *
 * PLACEMENT — TILEMAP SYSTEM
 *   Paint WaterBattery_PropTile / BigWaterBattery_PropTile on the Props tilemap.
 *   PropTilemapSpawner passes Connection ID (→ Battery Id), One Shot, and an optional
 *   per-cell launch speed/angle override.
 */
public class WaterBattery : MonoBehaviour, IPropConnectable, IPropOneShotConfigurable, IPropBatteryLaunchConfigurable
{
    public enum BatterySize { Small, Big }

    private enum BatteryState { Idle, Sucking, Holding, Running, Cooldown }

    [Header("Connection")]
    [Tooltip("Id fired through EventManager's pressure-plate events. Overridden by the tilemap cell's Connection ID.")]
    [SerializeField] private string batteryId;

    [Tooltip("Stay powered after the first run instead of switching off when emptied.")]
    [SerializeField] private bool oneShot;

    [Header("Variant")]
    [SerializeField] private BatterySize size = BatterySize.Small;

    [Header("Suction")]
    [Tooltip("Centre of the suction zone, local to the battery's bottom-centre pivot.")]
    [SerializeField] private Vector2 suctionOffset = new Vector2(0f, 0.9f);
    [Tooltip("Size of the suction zone (local units, scaled with the battery).")]
    [SerializeField] private Vector2 suctionSize = new Vector2(3.2f, 2.2f);
    [Tooltip("Point the droplet is pulled toward and parked at (the funnel throat).")]
    [SerializeField] private Vector2 intakeOffset = new Vector2(0f, 1.35f);
    [Tooltip("The droplet is captured once its centre is this close to the intake.")]
    [SerializeField, Min(0.05f)] private float captureRadius = 0.35f;
    [Tooltip("Capture anyway after this long, in case geometry stops the droplet reaching the intake.")]
    [SerializeField, Min(0.1f)] private float maxSuctionTime = 1.25f;

    [Header("Launch")]
    [Tooltip("Speed the droplet leaves the battery at.")]
    [SerializeField, Min(0f)] private float launchSpeed = 9f;
    [Tooltip("Upward angle of the launch in degrees. 0 = flat; mirrored for left/right.")]
    [SerializeField, Range(0f, 85f)] private float launchAngle = 30f;
    [Tooltip("Where the droplet reappears on exit, relative to the intake. X is mirrored for left exits.")]
    [SerializeField] private Vector2 exitOffset = new Vector2(1.1f, 0f);
    [Tooltip("Seconds after an eject before the battery can suck again.")]
    [SerializeField, Min(0f)] private float cooldownDuration = 1f;

    [Header("Visuals")]
    [SerializeField] private SpriteRenderer fanRenderer;
    [Tooltip("Red light, funnels spinning. Looped while idle, sucking and cooling down.")]
    [SerializeField] private Sprite[] animationFramesIdle;
    [Tooltip("Droplet entering the tank, light turning green. Played once on capture.")]
    [SerializeField] private Sprite[] animationFramesProcess;
    [Tooltip("First frame of Process that the running (green light) loop returns to.")]
    [SerializeField, Min(0)] private int runningLoopStart = 7;
    [Tooltip("Frame of Process shown while a big battery holds only one half (red light, water in tank).")]
    [SerializeField, Min(0)] private int holdingFrame = 5;
    [Tooltip("Played once when the droplet is launched out.")]
    [SerializeField] private Sprite[] animationFramesEject;
    [SerializeField, Min(0.1f)] private float animationFramesPerSecond = 8f;
    [Tooltip("Idle loop speed multiplier while actively sucking.")]
    [SerializeField, Min(1f)] private float suctionAnimationSpeedup = 2f;

    // A whole droplet is two units of water, each half is one.
    private int Capacity => size == BatterySize.Big ? 2 : 1;

    private class Suction
    {
        public SoftBodyPlayer body;
        public float          startTime;
        public float          entrySide; // -1 came from the left, +1 from the right
    }

    private class Occupant
    {
        public SoftBodyPlayer body;
        public bool           ejectArmed; // horizontal input must be released once before ejecting
    }

    private readonly List<Suction>  _sucking   = new();
    private readonly List<Occupant> _occupants = new();
    private readonly HashSet<SoftBodyPlayer> _seen = new();
    private readonly Collider2D[] _hits = new Collider2D[64]; // room for both halves' ring points

    private PlayerSplitController _splitController;
    private int   _playerMask;
    private float _cooldownTimer;
    private Vector2 _lastIntake;
    private bool  _powered;
    private BatteryState _state = BatteryState.Idle;

    // Animation playback
    private Sprite[] _clip;
    private int      _clipFrame;
    private float    _clipTimer;
    private int      _loopStart;   // -1 = play once then hold the last frame
    private float    _clipSpeed = 1f;

    public bool IsPowered => _powered;
    public bool IsRunning => _state == BatteryState.Running;
    public BatterySize Size => size;

    // ── Tilemap configuration ────────────────────────────────────────────

    public void SetConnectionId(string id) => batteryId = id;
    public void SetOneShot(bool value) => oneShot = value;

    public void SetLaunchConfig(float speed, float angleDegrees)
    {
        launchSpeed = Mathf.Max(0f, speed);
        launchAngle = Mathf.Clamp(angleDegrees, 0f, 85f);
    }

    // ── Lifecycle ────────────────────────────────────────────────────────

    private void Reset() => batteryId = System.Guid.NewGuid().ToString();

    private void Start()
    {
        _playerMask      = LayerMask.GetMask("Player", "SoftBodyPoint");
        _splitController = FindFirstObjectByType<PlayerSplitController>();
        if (fanRenderer == null) fanRenderer = GetComponent<SpriteRenderer>();
        PlayIdle();
    }

    private void Update()
    {
        HandleEjectInput();
        TickAnimation();
    }

    private void FixedUpdate()
    {
        if (_cooldownTimer > 0f) _cooldownTimer -= Time.fixedDeltaTime;

        PruneDestroyed();
        FollowIntake();
        DetectNewBodies();
        UpdateSuction();
        RefreshState();
    }

    // A battery riding a moving platform drags its parked occupants along.
    private void FollowIntake()
    {
        Vector2 intake = IntakePoint;
        if (Vector2.SqrMagnitude(intake - _lastIntake) < 0.000001f) return;
        _lastIntake = intake;
        foreach (var o in _occupants) o.body.MoveParked(intake);
    }

    // ── Detection & suction ──────────────────────────────────────────────

    private void DetectNewBodies()
    {
        if (_cooldownTimer > 0f) return;

        int count = Physics2D.OverlapBoxNonAlloc(SuctionCenter, SuctionSize, 0f, _hits, _playerMask);
        _seen.Clear();
        for (int i = 0; i < count; i++)
        {
            if (!TryResolveOwner(_hits[i], out SoftBodyPlayer body) || !_seen.Add(body)) continue;
            if (CanAccept(body))
                StartSuction(body);
        }
    }

    private bool CanAccept(SoftBodyPlayer body)
    {
        if (body.IsParked || IsTracked(body)) return false;
        if (body.getBodyState() != PlayerBodyState.Liquid) return false;

        int units = UnitsOf(body);
        if (units == 2 && size == BatterySize.Small)
        {
            // A whole droplet in a small battery has to be split, which needs the
            // SplittingMachine to have unlocked splitting first.
            if (_splitController == null || !_splitController.SplittingUnlocked) return false;
            units = 1;
        }

        return FilledUnits() + SuckingUnits() + units <= Capacity;
    }

    private void StartSuction(SoftBodyPlayer body)
    {
        _sucking.Add(new Suction
        {
            body      = body,
            startTime = Time.time,
            entrySide = body.Center.x < IntakePoint.x ? -1f : 1f,
        });
        body.applyVaccum(true, IntakePoint);
    }

    private void UpdateSuction()
    {
        for (int i = _sucking.Count - 1; i >= 0; i--)
        {
            Suction s = _sucking[i];

            // Cancel if the body changed state or was replaced by a split mid-suction.
            bool stale = s.body.getBodyState() != PlayerBodyState.Liquid
                      || (UnitsOf(s.body) == 2 && _splitController != null && _splitController.IsSplit);
            if (stale)
            {
                s.body.applyVaccum(false, Vector2.zero);
                _sucking.RemoveAt(i);
                continue;
            }

            s.body.applyVaccum(true, IntakePoint); // the intake moves if the battery does
            bool reached = Vector2.Distance(s.body.Center, IntakePoint) <= captureRadius;
            if (reached || Time.time - s.startTime >= maxSuctionTime)
            {
                _sucking.RemoveAt(i);
                Capture(s);
            }
        }
    }

    // ── Capture ──────────────────────────────────────────────────────────

    private void Capture(Suction s)
    {
        SoftBodyPlayer body = s.body;
        body.applyVaccum(false, Vector2.zero);

        if (UnitsOf(body) == 2 && size == BatterySize.Small)
        {
            // Keep one half inside; the other pops back out of the entry side with control.
            if (_splitController == null || !_splitController.SplitForBattery(
                    IntakePoint, ExitPoint(s.entrySide), LaunchVelocity(s.entrySide),
                    out SoftBodyPlayer kept, out _))
                return;

            Park(kept);
            return;
        }

        Park(body);

        // Big battery: both halves inside → merge them into one whole body.
        if (size == BatterySize.Big && _occupants.Count == 2 && _splitController != null)
        {
            SoftBodyPlayer merged = _splitController.MergeForBattery(IntakePoint);
            if (merged != null)
            {
                _occupants.Clear();
                Park(merged);
            }
        }
    }

    private void Park(SoftBodyPlayer body)
    {
        body.Park(IntakePoint);
        _occupants.Add(new Occupant { body = body, ejectArmed = false });
    }

    // ── Eject ────────────────────────────────────────────────────────────

    private void HandleEjectInput()
    {
        for (int i = _occupants.Count - 1; i >= 0; i--)
        {
            Occupant o = _occupants[i];
            if (o.body == null || !o.body.InputEnabled) continue;

            float h = Input.GetAxisRaw("Horizontal");
            if (Mathf.Abs(h) < 0.1f)
            {
                o.ejectArmed = true;
            }
            else if (o.ejectArmed)
            {
                Eject(o, Mathf.Sign(h));
                return; // one eject per frame keeps the occupant list stable
            }
        }
    }

    // Launches a parked body out toward direction (sign only). Player input goes
    // through here; scripts can also use it to force an eject.
    public bool TryEject(SoftBodyPlayer body, float direction)
    {
        Occupant o = _occupants.Find(x => x.body == body);
        if (o == null || Mathf.Approximately(direction, 0f)) return false;
        Eject(o, Mathf.Sign(direction));
        return true;
    }

    private void Eject(Occupant o, float direction)
    {
        _occupants.Remove(o);
        o.body.ReleaseFromPark(ExitPoint(direction), LaunchVelocity(direction));
        _cooldownTimer = cooldownDuration;
        PlayClip(animationFramesEject, -1);
    }

    // ── State & power ────────────────────────────────────────────────────

    private void RefreshState()
    {
        BatteryState next;
        if (FilledUnits() >= Capacity)       next = BatteryState.Running;
        else if (_occupants.Count > 0)       next = BatteryState.Holding;
        else if (_sucking.Count > 0)         next = BatteryState.Sucking;
        else if (_cooldownTimer > 0f)        next = BatteryState.Cooldown;
        else                                 next = BatteryState.Idle;

        if (next != _state)
            EnterState(next);

        bool shouldPower = _state == BatteryState.Running;
        if (shouldPower && !_powered)
        {
            _powered = true;
            if (!string.IsNullOrEmpty(batteryId))
                EventManager.PressurePlateActivated(batteryId);
        }
        else if (!shouldPower && _powered && !oneShot)
        {
            _powered = false;
            if (!string.IsNullOrEmpty(batteryId))
                EventManager.PressurePlateDeactivated(batteryId);
        }
    }

    private void EnterState(BatteryState next)
    {
        BatteryState previous = _state;
        _state = next;

        switch (next)
        {
            case BatteryState.Running:
                // Fill animation, then loop the green-light frames.
                PlayClip(animationFramesProcess, runningLoopStart);
                break;
            case BatteryState.Holding:
                ShowFrame(animationFramesProcess, holdingFrame);
                break;
            case BatteryState.Sucking:
                PlayIdle();
                _clipSpeed = suctionAnimationSpeedup;
                break;
            case BatteryState.Idle:
            case BatteryState.Cooldown:
                // Let a running eject animation finish; TickAnimation returns to idle.
                if (_clip != animationFramesEject || previous == BatteryState.Sucking)
                    PlayIdle();
                break;
        }
    }

    // ── Animation ────────────────────────────────────────────────────────

    private void PlayIdle()
    {
        PlayClip(animationFramesIdle, 0);
    }

    private void PlayClip(Sprite[] clip, int loopStart)
    {
        _clip      = clip;
        _clipFrame = 0;
        _clipTimer = 0f;
        _loopStart = loopStart;
        _clipSpeed = 1f;
        ApplyFrame();
    }

    private void ShowFrame(Sprite[] clip, int frame)
    {
        _clip      = clip;
        _clipFrame = clip != null && clip.Length > 0 ? Mathf.Clamp(frame, 0, clip.Length - 1) : 0;
        _clipTimer = 0f;
        _loopStart = -2; // static frame
        ApplyFrame();
    }

    private void TickAnimation()
    {
        if (_clip == null || _clip.Length == 0 || _loopStart == -2) return;

        _clipTimer += Time.deltaTime * animationFramesPerSecond * _clipSpeed;
        if (_clipTimer < 1f) return;

        int steps = Mathf.FloorToInt(_clipTimer);
        _clipTimer -= steps;

        for (int i = 0; i < steps; i++)
        {
            if (_clipFrame < _clip.Length - 1)
            {
                _clipFrame++;
            }
            else if (_loopStart >= 0)
            {
                _clipFrame = Mathf.Clamp(_loopStart, 0, _clip.Length - 1);
            }
            else if (_clip == animationFramesEject && _state != BatteryState.Running)
            {
                // One-shot eject finished — fall back to the idle loop.
                PlayIdle();
                return;
            }
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (fanRenderer == null || _clip == null || _clip.Length == 0) return;
        Sprite sprite = _clip[Mathf.Clamp(_clipFrame, 0, _clip.Length - 1)];
        if (sprite != null) fanRenderer.sprite = sprite;
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private Vector2 SuctionCenter => transform.TransformPoint(suctionOffset);
    private Vector2 SuctionSize   => Vector2.Scale(suctionSize, Abs(transform.lossyScale));
    private Vector2 IntakePoint   => transform.TransformPoint(intakeOffset);

    private Vector2 ExitPoint(float direction)
    {
        Vector2 scale = Abs(transform.lossyScale);
        return IntakePoint + new Vector2(exitOffset.x * scale.x * direction, exitOffset.y * scale.y);
    }

    private Vector2 LaunchVelocity(float direction)
    {
        float rad = launchAngle * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * direction, Mathf.Sin(rad)) * launchSpeed;
    }

    private static Vector2 Abs(Vector3 v) => new Vector2(Mathf.Abs(v.x), Mathf.Abs(v.y));

    private int UnitsOf(SoftBodyPlayer body)
    {
        return _splitController != null && _splitController.IsHalfDroplet(body) ? 1 : 2;
    }

    private int FilledUnits()
    {
        int units = 0;
        foreach (var o in _occupants) units += UnitsOf(o.body);
        return units;
    }

    private int SuckingUnits()
    {
        int units = 0;
        foreach (var s in _sucking)
            units += size == BatterySize.Small ? 1 : UnitsOf(s.body);
        return units;
    }

    private bool IsTracked(SoftBodyPlayer body)
    {
        foreach (var s in _sucking)   if (s.body == body) return true;
        foreach (var o in _occupants) if (o.body == body) return true;
        return false;
    }

    // Bodies can be destroyed under us (merge elsewhere, death, scene change).
    private void PruneDestroyed()
    {
        _sucking.RemoveAll(s => s.body == null);
        _occupants.RemoveAll(o => o.body == null);
    }

    private static bool TryResolveOwner(Collider2D collider, out SoftBodyPlayer owner)
    {
        // Ring points are not parented to the player, so SoftBodyPointRef is authoritative.
        SoftBodyPointRef pointRef = collider.GetComponent<SoftBodyPointRef>();
        if (pointRef != null && pointRef.owner != null)
        {
            owner = pointRef.owner;
            return true;
        }

        owner = collider.GetComponent<SoftBodyPlayer>();
        if (owner == null)
            owner = collider.GetComponentInParent<SoftBodyPlayer>();
        return owner != null;
    }

    private void OnDrawGizmosSelected()
    {
        // Suction zone
        Gizmos.color = new Color(0.3f, 0.7f, 1f, 0.8f);
        Gizmos.DrawWireCube(SuctionCenter, SuctionSize);

        // Intake and capture radius
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
        Gizmos.DrawWireSphere(IntakePoint, captureRadius);

        // Exit points and a short preview of each launch arc
        Gizmos.color = new Color(1f, 0.3f, 0.9f, 0.9f);
        foreach (float dir in new[] { -1f, 1f })
        {
            Vector2 p = ExitPoint(dir);
            Vector2 v = LaunchVelocity(dir);
            Gizmos.DrawWireSphere(p, 0.12f);
            const float step = 0.05f;
            for (int i = 0; i < 16; i++)
            {
                Vector2 next = p + v * step;
                v += Physics2D.gravity * step;
                Gizmos.DrawLine(p, next);
                p = next;
            }
        }
    }
}
