using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class Area2MechanicsBuilder
{
    public const string ArtPath = "Assets/Art/Environment/Props/Conveyor/conveyer_block.png";
    public const string ConveyorTilePath = "Assets/Tiles/Factory/Props/ConveyorBelt_PropTile.asset";
    public const string ElectricTilePath = "Assets/Tiles/Factory/Props/ElectricPlatform_PropTile.asset";
    public const string HumidifierTilePath = "Assets/Tiles/Factory/Props/Humidifier_PropTile.asset";
    private const string AnimationPath = "Assets/Animations/Environment/Props/Conveyor";
    private const string PalettePath = "Assets/Tilemaps/1x1.prefab";

    public static void BuildBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run scene migration in an isolated batch project.");
        ApplyToRooms();
        ValidateBatch();
        Area2RoomSceneBuilder.ValidateBatch();
        Debug.Log("[Area2Mechanics] Assets, palette and three rooms generated and validated.");
    }

    [MenuItem("Tools/Poko Pond/Mechanics/Rebuild Conveyor, Electric Platform and Humidifier Assets")]
    public static void EnsureAssets()
    {
        Directory.CreateDirectory(AnimationPath);
        AssetDatabase.Refresh();
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(ArtPath);
        importer.GetSourceTextureWidthAndHeight(out int width, out int height);
        Require(width == 256 && height == 32, "Expected the supplied 256x32 conveyor sheet.");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = 32f;
        importer.filterMode = FilterMode.Point;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        SpriteMetaData[] slices = Enumerable.Range(0, 8).Select(i => new SpriteMetaData
        {
            name = "conveyor_" + i, rect = new Rect(i * 32, 0, 32, 32),
            alignment = (int)SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f)
        }).ToArray();
#pragma warning disable 0618
        importer.spritesheet = slices;
