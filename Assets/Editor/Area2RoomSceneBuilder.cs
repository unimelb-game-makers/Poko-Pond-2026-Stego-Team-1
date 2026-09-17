using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

// One-time migration/rebuild from the archived combined scene. Subsequent room
// edits belong in the three scenes; rebuilding intentionally replaces them.
public static class Area2RoomSceneBuilder
{
    public const string SourcePath = "Assets/Editor/SceneTemplates/Area2-Combined.unity";
    private static readonly string[] Rooms = { "Introduction", "Familiarity", "Challenge" };
    private static readonly int[] Starts = { 0, 30, 64 };
    private static readonly int[] Ends = { 29, 62, 104 };
    private static readonly int[] Doors = { 29, 62, 100 };

    public static void BuildBatch()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run this migration in an isolated batch project.");
        for (int i = 0; i < Rooms.Length; i++) BuildRoom(i);
        RegisterScenes();
        Area2MechanicsBuilder.ApplyToRooms();
        ValidateBatch();
        AssetDatabase.SaveAssets();
        Debug.Log("[Area2Rooms] Built and validated all three rooms.");
    }

    private static void BuildRoom(int index)
    {
        Scene scene = EditorSceneManager.OpenScene(SourcePath, OpenSceneMode.Single);
        // Unpack the level Grid to trim its tile data. Keep player, UI and
        // other shared prefabs linked so future prefab fixes reach each room.
        foreach (GameObject root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<Tilemap>(true) != null && PrefabUtility.IsPartOfPrefabInstance(root))
                PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);

        int start = Starts[index];
        int end = Ends[index];
        int exit = Doors[index] - start;
        GameObject area = scene.GetRootGameObjects().Single(go => go.name == "Area2");
        foreach (Transform child in area.transform.Cast<Transform>().ToArray())
            if (child.name != Rooms[index]) UnityEngine.Object.DestroyImmediate(child.gameObject);
        area.name = $"Area2-{index + 1}";
        area.transform.position -= new Vector3(start, 0f, 0f);
        AddMarker(area.transform, "Spawn", new Vector3(index == 2 ? 3f : 4f, 2f));
        AddMarker(area.transform, "Exit", new Vector3(exit + 1.5f, 2f));

        foreach (Tilemap map in Objects<Tilemap>(scene))
        {
            var kept = new List<(Vector3Int cell, TileBase tile, Matrix4x4 matrix, Color color)>();
            foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                if (cell.x >= start && cell.x <= end && map.HasTile(cell))
                    kept.Add((cell - new Vector3Int(start, 0, 0), map.GetTile(cell), map.GetTransformMatrix(cell), map.GetColor(cell)));
            map.ClearAllTiles();
            foreach (var item in kept)
            {
                map.SetTile(item.cell, item.tile);
                map.SetTransformMatrix(item.cell, item.matrix);
                map.SetColor(item.cell, item.color);
            }
            map.CompressBounds();
            map.RefreshAllTiles();
        }

        Tilemap solid = Objects<Tilemap>(scene).Single(map => map.name == "SolidPlatforms");
        TileBase floor = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Factory/Platforms/TileCenter.asset");
        // A supported threshold beyond each outgoing gate and a closed left
        // boundary keep independently loaded rooms safe at both ends.
        for (int x = exit + 1; x <= exit + 3; x++) solid.SetTile(new Vector3Int(x, 0, 0), floor);
        int ceiling = index == 0 ? 6 : index == 1 ? 7 : 16;
        for (int y = 1; y <= ceiling; y++)
            solid.SetTile(new Vector3Int(0, y, 0), floor);

        PropTilemapSpawner spawner = Objects<PropTilemapSpawner>(scene).Single();
        SerializedObject so = new SerializedObject(spawner);
        SerializedProperty entries = so.FindProperty("cellOverrides");
        for (int i = entries.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty item = entries.GetArrayElementAtIndex(i);
            Vector3Int cell = item.FindPropertyRelative("cell").vector3IntValue;
            if (cell.x < start || cell.x > end)
            {
                entries.DeleteArrayElementAtIndex(i);
                continue;
            }
            item.FindPropertyRelative("cell").vector3IntValue = cell - new Vector3Int(start, 0, 0);
            item.FindPropertyRelative("exitScene").stringValue = cell == new Vector3Int(Doors[index], 1, 0)
                ? Destination(index) : "";
        }
        so.ApplyModifiedPropertiesWithoutUndo();

        Vector3 spawn = new Vector3(index == 2 ? 3f : 4f, 2f, 0f);
        SoftBodyPlayer player = Objects<SoftBodyPlayer>(scene).Single();
        player.transform.position = spawn;
        // The combined scene's player bounds, when present, must not pull the
        // newly localised player back toward coordinates from another room.
        player.levelBounds = null;
        Objects<CameraFollowProxy>(scene).Single().transform.position = spawn;
        Objects<PlayerSplitController>(scene).Single().SetSplittingUnlocked(false);
        foreach (Camera camera in Objects<Camera>(scene))
            camera.transform.position = new Vector3(spawn.x, spawn.y, camera.transform.position.z);

        BoxCollider2D bounds = Objects<BoxCollider2D>(scene).Single(c => c.name == "Area2CameraBounds");
        bounds.name = $"Area2-{index + 1}CameraBounds";
        bounds.transform.position = new Vector3((exit + 3f) * 0.5f, 8f, 0f);
        bounds.size = new Vector2(exit + 13f, 24f);
        foreach (ParallaxBackground background in Objects<ParallaxBackground>(scene))
            background.transform.position = new Vector3((exit + 3f) * 0.5f,
                background.transform.position.y, background.transform.position.z);

        foreach (Tilemap map in Objects<Tilemap>(scene))
        {
            map.CompressBounds();
            map.RefreshAllTiles();
            var collider = map.GetComponent<TilemapCollider2D>();
            if (collider != null) collider.ProcessTilemapChanges();
        }
        foreach (Component component in Objects<Component>(scene))
            if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                PrefabUtility.RecordPrefabInstancePropertyModifications(component);
        if (!EditorSceneManager.SaveScene(scene, Path(index)))
            throw new InvalidOperationException("Could not save " + Path(index));
    }

    private static void AddMarker(Transform parent, string name, Vector3 position)
    {
        var marker = new GameObject(name);
        marker.transform.SetParent(parent, false);
        marker.transform.position = position;
    }

    private static void RegisterScenes()
    {
        var scenes = EditorBuildSettings.scenes.Where(s => s.path != "Assets/Scenes/Area2.unity"
            && s.path != SourcePath && !Enumerable.Range(0, 3).Any(i => s.path == Path(i))
            && s.path != "Assets/Scenes/Area3-1.unity").ToList();
        for (int i = 0; i < 3; i++) scenes.Add(new EditorBuildSettingsScene(Path(i), true));
        scenes.Add(new EditorBuildSettingsScene("Assets/Scenes/Area3-1.unity", true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    [MenuItem("Tools/Poko Pond/Area 2/Validate Split Scenes")]
    private static void ValidateMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try { ValidateBatch(); }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    public static void ValidateBatch()
    {
        for (int i = 0; i < 3; i++)
        {
            Scene scene = EditorSceneManager.OpenScene(Path(i), OpenSceneMode.Single);
            Require(Objects<SoftBodyPlayer>(scene).Count() == 1, "Expected one player in " + Path(i));
            Require(Objects<GameStateManager>(scene).Count() == 1, "Missing game state manager.");
            Require(Objects<GameOverUI>(scene).Count() == 1, "Missing retry UI.");
            Transform area = scene.GetRootGameObjects().Single(go => go.name == $"Area2-{i + 1}").transform;
            Require(area.Find(Rooms[i]) != null, "Room content missing.");
            Require(Rooms.Where(r => r != Rooms[i]).All(r => area.Find(r) == null), "Foreign room content remains.");
            foreach (Tilemap map in Objects<Tilemap>(scene))
                foreach (Vector3Int cell in map.cellBounds.allPositionsWithin)
                    Require(!map.HasTile(cell) || (cell.x >= (i == 1 ? -2 : 0) && cell.x <= Ends[i] - Starts[i] + 3), "Tile outside room.");

            var so = new SerializedObject(Objects<PropTilemapSpawner>(scene).Single());
            SerializedProperty entries = so.FindProperty("cellOverrides");
            int exits = 0;
            string connection = "";
            for (int j = 0; j < entries.arraySize; j++)
            {
                SerializedProperty item = entries.GetArrayElementAtIndex(j);
                if (string.IsNullOrEmpty(item.FindPropertyRelative("exitScene").stringValue)) continue;
                exits++;
                Require(item.FindPropertyRelative("exitScene").stringValue == Destination(i), "Wrong destination.");
                Require(!item.FindPropertyRelative("initialActive").boolValue, "Exit must start locked.");
                Require(item.FindPropertyRelative("connectionMode").enumValueIndex == (int)ConnectionMode.Toggle, "Exit must latch.");
                connection = item.FindPropertyRelative("connectionId").stringValue;
            }
            Require(exits == 1, "Expected exactly one scene exit.");
            bool plate = false;
            for (int j = 0; j < entries.arraySize; j++)
            {
                var item = entries.GetArrayElementAtIndex(j);
                if (item.FindPropertyRelative("propName").stringValue == "PressurePlate"
                    && item.FindPropertyRelative("connectionId").stringValue == connection
                    && item.FindPropertyRelative("oneShot").boolValue) plate = true;
            }
            Require(plate, "Exit has no matching one-shot plate.");
            Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == Path(i)), "Room absent from build.");
            Require(EditorBuildSettings.scenes.Any(s => s.enabled && s.path == "Assets/Scenes/" + Destination(i) + ".unity"), "Destination absent from build.");
            foreach (Transform t in Objects<Transform>(scene))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject) == 0, "Missing script on " + t.name);
        }
        EditorSceneManager.OpenScene(Path(0), OpenSceneMode.Single);
        Debug.Log("[Area2Rooms] Scene structure, gates, connections and destinations passed.");
    }

    private static string Path(int index) => $"Assets/Scenes/Area2-{index + 1}.unity";
    private static string Destination(int index) => index == 2 ? "Area3-1" : $"Area2-{index + 2}";
    private static IEnumerable<T> Objects<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Area2Rooms] " + message);
    }
}
