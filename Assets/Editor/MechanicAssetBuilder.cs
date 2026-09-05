using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

// Imports the supplied production artwork, builds the runtime prefabs and
// PropTiles, and keeps the relevant Tile Palette prefabs in sync.
public static class MechanicAssetBuilder
{
    public const string DoorPrefabPath = "Assets/Prefabs/Props/Door.prefab";
    public const string SplittingMachinePrefabPath = "Assets/Prefabs/Props/SplittingMachine.prefab";
    public const string DoorTilePath = "Assets/Tiles/Factory/Props/Door_PropTile.asset";
    public const string SplittingMachineTilePath = "Assets/Tiles/Factory/Props/SplittingMachine_PropTile.asset";

    private const string GreenDoorPath = "Assets/Art/Environment/Props/Door/green_door.png";
    private const string GreenDoorOpeningPath = "Assets/Art/Environment/Props/Door/greendoor_opening.png";
    private const string RedDoorPath = "Assets/Art/Environment/Props/Door/red_door.png";
    private const string YellowDoorPath = "Assets/Art/Environment/Props/Door/yellow_door.png";
    private const string YellowToGreenDoorPath = "Assets/Art/Environment/Props/Door/yellowtogreen_door.png";
    private const string SplittingMachineArtPath = "Assets/Art/Environment/Props/SplittingMachine/splitting_machine.png";
    private const string OneByOnePalettePath = "Assets/Tilemaps/1x1.prefab";
    private const string TwoByTwoPalettePath = "Assets/Tilemaps/2x2.prefab";

    [MenuItem("Tools/Poko Pond/Mechanics/Rebuild Door and Splitting Machine")]
    public static void EnsureMechanicAssets()
    {
        Directory.CreateDirectory("Assets/Prefabs/Props");
        Directory.CreateDirectory("Assets/Tiles/Factory/Props");

        ConfigureSingleSprite(GreenDoorPath, 32f, new Vector2(0.5f, 0f));
        ConfigureSpriteSheet(GreenDoorOpeningPath, 5, 32, 32, 32f, "green_door_opening");
        ConfigureSingleSprite(RedDoorPath, 32f, new Vector2(0.5f, 0f));
        ConfigureSingleSprite(YellowDoorPath, 32f, new Vector2(0.5f, 0f));
        ConfigureSpriteSheet(YellowToGreenDoorPath, 3, 32, 32, 32f, "yellow_to_green_door");
        ConfigureSingleSprite(SplittingMachineArtPath, 320f, new Vector2(0.5f, 155f / 640f));

        Sprite greenDoor = LoadRequiredSprite(GreenDoorPath);
        Sprite redDoor = LoadRequiredSprite(RedDoorPath);
        Sprite yellowDoor = LoadRequiredSprite(YellowDoorPath);
        Sprite[] openingFrames = LoadRequiredSprites(GreenDoorOpeningPath, 5);
        Sprite[] unlockFrames = LoadRequiredSprites(YellowToGreenDoorPath, 3);
        Sprite splittingMachineSprite = LoadRequiredSprite(SplittingMachineArtPath);

        GameObject door = BuildDoorPrefab(greenDoor, yellowDoor, redDoor, openingFrames, unlockFrames);
        GameObject splittingMachine = BuildSplittingMachinePrefab(splittingMachineSprite);
        PropTile doorTile = CreateOrUpdatePropTile(DoorTilePath, door, greenDoor, new Vector3(0f, -0.5f, 0f));
        PropTile splittingTile = CreateOrUpdatePropTile(
            SplittingMachineTilePath,
            splittingMachine,
            splittingMachineSprite,
            new Vector3(0f, -0.5f, 0f));

        AddTileToPalette(OneByOnePalettePath, doorTile);
        AddTileToPalette(TwoByTwoPalettePath, splittingTile);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Debug.Log("[MechanicAssetBuilder] Rebuilt production door and splitting-machine assets and palettes.");
    }

