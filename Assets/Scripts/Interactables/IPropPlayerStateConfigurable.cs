// Implemented by props that accept an optional player body-state requirement
// from a painted cell in PropTilemapSpawner.
public interface IPropPlayerStateConfigurable
{
    void SetPlayerStateRequirement(bool requireState, PlayerBodyState requiredState);
}
