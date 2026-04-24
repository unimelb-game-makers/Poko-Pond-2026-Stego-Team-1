using UnityEngine;

/*
 *  Sits between VC_SideScroll and the player/droplets.
 *  During normal merged play the proxy snaps directly to the player position
 *  every frame — Cinemachine's own dead zone and soft zone handle smoothness
 *  exactly as they did before this script existed.
 *  After a Tab droplet-switch the proxy SmoothDamps to the new target so the
 *  camera pans across gracefully, then resumes snap-tracking once settled.
 *
 *  Setup:
 *    1. Create an empty GO at SCENE ROOT (NOT a child of VC_SideScroll)
 *    2. Add this component to it
 *    3. Drag it into VC_SideScroll's Follow field in the Inspector
 *    4. Drag it into PlayerSplitController's Camera Proxy field
 */
public class CameraFollowProxy : MonoBehaviour
{
    [Tooltip("Pan duration when switching active droplet with Tab (seconds).")]
    public float switchSmoothTime = 0.35f;
    [Tooltip("Distance threshold at which a tab-switch pan is considered complete.")]
    public float switchSettleDistance = 0.05f;

    private Vector2 _targetPos;
    private Vector2 _velocity;
    private bool    _initialized;
    private bool    _isSwitching;

    // Called every LateUpdate by PlayerSplitController
    public void UpdateTarget(Vector2 position)
    {
        if (!_initialized)
        {
            _initialized = true;
            _isSwitching = false;
            _targetPos   = position;
            _velocity    = Vector2.zero;
            transform.position = new Vector3(position.x, position.y, transform.position.z);
            return;
        }
        _targetPos = position;
    }

    // Called by SetActiveDroplet on Tab press
    public void SwitchTarget(Vector2 newTargetPos)
    {
        _targetPos   = newTargetPos;
        _isSwitching = true;
        // Don't reset _velocity — carry through for a continuous-feeling pan
    }

    private void LateUpdate()
    {
        if (!_initialized) return;

        Vector2 current = transform.position;

        if (!IsValid(current) || !IsValid(_targetPos))
        {
            _velocity    = Vector2.zero;
            _isSwitching = false;
            if (IsValid(_targetPos))
                transform.position = new Vector3(_targetPos.x, _targetPos.y, transform.position.z);
            return;
        }

        Vector2 next;

        if (_isSwitching)
        {
            next = Vector2.SmoothDamp(current, _targetPos, ref _velocity, switchSmoothTime);
            if (!IsValid(next)) { _velocity = Vector2.zero; next = _targetPos; }

            // Once close enough, stop the pan and resume snap-tracking
            if (Vector2.Distance(next, _targetPos) < switchSettleDistance)
            {
                _isSwitching = false;
                _velocity    = Vector2.zero;
                next         = _targetPos;
            }
        }
        else
        {
            // Snap directly — Cinemachine's soft zone handles the camera smoothness
            next      = _targetPos;
            _velocity = Vector2.zero;
        }

        transform.position = new Vector3(next.x, next.y, transform.position.z);
    }

    private static bool IsValid(Vector2 v) =>
        !float.IsNaN(v.x) && !float.IsNaN(v.y) &&
        !float.IsInfinity(v.x) && !float.IsInfinity(v.y);
}