    public static void ValidateMechanicsBatch()
    {
        RequireComponent<Door>(DoorPrefabPath);
        RequireComponent<SplittingMachine>(SplittingMachinePrefabPath);
        RequireTilePrefab(DoorTilePath, DoorPrefabPath);
        RequireTilePrefab(SplittingMachineTilePath, SplittingMachinePrefabPath);
        RequirePaletteTile(OneByOnePalettePath, DoorTilePath);
        RequirePaletteTile(TwoByTwoPalettePath, SplittingMachineTilePath);
        Require(LoadRequiredSprites(GreenDoorOpeningPath, 5).Length == 5,
            "Door opening sheet does not contain five frames.");
        Require(LoadRequiredSprites(YellowToGreenDoorPath, 3).Length == 3,
            "Yellow-to-green door sheet does not contain three frames.");

        GameObject testObject = new GameObject("MechanicsValidation");
        try
        {
            testObject.AddComponent<BoxCollider2D>();
            Door door = testObject.AddComponent<Door>();
            door.SetConnectionId("door_test");
            door.SetActivationConfig(ConnectionMode.Toggle, false);
            InvokeDoorTrigger(door, "OnTriggerActivated", "other");
            Require(!door.IsUnlocked, "Door reacted to a non-matching connection ID.");
            InvokeDoorTrigger(door, "OnTriggerActivated", "door_test");
            Require(door.IsUnlocked, "Toggle door did not unlock on activation.");
            InvokeDoorTrigger(door, "OnTriggerActivated", "door_test");
            Require(door.IsUnlocked, "Repeated activation relocked a permanently unlocked yellow door.");
            InvokeDoorTrigger(door, "OnTriggerDeactivated", "door_test");
            Require(door.IsUnlocked, "Toggle door incorrectly relocked on release.");

            door.SetActivationConfig(ConnectionMode.Hold, false);
            InvokeDoorTrigger(door, "OnTriggerActivated", "door_test");
            Require(door.IsUnlocked, "Hold door did not unlock while its activator was held.");
            InvokeDoorTrigger(door, "OnTriggerDeactivated", "door_test");
            Require(!door.IsUnlocked, "Hold door did not relock when its activator was released.");

            PlayerSplitController splitController = testObject.AddComponent<PlayerSplitController>();
            splitController.SetSplittingUnlocked(false);
            Require(splitController.UnlockSplitting(), "First splitting-machine unlock did not change state.");
            Require(splitController.SplittingUnlocked, "Split controller remained locked after unlock.");
            Require(!splitController.UnlockSplitting(), "Repeated splitting-machine unlock was not idempotent.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(testObject);
        }

        Debug.Log("[MechanicAssetBuilder] Mechanics validation passed.");
    }

    private static GameObject BuildDoorPrefab(
        Sprite green,
        Sprite yellow,
        Sprite red,
        Sprite[] openingFrames,
        Sprite[] unlockFrames)
    {
        GameObject root = new GameObject("Door");
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(0.82f, 3f);
        collider.offset = new Vector2(0f, 1.5f);

        GameObject visual = new GameObject("Visual");
        visual.transform.SetParent(root.transform, false);
        visual.transform.localScale = new Vector3(3f, 3f, 1f);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = green;
        renderer.sortingOrder = 40;

        Door door = root.AddComponent<Door>();
        SerializedObject serialized = new SerializedObject(door);
        serialized.FindProperty("doorRenderer").objectReferenceValue = renderer;
        serialized.FindProperty("greenClosedSprite").objectReferenceValue = green;
        serialized.FindProperty("yellowClosedSprite").objectReferenceValue = yellow;
        serialized.FindProperty("redClosedSprite").objectReferenceValue = red;
        AssignSpriteArray(serialized.FindProperty("greenOpeningFrames"), openingFrames);
        AssignSpriteArray(serialized.FindProperty("yellowToGreenFrames"), unlockFrames);
        serialized.FindProperty("blockingCollider").objectReferenceValue = collider;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, DoorPrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static GameObject BuildSplittingMachinePrefab(Sprite sprite)
    {
        GameObject root = new GameObject("SplittingMachine");
        int groundLayer = LayerMask.NameToLayer("Ground");
        if (groundLayer >= 0) root.layer = groundLayer;

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.size = new Vector2(1.68f, 0.24f);
        collider.offset = new Vector2(0f, 0.12f);

        SpriteRenderer renderer = root.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 40;

        SplittingMachine machine = root.AddComponent<SplittingMachine>();
        SerializedObject serialized = new SerializedObject(machine);
        serialized.FindProperty("machineRenderer").objectReferenceValue = renderer;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, SplittingMachinePrefabPath);
        UnityEngine.Object.DestroyImmediate(root);
        return prefab;
    }

    private static void ConfigureSingleSprite(string path, float pixelsPerUnit, Vector2 pivot)
    {
        TextureImporter importer = RequireImporter(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        TextureImporterSettings settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteAlignment = (int)SpriteAlignment.Custom;
        settings.spritePivot = pivot;
        importer.SetTextureSettings(settings);
        ApplyPixelArtSettings(importer);
        importer.SaveAndReimport();
    }

    private static void ConfigureSpriteSheet(
        string path,
        int frameCount,
        int frameWidth,
        int frameHeight,
        float pixelsPerUnit,
        string frameName)
    {
        TextureImporter importer = RequireImporter(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        ApplyPixelArtSettings(importer);

        SpriteMetaData[] frames = new SpriteMetaData[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            frames[i] = new SpriteMetaData
            {
                name = frameName + "_" + i,
                rect = new Rect(i * frameWidth, 0f, frameWidth, frameHeight),
                alignment = (int)SpriteAlignment.Custom,
                pivot = new Vector2(0.5f, 0f)
            };
        }

#pragma warning disable 0618
        importer.spritesheet = frames;
#pragma warning restore 0618
        importer.SaveAndReimport();
    }

    private static void ApplyPixelArtSettings(TextureImporter importer)
    {
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
    }

    private static TextureImporter RequireImporter(string path)
    {
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
            throw new InvalidOperationException("[MechanicAssetBuilder] Missing texture at " + path + ".");
        return importer;
    }

    private static void AssignSpriteArray(SerializedProperty property, Sprite[] sprites)
    {
        property.arraySize = sprites.Length;
        for (int i = 0; i < sprites.Length; i++)
            property.GetArrayElementAtIndex(i).objectReferenceValue = sprites[i];
    }

    private static PropTile CreateOrUpdatePropTile(
        string path,
        GameObject prefab,
        Sprite previewSprite,
        Vector3 spawnOffset)
    {
        PropTile tile = AssetDatabase.LoadAssetAtPath<PropTile>(path);
        if (tile == null)
        {
            tile = ScriptableObject.CreateInstance<PropTile>();
            tile.name = Path.GetFileNameWithoutExtension(path);
            AssetDatabase.CreateAsset(tile, path);
        }

        tile.previewSprite = previewSprite;
        tile.prefab = prefab;
        tile.spawnOffset = spawnOffset;
        tile.connectionId = "";
        EditorUtility.SetDirty(tile);
        return tile;
    }

    private static void AddTileToPalette(string palettePath, PropTile tile)
    {
        GameObject root = PrefabUtility.LoadPrefabContents(palettePath);
        try
        {
            Tilemap tilemap = root.GetComponentInChildren<Tilemap>(true);
            if (tilemap == null)
                throw new InvalidOperationException("[MechanicAssetBuilder] Palette has no Tilemap: " + palettePath);

            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) == tile)
                    return;
            }

            BoundsInt bounds = tilemap.cellBounds;
            Vector3Int destination = new Vector3Int(bounds.xMax, bounds.yMin, 0);
            tilemap.SetTile(destination, tile);
            tilemap.CompressBounds();
            EditorUtility.SetDirty(tilemap);
            PrefabUtility.SaveAsPrefabAsset(root, palettePath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Sprite LoadRequiredSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null)
            throw new InvalidOperationException("[MechanicAssetBuilder] Missing sprite at " + path + ".");
        return sprite;
    }

    private static Sprite[] LoadRequiredSprites(string path, int expectedCount)
    {
        Sprite[] sprites = AssetDatabase.LoadAllAssetsAtPath(path)
            .OfType<Sprite>()
            .OrderBy(sprite => sprite.name)
            .ToArray();
        if (sprites.Length != expectedCount)
            throw new InvalidOperationException(
                "[MechanicAssetBuilder] Expected " + expectedCount + " sprites at " + path + ", found " + sprites.Length + ".");
        return sprites;
    }

    private static void RequireComponent<T>(string prefabPath) where T : Component
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Require(prefab != null, "Missing prefab at " + prefabPath + ".");
        Require(prefab.GetComponent<T>() != null, prefabPath + " is missing " + typeof(T).Name + ".");
    }

