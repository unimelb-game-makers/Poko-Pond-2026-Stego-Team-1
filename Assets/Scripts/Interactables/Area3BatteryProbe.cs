#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Editor-only Play Mode probe for the Area 3 battery wiring (design doc pp.8–11).
// Run in batch mode (without -quit) via Area3BatteryProbe.ValidateArea31 /
// ValidateArea32. Bodies are positioned by teleport; suction, splitting, power
// and the linked doors, fans and platform run through the real game code.
[DefaultExecutionOrder(10000)]
public class Area3BatteryProbe : MonoBehaviour
{
    private const string Key = "Area3BatteryProbe.Scene";
    private float deadline;

    public static void ValidateArea31() => Begin("Assets/Scenes/Area3-1.unity");
    public static void ValidateArea32() => Begin("Assets/Scenes/Area3-2.unity");

    private static void Begin(string scene)
    {
        Require(Application.isBatchMode, "Run the Area 3 probe in batch mode.");
        EditorSceneManager.OpenScene(scene);
        SessionState.SetString(Key, scene);
        EditorApplication.isPlaying = true;
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlay;
        EditorApplication.playModeStateChanged += OnPlay;
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetString(Key, "") != "")
            new GameObject("Area3BatteryProbe").AddComponent<Area3BatteryProbe>();
    }

    private IEnumerator Start()
    {
        Time.captureDeltaTime = 1f / 60f;
        deadline = Time.realtimeSinceStartup + 120f;
        yield return Seconds(.5f);
        string scene = SessionState.GetString(Key, "");
        if (scene.EndsWith("Area3-1.unity")) yield return Intro();
        else yield return Familiarity();
        Debug.Log("[Area3QA] ALL PASS " + scene);
        SessionState.SetString(Key, "");
        EditorApplication.Exit(0);
    }

    // Area3-1: splitter unlocks the battery, which takes half the player and opens the exit door.
    private IEnumerator Intro()
    {
        var main = GameObject.FindWithTag("Player").GetComponent<SoftBodyPlayer>();
        var split = FindFirstObjectByType<PlayerSplitController>();
        var battery = FindObjectsByType<WaterBattery>(FindObjectsSortMode.None).Single();
        var door = DoorWithId("open_door");
        Require(!door.IsUnlocked, "Exit door should start locked.");

        Vector2 entry = battery.transform.position + new Vector3(-1.2f, .6f);
        Place(main, entry);
        yield return Seconds(1.2f);
        Require(!main.IsParked && !door.IsUnlocked, "Battery worked before the splitter was used.");
        Debug.Log("[Area3QA] PASS intro 1: battery idle before the splitting machine.");

        split.UnlockSplitting();
        Place(main, entry);
        yield return Until(() => split.IsSplit && battery.IsRunning, 3f);
        Require(split.IsSplit && battery.IsRunning, "Battery did not take half the player.");
        yield return Seconds(.6f);
        Require(door.IsUnlocked, "Exit door did not open while the battery runs.");
        Debug.Log("[Area3QA] PASS intro 2: half parked in battery, exit door open.");

        var parked = Halves(split).Single(h => h.IsParked);
        Require(battery.TryEject(parked, -1f), "Could not eject the parked half.");
        yield return Seconds(.4f);
        Require(!door.IsUnlocked, "Exit door stayed open after the battery emptied.");
        Debug.Log("[Area3QA] PASS intro 3: door relocks when the battery empties.");
    }

    // Area3-2: lower battery runs the fans, upper battery moves the platform, the
    // battery riding the platform opens the end door.
    private IEnumerator Familiarity()
    {
        var main = GameObject.FindWithTag("Player").GetComponent<SoftBodyPlayer>();
        var split = FindFirstObjectByType<PlayerSplitController>();
        var batteries = FindObjectsByType<WaterBattery>(FindObjectsSortMode.None);
        Require(batteries.Length == 3, "Expected three batteries, found " + batteries.Length);
        var riding = batteries.Single(b => b.GetComponentInParent<MovingPlatform>() != null);
        var fanBattery = batteries.Where(b => b != riding).OrderBy(b => b.transform.position.y).First();
        var platformBattery = batteries.Where(b => b != riding).OrderBy(b => b.transform.position.y).Last();
        var platform = riding.GetComponentInParent<MovingPlatform>();
        var blowers = FindObjectsByType<Blower>(FindObjectsSortMode.None);
        var endDoor = DoorWithId("open_end");

        Require(blowers.All(b => !b.IsActive) && !platform.IsActive && !endDoor.IsUnlocked,
            "Fans, platform and end door must start off.");
        Debug.Log($"[Area3QA] fan battery {fanBattery.transform.position}, platform battery {platformBattery.transform.position}, riding {riding.transform.position}");

        split.UnlockSplitting();
        Place(main, (Vector2)fanBattery.transform.position + new Vector2(1.2f, .6f));
        yield return Until(() => split.IsSplit && fanBattery.IsRunning, 3f);
        Require(fanBattery.IsRunning, "Lower battery did not take half the player.");
        yield return Seconds(.2f);
        Require(blowers.All(b => b.IsActive), "Fans did not start with the lower battery running.");
        Require(!platform.IsActive && !endDoor.IsUnlocked, "Lower battery powered the wrong props.");
        Debug.Log("[Area3QA] PASS fam 1: lower battery runs all three fans.");

        var outside = Halves(split).Single(h => !h.IsParked);
        Place(outside, (Vector2)platformBattery.transform.position + new Vector2(1.2f, .6f));
        yield return Until(() => platformBattery.IsRunning, 3f);
        Require(platformBattery.IsRunning, "Upper battery did not take the other half.");
        Vector3 before = platform.transform.position;
        yield return Seconds(1f);
        Require(platform.IsActive && Vector3.Distance(before, platform.transform.position) > .3f,
            "Platform did not move with the upper battery running.");
        Require(!endDoor.IsUnlocked, "Upper battery opened the end door.");
        Debug.Log("[Area3QA] PASS fam 2: upper battery moves the platform.");

        var fanHalf = Halves(split).Single(h => h.IsParked && Vector2.Distance(h.Center, fanBattery.transform.position) < 3f);
        Require(fanBattery.TryEject(fanHalf, 1f), "Could not eject from the lower battery.");
        yield return Seconds(.3f);
        Require(blowers.All(b => !b.IsActive), "Fans kept running after the lower battery emptied.");
        Debug.Log("[Area3QA] PASS fam 3: fans stop when the lower battery empties.");

        Place(fanHalf, (Vector2)riding.transform.position + new Vector2(1.0f, .6f));
        yield return Until(() => riding.IsRunning, 3f);
        Require(riding.IsRunning, "Battery on the platform did not take the half.");
        yield return Seconds(.3f);
        Require(endDoor.IsUnlocked, "Battery on the platform did not open the end door.");
        Vector3 rideStart = riding.transform.position;
        yield return Seconds(.6f);
        Vector2 intakeNow = riding.transform.TransformPoint(new Vector2(0f, 1.35f));
        Require(Vector3.Distance(rideStart, riding.transform.position) > .2f, "Platform stopped while carrying the battery.");
        Require(Vector2.Distance(fanHalf.Center, intakeNow) < .1f, "Parked half did not ride along with the platform battery.");
        Debug.Log("[Area3QA] PASS fam 4: riding battery opens the end door and carries its half.");
    }

    // ── Helpers ──────────────────────────────────────────────────────────

    private void Update()
    {
        if (deadline > 0 && Time.realtimeSinceStartup > deadline) Require(false, "Probe timed out.");
    }

    private static Door DoorWithId(string id)
    {
        var field = typeof(Door).GetField("_connectionId", BindingFlags.Instance | BindingFlags.NonPublic);
        var doors = FindObjectsByType<Door>(FindObjectsSortMode.None).Where(d => (string)field.GetValue(d) == id).ToArray();
        Require(doors.Length == 1, $"Expected one door with id '{id}', found {doors.Length}.");
        return doors[0];
    }

    private static SoftBodyPlayer[] Halves(PlayerSplitController split)
    {
        var field = typeof(PlayerSplitController).GetField("_droplets", BindingFlags.Instance | BindingFlags.NonPublic);
        return ((SoftBodyPlayer[])field.GetValue(split)).Where(d => d != null).ToArray();
    }

    private static void Place(SoftBodyPlayer body, Vector2 position)
    {
        if (body.IsParked) return;
        body.Unfreeze();
        body.TeleportTo(position, Vector2.zero);
    }

    private static IEnumerator Seconds(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return new WaitForFixedUpdate();
    }

    private static IEnumerator Until(Func<bool> condition, float timeout)
    {
        float end = Time.time + timeout;
        while (Time.time < end && !condition()) yield return new WaitForFixedUpdate();
    }

    private static void Require(bool condition, string message)
    {
        if (condition) return;
        Debug.LogError("[Area3QA] FAIL: " + message);
        SessionState.SetString(Key, "");
        EditorApplication.Exit(1);
        throw new InvalidOperationException(message);
    }
}
#endif
