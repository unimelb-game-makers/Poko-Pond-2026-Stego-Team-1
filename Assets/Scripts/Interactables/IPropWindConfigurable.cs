// Implemented by props that accept per-cell wind settings from PropTilemapSpawner.
public interface IPropWindConfigurable
{
    void SetWindConfig(UnityEngine.Vector2 direction, float strength);
}