    private static void RequireTilePrefab(string tilePath, string prefabPath)
    {
        PropTile tile = AssetDatabase.LoadAssetAtPath<PropTile>(tilePath);
        Require(tile != null, "Missing PropTile at " + tilePath + ".");
        Require(tile.prefab != null && AssetDatabase.GetAssetPath(tile.prefab) == prefabPath,
            tilePath + " does not reference " + prefabPath + ".");
    }

    private static void RequirePaletteTile(string palettePath, string tilePath)
    {
        PropTile expected = AssetDatabase.LoadAssetAtPath<PropTile>(tilePath);
        GameObject palette = AssetDatabase.LoadAssetAtPath<GameObject>(palettePath);
        Tilemap tilemap = palette == null ? null : palette.GetComponentInChildren<Tilemap>(true);
        Require(tilemap != null, "Missing Tilemap on palette " + palettePath + ".");
        bool found = false;
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
        {
            if (tilemap.GetTile(cell) != expected) continue;
            found = true;
            break;
        }
        Require(found, palettePath + " does not contain " + tilePath + ".");
    }

    private static void InvokeDoorTrigger(Door door, string methodName, string id)
    {
        MethodInfo method = typeof(Door).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Require(method != null, "Door is missing " + methodName + ".");
        method.Invoke(door, new object[] { id });
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException("[MechanicAssetBuilder] " + message);
    }
}
