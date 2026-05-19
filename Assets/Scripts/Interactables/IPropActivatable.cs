// Implemented by any prop that can be activated or deactivated by a trigger.
// Called by PropTilemapSpawner at spawn time to pass the per-cell activation config.
public interface IPropActivatable
{
    void SetActivationConfig(ConnectionMode mode, bool initialActive);
}
