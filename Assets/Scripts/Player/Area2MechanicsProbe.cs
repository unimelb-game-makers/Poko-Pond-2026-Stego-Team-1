#if UNITY_EDITOR
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

// Runtime regression in an isolated project. The return-route test walks from
// the real spawn without teleporting; focused hazard tests use explicit setup.
[DefaultExecutionOrder(10000)]
public class Area2MechanicsProbe : MonoBehaviour
{
    private const string Pending = "Area2MechanicsProbe.Pending";
    private SoftBodyPlayer player;
    private float? targetX;
    private float walkSpeed = 5f;
    private float deadline;
    private int deaths;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnMode;
        EditorApplication.playModeStateChanged += OnMode;
    }
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        EditorSceneManager.OpenScene("Assets/Scenes/Area2-2.unity", OpenSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }
    private static void OnMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Area2MechanicsProbe").AddComponent<Area2MechanicsProbe>();
    }
    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Time.captureDeltaTime = 1f / 60f;
        deadline = Time.realtimeSinceStartup + 240f;
        EventManager.OnPlayerKilled += OnKilled;
        Application.logMessageReceived += OnRuntimeLog;
        yield return new WaitForSeconds(0.5f);
        yield return FullReturnRoute();
        yield return ConveyorCases();
        yield return HumidifierCases();
        yield return ElectricCases();
        yield return MovingPlatformRegression();
        Debug.Log("[Area2MechanicsQA] PASS all mechanics, animation, return route, humidity and alternating-platform cases.");
        EditorApplication.Exit(0);
    }
    private void OnDestroy()
    {
        EventManager.OnPlayerKilled -= OnKilled;
        Application.logMessageReceived -= OnRuntimeLog;
    }
    private static void OnRuntimeLog(string message, string stack, LogType type)
    {
        if (type == LogType.Exception || type == LogType.Error) EditorApplication.Exit(1);
    }
    private void OnKilled() => deaths++;
    private void Update()
    {
        if (Time.realtimeSinceStartup > deadline) Fail("Overall timeout.");
    }
    private void FixedUpdate()
    {
        if (player == null || !targetX.HasValue) return;
        float delta = targetX.Value - Position.x;
        float speed = Mathf.Abs(delta) < 0.06f ? 0f : Mathf.Sign(delta) * walkSpeed;
        foreach (Rigidbody2D point in player.Points) point.linearVelocity = new Vector2(speed, point.linearVelocity.y);
    }
    private Vector2 Position => player.Points.Aggregate(Vector2.zero, (sum, p) => sum + p.position) / player.Points.Length;

    private IEnumerator FullReturnRoute()
    {
        player = FindObjectsByType<SoftBodyPlayer>(FindObjectsSortMode.None).Single();
        player.InputEnabled = false;
        SceneDoorExit exit = FindObjectsByType<SceneDoorExit>(FindObjectsSortMode.None).Single();
        exit.enabled = false; // Inspect humidity before allowing the real transition.
        Require(exit.ExitToLeft, "Familiarity exit must face left.");
        Require(Position.y > 5f, "Spawn is not on the upper entrance ledge.");
        Require(Belts().Length == 14 && Belts().All(b => !b.IsActive), "Belt must start off.");
        AutoCrusherTrap[] crushers = FindObjectsByType<AutoCrusherTrap>(FindObjectsSortMode.None);
        Require(crushers.Length == 3 && crushers.All(c => !CrusherActive(c)), "Crushers must start off.");
        Capture("mechanics-room2-off.png", new Vector3(16, 6, -10), 11f);

        targetX = 29f;
        yield return Until(() => player.getBodyState() == PlayerBodyState.Solid, "Walk from entrance to freezer failed", 25f);
        targetX = 26.5f;
        yield return Until(() => exit.GetComponent<Door>().IsUnlocked, "Ice return to plate failed", 8f);
        targetX = null;
        Require(Belts().All(b => b.IsActive) && crushers.All(CrusherActive), "Plate did not activate all machinery.");
        Require(GameStateManager.Instance.State == GameState.Playing, "Died before return route.");
        // Wait outside the belt for the first impact, then traverse its full
        // width during recovery. No teleport or vertical boost is used.
        yield return new WaitForSeconds(0.35f);
        Capture("mechanics-room2-on.png", new Vector3(16, 6, -10), 11f);
        targetX = 3f;
        yield return Until(() => Position.x < 3.2f || GameStateManager.Instance.State == GameState.GameOver,
            "Could not return through the machinery", 15f);
        Require(GameStateManager.Instance.State == GameState.Playing, "Crusher timing makes the tested return route fatal at " + Position);
        Require(player.getBodyState() == PlayerBodyState.Solid, "Return route bypassed ice hazards by changing state.");
        targetX = 0.5f;
        yield return Until(() => player.getBodyState() == PlayerBodyState.Liquid, "Exit humidifier did not restore liquid", 5f);
        Require(SceneManager.GetActiveScene().name == "Area2-2", "Humidity was only a scene reset.");
        targetX = null;
        player.InputEnabled = true;
        exit.enabled = true;
        targetX = 0.5f;
        yield return Until(() => SceneManager.GetActiveScene().name == "Area2-3", "Left exit did not transition", 5f);
        targetX = null;
        Debug.Log("[Area2MechanicsQA] PASS full familiarity route: upper entry, inactive crossing, freezer, plate, active ice return, humidifier, left exit.");
    }

    private IEnumerator ConveyorCases()
    {
        yield return Load("Area2-2");
        player.TeleportTo(new Vector2(20.5f, 1.65f), Vector2.zero);
        yield return new WaitForSeconds(0.4f);
        float offX = Position.x;
        Sprite idle = Belts()[0].GetComponent<SpriteRenderer>().sprite;
        yield return new WaitForSeconds(0.5f);
        Require(Mathf.Abs(Position.x - offX) < 0.12f, "Inactive belt moves the player.");
        Require(Belts()[0].GetComponent<SpriteRenderer>().sprite == idle, "Inactive belt animates.");
        EventManager.PressurePlateActivated("unrelated_plate");
        Require(Belts().All(b => !b.IsActive), "Unrelated plate activated belt.");
        // Actual plate wiring was exercised by the full route above. Isolate
        // surface transport now so crushers cannot invalidate measurements.
        foreach (AutoCrusherTrap crusher in FindObjectsByType<AutoCrusherTrap>(FindObjectsSortMode.None)) crusher.StopAllCoroutines();
        EventManager.PressurePlateActivated("familiarity_crushers");
        EventManager.PressurePlateDeactivated("familiarity_crushers");
        Require(Belts().All(b => b.IsActive), "One-shot activation did not stay on after release.");
        var sprites = new HashSet<Sprite>();
        float end = Time.time + 0.85f;
        while (Time.time < end) { sprites.Add(Belts()[0].GetComponent<SpriteRenderer>().sprite); yield return null; }
        Require(sprites.Count == 8, "Animation must show all eight frames; saw " + sprites.Count);
        foreach (PlayerBodyState state in new[] { PlayerBodyState.Liquid, PlayerBodyState.Solid })
        {
            player.changeBodyState(state, new Vector2(20.5f, 1.65f), Vector2.zero);
            player.TeleportTo(new Vector2(20.5f, 1.65f), Vector2.zero);
            player.InputEnabled = true;
            yield return new WaitForSeconds(0.4f);
            float startX = Position.x;
            yield return new WaitForSeconds(1f);
            float distance = startX - Position.x;
            Require(distance > 1.6f && distance < 2.4f, "Belt/seam transport is incorrect for " + state + ": " + distance);
        }
        player.changeBodyState(PlayerBodyState.Liquid, new Vector2(20.5f, 3f), Vector2.zero);
        player.TeleportTo(new Vector2(20.5f, 3f), Vector2.zero);
        float airborneX = Position.x;
        yield return new WaitForSeconds(0.1f);
        Require(Mathf.Abs(Position.x - airborneX) < 0.1f, "Belt affects an airborne player.");
        Debug.Log("[Area2MechanicsQA] PASS conveyor: off/on, connections, latched state, eight animated frames, liquid/solid transport across seams, airborne exclusion.");
    }

    private IEnumerator HumidifierCases()
    {
        for (int room = 1; room <= 3; room++)
        {
            yield return Load("Area2-" + room);
            foreach (SceneDoorExit exit in FindObjectsByType<SceneDoorExit>(FindObjectsSortMode.None)) exit.enabled = false;
            Humidifier mist = FindObjectsByType<Humidifier>(FindObjectsSortMode.None).Single();
            Vector2 centre = (Vector2)mist.transform.position + new Vector2(0, 0.7f);
            // Area 2 implements solid and liquid. The Gas enum is a future
            // placeholder and cannot currently construct a player body.
            foreach (PlayerBodyState state in new[] { PlayerBodyState.Solid })
            {
                player.changeBodyState(state, centre, Vector2.zero);
                player.TeleportTo(centre, Vector2.zero);
                yield return Until(() => player.getBodyState() == PlayerBodyState.Liquid, "Humidifier failed for " + state, 2f);
                Require(Vector2.Distance(Position, centre) < 0.7f, "Humidifier launched or displaced the body.");
            }
            int pointId = player.Points[0].GetInstanceID();
            yield return new WaitForSeconds(0.15f);
            Require(player.Points[0].GetInstanceID() == pointId, "Liquid body is repeatedly reconstructed.");
            Capture("mechanics-room" + room + "-humidifier.png", new Vector3(centre.x, 3, -10), 4f);
        }
        Debug.Log("[Area2MechanicsQA] PASS all three physical humidifiers: solid -> liquid, in-place conversion, no repeat reconstruction.");
    }

    private IEnumerator ElectricCases()
    {
        foreach (PlayerBodyState state in new[] { PlayerBodyState.Liquid, PlayerBodyState.Solid })
        foreach (int row in new[] { 11, 7, 3 })
        {
            yield return Load("Area2-3");
            Require(FindObjectsByType<ElectricPlatform>(FindObjectsSortMode.None).Length == 12, "Wrong electric-strip count.");
            player.changeBodyState(state, new Vector2(13.5f, row + 2f), Vector2.zero);
            player.TeleportTo(new Vector2(13.5f, row + 2f), Vector2.zero);
            int before = deaths;
            yield return Until(() => deaths > before, "Electric row " + row + " failed to kill " + state, 3f);
        }
        foreach (int row in new[] { 13, 9, 5 })
        {
            yield return Load("Area2-3");
            player.changeBodyState(PlayerBodyState.Solid, new Vector2(19.5f, row + 1.65f), Vector2.zero);
            player.TeleportTo(new Vector2(19.5f, row + 1.65f), Vector2.zero);
            yield return new WaitForSeconds(0.6f);
            Require(GameStateManager.Instance.State == GameState.Playing && Position.y > row + 1f, "Normal ledge is unsafe or missing.");
            float beforeY = Position.y;
            typeof(PlatformDropThrough).GetMethod("ProbeAndDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player.GetComponent<PlatformDropThrough>(), new object[] { Vector2.down });
            yield return new WaitForSeconds(0.3f);
            Require(Position.y < beforeY - 0.25f && GameStateManager.Instance.State == GameState.Playing,
                "Normal ledge no longer supports safe drop-through.");
        }
        yield return Load("Area2-3");
        Capture("mechanics-room3-alternating.png", new Vector3(20, 8, -10), 12f);
        // Disabling physical collision for a drop must not disable electricity.
        foreach (ElectricPlatform live in FindObjectsByType<ElectricPlatform>(FindObjectsSortMode.None))
            live.GetComponent<Rigidbody2D>().simulated = false;
        player.TeleportTo(new Vector2(13.5f, 12.7f), Vector2.zero);
        int deathsBefore = deaths;
        yield return Until(() => deaths > deathsBefore, "Drop-through bypassed the electric hazard", 3f);
        Debug.Log("[Area2MechanicsQA] PASS three live rows kill liquid/ice; freezer and two normal ledges remain safe and droppable; disabling collision does not disable electricity.");
    }

    private IEnumerator Load(string scene)
    {
        targetX = null;
        player = null;
        Time.timeScale = 1f;
        yield return SceneManager.LoadSceneAsync(scene);
        yield return new WaitForSeconds(0.4f);
        player = FindObjectsByType<SoftBodyPlayer>(FindObjectsSortMode.None).Single();
        player.InputEnabled = true;
    }

    private IEnumerator MovingPlatformRegression()
    {
        targetX = null;
        player = null;
        Time.timeScale = 1f;
        yield return EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Scenes/Level2.unity",
            new LoadSceneParameters(LoadSceneMode.Single));
        yield return new WaitForSeconds(0.5f);
        MovingPlatform[] moving = FindObjectsByType<MovingPlatform>(FindObjectsSortMode.None).Where(p => p.IsElectrifying).ToArray();
        Require(moving.Length > 0, "Existing level has no electrified moving platforms to test.");
        foreach (MovingPlatform platform in moving)
        {
            Require(platform.GetComponent<ElectrifyingPlatformEffect>() != null, "Moving platform lost its effect.");
            Require(platform.GetComponentsInChildren<LineRenderer>().Any(l => l.enabled), "Moving-platform sparks stopped rendering.");
        }
        Debug.Log("[Area2MechanicsQA] PASS existing Level2 moving-platform electric visuals.");
    }
    private static ConveyorBelt[] Belts() => FindObjectsByType<ConveyorBelt>(FindObjectsSortMode.None);
    private static bool CrusherActive(AutoCrusherTrap crusher) => (bool)typeof(AutoCrusherTrap)
        .GetField("_isActive", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(crusher);
    private static IEnumerator Until(Func<bool> condition, string message, float seconds)
    {
        float end = Time.realtimeSinceStartup + seconds;
        while (!condition()) { if (Time.realtimeSinceStartup > end) Fail(message); yield return null; }
    }
    private static void Capture(string name, Vector3 position, float size)
    {
        var go = new GameObject("MechanicsQA_Camera");
        Camera camera = go.AddComponent<Camera>();
        camera.CopyFrom(Camera.main);
        camera.orthographic = true;
        camera.orthographicSize = size;
        camera.transform.position = position;
        var target = new RenderTexture(1600, 900, 24);
        RenderTexture previous = RenderTexture.active;
        camera.targetTexture = target;
        camera.aspect = 1600f / 900f;
        camera.Render();
        RenderTexture.active = target;
        var pixels = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        pixels.Apply();
        File.WriteAllBytes(Path.Combine(Application.dataPath, "..", name), pixels.EncodeToPNG());
        RenderTexture.active = previous;
        camera.targetTexture = null;
        Destroy(pixels); Destroy(target); Destroy(go);
    }
    private static void Require(bool condition, string message) { if (!condition) Fail(message); }
    private static void Fail(string message)
    {
        Debug.LogError("[Area2MechanicsQA] FAIL " + message);
        EditorApplication.Exit(1);
        throw new InvalidOperationException(message);
    }
}
#endif
