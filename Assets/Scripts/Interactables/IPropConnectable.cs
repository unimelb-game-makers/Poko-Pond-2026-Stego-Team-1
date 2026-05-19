// Implemented by any prop that receives a runtime connection ID from PropTilemapSpawner.
// The ID links related props (e.g. a pressure plate to its crusher) without needing
// separate prefabs per connection — set the ID on the PropTile asset instead.
public interface IPropConnectable
{
    void SetConnectionId(string id);
}
