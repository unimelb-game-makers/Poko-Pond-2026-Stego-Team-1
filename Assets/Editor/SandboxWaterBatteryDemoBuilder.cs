using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using static SandboxMechanicsDemoBuilder;

// Additive Sandbox demo for the water battery, built in the starting cave next to
// the player spawn. Replaces the old hand-placed WaterBattery
// instance with painted PropTiles. Existing tiles and connections are preserved.
public static class SandboxWaterBatteryDemoBuilder
{
    public const string RootName = "Sandbox_WaterBatteryDemos";
    public const string SmallId = "sandbox_battery_small";
    public const string BigId = "sandbox_battery_big";
    public const string SmallTilePath = "Assets/Tiles/Factory/Props/WaterBattery_PropTile.asset";
    public const string BigTilePath = "Assets/Tiles/Factory/Props/BigWaterBattery_PropTile.asset";

    // Starting cave: solid floor at y=-7, open up to y=1, nothing moving. Spawn is at
    // x≈-36.5, left of both suction zones; the floor blowers at x=-25/-24 are clear.
    // The hub's WELCOME panel ends at x≈-31.7, so both doors sit to its right.
    public static readonly Vector3Int SmallBattery = new Vector3Int(-32, -6, 0);
    public static readonly Vector3Int SmallDoor = new Vector3Int(-30, -2, 0);
    public static readonly Vector3Int BigBattery = new Vector3Int(-28, -6, 0);
    public static readonly Vector3Int BigDoor = new Vector3Int(-27, -2, 0);

    // Batch entry point: build, then run WaterBatteryDemoProbe in Play Mode.
    // Use -batchmode without -quit or -nographics; the probe exits Unity itself.
    public static void ValidateBatch()
    {
        Require(Application.isBatchMode, "Run the Play Mode probe in batch mode.");
        Build();
        WaterBatteryDemoProbe.Begin();
    }