#pragma warning restore 0618
        importer.SaveAndReimport();
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(ArtPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        Require(frames.Length == 8, "Conveyor slicing failed.");

        string clipPath = AnimationPath + "/Conveyor_Run.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clip == null) { clip = new AnimationClip(); AssetDatabase.CreateAsset(clip, clipPath); }
        clip.frameRate = 12f;
        AnimationUtility.SetObjectReferenceCurve(clip,
            EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite"),
            Enumerable.Range(0, 9).Select(i => new ObjectReferenceKeyframe { time = i / 12f, value = frames[i % 8] }).ToArray());
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        string controllerPath = AnimationPath + "/Conveyor.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = machine.states.Length == 0 ? machine.AddState("Run") : machine.states[0].state;
        state.motion = clip;
        machine.defaultState = state;
        EditorUtility.SetDirty(controller);

        var conveyor = new GameObject("ConveyorBelt");
        conveyor.layer = LayerMask.NameToLayer("Ground");
        conveyor.AddComponent<SpriteRenderer>().sprite = frames[0];
        conveyor.GetComponent<SpriteRenderer>().sortingOrder = 20;
        conveyor.AddComponent<BoxCollider2D>().size = Vector2.one;
        conveyor.AddComponent<Animator>().runtimeAnimatorController = controller;
        conveyor.AddComponent<ConveyorBelt>();
        PropTile conveyorTile = SaveProp(conveyor, ConveyorTilePath, frames[0], Color.white, Vector3.zero);

        Sprite thin = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environment/Platforms/Factory/factory_tile_thin.png");
        var electric = new GameObject("ElectricPlatform");
        electric.layer = LayerMask.NameToLayer("Platform");
        SpriteRenderer electricRenderer = electric.AddComponent<SpriteRenderer>();
        electricRenderer.sprite = thin;
        electricRenderer.color = new Color(1f, 0.85f, 0.25f);
        electricRenderer.sortingOrder = 20;
        BoxCollider2D electricBox = electric.AddComponent<BoxCollider2D>();
        electricBox.size = new Vector2(1f, 7f / 32f);
        electricBox.offset = new Vector2(0f, 0.5f - 3.5f / 32f);
        electricBox.usedByEffector = true;
        electric.AddComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        PlatformEffector2D effector = electric.AddComponent<PlatformEffector2D>();
        effector.useOneWay = true;
        effector.useOneWayGrouping = true;
        electric.AddComponent<ElectricPlatform>();
        electric.AddComponent<ElectrifyingPlatformEffect>();
        PropTile electricTile = SaveProp(electric, ElectricTilePath, thin, electricRenderer.color, Vector3.zero);

        Sprite blank = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Environment/Platforms/Factory/factory_tile_blank.png");
        var humidifier = new GameObject("Humidifier");
        BoxCollider2D mist = humidifier.AddComponent<BoxCollider2D>();
        mist.isTrigger = true;
        mist.size = new Vector2(1f, 3f);
        mist.offset = new Vector2(0f, 1.5f);
        humidifier.AddComponent<Humidifier>();
        AddBlock(humidifier.transform, "Mist_Placeholder", blank, new Vector2(0, 1.5f), new Vector2(0.8f, 2.7f), new Color(0.3f, 0.85f, 1f, 0.18f));
        AddBlock(humidifier.transform, "UpperNozzle_Placeholder", blank, new Vector2(0, 2.9f), new Vector2(0.9f, 0.2f), Color.cyan);
        AddBlock(humidifier.transform, "LowerNozzle_Placeholder", blank, new Vector2(0, 0.1f), new Vector2(0.9f, 0.2f), Color.cyan);
        var label = new GameObject("ArtTodoLabel");
        label.transform.SetParent(humidifier.transform, false);
        label.transform.localPosition = new Vector3(0f, 3.3f);
        TextMesh text = label.AddComponent<TextMesh>();
        text.text = "HUMIDIFIER\nART TODO";
        text.fontSize = 32;
        text.characterSize = 0.045f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        label.GetComponent<MeshRenderer>().sortingOrder = 50;
        PropTile humidifierTile = SaveProp(humidifier, HumidifierTilePath, blank, Color.cyan, new Vector3(0, -0.5f));

        GameObject palette = PrefabUtility.LoadPrefabContents(PalettePath);
        try
        {
            Tilemap map = palette.GetComponentInChildren<Tilemap>();
            foreach (PropTile tile in new[] { conveyorTile, electricTile, humidifierTile })
            {
                if (Cells(map).Any(cell => map.GetTile(cell) == tile)) continue;
                map.SetTile(new Vector3Int(map.cellBounds.xMax, map.cellBounds.yMin, 0), tile);
                map.CompressBounds();
            }
            PrefabUtility.SaveAsPrefabAsset(palette, PalettePath);
        }
        finally { PrefabUtility.UnloadPrefabContents(palette); }
        AssetDatabase.SaveAssets();
    }

    private static void AddBlock(Transform root, string name, Sprite sprite, Vector2 position, Vector2 size, Color color)
    {
        var block = new GameObject(name);
        block.transform.SetParent(root, false);
        block.transform.localPosition = position;
        block.transform.localScale = new Vector3(size.x, size.y, 1f);
        SpriteRenderer renderer = block.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 35;
    }

    private static PropTile SaveProp(GameObject root, string tilePath, Sprite preview, Color color, Vector3 offset)
    {
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, "Assets/Prefabs/Props/" + root.name + ".prefab");
        UnityEngine.Object.DestroyImmediate(root);
        PropTile tile = AssetDatabase.LoadAssetAtPath<PropTile>(tilePath);
        if (tile == null) { tile = ScriptableObject.CreateInstance<PropTile>(); AssetDatabase.CreateAsset(tile, tilePath); }
        tile.previewSprite = preview;
        tile.previewColor = color;
        tile.prefab = prefab;
        tile.spawnOffset = offset;
        EditorUtility.SetDirty(tile);
        return tile;
    }

    public static void ApplyToRooms()
    {
        EnsureAssets();
        // The old greybox used a very short demonstration cycle. The return
        // route needs a readable slam followed by time to traverse in ice.
        string crusherPath = "Assets/Prefabs/Props/Area2AutoCrusherTrap.prefab";
        GameObject crusherPrefab = PrefabUtility.LoadPrefabContents(crusherPath);
        try
        {
            SerializedObject crusher = new SerializedObject(crusherPrefab.GetComponent<AutoCrusherTrap>());
            crusher.FindProperty("slamDuration").floatValue = 0.2f;
            crusher.FindProperty("retractDuration").floatValue = 1.8f;
            crusher.FindProperty("idlePause").floatValue = 1f;
            crusher.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(crusherPrefab, crusherPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(crusherPrefab); }
        for (int room = 1; room <= 3; room++)
        {
            Scene scene = EditorSceneManager.OpenScene($"Assets/Scenes/Area2-{room}.unity", OpenSceneMode.Single);
            Tilemap props = Objects<Tilemap>(scene).Single(m => m.name == "Props");
            Tilemap solid = Objects<Tilemap>(scene).Single(m => m.name == "SolidPlatforms");
            Tilemap thin = Objects<Tilemap>(scene).Single(m => m.name == "ThinPlatforms");
            foreach (Transform marker in Objects<Transform>(scene).Where(t => t.name.StartsWith("TODO_")).ToArray())
                if (marker != null) UnityEngine.Object.DestroyImmediate(marker.gameObject);
            if (room == 2) ConfigureReturnRoute(scene, solid, props);
            if (room == 3)
            {
                foreach (int y in new[] { 11, 7, 3 })
                for (int x = 12; x <= 15; x++)
                {
                    thin.SetTile(new Vector3Int(x, y), null);
                    props.SetTile(new Vector3Int(x, y), AssetDatabase.LoadAssetAtPath<PropTile>(ElectricTilePath));
                }
                // The other two descent ledges remain the existing normal thin
                // platforms. Safe landing space beside each live strip remains.
            }
            int humidifierX = room == 1 ? 30 : room == 2 ? 0 : 37;
            props.SetTile(new Vector3Int(humidifierX, 1), AssetDatabase.LoadAssetAtPath<PropTile>(HumidifierTilePath));
            var spawner = props.GetComponent<PropTilemapSpawner>();
            spawner.SendMessage("SyncCellList");
            SerializedObject overrides = new SerializedObject(spawner);
            SerializedProperty cells = overrides.FindProperty("cellOverrides");
            for (int i = 0; i < cells.arraySize; i++)
            {
                var item = cells.GetArrayElementAtIndex(i);
                string prop = item.FindPropertyRelative("propName").stringValue;
                Vector3Int cell = item.FindPropertyRelative("cell").vector3IntValue;
                if (room == 2 && prop == "ConveyorBelt") SetConnection(item, "familiarity_crushers", false);
                if (room == 2 && cell == new Vector3Int(1, 1))
                {
                    SetConnection(item, "familiarity_crushers", false);
                    item.FindPropertyRelative("exitScene").stringValue = "Area2-3";
                    item.FindPropertyRelative("exitToLeft").boolValue = true;
                }
            }
            overrides.ApplyModifiedPropertiesWithoutUndo();
            foreach (Tilemap map in Objects<Tilemap>(scene))
            {
                map.CompressBounds();
                map.RefreshAllTiles();
                TilemapCollider2D collider = map.GetComponent<TilemapCollider2D>();
                if (collider != null) collider.ProcessTilemapChanges();
            }
            foreach (Component component in Objects<Component>(scene))
                if (component != null && PrefabUtility.IsPartOfPrefabInstance(component))
                    PrefabUtility.RecordPrefabInstancePropertyModifications(component);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
    }

    private static void ConfigureReturnRoute(Scene scene, Tilemap solid, Tilemap props)
    {
        TileBase floor = AssetDatabase.LoadAssetAtPath<TileBase>("Assets/Tiles/Factory/Platforms/TileCenter.asset");
        PropTile door = AssetDatabase.LoadAssetAtPath<PropTile>(MechanicAssetBuilder.DoorTilePath);
        props.SetTile(new Vector3Int(32, 1), null);
        props.SetTile(new Vector3Int(1, 1), door);
        props.SetTile(new Vector3Int(1, 5), door);
        for (int x = -2; x <= 0; x++) solid.SetTile(new Vector3Int(x, 0), floor);
        for (int y = 1; y <= 3; y++)
        {
            solid.SetTile(new Vector3Int(0, y), null);
            solid.SetTile(new Vector3Int(32, y), floor);
        }
        for (int x = 0; x <= 8; x++)
        {
            solid.SetTile(new Vector3Int(x, 4), floor);
            solid.SetTile(new Vector3Int(x, 7), null);
            solid.SetTile(new Vector3Int(x, 8), floor);
        }
        solid.SetTile(new Vector3Int(0, 8), floor);
        for (int x = 11; x <= 24; x++)
        {
            solid.SetTile(new Vector3Int(x, 0), null);
            props.SetTile(new Vector3Int(x, 0), AssetDatabase.LoadAssetAtPath<PropTile>(ConveyorTilePath));
        }
        Vector3 spawn = new Vector3(4f, 6f);
        Objects<SoftBodyPlayer>(scene).Single().transform.position = spawn;
        Objects<CameraFollowProxy>(scene).Single().transform.position = spawn;
        foreach (Transform marker in Objects<Transform>(scene))
        {
            if (marker.name == "Spawn") marker.position = spawn;
            if (marker.name == "Exit" || marker.name == "ExitDoor_MechanicsPlate") marker.position = new Vector3(0.5f, 2f);
            if (marker.name == "ConveyorCorridor_MechanicallyEmpty") marker.name = "ConveyorCorridor";
        }
    }

    private static void SetConnection(SerializedProperty item, string id, bool active)
    {
        item.FindPropertyRelative("connectionId").stringValue = id;
        item.FindPropertyRelative("connectionMode").enumValueIndex = (int)ConnectionMode.Toggle;
        item.FindPropertyRelative("initialActive").boolValue = active;
    }

    public static void ValidateBatch()
    {
        Sprite[] frames = AssetDatabase.LoadAllAssetsAtPath(ArtPath).OfType<Sprite>().OrderBy(s => s.name).ToArray();
        Require(frames.Length == 8, "Missing conveyor frames.");
        for (int i = 0; i < 8; i++) Require(frames[i].rect == new Rect(i * 32, 0, 32, 32) && frames[i].pixelsPerUnit == 32f, "Incorrect sprite slice.");
        Tilemap palette = AssetDatabase.LoadAssetAtPath<GameObject>(PalettePath).GetComponentInChildren<Tilemap>();
        foreach (string path in new[] { ConveyorTilePath, ElectricTilePath, HumidifierTilePath })
        {
            PropTile tile = AssetDatabase.LoadAssetAtPath<PropTile>(path);
            Require(tile != null && tile.prefab != null, "Missing tile/prefab " + path);
            Require(Cells(palette).Any(c => palette.GetTile(c) == tile), "Missing palette entry " + path);
        }
        for (int room = 1; room <= 3; room++)
        {
            Scene scene = EditorSceneManager.OpenScene($"Assets/Scenes/Area2-{room}.unity", OpenSceneMode.Single);
            Tilemap props = Objects<Tilemap>(scene).Single(m => m.name == "Props");
            var propTiles = Cells(props).Where(props.HasTile).Select(c => (cell: c, tile: props.GetTile<PropTile>(c))).ToArray();
            Require(propTiles.Count(t => t.tile.prefab.GetComponent<Humidifier>() != null) == 1, "Expected one exit humidifier.");
            Require(!Objects<Transform>(scene).Any(t => t.name.StartsWith("TODO_")), "Old mechanic placeholders remain.");
            if (room == 2)
            {
                Require(propTiles.Count(t => t.tile.prefab.GetComponent<ConveyorBelt>() != null) == 14, "Expected 14 belt blocks.");
                Require(props.GetTile(new Vector3Int(1, 1)) != null && props.GetTile(new Vector3Int(32, 1)) == null, "Wrong return-route exit.");
                Require(Objects<SoftBodyPlayer>(scene).Single().transform.position == new Vector3(4, 6), "Entry must be on upper ledge.");
            }
            if (room == 3)
            {
                var live = propTiles.Where(t => t.tile.prefab.GetComponent<ElectricPlatform>() != null).Select(t => t.cell).ToArray();
                Require(live.Length == 12 && live.Select(c => c.y).Distinct().OrderBy(y => y).SequenceEqual(new[] { 3, 7, 11 }), "Descent must alternate electric and normal ledges.");
                Tilemap thin = Objects<Tilemap>(scene).Single(m => m.name == "ThinPlatforms");
                foreach (int y in new[] { 5, 9, 13 })
                for (int x = 4; x <= 21; x++) Require(thin.HasTile(new Vector3Int(x, y)), "Safe ledge was removed.");
                Require(thin.HasTile(new Vector3Int(18, 3)), "Switch must retain a safe landing.");
            }
        }
        Debug.Log("[Area2Mechanics] Sprite slicing, prefabs, palettes, return route and alternating ledges passed.");
    }

    private static System.Collections.Generic.IEnumerable<T> Objects<T>(Scene scene) where T : Component =>
        scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<T>(true));
    private static System.Collections.Generic.IEnumerable<Vector3Int> Cells(Tilemap map)
    {
        foreach (Vector3Int cell in map.cellBounds.allPositionsWithin) yield return cell;
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("[Area2Mechanics] " + message);
    }
}
