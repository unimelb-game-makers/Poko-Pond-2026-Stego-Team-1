using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;

// Brings the Area 3 water batteries in line with the level design doc (Area 3,
// pp.8–11). The scenes used hand-placed WaterBattery art on top of stand-in
// pressure plates; the doc has no plates in these rooms, so each plate becomes a
// painted battery on the same cell and the props the doc says it powers are linked.
// Re-runnable: run it again after other branches touch the scenes.
public static class Area3BatteryTileMigration
{
    public const string Area31 = "Assets/Scenes/Area3-1.unity";
    public const string Area32 = "Assets/Scenes/Area3-2.unity";

    // Area3-1 (Intro): battery on the ledge takes half the player and opens the exit door.
    public static readonly Vector3Int IntroBattery = new Vector3Int(6, 12, 0);
    public const string IntroDoorId = "open_door";

    // Area3-2 (Familiarity): lower battery keeps the fans running, upper battery
    // moves the platform, the battery riding the platform opens the end door.
    public static readonly Vector3Int FanBattery = new Vector3Int(-21, -5, 0);
    public static readonly Vector3Int PlatformBattery = new Vector3Int(-19, 3, 0);
    public const string FanId = "activate_fans";
    public const string PlatformId = "move_platform";
    public const string EndDoorId = "open_end";

    // Idle sprite height as the old instances were placed: 462 px at 138 PPU, centre pivot.
    private const float OldSpriteHeight = 462f / 138f;

    [MenuItem("Tools/Poko Pond/Area 3/Wire Water Batteries (Design Doc)")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        BuildIntro();
        BuildFamiliarity();
    }

    private static void BuildIntro()
    {
        var ctx = Open(Area31);
        ReplacePlateWithBattery(ctx, IntroBattery, IntroDoorId);
        RemoveHandPlaced(ctx, keepParented: false);
        Save(ctx, $"battery {IntroBattery} → '{IntroDoorId}' (exit door)");
    }

