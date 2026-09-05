using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Additive Sandbox gallery. Existing painted mechanics and their overrides are
// preserved; this owns its named signs/bounds, demo props and ascent springs.
public static class SandboxMechanicsDemoBuilder
{
    public const string RootName = "Sandbox_DoorAndSplitterDemos";
    public const string YellowId = "sandbox_demo_yellow";
    public const string RedId = "sandbox_demo_red";
    public static readonly Vector3Int Green = new Vector3Int(24, 16, 0);
    public static readonly Vector3Int Yellow = new Vector3Int(18, 16, 0);
    public static readonly Vector3Int YellowPlate = new Vector3Int(20, 16, 0);
    public static readonly Vector3Int Red = new Vector3Int(12, 16, 0);
    public static readonly Vector3Int RedPlate = new Vector3Int(14, 16, 0);
    public static readonly Vector3Int Splitter = new Vector3Int(28, 16, 0);

    public static void ValidateBatch()
    {
        Require(Application.isBatchMode, "Run the Play Mode probe in an isolated batch project.");
        Build();
        SandboxMechanicsDemoProbe.Begin();
    }

    [MenuItem("Tools/Poko Pond/Sandbox/Add Door and Splitter Demos")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity");
        var maps = UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None);
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
        var root = GameObject.Find(RootName);
        if (root == null)
        {
            // The surveyed upper-right chamber contains no existing mechanics.
            foreach (var map in maps)
                for (int x = 10; x <= 29; x++)
                    for (int y = 16; y <= 21; y++)
                        Require(!map.HasTile(new Vector3Int(x, y, 0)), "Gallery is no longer empty at " + x + "," + y);
            foreach (var col in UnityEngine.Object.FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
                if (col.GetComponent<Tilemap>() == null && !col.isTrigger)
                    Require(!col.bounds.Intersects(new Bounds(new Vector3(20, 19, col.bounds.center.z), new Vector3(20, 6, 1))),
                        "Existing mechanic intersects gallery: " + col.name);
            root = new GameObject(RootName);
        }

        // A one-way gallery floor joins the existing top-right landing. It does
        // not wall off the climb below, and Down remains a way out of every demo.
        TileBase floor = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Factory/Platforms/ThinTileCenter.asset");
        for (int x = 10; x <= 28; x++)
        {
            var cell = new Vector3Int(x, 15, 0);
            if (!solid.HasTile(cell) && !thin.HasTile(cell)) thin.SetTile(cell, floor);
        }
        // A small intermediate step softens the existing final climb.
        var step = new Vector3Int(26, 14, 0);
        if (!solid.HasTile(step) && !thin.HasTile(step)) thin.SetTile(step, floor);

        var door = AssetDatabase.LoadAssetAtPath<PropTile>(MechanicAssetBuilder.DoorTilePath);
        var machine = AssetDatabase.LoadAssetAtPath<PropTile>(MechanicAssetBuilder.SplittingMachineTilePath);
        var plate = AssetDatabase.LoadAssetAtPath<PropTile>("Assets/Tiles/Factory/Props/PressurePlate_PropTile.asset");
        var serialized = new SerializedObject(props.GetComponent<PropTilemapSpawner>());
        var overrides = serialized.FindProperty("cellOverrides");
        var originalOverrides = new Dictionary<Vector3Int, string>();
        for (int i = 0; i < overrides.arraySize; i++)
        {
            var entry = overrides.GetArrayElementAtIndex(i);
            originalOverrides[entry.FindPropertyRelative("cell").vector3IntValue] = entry.FindPropertyRelative("connectionId").stringValue;
        }
        Paint(props, overrides, Green, door, "", ConnectionMode.Hold, true, false);
        Paint(props, overrides, Yellow, door, YellowId, ConnectionMode.Toggle, false, false);
        Paint(props, overrides, YellowPlate, plate, YellowId, ConnectionMode.Hold, true, true);
        Paint(props, overrides, Red, door, RedId, ConnectionMode.Hold, false, false);
        Paint(props, overrides, RedPlate, plate, RedId, ConnectionMode.Hold, true, false);
        Paint(props, overrides, Splitter, machine, "", ConnectionMode.Hold, true, false);
        var spring = EnsureGallerySpringTile();
        foreach (var cell in new[] { new Vector3Int(16, -5, 0), new Vector3Int(19, 2, 0), new Vector3Int(22, 9, 0) })
        {
            Require(solid.HasTile(cell + Vector3Int.down) || thin.HasTile(cell + Vector3Int.down),
                "Ascent spring has no supporting platform: " + cell);
            Paint(props, overrides, cell, spring, "", ConnectionMode.Hold, true, false);
        }
        serialized.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(serialized.targetObject);

        Sign(root, "Title", new Vector2(20, 22), "DOORS + SPLITTING", Color.white, .11f);
        Sign(root, "Green_Hint", new Vector2(24.5f, 20.2f), "GREEN: AUTOMATIC\nApproach to open\nNo pressure plate", Color.green);
        Sign(root, "Yellow_Hint", new Vector2(18.8f, 20.2f), "YELLOW: LATCH\nPress plate once\nStays unlocked", Color.yellow);
        Sign(root, "Red_Hint", new Vector2(13.5f, 20.2f), "RED: HOLD\nLeave one droplet on plate\nRelease = locked again", new Color(1f, .5f, .5f));
        Sign(root, "Splitter_Hint", new Vector2(29.2f, 19.6f), "SPLITTER\nStep on to unlock\nLeft Shift: split\nTab: switch droplet", new Color(.6f, 1f, 1f));
        Sign(root, "Exit_Hint", new Vector2(20, 21.25f), "S / DOWN: leave the demo gallery", Color.white, .045f);
        Sign(root, "Climb_Hint", new Vector2(24f, 5.5f), "DOOR + SPLIT DEMOS\nBounce up the springs\nSteer right between landings", new Color(.6f, 1f, 1f));
        Sign(root, "Spring_Start", new Vector2(16.5f, -2.1f), "^ SPRINGS TO DOOR DEMOS", new Color(.6f, 1f, 1f), .045f);
        Link(root, "Yellow_PlateLink", new Vector2(18.5f, 16.2f), new Vector2(20.5f, 16.2f), Color.yellow);
        Link(root, "Red_PlateLink", new Vector2(12.5f, 16.2f), new Vector2(14.5f, 16.2f), new Color(1f, .4f, .4f));
        ExtendCameraBounds(root);

        foreach (var map in new[] { thin, props })
        {
            map.RefreshAllTiles();
            var tileCollider = map.GetComponent<TilemapCollider2D>();
            if (tileCollider != null) tileCollider.ProcessTilemapChanges();
            var composite = map.GetComponent<CompositeCollider2D>();
            if (composite != null) composite.GenerateGeometry();
            EditorUtility.SetDirty(map);
            PrefabUtility.RecordPrefabInstancePropertyModifications(map);
            if (map.GetComponent<CompositeCollider2D>() != null)
                PrefabUtility.RecordPrefabInstancePropertyModifications(map.GetComponent<CompositeCollider2D>());
        }
        foreach (var map in before)
            foreach (var cell in map.Value)
                Require(map.Key.GetTile(cell.Key) == cell.Value, "Existing tile was changed: " + cell.Key);
        serialized.Update();
        foreach (var old in originalOverrides)
        {
            var entry = FindOverride(serialized.FindProperty("cellOverrides"), old.Key);
            Require(entry != null && entry.FindPropertyRelative("connectionId").stringValue == old.Value, "Existing connection was changed: " + old.Key);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        Require(EditorSceneManager.SaveScene(scene), "Could not save Sandbox.");
        AssetDatabase.SaveAssets();
        Debug.Log("[SandboxDemos] PASS: added gallery; every existing tile and connection ID preserved.");
    }

    private static PropTile EnsureGallerySpringTile()
    {
        const string prefabPath = "Assets/Prefabs/Props/SandboxGallerySpring.prefab";
        const string tilePath = "Assets/Tiles/Factory/Props/SandboxGallerySpring_PropTile.asset";
        var source = AssetDatabase.LoadAssetAtPath<PropTile>("Assets/Tiles/Factory/Props/Trampoline_PropTile.asset");
        var root = PrefabUtility.LoadPrefabContents("Assets/Prefabs/Props/Trampoline.prefab");
        GameObject prefab;
        try
        {
            root.name = "SandboxGallerySpring";
            // The original 16-unit bounce cannot bridge the two seven-unit gaps.
            // Tune only this Sandbox prop, preserving the shared trampoline.
            var settings = new SerializedObject(root.GetComponent<Trampoline>());
            settings.FindProperty("bounceStrength").floatValue = 24f;
            settings.ApplyModifiedPropertiesWithoutUndo();
            prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var tile = AssetDatabase.LoadAssetAtPath<PropTile>(tilePath);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<PropTile>();
            AssetDatabase.CreateAsset(tile, tilePath);
        }
        tile.prefab = prefab;
        tile.previewSprite = source.previewSprite;
        tile.spawnOffset = source.spawnOffset;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void Paint(Tilemap map, SerializedProperty entries, Vector3Int cell, PropTile tile,
        string id, ConnectionMode mode, bool active, bool oneShot)
    {
        Require(tile != null, "Missing production prop asset.");
        Require(!map.HasTile(cell) || map.GetTile(cell) == tile, "Demo cell occupied: " + cell);
        map.SetTile(cell, tile);
        var entry = FindOverride(entries, cell);
        if (entry == null) { entries.InsertArrayElementAtIndex(entries.arraySize); entry = entries.GetArrayElementAtIndex(entries.arraySize - 1); }
        entry.FindPropertyRelative("cell").vector3IntValue = cell;
        entry.FindPropertyRelative("propName").stringValue = tile.prefab.name;
        entry.FindPropertyRelative("connectionId").stringValue = id;
        entry.FindPropertyRelative("connectionMode").enumValueIndex = (int)mode;
        entry.FindPropertyRelative("initialActive").boolValue = active;
        entry.FindPropertyRelative("oneShot").boolValue = oneShot;
        entry.FindPropertyRelative("requirePlayerState").boolValue = false;
        entry.FindPropertyRelative("overrideBlowerSettings").boolValue = false;
    }

    private static SerializedProperty FindOverride(SerializedProperty entries, Vector3Int cell)
    {
        for (int i = 0; i < entries.arraySize; i++)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("cell").vector3IntValue == cell)
                return entries.GetArrayElementAtIndex(i);
        return null;
    }

    private static GameObject Child(GameObject root, string name)
    {
        var child = root.transform.Find(name);
        if (child != null) return child.gameObject;
        var result = new GameObject(name); result.transform.SetParent(root.transform, false); return result;
    }

    private static void Sign(GameObject root, string name, Vector2 position, string label, Color color, float size = .055f)
    {
        var go = Child(root, name); go.transform.position = position;
        var text = go.GetComponent<TextMesh>();
        if (text == null) text = go.AddComponent<TextMesh>();
        text.text = label; text.characterSize = size; text.fontSize = 32;
        text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = color;
        go.GetComponent<MeshRenderer>().sortingOrder = 50;
    }

    private static void Link(GameObject root, string name, Vector2 start, Vector2 end, Color color)
    {
        var go = Child(root, name);
        var line = go.GetComponent<LineRenderer>();
        if (line == null) line = go.AddComponent<LineRenderer>();
        line.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Sprites-Default.mat");
        line.startColor = line.endColor = color; line.startWidth = line.endWidth = .035f;
        line.useWorldSpace = true; line.positionCount = 2; line.SetPosition(0, start); line.SetPosition(1, end); line.sortingOrder = 39;
    }

    private static void ExtendCameraBounds(GameObject root)
    {
        var go = Child(root, "GalleryCameraBounds"); go.layer = LayerMask.NameToLayer("Ignore Raycast");
        var bounds = go.GetComponent<PolygonCollider2D>();
        if (bounds == null) bounds = go.AddComponent<PolygonCollider2D>();
        bounds.isTrigger = true;
        // Sandbox uses a wide orthographic size of 10. Include viewport margins,
        // not just the room itself, or the confiner removes the entire gallery.
        // Keep the existing left/bottom limits and camera zoom unchanged.
        bounds.SetPath(0, new[] { new Vector2(-24,-13.5f), new Vector2(48,-13.5f),
            new Vector2(48,30), new Vector2(-24,30) });
        foreach (var component in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
        {
            if (component == null || component.GetType().Name != "CinemachineConfiner2D") continue;
            var serialized = new SerializedObject(component);
            serialized.FindProperty("m_BoundingShape2D").objectReferenceValue = bounds;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException("[SandboxDemos] " + message);
    }
}
