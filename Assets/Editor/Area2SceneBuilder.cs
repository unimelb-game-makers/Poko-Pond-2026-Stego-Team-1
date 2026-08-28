using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

/// <summary>
/// Deterministic editor-only builder for the Area 2 state tutorial greybox.
///
/// The builder always starts from Sandbox.unity, saves that copy as Area2.unity,
/// clears Sandbox-specific map content, and then repaints the level through the
/// existing tilemap/PropTile architecture. Running it repeatedly produces the
/// same scene structure and cell layout.
/// </summary>
public static class Area2SceneBuilder
{
    private const string SandboxScenePath = "Assets/Scenes/Sandbox.unity";
    private const string Area2ScenePath = "Assets/Scenes/Area2.unity";
    private const string Area2RootName = "Area2";
    private const string Area2DialoguePath = "Assets/Dialogues/Area2TutorialDialogue.asset";
    private const string GridPrefabPath = "Assets/Prefabs/Core/Grid.prefab";
    private const string PlayerPrefabPath = "Assets/Prefabs/Characters/Player.prefab";
    private const string NpcPrefabPath = "Assets/Prefabs/Characters/NPC.prefab";

    private const string SolidTilemapName = "SolidPlatforms";
    private const string ThinTilemapName = "ThinPlatforms";
    private const string PropsTilemapName = "Props";

    private const float Area2CameraCenterX = 52f;
    private const float Area2BackgroundWidth = 220f;

    private static readonly Vector3Int IntroFreezerCell = new Vector3Int(14, 1, 0);
    private static readonly Vector3Int IntroHeaterCell = new Vector3Int(20, 1, 0);
    private static readonly Vector3Int IntroPlateCell = new Vector3Int(9, 1, 0);

    private static readonly Vector3Int FamiliarityPlateCell = new Vector3Int(59, 1, 0);
    private static readonly Vector3Int FamiliarityFreezerCell = new Vector3Int(61, 1, 0);
    private static readonly Vector3Int FamiliarityCrusherOneCell = new Vector3Int(44, 2, 0);
    private static readonly Vector3Int FamiliarityCrusherTwoCell = new Vector3Int(50, 2, 0);
    private static readonly Vector3Int FamiliarityCrusherThreeCell = new Vector3Int(56, 2, 0);

    private static readonly Vector3Int ChallengeHeaterCell = new Vector3Int(89, 1, 0);
    private static readonly Vector3Int ChallengeBlowerCell = new Vector3Int(93, 1, 0);
    private static readonly Vector3Int ChallengeFreezerCell = new Vector3Int(70, 14, 0);

    private static readonly string[] IntroductionTodos =
    {
        "TODO_Door_Entry",
        "TODO_Door",
        "TODO_Humidifier",
    };

    private static readonly string[] FamiliarityTodos =
    {
        "TODO_ConveyorBelt",
        "TODO_Door_Entry",
        "TODO_Door",
        "TODO_Humidifier",
    };

    private static readonly string[] ChallengeTodos =
    {
        "TODO_Door_Entry",
        "TODO_Door_Divider",
        "TODO_ElectrifiedPlatform",
        "TODO_ElectrifiedPlatform_2",
        "TODO_ElectrifiedPlatform_3",
        "TODO_ElectrifiedPlatform_4",
        "TODO_ElectrifiedPlatform_5",
        "TODO_Switch",
        "TODO_Door",
        "TODO_Humidifier",
    };

    private sealed class PropPlacement
    {
        public Vector3Int Cell;
        public PropTile Tile;
        public string ConnectionId;
        public ConnectionMode ConnectionMode;
        public bool InitialActive;
        public bool RequirePlayerState;
        public PlayerBodyState RequiredPlayerState;
        public bool OverrideBlowerSettings;
        public Vector2 BlowerDirection;
        public float BlowerStrength;
        public float BlowerRange;
        public float BlowerWidth;
    }

    private sealed class CellOverrideSnapshot
    {
        public Vector3Int Cell;
        public string PropName;
        public string ConnectionId;
        public int ConnectionMode;
        public bool InitialActive;
        public bool HasPlayerStateFields;
        public bool RequirePlayerState;
        public int RequiredPlayerState;
        public bool OverrideBlowerSettings;
        public Vector2 BlowerDirection;
        public float BlowerStrength;
        public bool HasBlowerRangeFields;
        public float BlowerRange;
        public float BlowerWidth;
    }

    [MenuItem("Tools/Poko Pond/Area 2/Build From Sandbox")]
    private static void BuildArea2MenuItem()
    {
        BuildArea2();
    }

    /// <summary>
    /// Batch-mode entry point. Example:
    /// Unity -batchmode -quit -projectPath ... -executeMethod Area2SceneBuilder.BuildArea2Batch
    /// </summary>
    public static void BuildArea2Batch()
    {
        BuildArea2();
    }

    /// <summary>
    /// Builds Assets/Scenes/Area2.unity from the committed Sandbox scene.
    /// </summary>
    public static bool BuildArea2()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
        {
            Debug.LogWarning("[Area2SceneBuilder] Build cancelled because the current scene was not saved.");
            return false;
        }

        Scene sandbox = EditorSceneManager.OpenScene(SandboxScenePath, OpenSceneMode.Single);
        if (!sandbox.IsValid())
            throw new InvalidOperationException("[Area2SceneBuilder] Could not open " + SandboxScenePath + ".");

        if (!EditorSceneManager.SaveScene(sandbox, Area2ScenePath, false))
            throw new InvalidOperationException("[Area2SceneBuilder] Could not create " + Area2ScenePath + ".");

