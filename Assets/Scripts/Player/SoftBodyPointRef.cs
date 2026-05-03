// Attached to each ring-point GameObject by SoftBodyPlayer.Awake.
// Lets external scripts (e.g. Evaporator) trace a hit collider back to its owning SoftBodyPlayer.
public class SoftBodyPointRef : UnityEngine.MonoBehaviour
{
    public SoftBodyPlayer owner;
}