    private static void BuildFamiliarity()
    {
        var ctx = Open(Area32);
        ReplacePlateWithBattery(ctx, FanBattery, FanId);
        ReplacePlateWithBattery(ctx, PlatformBattery, PlatformId);

        // Fans only run while the lower battery is occupied ("needs to be constantly running").
        var blowers = Object.FindObjectsByType<Blower>(FindObjectsSortMode.None);
        Require(blowers.Length == 3, $"Expected the 3 fan blowers in {Area32}, found {blowers.Length}.");
        foreach (var blower in blowers)
            Link(blower, FanId);

        // The battery riding the vertical platform is the only hand-placed one kept:
        // tiles cannot move. Restore its base height and give it the end door id.
        var riding = HandPlaced().Where(b => b.GetComponentInParent<MovingPlatform>() != null).ToList();
        Require(riding.Count == 1, $"Expected one battery parented to the moving platform, found {riding.Count}.");
        var ridingBattery = riding[0];
        var platform = ridingBattery.GetComponentInParent<MovingPlatform>();
        Link(platform, PlatformId);

        if (!SessionAlreadyMigrated(ridingBattery))
        {
            var t = ridingBattery.transform;
            t.position -= new Vector3(0f, OldSpriteHeight * Mathf.Abs(t.lossyScale.y) * 0.5f, 0f);
        }
        var battery = new SerializedObject(ridingBattery.GetComponent<WaterBattery>());
        battery.FindProperty("batteryId").stringValue = EndDoorId;
        battery.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(ridingBattery.GetComponent<WaterBattery>());
        PrefabUtility.RecordPrefabInstancePropertyModifications(ridingBattery.transform);

        RemoveHandPlaced(ctx, keepParented: true);
        Save(ctx, $"fan battery {FanBattery} → '{FanId}' (3 blowers), platform battery {PlatformBattery} → '{PlatformId}', riding battery → '{EndDoorId}'");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private class Context
    {
        public string path;
        public UnityEngine.SceneManagement.Scene scene;
        public Tilemap props;
        public SerializedObject spawner;
        public SerializedProperty overrides;
        public PropTile battery;
        public StringBuilder log = new StringBuilder();
    }

    private static Context Open(string path)
    {
        var ctx = new Context { path = path, scene = EditorSceneManager.OpenScene(path) };
        ctx.props = Object.FindObjectsByType<Tilemap>(FindObjectsSortMode.None).Single(m => m.name == "Props");
        ctx.spawner = new SerializedObject(ctx.props.GetComponent<PropTilemapSpawner>());
        ctx.overrides = ctx.spawner.FindProperty("cellOverrides");
        ctx.battery = AssetDatabase.LoadAssetAtPath<PropTile>(SandboxWaterBatteryDemoBuilder.SmallTilePath);
        return ctx;
    }

    // The doc has no pressure plates here: swap the stand-in plate for a battery on
    // the same cell and give that cell the id of what the doc says it powers.
    private static void ReplacePlateWithBattery(Context ctx, Vector3Int cell, string id)
    {
        var existing = ctx.props.GetTile(cell) as PropTile;
        bool isPlate = existing != null && existing.prefab != null && existing.prefab.GetComponent<PressurePlate>() != null;
        Require(isPlate || existing == ctx.battery,
            $"{ctx.path}: expected a stand-in pressure plate or battery at {cell}, found {(existing ? existing.name : "nothing")}.");
        ctx.props.SetTile(cell, ctx.battery);

        var entry = SandboxMechanicsDemoBuilder.FindOverride(ctx.overrides, cell);
        if (entry == null)
        {
            ctx.overrides.InsertArrayElementAtIndex(ctx.overrides.arraySize);
            entry = ctx.overrides.GetArrayElementAtIndex(ctx.overrides.arraySize - 1);
            entry.FindPropertyRelative("cell").vector3IntValue = cell;
        }
        string previous = entry.FindPropertyRelative("connectionId").stringValue;
        entry.FindPropertyRelative("propName").stringValue = ctx.battery.prefab.name;
        entry.FindPropertyRelative("connectionId").stringValue = id;
        entry.FindPropertyRelative("oneShot").boolValue = false;
        entry.FindPropertyRelative("requirePlayerState").boolValue = false;
        ctx.log.Append($"\n  {cell}: {(isPlate ? "plate" : "battery")} '{previous}' → battery '{id}'");
    }

    private static void Link(Object component, string id)
    {
        var so = new SerializedObject(component);
        so.FindProperty("activation.connectionId").stringValue = id;
        so.FindProperty("activation.mode").enumValueIndex = (int)ConnectionMode.Hold;
        so.FindProperty("activation.initialActive").boolValue = false;
        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(component);
    }

    private static List<GameObject> HandPlaced()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Props/WaterBattery.prefab");
        return Object.FindObjectsByType<WaterBattery>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Select(b => b.gameObject)
            .Where(go => PrefabUtility.IsPartOfPrefabInstance(go)
                && PrefabUtility.GetCorrespondingObjectFromSource(PrefabUtility.GetNearestPrefabInstanceRoot(go)) == prefab)
            .ToList();
    }

    // The riding battery's id is only set by this migration, so its presence means
    // the height correction has already been applied on a previous run.
    private static bool SessionAlreadyMigrated(GameObject battery)
    {
        return new SerializedObject(battery.GetComponent<WaterBattery>()).FindProperty("batteryId").stringValue == EndDoorId;
    }

    private static void RemoveHandPlaced(Context ctx, bool keepParented)
    {
        foreach (var go in HandPlaced())
        {
            if (keepParented && go.GetComponentInParent<MovingPlatform>() != null) continue;
            ctx.log.Append($"\n  removed hand-placed '{go.name}' at {go.transform.position}");
            Object.DestroyImmediate(PrefabUtility.GetNearestPrefabInstanceRoot(go));
        }
    }

    private static void Save(Context ctx, string summary)
    {
        ctx.spawner.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.RecordPrefabInstancePropertyModifications(ctx.spawner.targetObject);
        ctx.props.RefreshAllTiles();
        EditorUtility.SetDirty(ctx.props);
        PrefabUtility.RecordPrefabInstancePropertyModifications(ctx.props);
        EditorSceneManager.MarkSceneDirty(ctx.scene);
        Require(EditorSceneManager.SaveScene(ctx.scene), "Could not save " + ctx.path);
        Debug.Log($"[Area3Batteries] PASS {ctx.path}: {summary}{ctx.log}");
    }

    private static void Require(bool condition, string message) =>
        SandboxMechanicsDemoBuilder.Require(condition, message);
}
