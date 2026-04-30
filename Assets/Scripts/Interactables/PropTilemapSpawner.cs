using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// Reads the Tilemap this component sits on and instantiates prefabs for every PropTile cell.
// Runs once at Start — props are spawned as siblings of the tilemap under the Grid.
//
// CONNECTIONS (linking plates to crushers)
//   No duplicate tiles or prefabs needed. Workflow:
//     1. Paint tiles normally using one generic tile per prop type.
//     2. Right-click this component in the Inspector → "Sync Cell List".
//        The list auto-populates with every PropTile cell, its prop type name, and position.
//     3. Type a shared connection ID string on the plate cell and the crusher cell
//        (e.g. both set to "crusher_a"). They will be linked automatically at runtime.
//     4. With this component selected, prop positions are labelled in the Scene view.
//     5. Re-run Sync Cell List any time you add or remove tiles to keep the list current.
//
// SETUP
//   1. Add a Tilemap child to your Grid named "Props" — separate from geometry.
//   2. Add this component to that Tilemap GameObject.
//   3. Paint PropTile tiles via the Tile Palette. Multi-cell props use one oversized sprite
//      on a single tile — no filler tiles needed.
//   4. Right-click this component → Sync Cell List, fill in connection IDs.
//   5. Enter Play mode — prefabs spawn and connections are applied automatically.
[RequireComponent(typeof(Tilemap))]
public class PropTilemapSpawner : MonoBehaviour
{
    [System.Serializable]
    public struct CellOverride
    {
        [HideInInspector] public string propName; // shown via custom label in inspector
        public Vector3Int cell;
        public string connectionId;
    }

    [Tooltip("Per-cell connection IDs. Right-click this component → Sync Cell List after painting to auto-populate.")]
    [SerializeField] private List<CellOverride> cellOverrides = new();

    // Call this from the Inspector context menu after painting tiles.
    // Adds entries for new PropTile cells, removes entries for deleted cells,
    // and preserves any connection IDs you have already typed in.
    [ContextMenu("Sync Cell List")]
    private void SyncCellList()
    {
        var tilemap = GetComponent<Tilemap>();
        tilemap.CompressBounds();

        var existing = new Dictionary<Vector3Int, string>();
        foreach (var entry in cellOverrides)
            existing[entry.cell] = entry.connectionId;

        cellOverrides.Clear();
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.GetTile(cell) is not PropTile propTile) continue;
            cellOverrides.Add(new CellOverride
            {
                propName     = propTile.prefab != null ? propTile.prefab.name : propTile.name,
                cell         = cell,
                connectionId = existing.TryGetValue(cell, out var id) ? id : ""
            });
        }

        cellOverrides.Sort((a, b) => a.cell.y != b.cell.y
            ? b.cell.y.CompareTo(a.cell.y)
            : a.cell.x.CompareTo(b.cell.x));

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void Start()
    {
        var tilemapRenderer = GetComponent<TilemapRenderer>();
        if (tilemapRenderer != null) tilemapRenderer.enabled = false;

        var tilemap = GetComponent<Tilemap>();
        tilemap.CompressBounds();

        var overrideLookup = new Dictionary<Vector3Int, string>();
        foreach (var entry in cellOverrides)
            if (!string.IsNullOrEmpty(entry.connectionId))
                overrideLookup[entry.cell] = entry.connectionId;

        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = tilemap.GetTile(cell);
            if (tile is not PropTile propTile || propTile.prefab == null) continue;

            Vector3 worldPos = tilemap.GetCellCenterWorld(cell) + propTile.spawnOffset;
            var go = Instantiate(propTile.prefab, worldPos, Quaternion.identity, transform.parent);

            var connId = overrideLookup.TryGetValue(cell, out var id) ? id : propTile.connectionId;
            if (!string.IsNullOrEmpty(connId) && go.TryGetComponent(out IPropConnectable connectable))
                connectable.SetConnectionId(connId);
        }
    }

    private void OnDrawGizmosSelected()
    {
        var tilemap = GetComponent<Tilemap>();
        if (tilemap == null) return;

        foreach (var entry in cellOverrides)
        {
            Vector3 world = tilemap.GetCellCenterWorld(entry.cell);
            Vector3 size  = tilemap.cellSize * 0.95f;

            // Colour by connection ID so linked props share a colour
            Gizmos.color = string.IsNullOrEmpty(entry.connectionId)
                ? new Color(1f, 1f, 1f, 0.4f)
                : ConnectionColour(entry.connectionId);

            Gizmos.DrawWireCube(world, size);

#if UNITY_EDITOR
            var label = string.IsNullOrEmpty(entry.connectionId)
                ? entry.propName
                : $"{entry.propName}\n[{entry.connectionId}]";
            UnityEditor.Handles.Label(world + Vector3.up * (size.y * 0.6f), label);
#endif
        }
    }

    // Generates a stable colour from a string so linked props always share the same hue
    private static Color ConnectionColour(string id)
    {
        float hue = (Mathf.Abs(id.GetHashCode()) % 1000) / 1000f;
        return Color.HSVToRGB(hue, 0.8f, 1f);
    }
}