    [MenuItem("Tools/Poko Pond/Sandbox/Add Water Battery Demos")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
        var maps = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
        var props = maps.Single(m => m.name == "Props");
        var thin = maps.Single(m => m.name == "ThinPlatforms");
        var solid = maps.Single(m => m.name == "SolidPlatforms");

        var before = new Dictionary<Tilemap, Dictionary<Vector3Int, TileBase>>();
        foreach (var map in maps)
        {
            before[map] = new Dictionary<Vector3Int, TileBase>();
            foreach (var cell in map.cellBounds.allPositionsWithin)
                if (map.HasTile(cell)) before[map][cell] = map.GetTile(cell);
        }

        var smallTile = AssetDatabase.LoadAssetAtPath<PropTile>(SmallTilePath);
        var bigTile = AssetDatabase.LoadAssetAtPath<PropTile>(BigTilePath);
        var redDoor = AssetDatabase.LoadAssetAtPath<PropTile>(MechanicAssetBuilder.DoorTileFor(DoorType.Red));

        // Batteries stand on the existing solid floor; doors sit on new thin ledges
        // above the walkway so they show power state without blocking it.
        foreach (var battery in new[] { SmallBattery, BigBattery })
            Require(solid.HasTile(battery + Vector3Int.down), "Battery has no supporting floor: " + battery);

        var root = GameObject.Find(RootName);
        if (root == null)
        {
            foreach (var cell in DemoCells())
                foreach (var map in maps)
                    Require(!map.HasTile(cell), "Battery demo cell is no longer empty: " + cell + " on " + map.name);
            // Scene-placed props (moving platforms etc.) are not on any tilemap,
            // so check their colliders against the demo area as well.
            var area = new Bounds(new Vector3(-29.5f, -3f, 0f), new Vector3(9f, 8f, 1000f));
            foreach (var col in Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (!col.isTrigger && col.GetComponent<Tilemap>() == null && col.GetComponentInParent<WaterBattery>() == null)
                    Require(!col.bounds.Intersects(area), "Scene object intersects battery demo area: " + col.name);
            root = new GameObject(RootName);
        }

        RemoveHandPlacedBatteries();

        TileBase ledge = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Factory/Platforms/ThinTileCenter.asset");
        foreach (var cell in LedgeCells())
            if (!thin.HasTile(cell)) thin.SetTile(cell, ledge);

        var serialized = new SerializedObject(props.GetComponent<PropTilemapSpawner>());
        var overrides = serialized.FindProperty("cellOverrides");
        var originalOverrides = new Dictionary<Vector3Int, string>();
        for (int i = 0; i < overrides.arraySize; i++)
        {
            var entry = overrides.GetArrayElementAtIndex(i);
            originalOverrides[entry.FindPropertyRelative("cell").vector3IntValue] = entry.FindPropertyRelative("connectionId").stringValue;
        }

        Paint(props, overrides, SmallBattery, smallTile, SmallId, ConnectionMode.Hold, true, false);
        Paint(props, overrides, SmallDoor, redDoor, SmallId, ConnectionMode.Hold, false, false);
        Paint(props, overrides, BigBattery, bigTile, BigId, ConnectionMode.Hold, true, false);
        Paint(props, overrides, BigDoor, redDoor, BigId, ConnectionMode.Hold, false, false);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(serialized.targetObject);

        Sign(root, "Small_Hint", new Vector2(-35.3f, 0.15f),
            "SMALL BATTERY ->\nLiquid only - takes HALF\nWhole drop is split for you\n(unlock splitter first)\nLeft/Right: launch out\nPowers red door above",
            new Color(1f, .85f, .4f), .045f);
        Sign(root, "Big_Hint", new Vector2(-23.6f, -0.9f),
            "<- BIG BATTERY\nNeeds BOTH halves\n(or the whole drop)\nHalves merge inside\nLeft/Right: launch out",
            new Color(.95f, .7f, .3f), .045f);

        foreach (var map in new[] { thin, props })
        {
            map.RefreshAllTiles();
            var tileCollider = map.GetComponent<TilemapCollider2D>();
            if (tileCollider != null) tileCollider.ProcessTilemapChanges();
            var composite = map.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.GenerateGeometry();
            EditorUtility.SetDirty(map);
            PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            if (composite != null) PrefabUtility.RecordPrefabInstancePropertyModifications(composite);
        }

        foreach (var map in before)
            foreach (var cell in map.Value)
                Require(map.Key.GetTile(cell.Key) == cell.Value, "Existing tile was changed: " + cell.Key);
        serialized.Update();
        foreach (var old in originalOverrides)
        {
            var entry = FindOverride(serialized.FindProperty("cellOverrides"), old.Key);
            Require(entry != null && entry.FindPropertyRelative("connectionId").stringValue == old.Value,
                "Existing connection was changed: " + old.Key);
        }

        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save Sandbox.");
        Debug.Log("[SandboxBatteryDemos] PASS: added water battery demos; every existing tile and connection ID preserved.");
    }

    private static IEnumerable<Vector3Int> LedgeCells()
    {
        foreach (var door in new[] { SmallDoor, BigDoor })
            for (int dx = -1; dx <= 1; dx++)
                yield return door + new Vector3Int(dx, -1, 0);
    }

    private static IEnumerable<Vector3Int> DemoCells()
    {
        yield return SmallBattery;
        yield return BigBattery;
        foreach (var door in new[] { SmallDoor, BigDoor })
            for (int dy = 0; dy < 3; dy++)
                yield return door + new Vector3Int(0, dy, 0);
        foreach (var cell in LedgeCells())
            yield return cell;
    }

    // The original Sandbox battery was dragged into the scene, bypassing the Props
    // tilemap. The painted demos replace it.
    private static void RemoveHandPlacedBatteries()
    {
        var batteryPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/WaterBattery.prefab");
        foreach (var battery in Object.FindObjectsByType<WaterBattery>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var instanceRoot = PrefabUtility.GetOutermostPrefabInstanceRoot(battery.gameObject);
            if (instanceRoot == null) continue;
            if (PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot) != batteryPrefab) continue;
            Debug.Log("[SandboxBatteryDemos] Removing hand-placed battery at " + instanceRoot.transform.position);
            Object.DestroyImmediate(instanceRoot);
        }
    }
}