        Scene area2 = EditorSceneManager.GetActiveScene();
        if (!area2.IsValid() || !string.Equals(area2.path, Area2ScenePath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("[Area2SceneBuilder] SaveScene did not make Area2 the active scene.");

        ClearSandboxContent(area2);

        Tilemap solidPlatforms = FindTilemap(area2, SolidTilemapName);
        Tilemap thinPlatforms = FindTilemap(area2, ThinTilemapName);
        Tilemap props = FindTilemap(area2, PropsTilemapName);
        if (solidPlatforms == null || thinPlatforms == null || props == null)
        {
            throw new InvalidOperationException(
                "[Area2SceneBuilder] Sandbox scaffolding is incomplete. Required tilemaps: " +
                SolidTilemapName + ", " + ThinTilemapName + ", " + PropsTilemapName + ".");
        }

        PropTilemapSpawner spawner = props.GetComponent<PropTilemapSpawner>();
        if (spawner == null)
        {
            spawner = Undo.AddComponent<PropTilemapSpawner>(props.gameObject);
            EditorUtility.SetDirty(spawner);
        }

        GameObject area2Root = CreateArea2Markers(area2, props);
        ConfigureScaffolding(area2, area2Root);
        BuildGreybox(solidPlatforms, thinPlatforms);
        BuildProps(props, spawner);
        ConfigureTutorialNpc(area2, area2Root.transform.Find("Introduction").gameObject);
        ConfigureTodoMarkers(area2, area2Root);

        AddArea2ToBuildSettings();

        EditorSceneManager.MarkSceneDirty(area2);
        if (!EditorSceneManager.SaveScene(area2))
            throw new InvalidOperationException("[Area2SceneBuilder] Could not save " + Area2ScenePath + ".");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        List<string> validationErrors = CollectValidationErrors(area2, true);
        if (validationErrors.Count > 0)
        {
            LogValidationErrors(validationErrors);
            throw new InvalidOperationException("[Area2SceneBuilder] Generated Area2 failed validation.");
        }

        Debug.Log("[Area2SceneBuilder] Built deterministic Area 2 scene at " + Area2ScenePath + ".");
        return true;
    }

    [MenuItem("Tools/Poko Pond/Area 2/Validate Scene")]
    private static void ValidateArea2MenuItem()
    {
        ValidateArea2Batch();
    }

    /// <summary>
    /// Strict batch-mode validation entry point. Throws with detailed errors on failure.
    /// </summary>
    public static void ValidateArea2Batch()
    {
        if (!GetLoadedArea2Scene().IsValid())
            EditorSceneManager.OpenScene(Area2ScenePath, OpenSceneMode.Single);
        if (!ValidateArea2Scene())
            throw new InvalidOperationException("[Area2SceneBuilder] Area2 validation failed. See the detailed errors above.");
    }

    /// <summary>
    /// Validates the loaded Area2 scene, including the completed state-aware prop contract.
    /// </summary>
    public static bool ValidateArea2Scene()
    {
        Scene area2 = GetLoadedArea2Scene();
        if (!area2.IsValid())
        {
            Debug.LogError("[Area2SceneBuilder] Area2 is not loaded. Open " + Area2ScenePath + " before validating.");
            return false;
        }

        List<string> errors = CollectValidationErrors(area2, true);
        if (errors.Count == 0)
        {
            Debug.Log("[Area2SceneBuilder] Area2 validation passed.");
            return true;
        }

        LogValidationErrors(errors);
        return false;
    }

    private static void ClearSandboxContent(Scene scene)
    {
        // The Sandbox scene has a hand-authored non-tilemap Props group containing
        // old prop prefab instances. Preserve the Grid/Props tilemap architecture.
        foreach (GameObject go in FindSceneObjects<GameObject>(scene).ToArray())
        {
            if (!string.Equals(go.name, PropsTilemapName, StringComparison.OrdinalIgnoreCase))
                continue;
            if (go.GetComponent<Tilemap>() != null)
                continue;
            if (go.transform.parent != null)
                continue;
            UnityEngine.Object.DestroyImmediate(go);
        }

        foreach (Tilemap tilemap in FindSceneObjects<Tilemap>(scene))
        {
            tilemap.ClearAllTiles();
            tilemap.CompressBounds();
            RegenerateTilemapColliderGeometry(tilemap);
            EditorUtility.SetDirty(tilemap);
            RecordPrefabOverride(tilemap);
        }

        foreach (GameObject go in FindSceneObjects<GameObject>(scene).ToArray())
        {
            if (!string.Equals(go.name, Area2RootName, StringComparison.Ordinal))
                continue;
            UnityEngine.Object.DestroyImmediate(go);
        }
    }

    private static GameObject CreateArea2Markers(Scene scene, Tilemap props)
    {
        GameObject area2 = NewSceneObject(scene, Area2RootName, null, Vector3.zero);

        GameObject introduction = NewSceneObject(scene, "Introduction", area2.transform, new Vector3(15f, 0f, 0f));
        GameObject familiarity = NewSceneObject(scene, "Familiarity", area2.transform, new Vector3(46f, 0f, 0f));
        GameObject challenge = NewSceneObject(scene, "Challenge", area2.transform, new Vector3(82f, 0f, 0f));

        NewMarker(scene, introduction.transform, "Introduction_RoomMarker", new Vector3(15f, 5f, 0f));
        NewMarker(scene, familiarity.transform, "Familiarity_RoomMarker", new Vector3(46f, 6f, 0f));
        NewMarker(scene, challenge.transform, "Challenge_RoomMarker", new Vector3(82f, 15f, 0f));

        NewMarker(scene, introduction.transform, "Freezer_SolidChanger", CellWorld(props, IntroFreezerCell));
        NewMarker(scene, introduction.transform, "Heater_LiquidChanger", CellWorld(props, IntroHeaterCell));
        NewMarker(scene, introduction.transform, "PressurePlate_SolidOnly", CellWorld(props, IntroPlateCell));
        NewMarker(scene, familiarity.transform, "Freezer_SolidChanger", CellWorld(props, FamiliarityFreezerCell));
        NewMarker(scene, familiarity.transform, "ConveyorCorridor_MechanicallyEmpty", new Vector3(49f, 1.2f, 0f));
        NewMarker(scene, familiarity.transform, "AutomaticCrushers_SolidOnlyPlate", CellWorld(props, FamiliarityPlateCell));
        NewMarker(scene, familiarity.transform, "AutomaticCrusher_01", CellWorld(props, FamiliarityCrusherOneCell));
        NewMarker(scene, familiarity.transform, "AutomaticCrusher_02", CellWorld(props, FamiliarityCrusherTwoCell));
        NewMarker(scene, familiarity.transform, "AutomaticCrusher_03", CellWorld(props, FamiliarityCrusherThreeCell));
        NewMarker(scene, challenge.transform, "Blower_Upward", CellWorld(props, ChallengeBlowerCell));
        NewMarker(scene, challenge.transform, "RetryHeater_LiquidChanger", CellWorld(props, ChallengeHeaterCell));
        NewMarker(scene, challenge.transform, "UpperLanding_Freezer", CellWorld(props, ChallengeFreezerCell));

        NewMarker(scene, area2.transform, "Area2_Start", new Vector3(4f, 1.5f, 0f));
        NewMarker(scene, area2.transform, "Area2_End", new Vector3(103f, 1.5f, 0f));
        return area2;
    }

    private static void ConfigureTodoMarkers(Scene scene, GameObject area2)
    {
        GameObject introduction = area2.transform.Find("Introduction").gameObject;
        GameObject familiarity = area2.transform.Find("Familiarity").gameObject;
        GameObject challenge = area2.transform.Find("Challenge").gameObject;

        NewVisualTodoMarker(scene, introduction.transform, "TODO_Door_Entry", new Vector3(1f, 2.5f, 0f), "TODO DOOR\nENTRY", new Color(0.8f, 0.35f, 1f));
        NewVisualTodoMarker(scene, introduction.transform, "TODO_Door", new Vector3(29f, 2.5f, 0f), "TODO DOOR\nEXIT", new Color(0.8f, 0.35f, 1f));
        NewVisualTodoMarker(scene, introduction.transform, "TODO_Humidifier", new Vector3(26f, 5.25f, 0f), "TODO\nHUMIDIFIER", new Color(0.2f, 0.9f, 1f));

        Sprite conveyorSprite = LoadRequiredSprite("Assets/Art/Environment/Platforms/Factory/factory_tile.png");
        NewVisualSpriteStrip(scene, familiarity.transform, "TODO_ConveyorBelt_Visual", 41, 57, 1.08f, conveyorSprite, new Color(0.25f, 0.85f, 1f, 0.8f));
        NewVisualTodoMarker(scene, familiarity.transform, "TODO_ConveyorBelt", new Vector3(49f, 1.45f, 0f), "TODO\nCONVEYOR", new Color(0.25f, 0.95f, 1f));
        NewCrusherSuspensionVisual(scene, familiarity.transform, 44f, conveyorSprite);
        NewCrusherSuspensionVisual(scene, familiarity.transform, 50f, conveyorSprite);
        NewCrusherSuspensionVisual(scene, familiarity.transform, 56f, conveyorSprite);
        NewVisualTodoMarker(scene, familiarity.transform, "TODO_Door_Entry", new Vector3(30f, 2.5f, 0f), "TODO DOOR\nENTRY", new Color(0.8f, 0.35f, 1f));
        NewVisualTodoMarker(scene, familiarity.transform, "TODO_Door", new Vector3(62f, 2.5f, 0f), "TODO DOOR\nEXIT", new Color(0.8f, 0.35f, 1f));
        NewVisualTodoMarker(scene, familiarity.transform, "TODO_Humidifier", new Vector3(33f, 5.9f, 0f), "TODO\nHUMIDIFIER", new Color(0.2f, 0.9f, 1f));

        Sprite warningSprite = LoadRequiredSprite("Assets/Art/Environment/Platforms/Factory/factory_tile_alter.png");
        NewVisualTodoMarker(scene, challenge.transform, "TODO_Door_Entry", new Vector3(64f, 2.5f, 0f), "TODO DOOR\nENTRY", new Color(0.8f, 0.35f, 1f));
        NewVisualTodoMarker(scene, challenge.transform, "TODO_Door_Divider", new Vector3(86f, 2.5f, 0f), "TODO DOOR\nDIVIDER", new Color(0.8f, 0.35f, 1f));
        NewVisualTodoMarker(scene, challenge.transform, "TODO_ElectrifiedPlatform", new Vector3(76f, 11.45f, 0f), "TODO\nELECTRIC", new Color(0.7f, 0.35f, 1f), warningSprite);
        NewVisualTodoMarker(scene, challenge.transform, "TODO_ElectrifiedPlatform_2", new Vector3(77f, 9.45f, 0f), "TODO\nELECTRIC", new Color(1f, 0.95f, 0.15f), warningSprite);
        NewVisualTodoMarker(scene, challenge.transform, "TODO_ElectrifiedPlatform_3", new Vector3(76f, 7.45f, 0f), "TODO\nELECTRIC", new Color(0.7f, 0.35f, 1f), warningSprite);
        NewVisualTodoMarker(scene, challenge.transform, "TODO_ElectrifiedPlatform_4", new Vector3(77f, 5.45f, 0f), "TODO\nELECTRIC", new Color(1f, 0.95f, 0.15f), warningSprite);
        NewVisualTodoMarker(scene, challenge.transform, "TODO_ElectrifiedPlatform_5", new Vector3(76f, 3.45f, 0f), "TODO\nELECTRIC", new Color(0.7f, 0.35f, 1f), warningSprite);
        NewVisualTodoMarker(scene, challenge.transform, "TODO_Switch", new Vector3(82f, 3.55f, 0f), "TODO\nSWITCH", new Color(1f, 0.75f, 0.2f));
        NewVisualTodoMarker(scene, challenge.transform, "TODO_Door", new Vector3(100f, 2.5f, 0f), "TODO DOOR\nEXIT", new Color(0.8f, 0.35f, 1f));
        NewVisualTodoMarker(scene, challenge.transform, "TODO_Humidifier", new Vector3(103f, 2f, 0f), "TODO\nHUMIDIFIER", new Color(0.2f, 0.9f, 1f));
    }

    private static void ConfigureScaffolding(Scene scene, GameObject area2Root)
    {
        GameObject player = FindSceneObjects<GameObject>(scene)
            .FirstOrDefault(go => SafeCompareTag(go, "Player"));
        if (player == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefabPath);
            if (prefab != null)
            {
                player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
                if (player != null)
                    SceneManager.MoveGameObjectToScene(player, scene);
            }
        }

        if (player == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Sandbox has no Player prefab instance.");

        player.name = "Player";
        player.transform.position = new Vector3(4f, 2f, 0f);
        RecordPrefabOverride(player.transform);

        CameraFollowProxy proxy = FindSceneObjects<CameraFollowProxy>(scene).FirstOrDefault();
        if (proxy == null)
        {
            GameObject proxyObject = NewSceneObject(scene, "CameraFollowProxy", null, new Vector3(4f, 2f, 0f));
            proxy = proxyObject.AddComponent<CameraFollowProxy>();
        }
        proxy.name = "CameraFollowProxy";
        proxy.transform.position = new Vector3(4f, 2f, 0f);
        EditorUtility.SetDirty(proxy);

        if (!FindSceneObjects<Camera>(scene).Any(camera => SafeCompareTag(camera.gameObject, "MainCamera")))
            throw new InvalidOperationException("[Area2SceneBuilder] Sandbox is missing its Main Camera.");
        if (!FindSceneObjects<DialogueManager>(scene).Any())
            throw new InvalidOperationException("[Area2SceneBuilder] Sandbox is missing DialogueManager scaffolding.");
        if (!FindSceneObjects<GameStateManager>(scene).Any())
            throw new InvalidOperationException("[Area2SceneBuilder] Sandbox is missing GameStateManager scaffolding.");

        GameObject vcSideScroll = FindSceneObjects<GameObject>(scene)
            .FirstOrDefault(go => string.Equals(go.name, "VC_SideScroll", StringComparison.Ordinal));
        if (vcSideScroll == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Sandbox is missing VC_SideScroll camera scaffolding.");

        ConfigureCameraBoundsAndBackgrounds(scene, vcSideScroll);

        // Keep the generated root referenced in the scene hierarchy even though
        // the actual tilemaps remain under the cloned Grid prefab.
        EditorUtility.SetDirty(area2Root);
    }

    private static void ConfigureCameraBoundsAndBackgrounds(Scene scene, GameObject vcSideScroll)
    {
        GameObject boundsObject = FindSceneObjects<GameObject>(scene)
            .FirstOrDefault(go => string.Equals(go.name, "Area2CameraBounds", StringComparison.Ordinal));
        if (boundsObject == null)
            boundsObject = NewSceneObject(scene, "Area2CameraBounds", null, new Vector3(Area2CameraCenterX, 8f, 0f));
        boundsObject.transform.position = new Vector3(Area2CameraCenterX, 8f, 0f);

        BoxCollider2D bounds = boundsObject.GetComponent<BoxCollider2D>();
        if (bounds == null)
            bounds = boundsObject.AddComponent<BoxCollider2D>();
        bounds.isTrigger = true;
        bounds.size = new Vector2(116f, 24f);
        EditorUtility.SetDirty(bounds);

        bool assignedBounds = false;
        foreach (MonoBehaviour component in vcSideScroll.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null)
                continue;
            SerializedObject serializedComponent = new SerializedObject(component);
            SerializedProperty boundingShape = serializedComponent.FindProperty("m_BoundingShape2D");
            if (boundingShape == null)
                continue;
            boundingShape.objectReferenceValue = bounds;
            serializedComponent.ApplyModifiedProperties();
            EditorUtility.SetDirty(component);
            RecordPrefabOverride(component);
            assignedBounds = true;
        }

        if (!assignedBounds)
            throw new InvalidOperationException("[Area2SceneBuilder] VC_SideScroll has no Cinemachine bounding-shape property.");

        // The Sandbox art only spans its original short room. Stretch each
        // parallax layer far enough that camera-relative drift cannot expose an
        // empty edge while traversing the full Area 2 room sequence.
        foreach (ParallaxBackground parallax in FindSceneObjects<ParallaxBackground>(scene))
        {
            SpriteRenderer renderer = parallax.GetComponentInChildren<SpriteRenderer>(true);
            if (renderer == null || renderer.sprite == null)
                continue;
            float spriteWidth = Mathf.Max(0.01f, renderer.sprite.bounds.size.x);
            Vector3 scale = parallax.transform.localScale;
            scale.x = Area2BackgroundWidth / spriteWidth;
            parallax.transform.localScale = scale;
            Vector3 position = parallax.transform.position;
            position.x = Area2CameraCenterX;
            parallax.transform.position = position;
            EditorUtility.SetDirty(parallax.transform);
            RecordPrefabOverride(parallax.transform);
        }
    }

    private static DialogueData CreateOrUpdateDialogue()
    {
        DialogueData dialogue = AssetDatabase.LoadAssetAtPath<DialogueData>(Area2DialoguePath);
        if (dialogue == null)
        {
            dialogue = ScriptableObject.CreateInstance<DialogueData>();
            AssetDatabase.CreateAsset(dialogue, Area2DialoguePath);
        }

        dialogue.lines = new[]
        {
            new DialogueLine
            {
                speakerName = "Area 2 Guide",
                text = "Welcome to Area 2. Here, water can change between liquid and solid ice.",
            },
            new DialogueLine
            {
                speakerName = "Area 2 Guide",
                text = "The freezer makes you solid. The heater changes you back into liquid.",
            },
            new DialogueLine
            {
                speakerName = "Area 2 Guide",
                text = "Some pressure plates only respond while you are solid. Try the ice form before moving on.",
            },
            new DialogueLine
            {
                speakerName = "Area 2 Guide",
                text = "The next rooms combine state changes with moving hazards. Good luck!",
            },
        };

        EditorUtility.SetDirty(dialogue);
        AssetDatabase.SaveAssets();
        return dialogue;
    }

    private static void ConfigureTutorialNpc(Scene scene, GameObject introduction)
    {
        DialogueData dialogue = CreateOrUpdateDialogue();
        GameObject npc = FindSceneObjects<DialogueTrigger>(scene)
            .Select(trigger => trigger.gameObject)
            .FirstOrDefault();

        if (npc == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(NpcPrefabPath);
            if (prefab == null)
                throw new InvalidOperationException("[Area2SceneBuilder] Could not load " + NpcPrefabPath + ".");
            npc = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            if (npc == null)
                throw new InvalidOperationException("[Area2SceneBuilder] Could not instantiate the NPC prefab.");
            SceneManager.MoveGameObjectToScene(npc, scene);
        }

        npc.name = "Introduction_TutorialNPC";
        npc.transform.SetParent(introduction.transform, true);
        npc.transform.position = new Vector3(5f, 1.55f, 0f);
        npc.transform.localScale = Vector3.one * 0.5f;
        RecordPrefabOverride(npc.transform);

        DialogueTrigger triggerComponent = npc.GetComponent<DialogueTrigger>();
        if (triggerComponent == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Tutorial NPC has no DialogueTrigger component.");

        SerializedObject serializedTrigger = new SerializedObject(triggerComponent);
        SerializedProperty dialogueProperty = serializedTrigger.FindProperty("dialogueData");
        if (dialogueProperty == null)
            throw new InvalidOperationException("[Area2SceneBuilder] DialogueTrigger.dialogueData was not found.");
        dialogueProperty.objectReferenceValue = dialogue;
        serializedTrigger.ApplyModifiedProperties();
        EditorUtility.SetDirty(triggerComponent);
        RecordPrefabOverride(triggerComponent);
    }

    private static void BuildGreybox(Tilemap solidPlatforms, Tilemap thinPlatforms)
    {
        TileBase solidCenter = LoadRequiredAsset<TileBase>("Assets/Tiles/Factory/Platforms/TileCenter.asset");
        TileBase solidLeft = LoadRequiredAsset<TileBase>("Assets/Tiles/Factory/Platforms/TileEdgeLeft.asset");
        TileBase solidRight = LoadRequiredAsset<TileBase>("Assets/Tiles/Factory/Platforms/TileEdgeRight.asset");
        TileBase thinCenter = LoadRequiredAsset<TileBase>("Assets/Tiles/Factory/Platforms/ThinTileCenter.asset");
        TileBase thinLeft = LoadRequiredAsset<TileBase>("Assets/Tiles/Factory/Platforms/ThinTileLeft.asset");
        TileBase thinRight = LoadRequiredAsset<TileBase>("Assets/Tiles/Factory/Platforms/ThinTileRight.asset");

        solidPlatforms.ClearAllTiles();
        thinPlatforms.ClearAllTiles();

        // Keep the unfinished doors physically open: one continuous floor lets
        // the player traverse the three room silhouettes in document order.
        for (int x = 0; x <= 104; x++)
        {
            TileBase tile = x == 0 ? solidLeft : (x == 104 ? solidRight : solidCenter);
            solidPlatforms.SetTile(new Vector3Int(x, 0, 0), tile);
        }

        // INTRODUCTION (x 0..29): compact enclosed tutorial corridor from the
        // first Area 2 diagram. Door-frame gaps remain walkable placeholders.
        PaintSolidRect(solidPlatforms, 0, 6, 30, 1, solidCenter);
        PaintSolidRect(solidPlatforms, 0, 1, 1, 5, solidCenter);
        PaintSolidRect(solidPlatforms, 29, 4, 1, 2, solidCenter);

        // FAMILIARITY (x 30..62): low crusher/conveyor chamber. The overhead
        // structure makes the room read distinctly without creating a hidden
        // collision band close to the player's head.
        PaintSolidRect(solidPlatforms, 30, 7, 33, 1, solidCenter);
        PaintSolidRect(solidPlatforms, 30, 4, 1, 3, solidCenter);
        PaintSolidRect(solidPlatforms, 62, 4, 1, 3, solidCenter);

        // CHALLENGE (x 64..100): tall shaft from the document. The player walks
        // through the divider's bottom door gap, rides the fan up its right side,
        // crosses above the divider, then descends the staggered platforms.
        PaintSolidRect(solidPlatforms, 64, 16, 37, 1, solidCenter);
        PaintSolidRect(solidPlatforms, 64, 4, 1, 12, solidCenter);
        PaintSolidRect(solidPlatforms, 100, 4, 1, 12, solidCenter);
        PaintSolidRect(solidPlatforms, 86, 4, 1, 10, solidCenter);

        // The document uses full-width stacked ledges. Their one-way collision
        // lets the player time downward drops while avoiding marked live zones.
        PaintThinSegment(thinPlatforms, 68, 13, 18, thinLeft, thinCenter, thinRight);
        PaintThinSegment(thinPlatforms, 68, 11, 18, thinLeft, thinCenter, thinRight);
        PaintThinSegment(thinPlatforms, 68, 9, 18, thinLeft, thinCenter, thinRight);
        PaintThinSegment(thinPlatforms, 68, 7, 18, thinLeft, thinCenter, thinRight);
        PaintThinSegment(thinPlatforms, 68, 5, 18, thinLeft, thinCenter, thinRight);
        PaintThinSegment(thinPlatforms, 68, 3, 18, thinLeft, thinCenter, thinRight);

        solidPlatforms.CompressBounds();
        thinPlatforms.CompressBounds();
        solidPlatforms.RefreshAllTiles();
        thinPlatforms.RefreshAllTiles();
        RegenerateTilemapColliderGeometry(solidPlatforms);
        RegenerateTilemapColliderGeometry(thinPlatforms);
        EditorUtility.SetDirty(solidPlatforms);
        EditorUtility.SetDirty(thinPlatforms);
        RecordPrefabOverride(solidPlatforms);
        RecordPrefabOverride(thinPlatforms);
    }

    private static void BuildProps(Tilemap props, PropTilemapSpawner spawner)
    {
        PropTile solidChanger = LoadRequiredAsset<PropTile>("Assets/Tiles/Factory/Props/Condenser.asset");
        PropTile liquidChanger = LoadRequiredAsset<PropTile>("Assets/Tiles/Factory/Props/Evaporator.asset");
        PropTile pressurePlate = LoadRequiredAsset<PropTile>("Assets/Tiles/Factory/Props/PressurePlate_PropTile.asset");
        PropTile autoCrusher = LoadRequiredAsset<PropTile>("Assets/Tiles/Props/AutoCrusherTrap_PropTile.asset");
        PropTile blower = LoadRequiredAsset<PropTile>("Assets/Tiles/Factory/Props/Blower_PropTile.asset");

        props.ClearAllTiles();
        List<PropPlacement> placements = new List<PropPlacement>
        {
            new PropPlacement
            {
                Cell = IntroFreezerCell,
                Tile = solidChanger,
                ConnectionId = "",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = true,
            },
            new PropPlacement
            {
                Cell = IntroHeaterCell,
                Tile = liquidChanger,
                ConnectionId = "",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = true,
            },
            new PropPlacement
            {
                Cell = IntroPlateCell,
                Tile = pressurePlate,
                ConnectionId = "intro_door",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = false,
                RequirePlayerState = true,
                RequiredPlayerState = PlayerBodyState.Solid,
            },
            new PropPlacement
            {
                Cell = FamiliarityFreezerCell,
                Tile = solidChanger,
                ConnectionId = "",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = true,
            },
            new PropPlacement
            {
                Cell = FamiliarityPlateCell,
                Tile = pressurePlate,
                ConnectionId = "familiarity_crushers",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = false,
                RequirePlayerState = true,
                RequiredPlayerState = PlayerBodyState.Solid,
            },
            new PropPlacement
            {
                Cell = FamiliarityCrusherOneCell,
                Tile = autoCrusher,
                ConnectionId = "familiarity_crushers",
                ConnectionMode = ConnectionMode.Toggle,
                InitialActive = false,
            },
            new PropPlacement
            {
                Cell = FamiliarityCrusherTwoCell,
                Tile = autoCrusher,
                ConnectionId = "familiarity_crushers",
                ConnectionMode = ConnectionMode.Toggle,
                InitialActive = false,
            },
            new PropPlacement
            {
                Cell = FamiliarityCrusherThreeCell,
                Tile = autoCrusher,
                ConnectionId = "familiarity_crushers",
                ConnectionMode = ConnectionMode.Toggle,
                InitialActive = false,
            },
            new PropPlacement
            {
                Cell = ChallengeBlowerCell,
                Tile = blower,
                ConnectionId = "",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = true,
                OverrideBlowerSettings = true,
                BlowerDirection = Vector2.up,
                BlowerStrength = 34f,
                BlowerRange = 13f,
                BlowerWidth = 3f,
            },
            new PropPlacement
            {
                Cell = ChallengeHeaterCell,
                Tile = liquidChanger,
                ConnectionId = "",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = true,
            },
            new PropPlacement
            {
                Cell = ChallengeFreezerCell,
                Tile = solidChanger,
                ConnectionId = "",
                ConnectionMode = ConnectionMode.Hold,
                InitialActive = true,
            },
        };

        foreach (PropPlacement placement in placements)
            props.SetTile(placement.Cell, placement.Tile);

        props.CompressBounds();
        props.RefreshAllTiles();
        EditorUtility.SetDirty(props);
        RecordPrefabOverride(props);
        ConfigureCellOverrides(spawner, placements);
    }

    private static void ConfigureCellOverrides(PropTilemapSpawner spawner, List<PropPlacement> placements)
    {
        placements = placements
            .OrderBy(placement => placement.Cell.y)
            .ThenBy(placement => placement.Cell.x)
            .ToList();

        SerializedObject serializedSpawner = new SerializedObject(spawner);
        SerializedProperty overrides = serializedSpawner.FindProperty("cellOverrides");
        if (overrides != null)
        {
            overrides.arraySize = 0;
            foreach (PropPlacement placement in placements)
            {
                overrides.InsertArrayElementAtIndex(overrides.arraySize);
                SerializedProperty element = overrides.GetArrayElementAtIndex(overrides.arraySize - 1);
                SetRequiredProperty(element, "propName", placement.Tile.prefab != null ? placement.Tile.prefab.name : placement.Tile.name);
                SetRequiredProperty(element, "cell", placement.Cell);
                SetRequiredProperty(element, "connectionId", placement.ConnectionId);
                SetRequiredProperty(element, "connectionMode", (int)placement.ConnectionMode);
                SetRequiredProperty(element, "initialActive", placement.InitialActive);
                SetOptionalProperty(element, "overrideBlowerSettings", placement.OverrideBlowerSettings);
                SetOptionalProperty(element, "blowerDirection", placement.BlowerDirection);
                SetOptionalProperty(element, "blowerStrength", placement.BlowerStrength);
                SetOptionalProperty(element, "blowerRange", placement.BlowerRange);
                SetOptionalProperty(element, "blowerWidth", placement.BlowerWidth);

                // Mechanics patch contract: exact serialized names are
                // requirePlayerState and requiredPlayerState.
                SetOptionalProperty(element, "requirePlayerState", placement.RequirePlayerState);
                SetOptionalProperty(element, "requiredPlayerState", (int)placement.RequiredPlayerState);
            }

            serializedSpawner.ApplyModifiedProperties();
            EditorUtility.SetDirty(spawner);
            RecordPrefabOverride(spawner);
            return;
        }

        ConfigureCellOverridesByReflection(spawner, placements);
    }

    private static void ConfigureCellOverridesByReflection(PropTilemapSpawner spawner, List<PropPlacement> placements)
    {
        FieldInfo listField = typeof(PropTilemapSpawner).GetField(
            "cellOverrides", BindingFlags.Instance | BindingFlags.NonPublic);
        Type cellOverrideType = typeof(PropTilemapSpawner).GetNestedType(
            "CellOverride", BindingFlags.Public | BindingFlags.NonPublic);
        if (listField == null || cellOverrideType == null)
        {
            throw new InvalidOperationException(
                "[Area2SceneBuilder] PropTilemapSpawner cellOverrides/CellOverride could not be found by reflection.");
        }

        IList list = listField.GetValue(spawner) as IList;
        if (list == null)
            throw new InvalidOperationException("[Area2SceneBuilder] PropTilemapSpawner.cellOverrides is not an IList.");

        list.Clear();
        foreach (PropPlacement placement in placements)
        {
            object entry = Activator.CreateInstance(cellOverrideType);
            SetReflectedField(cellOverrideType, entry, "propName", placement.Tile.prefab != null ? placement.Tile.prefab.name : placement.Tile.name);
            SetReflectedField(cellOverrideType, entry, "cell", placement.Cell);
            SetReflectedField(cellOverrideType, entry, "connectionId", placement.ConnectionId);
            SetReflectedField(cellOverrideType, entry, "connectionMode", placement.ConnectionMode);
            SetReflectedField(cellOverrideType, entry, "initialActive", placement.InitialActive);
            SetReflectedField(cellOverrideType, entry, "overrideBlowerSettings", placement.OverrideBlowerSettings);
            SetReflectedField(cellOverrideType, entry, "blowerDirection", placement.BlowerDirection);
            SetReflectedField(cellOverrideType, entry, "blowerStrength", placement.BlowerStrength);
            SetReflectedField(cellOverrideType, entry, "blowerRange", placement.BlowerRange);
            SetReflectedField(cellOverrideType, entry, "blowerWidth", placement.BlowerWidth);
            SetReflectedField(cellOverrideType, entry, "requirePlayerState", placement.RequirePlayerState);
            SetReflectedField(cellOverrideType, entry, "requiredPlayerState", placement.RequiredPlayerState);
            list.Add(entry);
        }

        EditorUtility.SetDirty(spawner);
        RecordPrefabOverride(spawner);
    }

    private static List<string> CollectValidationErrors(Scene scene, bool requireCompletedMechanicsContract)
    {
        List<string> errors = new List<string>();

        if (!string.Equals(scene.path, Area2ScenePath, StringComparison.OrdinalIgnoreCase))
            errors.Add("Loaded scene path is '" + scene.path + "', expected '" + Area2ScenePath + "'.");

        GameObject area2 = FindDirectSceneObject(scene, Area2RootName);
        if (area2 == null)
        {
            errors.Add("Missing root marker 'Area2'.");
            return errors;
        }

        GameObject introduction = FindDirectChild(area2.transform, "Introduction");
        GameObject familiarity = FindDirectChild(area2.transform, "Familiarity");
        GameObject challenge = FindDirectChild(area2.transform, "Challenge");
        RequireObject(errors, introduction, "Area2/Introduction room root");
        RequireObject(errors, familiarity, "Area2/Familiarity room root");
        RequireObject(errors, challenge, "Area2/Challenge room root");

        if (introduction != null)
        {
            RequireChild(errors, introduction.transform, "Introduction_RoomMarker");
            RequireChild(errors, introduction.transform, "Freezer_SolidChanger");
            RequireChild(errors, introduction.transform, "Heater_LiquidChanger");
            RequireChild(errors, introduction.transform, "PressurePlate_SolidOnly");
            RequireTodoMarkers(errors, introduction.transform, IntroductionTodos);
            RequireChild(errors, introduction.transform, "Introduction_TutorialNPC");
        }

        if (familiarity != null)
        {
            RequireChild(errors, familiarity.transform, "Familiarity_RoomMarker");
            RequireChild(errors, familiarity.transform, "Freezer_SolidChanger");
            RequireChild(errors, familiarity.transform, "ConveyorCorridor_MechanicallyEmpty");
            RequireChild(errors, familiarity.transform, "AutomaticCrushers_SolidOnlyPlate");
            RequireChild(errors, familiarity.transform, "AutomaticCrusher_01");
            RequireChild(errors, familiarity.transform, "AutomaticCrusher_02");
            RequireChild(errors, familiarity.transform, "AutomaticCrusher_03");
            RequireTodoMarkers(errors, familiarity.transform, FamiliarityTodos);
        }

        if (challenge != null)
        {
            RequireChild(errors, challenge.transform, "Challenge_RoomMarker");
            RequireChild(errors, challenge.transform, "Blower_Upward");
            RequireChild(errors, challenge.transform, "RetryHeater_LiquidChanger");
            RequireChild(errors, challenge.transform, "UpperLanding_Freezer");
            RequireTodoMarkers(errors, challenge.transform, ChallengeTodos);
        }

        Tilemap solid = FindTilemap(scene, SolidTilemapName);
        Tilemap thin = FindTilemap(scene, ThinTilemapName);
        Tilemap props = FindTilemap(scene, PropsTilemapName);
        RequireObject(errors, solid, "SolidPlatforms tilemap");
        RequireObject(errors, thin, "ThinPlatforms tilemap");
        RequireObject(errors, props, "Props tilemap");

        if (solid != null && CountTiles(solid) < 180)
            errors.Add("SolidPlatforms has fewer than 180 cells; the three-room silhouette is incomplete.");
        if (thin != null && CountTiles(thin) < 100)
            errors.Add("ThinPlatforms is missing the required descending challenge route.");

        ValidateCompositeColliderBounds(errors, solid, "SolidPlatforms");
        ValidateCompositeColliderBounds(errors, thin, "ThinPlatforms");

        if (solid != null)
        {
            RequireTile(errors, solid, new Vector3Int(10, 6, 0), "Introduction roof");
            RequireTile(errors, solid, new Vector3Int(45, 7, 0), "Familiarity roof");
            RequireTile(errors, solid, new Vector3Int(86, 10, 0), "Challenge divider");
            RequireNoTile(errors, solid, new Vector3Int(29, 2, 0), "Introduction exit doorway");
            RequireNoTile(errors, solid, new Vector3Int(62, 2, 0), "Familiarity exit doorway");
            RequireNoTile(errors, solid, new Vector3Int(86, 2, 0), "Challenge divider doorway");
        }
        if (thin != null)
        {
            RequireTile(errors, thin, new Vector3Int(68, 13, 0), "Challenge top landing left edge");
            RequireTile(errors, thin, new Vector3Int(85, 13, 0), "Challenge top landing right edge");
            RequireTile(errors, thin, new Vector3Int(68, 3, 0), "Challenge bottom landing left edge");
            RequireTile(errors, thin, new Vector3Int(85, 3, 0), "Challenge bottom landing right edge");
        }

        PropTilemapSpawner spawner = props != null ? props.GetComponent<PropTilemapSpawner>() : null;
        RequireObject(errors, spawner, "PropTilemapSpawner on Props tilemap");

        GameObject player = FindSceneObjects<GameObject>(scene).FirstOrDefault(go => SafeCompareTag(go, "Player"));
        RequireObject(errors, player, "Player prefab instance");
        if (player != null && PrefabUtility.GetCorrespondingObjectFromSource<GameObject>(player) == null)
            errors.Add("Player exists but is not linked to a prefab source.");

        DialogueTrigger npcTrigger = FindSceneObjects<DialogueTrigger>(scene)
            .FirstOrDefault(trigger => trigger.gameObject.name == "Introduction_TutorialNPC");
        if (npcTrigger != null && Vector3.Distance(npcTrigger.transform.localScale, Vector3.one * 0.5f) > 0.01f)
            errors.Add("Introduction tutorial NPC scale must remain at the user's reduced 0.5 value.");

        bool mainCameraFound = FindSceneObjects<Camera>(scene).Any(camera => SafeCompareTag(camera.gameObject, "MainCamera"));
        if (!mainCameraFound)
            errors.Add("Missing Main Camera prefab/scaffolding instance.");
        if (!FindSceneObjects<CameraFollowProxy>(scene).Any())
            errors.Add("Missing CameraFollowProxy.");
        if (!FindSceneObjects<DialogueManager>(scene).Any())
            errors.Add("Missing DialogueManager.");
        if (!FindSceneObjects<GameStateManager>(scene).Any())
            errors.Add("Missing GameStateManager.");
        if (!FindSceneObjects<GameObject>(scene).Any(go => string.Equals(go.name, "VC_SideScroll", StringComparison.Ordinal)))
            errors.Add("Missing VC_SideScroll camera scaffolding.");

        DialogueTrigger tutorialTrigger = FindSceneObjects<DialogueTrigger>(scene)
            .FirstOrDefault(trigger => trigger.gameObject.name == "Introduction_TutorialNPC");
        if (tutorialTrigger == null)
        {
            errors.Add("Missing Introduction_TutorialNPC DialogueTrigger instance.");
        }
        else
        {
            SerializedObject serializedTrigger = new SerializedObject(tutorialTrigger);
            SerializedProperty dialogueProperty = serializedTrigger.FindProperty("dialogueData");
            if (dialogueProperty == null || dialogueProperty.objectReferenceValue == null)
                errors.Add("Introduction_TutorialNPC has no DialogueData assigned.");
            else if (AssetDatabase.GetAssetPath(dialogueProperty.objectReferenceValue) != Area2DialoguePath)
                errors.Add("Introduction_TutorialNPC is not assigned to " + Area2DialoguePath + ".");
        }

        if (props != null)
        {
            RequireProp(errors, props, IntroFreezerCell, "Condenser", "Introduction freezer/solid changer");
            RequireProp(errors, props, IntroHeaterCell, "Evaporator", "Introduction heater/liquid changer");
            RequireProp(errors, props, IntroPlateCell, "PressurePlate", "Introduction solid-only pressure plate");
            RequireProp(errors, props, FamiliarityFreezerCell, "Condenser", "Familiarity freezer/solid changer");
            RequireProp(errors, props, FamiliarityPlateCell, "PressurePlate", "Familiarity solid-only plate");
            RequireProp(errors, props, FamiliarityCrusherOneCell, "AutoCrusherTrap", "Familiarity automatic crusher 1");
            RequireProp(errors, props, FamiliarityCrusherTwoCell, "AutoCrusherTrap", "Familiarity automatic crusher 2");
            RequireProp(errors, props, FamiliarityCrusherThreeCell, "AutoCrusherTrap", "Familiarity automatic crusher 3");
            RequireProp(errors, props, ChallengeBlowerCell, "Blower", "Challenge upward blower");
            RequireProp(errors, props, ChallengeHeaterCell, "Evaporator", "Challenge retry heater/liquid changer");
            RequireProp(errors, props, ChallengeFreezerCell, "Condenser", "Challenge upper freezer/solid changer");
        }

        if (spawner != null)
        {
            List<CellOverrideSnapshot> snapshots = ReadCellOverrides(spawner, errors);
            ValidateOverride(errors, snapshots, IntroPlateCell, "intro_door", ConnectionMode.Hold, false, true, PlayerBodyState.Solid, requireCompletedMechanicsContract, "Introduction pressure plate");
            ValidateOverride(errors, snapshots, FamiliarityPlateCell, "familiarity_crushers", ConnectionMode.Hold, false, true, PlayerBodyState.Solid, requireCompletedMechanicsContract, "Familiarity solid-only plate");
            ValidateOverride(errors, snapshots, FamiliarityCrusherOneCell, "familiarity_crushers", ConnectionMode.Toggle, false, false, PlayerBodyState.Liquid, false, "Familiarity automatic crusher 1");
            ValidateOverride(errors, snapshots, FamiliarityCrusherTwoCell, "familiarity_crushers", ConnectionMode.Toggle, false, false, PlayerBodyState.Liquid, false, "Familiarity automatic crusher 2");
            ValidateOverride(errors, snapshots, FamiliarityCrusherThreeCell, "familiarity_crushers", ConnectionMode.Toggle, false, false, PlayerBodyState.Liquid, false, "Familiarity automatic crusher 3");

            CellOverrideSnapshot blower = snapshots.FirstOrDefault(snapshot => snapshot.Cell == ChallengeBlowerCell);
            if (blower == null)
                errors.Add("Challenge blower has no PropTilemapSpawner cell override.");
            else
            {
                if (!blower.OverrideBlowerSettings || blower.BlowerDirection.y < 0.9f || Mathf.Abs(blower.BlowerDirection.x) > 0.1f)
                    errors.Add("Challenge blower override must be enabled and point upward.");
                if (!blower.HasBlowerRangeFields || blower.BlowerRange < 12f || blower.BlowerWidth < 2.5f)
                    errors.Add("Challenge blower must span the tall lift shaft (range >= 12, width >= 2.5).");
            }
        }

        if (requireCompletedMechanicsContract)
        {
            if (!HasPlayerStateCellOverrideContract())
                errors.Add("PropTilemapSpawner.CellOverride is missing exact mechanics fields requirePlayerState and requiredPlayerState.");
            if (!typeof(IPropActivatable).IsAssignableFrom(typeof(AutoCrusherTrap)))
                errors.Add("AutoCrusherTrap does not implement IPropActivatable in the loaded runtime assembly.");
        }

        bool inBuildSettings = EditorBuildSettings.scenes.Any(sceneEntry =>
            string.Equals(sceneEntry.path, Area2ScenePath, StringComparison.OrdinalIgnoreCase) && sceneEntry.enabled);
        if (!inBuildSettings)
            errors.Add("Area2.unity is not enabled in EditorBuildSettings.");

        return errors;
    }

    private static List<CellOverrideSnapshot> ReadCellOverrides(PropTilemapSpawner spawner, List<string> errors)
    {
        List<CellOverrideSnapshot> snapshots = new List<CellOverrideSnapshot>();
        SerializedObject serializedSpawner = new SerializedObject(spawner);
        SerializedProperty overrides = serializedSpawner.FindProperty("cellOverrides");
        if (overrides == null)
        {
            errors.Add("PropTilemapSpawner.cellOverrides could not be read as a serialized list.");
            return snapshots;
        }

        for (int i = 0; i < overrides.arraySize; i++)
        {
            SerializedProperty element = overrides.GetArrayElementAtIndex(i);
            SerializedProperty cell = element.FindPropertyRelative("cell");
            SerializedProperty propName = element.FindPropertyRelative("propName");
            SerializedProperty connectionId = element.FindPropertyRelative("connectionId");
            SerializedProperty connectionMode = element.FindPropertyRelative("connectionMode");
            SerializedProperty initialActive = element.FindPropertyRelative("initialActive");
            if (cell == null || propName == null || connectionId == null || connectionMode == null || initialActive == null)
            {
                errors.Add("PropTilemapSpawner cell override index " + i + " is missing a core serialized field.");
                continue;
            }

            SerializedProperty stateFlag = element.FindPropertyRelative("requirePlayerState");
            SerializedProperty stateValue = element.FindPropertyRelative("requiredPlayerState");
            SerializedProperty blowerFlag = element.FindPropertyRelative("overrideBlowerSettings");
            SerializedProperty blowerDirection = element.FindPropertyRelative("blowerDirection");
            SerializedProperty blowerStrength = element.FindPropertyRelative("blowerStrength");
            SerializedProperty blowerRange = element.FindPropertyRelative("blowerRange");
            SerializedProperty blowerWidth = element.FindPropertyRelative("blowerWidth");

            snapshots.Add(new CellOverrideSnapshot
            {
                Cell = cell.vector3IntValue,
                PropName = propName.stringValue,
                ConnectionId = connectionId.stringValue,
                ConnectionMode = connectionMode.intValue,
                InitialActive = initialActive.boolValue,
                HasPlayerStateFields = stateFlag != null && stateValue != null,
                RequirePlayerState = stateFlag != null && stateFlag.boolValue,
                RequiredPlayerState = stateValue != null ? stateValue.intValue : -1,
                OverrideBlowerSettings = blowerFlag != null && blowerFlag.boolValue,
                BlowerDirection = blowerDirection != null ? blowerDirection.vector2Value : Vector2.zero,
                BlowerStrength = blowerStrength != null ? blowerStrength.floatValue : 0f,
                HasBlowerRangeFields = blowerRange != null && blowerWidth != null,
                BlowerRange = blowerRange != null ? blowerRange.floatValue : 0f,
                BlowerWidth = blowerWidth != null ? blowerWidth.floatValue : 0f,
            });
        }

        return snapshots;
    }

    private static void ValidateOverride(
        List<string> errors,
        List<CellOverrideSnapshot> snapshots,
        Vector3Int cell,
        string connectionId,
        ConnectionMode connectionMode,
        bool initialActive,
        bool requireState,
        PlayerBodyState requiredState,
        bool requireStateContract,
        string label)
    {
        CellOverrideSnapshot snapshot = snapshots.FirstOrDefault(candidate => candidate.Cell == cell);
        if (snapshot == null)
        {
            errors.Add(label + " is missing a cell override at " + cell + ".");
            return;
        }

        if (!string.Equals(snapshot.ConnectionId, connectionId, StringComparison.Ordinal))
            errors.Add(label + " has connection ID '" + snapshot.ConnectionId + "', expected '" + connectionId + "'.");
        if (snapshot.ConnectionMode != (int)connectionMode)
            errors.Add(label + " has connection mode " + snapshot.ConnectionMode + ", expected " + connectionMode + ".");
        if (snapshot.InitialActive != initialActive)
            errors.Add(label + " has initialActive=" + snapshot.InitialActive + ", expected " + initialActive + ".");

        if (requireStateContract && !snapshot.HasPlayerStateFields)
        {
            errors.Add(label + " is missing requirePlayerState/requiredPlayerState serialized fields.");
        }
        else if (requireStateContract && (snapshot.RequirePlayerState != requireState || snapshot.RequiredPlayerState != (int)requiredState))
        {
            errors.Add(label + " must require player state " + requiredState + ".");
        }
    }

    private static bool HasPlayerStateCellOverrideContract()
    {
        Type cellOverrideType = typeof(PropTilemapSpawner).GetNestedType(
            "CellOverride", BindingFlags.Public | BindingFlags.NonPublic);
        if (cellOverrideType == null)
            return false;
        return cellOverrideType.GetField("requirePlayerState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null &&
               cellOverrideType.GetField("requiredPlayerState", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic) != null;
    }

    private static void AddArea2ToBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = EditorBuildSettings.scenes.ToList();
        EditorBuildSettingsScene existing = scenes.FirstOrDefault(scene =>
            string.Equals(scene.path, Area2ScenePath, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            scenes.Add(new EditorBuildSettingsScene(Area2ScenePath, true));
        }
        else
        {
            existing.enabled = true;
        }
        EditorBuildSettings.scenes = scenes.ToArray();
    }

    private static void PaintSolidRect(Tilemap tilemap, int startX, int startY, int width, int height, TileBase tile)
    {
        for (int x = startX; x < startX + width; x++)
        for (int y = startY; y < startY + height; y++)
            tilemap.SetTile(new Vector3Int(x, y, 0), tile);
    }

    private static void PaintThinSegment(Tilemap tilemap, int startX, int y, int width, TileBase left, TileBase center, TileBase right)
    {
        for (int i = 0; i < width; i++)
        {
            TileBase tile = width == 1 ? center : i == 0 ? left : i == width - 1 ? right : center;
            tilemap.SetTile(new Vector3Int(startX + i, y, 0), tile);
        }
    }

    private static void RegenerateTilemapColliderGeometry(Tilemap tilemap)
    {
        if (tilemap == null)
            return;

        TilemapCollider2D tilemapCollider = tilemap.GetComponent<TilemapCollider2D>();
        if (tilemapCollider != null)
        {
            tilemapCollider.ProcessTilemapChanges();
            EditorUtility.SetDirty(tilemapCollider);
            RecordPrefabOverride(tilemapCollider);
        }

        CompositeCollider2D composite = tilemap.GetComponent<CompositeCollider2D>();
        if (composite != null)
        {
            composite.GenerateGeometry();
            EditorUtility.SetDirty(composite);
            RecordPrefabOverride(composite);
        }

        Physics2D.SyncTransforms();
    }

    private static Tilemap FindTilemap(Scene scene, string name)
    {
        IEnumerable<Tilemap> candidates = FindSceneObjects<Tilemap>(scene)
            .Where(tilemap => string.Equals(tilemap.name, name, StringComparison.Ordinal));
        if (name == PropsTilemapName)
            return candidates.FirstOrDefault(tilemap => tilemap.GetComponent<PropTilemapSpawner>() != null) ?? candidates.FirstOrDefault();
        return candidates.FirstOrDefault();
    }

    private static int CountTiles(Tilemap tilemap)
    {
        int count = 0;
        foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            if (tilemap.HasTile(cell))
                count++;
        return count;
    }

    private static void RequireTile(List<string> errors, Tilemap tilemap, Vector3Int cell, string label)
    {
        if (!tilemap.HasTile(cell))
            errors.Add(label + " is missing its expected tile at " + cell + ".");
    }

    private static void RequireNoTile(List<string> errors, Tilemap tilemap, Vector3Int cell, string label)
    {
        if (tilemap.HasTile(cell))
            errors.Add(label + " is unexpectedly blocked at " + cell + ".");
    }

    private static void ValidateCompositeColliderBounds(List<string> errors, Tilemap tilemap, string label)
    {
        if (tilemap == null)
            return;

        CompositeCollider2D composite = tilemap.GetComponent<CompositeCollider2D>();
        if (composite == null)
            return;

        BoundsInt cells = tilemap.cellBounds;
        float minX = cells.xMin - 0.25f;
        float maxX = cells.xMax + 0.25f;
        float minY = cells.yMin - 0.25f;
        float maxY = cells.yMax + 0.25f;

        for (int pathIndex = 0; pathIndex < composite.pathCount; pathIndex++)
        {
            Vector2[] points = new Vector2[composite.GetPathPointCount(pathIndex)];
            composite.GetPath(pathIndex, points);
            foreach (Vector2 point in points)
            {
                if (point.x < minX || point.x > maxX || point.y < minY || point.y > maxY)
                {
                    errors.Add(label + " composite collider contains stale geometry at " + point +
                               ", outside tile bounds " + cells + ".");
                    return;
                }
            }
        }
    }

    private static void RequireProp(List<string> errors, Tilemap props, Vector3Int cell, string expectedName, string label)
    {
        TileBase tile = props.GetTile(cell);
        PropTile propTile = tile as PropTile;
        string actual = propTile == null ? "<empty>" : propTile.prefab != null ? propTile.prefab.name : propTile.name;
        if (propTile == null || actual.IndexOf(expectedName, StringComparison.OrdinalIgnoreCase) < 0)
            errors.Add(label + " is missing at " + cell + "; found " + actual + ".");
    }

    private static void RequireTodoMarkers(List<string> errors, Transform room, IEnumerable<string> names)
    {
        foreach (string name in names)
            RequireChild(errors, room, name);
    }

    private static void RequireChild(List<string> errors, Transform parent, string name)
    {
        if (FindChild(parent, name) == null)
            errors.Add("Missing required marker '" + parent.name + "/" + name + "'.");
    }

    private static void RequireObject(List<string> errors, UnityEngine.Object value, string label)
    {
        if (value == null)
            errors.Add("Missing required " + label + ".");
    }

    private static GameObject NewSceneObject(Scene scene, string name, Transform parent, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        SceneManager.MoveGameObjectToScene(go, scene);
        if (parent != null)
            go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go;
    }

    private static GameObject NewMarker(Scene scene, Transform parent, string name, Vector3 worldPosition)
    {
        GameObject marker = NewSceneObject(scene, name, parent, Vector3.zero);
        marker.transform.position = worldPosition;
        return marker;
    }

    private static GameObject NewVisualTodoMarker(
        Scene scene,
        Transform parent,
        string name,
        Vector3 worldPosition,
        string label,
        Color color,
        Sprite sprite = null)
    {
        GameObject marker = NewMarker(scene, parent, name, worldPosition);
        GameObject visual = NewSceneObject(scene, "Visual", marker.transform, Vector3.zero);
        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite != null
            ? sprite
            : LoadRequiredSprite("Assets/Art/Environment/Platforms/Factory/factory_tile.png");
        renderer.color = color;
        renderer.sortingOrder = 40;

        Vector2 size = new Vector2(1.2f, 0.8f);
        if (name.IndexOf("Door", StringComparison.OrdinalIgnoreCase) >= 0)
            size = new Vector2(0.9f, 3f);
        else if (name.IndexOf("Humidifier", StringComparison.OrdinalIgnoreCase) >= 0)
            size = new Vector2(3f, 1.1f);
        else if (name.IndexOf("Electrified", StringComparison.OrdinalIgnoreCase) >= 0)
            size = new Vector2(3.5f, 0.35f);
        else if (name.IndexOf("Conveyor", StringComparison.OrdinalIgnoreCase) >= 0)
            size = new Vector2(3f, 0.35f);

        SetSpriteWorldSize(renderer, size);

        GameObject textObject = NewSceneObject(scene, "Label", marker.transform, new Vector3(0f, 0f, -0.1f));
        TextMesh text = textObject.AddComponent<TextMesh>();
        text.text = label;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.characterSize = 0.08f;
        text.fontSize = 24;
        text.color = Color.white;
        MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
        if (textRenderer != null)
            textRenderer.sortingOrder = 41;

        return marker;
    }

    private static void NewVisualSpriteStrip(
        Scene scene,
        Transform parent,
        string name,
        int startX,
        int endX,
        float y,
        Sprite sprite,
        Color color)
    {
        GameObject strip = NewMarker(scene, parent, name, Vector3.zero);
        for (int x = startX; x <= endX; x++)
        {
            GameObject tile = NewSceneObject(scene, "Visual_" + x, strip.transform, Vector3.zero);
            tile.transform.position = new Vector3(x + 0.5f, y, 0f);
            SpriteRenderer renderer = tile.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingOrder = 25;
            SetSpriteWorldSize(renderer, new Vector2(1f, 0.22f));
        }
    }

    private static void NewCrusherSuspensionVisual(Scene scene, Transform parent, float x, Sprite sprite)
    {
        GameObject mount = NewMarker(scene, parent, "CrusherMount_Visual_" + x, new Vector3(x + 0.5f, 4.75f, 0f));
        SpriteRenderer renderer = mount.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.55f, 0.22f, 0.38f, 1f);
        renderer.sortingOrder = 5;
        SetSpriteWorldSize(renderer, new Vector2(0.45f, 3.5f));
    }

    private static void SetSpriteWorldSize(SpriteRenderer renderer, Vector2 targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;
        Vector2 spriteSize = renderer.sprite.bounds.size;
        renderer.transform.localScale = new Vector3(
            targetSize.x / Mathf.Max(0.01f, spriteSize.x),
            targetSize.y / Mathf.Max(0.01f, spriteSize.y),
            1f);
    }

    private static Vector3 CellWorld(Tilemap tilemap, Vector3Int cell)
    {
        return tilemap.GetCellCenterWorld(cell);
    }

    private static GameObject FindDirectSceneObject(Scene scene, string name)
    {
        return FindSceneObjects<GameObject>(scene)
            .FirstOrDefault(go => go.transform.parent == null && string.Equals(go.name, name, StringComparison.Ordinal));
    }

    private static GameObject FindDirectChild(Transform parent, string name)
    {
        return parent == null ? null : parent.Cast<Transform>()
            .FirstOrDefault(child => string.Equals(child.name, name, StringComparison.Ordinal))?.gameObject;
    }

    private static Transform FindChild(Transform parent, string name)
    {
        if (parent == null)
            return null;
        foreach (Transform child in parent)
        {
            if (string.Equals(child.name, name, StringComparison.Ordinal))
                return child;
            Transform nested = FindChild(child, name);
            if (nested != null)
                return nested;
        }
        return null;
    }

    private static IEnumerable<T> FindSceneObjects<T>(Scene scene) where T : UnityEngine.Object
    {
        return Resources.FindObjectsOfTypeAll<T>()
            .Where(obj =>
            {
                GameObject gameObject = obj as GameObject;
                if (gameObject != null)
                    return gameObject.scene == scene;
                Component component = obj as Component;
                return component != null && component.gameObject.scene == scene;
            });
    }

    private static Scene GetLoadedArea2Scene()
    {
        Scene active = EditorSceneManager.GetActiveScene();
        if (active.IsValid() && string.Equals(active.path, Area2ScenePath, StringComparison.OrdinalIgnoreCase))
            return active;
        return SceneManager.GetSceneByPath(Area2ScenePath);
    }

    private static bool SafeCompareTag(GameObject go, string tag)
    {
        try
        {
            return go != null && go.CompareTag(tag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private static T LoadRequiredAsset<T>(string path) where T : UnityEngine.Object
    {
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Required asset is missing: " + path);
        return asset;
    }

    private static Sprite LoadRequiredSprite(string path)
    {
        Sprite sprite = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        if (sprite == null)
            sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if (sprite == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Required sprite is missing: " + path);
        return sprite;
    }

    private static void SetRequiredProperty(SerializedProperty parent, string name, string value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Serialized CellOverride field is missing: " + name);
        property.stringValue = value;
    }

    private static void SetRequiredProperty(SerializedProperty parent, string name, Vector3Int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Serialized CellOverride field is missing: " + name);
        property.vector3IntValue = value;
    }

    private static void SetRequiredProperty(SerializedProperty parent, string name, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Serialized CellOverride field is missing: " + name);
        property.intValue = value;
    }

    private static void SetRequiredProperty(SerializedProperty parent, string name, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property == null)
            throw new InvalidOperationException("[Area2SceneBuilder] Serialized CellOverride field is missing: " + name);
        property.boolValue = value;
    }

    private static void SetOptionalProperty(SerializedProperty parent, string name, string value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.stringValue = value;
    }

    private static void SetOptionalProperty(SerializedProperty parent, string name, int value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.intValue = value;
    }

    private static void SetOptionalProperty(SerializedProperty parent, string name, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetOptionalProperty(SerializedProperty parent, string name, Vector2 value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.vector2Value = value;
    }

    private static void SetOptionalProperty(SerializedProperty parent, string name, float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(name);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetReflectedField(Type entryType, object entry, string name, object value)
    {
        FieldInfo field = entryType.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null)
        {
            if (name == "requirePlayerState" || name == "requiredPlayerState")
                return;
            throw new InvalidOperationException("[Area2SceneBuilder] Reflected CellOverride field is missing: " + name);
        }

        object converted = value;
        if (value != null && field.FieldType.IsEnum && !field.FieldType.IsInstanceOfType(value))
            converted = Enum.ToObject(field.FieldType, value);
        field.SetValue(entry, converted);
    }

    private static void RecordPrefabOverride(UnityEngine.Object target)
    {
        if (target == null)
            return;
        if (PrefabUtility.IsPartOfPrefabInstance(target))
            PrefabUtility.RecordPrefabInstancePropertyModifications(target);
    }

    private static void LogValidationErrors(List<string> errors)
    {
        Debug.LogError("[Area2SceneBuilder] Area2 validation found " + errors.Count + " error(s):\n- " + string.Join("\n- ", errors));
    }
}
