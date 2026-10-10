// Implemented by water batteries so a painted cell in PropTilemapSpawner can
// override the prefab's launch speed and upward angle.
public interface IPropBatteryLaunchConfigurable
{
    void SetLaunchConfig(float speed, float angleDegrees);
}
