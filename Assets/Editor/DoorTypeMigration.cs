using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Converts legacy door cells once, preserving their connections and scene exits.
public static class DoorTypeMigration
{
    public static void MigrateBatch()
    {
        MechanicAssetBuilder.EnsureDoorAssets();
        foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/Scenes", "Assets/Editor/SceneTemplates" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            bool changed = false;
            foreach (var spawner in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PropTilemapSpawner>(true)))
            {
                var map = spawner.GetComponent<Tilemap>();
                var so = new SerializedObject(spawner);
                var entries = so.FindProperty("cellOverrides");
                bool mapChanged = false;
                for (int i = 0; i < entries.arraySize; i++)
                {
                    var entry = entries.GetArrayElementAtIndex(i);
                    if (entry.FindPropertyRelative("propName").stringValue != "Door") continue;
                    var cell = entry.FindPropertyRelative("cell").vector3IntValue;
                    var current = map.GetTile<PropTile>(cell);
                    if (current == null || current.prefab == null || current.prefab.GetComponent<Door>() == null)
                        throw new InvalidOperationException($"Missing legacy door in {path} at {cell}");
                    bool active = entry.FindPropertyRelative("initialActive").boolValue;
                    var mode = (ConnectionMode)entry.FindPropertyRelative("connectionMode").enumValueIndex;
                    var type = active ? DoorType.Green : mode == ConnectionMode.Toggle ? DoorType.Yellow : DoorType.Red;
                    var tile = AssetDatabase.LoadAssetAtPath<PropTile>(MechanicAssetBuilder.DoorTileFor(type));
                    var matrix = map.GetTransformMatrix(cell);
                    var color = map.GetColor(cell);
                    map.SetTile(cell, tile);
                    map.SetTransformMatrix(cell, matrix);
                    map.SetColor(cell, color);
                    entry.FindPropertyRelative("propName").stringValue = tile.prefab.name;
                    mapChanged = true;
                    Debug.Log($"[DoorSplit] {path} {cell}: {type}");
                }
                if (!mapChanged) continue;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(map);
                EditorUtility.SetDirty(spawner);
                if (PrefabUtility.IsPartOfPrefabInstance(map)) PrefabUtility.RecordPrefabInstancePropertyModifications(map);
                if (PrefabUtility.IsPartOfPrefabInstance(spawner)) PrefabUtility.RecordPrefabInstancePropertyModifications(spawner);
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + path);
            }
        }
        AssetDatabase.SaveAssets();
        ValidateBatch();
    }

    public static void ValidatePlayModeBatch()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Sandbox.unity", OpenSceneMode.Single);
        SandboxMechanicsDemoProbe.Begin();
    }

    public static void ValidateBatch()
    {
        var palette = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Tilemaps/1x1.prefab").GetComponentInChildren<Tilemap>();
        foreach (DoorType type in Enum.GetValues(typeof(DoorType)))
        {
            var tile = AssetDatabase.LoadAssetAtPath<PropTile>(MechanicAssetBuilder.DoorTileFor(type));
            Require(tile != null && tile.prefab.GetComponent<Door>().Type == type, "Incorrect prefab type: " + type);
            bool found = false;
            foreach (Vector3Int cell in palette.cellBounds.allPositionsWithin)
                if (palette.GetTile(cell) == tile) found = true;
            Require(found, "Missing palette tile: " + type);
            var instance = UnityEngine.Object.Instantiate(tile.prefab);
            try
            {
                var door = instance.GetComponent<Door>();
                door.SendMessage("Awake");
                door.SetConnectionId("validation");
                Require(door.IsUnlocked == (type == DoorType.Green), "Wrong initial state: " + type);
                Require(instance.GetComponentInChildren<SpriteRenderer>().sprite == tile.previewSprite, "Wrong initial artwork: " + type);
                door.SendMessage("OnTriggerActivated", "unrelated");
                Require(door.IsUnlocked == (type == DoorType.Green), "Cross-connection activation: " + type);
                door.SendMessage("OnTriggerActivated", "validation");
                Require(door.IsUnlocked, "Activation failed: " + type);
                door.SendMessage("OnTriggerDeactivated", "validation");
                Require(door.IsUnlocked == (type != DoorType.Red), "Release failed: " + type);
                door.SendMessage("OnTriggerActivated", "validation");
                Require(door.IsUnlocked, "Repeated activation failed: " + type);
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }
        for (int room = 1; room <= 3; room++)
        {
            var scene = EditorSceneManager.OpenScene($"Assets/Scenes/Area2-{room}.unity", OpenSceneMode.Single);
            var spawner = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<PropTilemapSpawner>(true)).Single();
            var map = spawner.GetComponent<Tilemap>();
            var entries = new SerializedObject(spawner).FindProperty("cellOverrides");
            int green = 0, yellow = 0;
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                var tile = map.GetTile<PropTile>(entry.FindPropertyRelative("cell").vector3IntValue);
                var door = tile != null && tile.prefab != null ? tile.prefab.GetComponent<Door>() : null;
                if (door == null) continue;
                Require(entry.FindPropertyRelative("propName").stringValue == tile.prefab.name, "Stale door entry");
                if (door.Type == DoorType.Green) green++;
                if (door.Type != DoorType.Yellow) continue;
                yellow++;
                string expectedId = room == 1 ? "intro_door" : room == 2 ? "familiarity_crushers" : "challenge_exit";
                Require(entry.FindPropertyRelative("connectionId").stringValue == expectedId, "Changed exit connection");
                Require(entry.FindPropertyRelative("exitScene").stringValue == (room == 3 ? "Area3-1" : $"Area2-{room + 1}"), "Changed scene exit");
                Require(entry.FindPropertyRelative("exitToLeft").boolValue == (room == 2), "Changed exit direction");
            }
            Require(green == 1 && yellow == 1, $"Area2-{room} must have a green entry and yellow exit");
        }
        MechanicAssetBuilder.ValidateMechanicsBatch();
        Area2MechanicsBuilder.ValidateBatch();
        Area2RoomSceneBuilder.ValidateBatch();
        Debug.Log("[DoorSplit] PASS: three prefab types, palette previews, trigger behaviours and all Area 2 doors/exits.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[DoorSplit] " + message);
    }
}
