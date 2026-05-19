using UnityEngine;
using UnityEngine.Tilemaps;

// Tile for a tilemap-placed prop. One PropTile cell = one prefab spawned at runtime.
// For multi-cell props (e.g. a 2×2 crusher), use a sprite sized to the full footprint
// and set its pivot to the anchor corner — paint only this one tile, no filler tiles needed.
//
// Create via: Assets → Create → Tiles → Prop Tile
[CreateAssetMenu(fileName = "New PropTile", menuName = "Tiles/Prop Tile")]
public class PropTile : TileBase
{
    [Tooltip("Sprite shown in the Tile Palette and Scene view for this cell.")]
    public Sprite previewSprite;

    [Tooltip("Prefab instantiated at runtime. Attach CrusherTrap, PressurePlate, etc. to this prefab.")]
    public GameObject prefab;

    [Tooltip("World-space offset added to the tile's cell-center position when spawning the prefab. " +
             "Use this to align the prefab visually with its tile footprint.")]
    public Vector3 spawnOffset;

    [Tooltip("Connection ID passed to the spawned prefab via IPropConnectable. " +
             "Set the same string on a linked plate and crusher to connect them — no separate prefabs needed.")]
    public string connectionId;

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData tileData)
    {
        tileData.sprite = previewSprite;
        tileData.color = Color.white;
        tileData.flags = TileFlags.None;
        tileData.colliderType = Tile.ColliderType.None;
    }
}
