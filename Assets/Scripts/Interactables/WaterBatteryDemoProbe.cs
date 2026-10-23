#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Editor-only Play Mode probe for the Sandbox water battery demos. Run via
// SandboxWaterBatteryDemoBuilder.ValidateBatch in batch mode (without -quit).
// Bodies are positioned by teleport; suction, capture, split, merge, power and
// launch all run through the real battery and player code.
[DefaultExecutionOrder(10000)]
public class WaterBatteryDemoProbe : MonoBehaviour
{
    private const string Key = "WaterBatteryDemoProbe.Pending";
    private float deadline;
    private bool capture;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlay;
        EditorApplication.playModeStateChanged += OnPlay;
    }

    public static void Begin()
    {
        SessionState.SetBool(Key, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlay(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(Key, false))
            new GameObject("WaterBatteryDemoProbe").AddComponent<WaterBatteryDemoProbe>();
    }

    private IEnumerator Start()
    {
        Time.captureDeltaTime = 1f / 60f;
        deadline = Time.realtimeSinceStartup + 120f;
        yield return new WaitForSeconds(.5f);

        var main = GameObject.FindWithTag("Player").GetComponent<SoftBodyPlayer>();
        var split = FindFirstObjectByType<PlayerSplitController>();
        var batteries = FindObjectsByType<WaterBattery>(FindObjectsSortMode.None);
        Require(batteries.Length == 2, "Expected exactly two Sandbox batteries, found " + batteries.Length);
        var small = batteries.Single(b => b.Size == WaterBattery.BatterySize.Small);
        var big = batteries.Single(b => b.Size == WaterBattery.BatterySize.Big);
        var smallDoor = DoorAbove(small);
        var bigDoor = DoorAbove(big);
        Require(!smallDoor.IsUnlocked && !bigDoor.IsUnlocked, "Battery doors must start locked.");
        Require(!split.SplittingUnlocked, "Sandbox should start with splitting locked.");
        Debug.Log("[BatteryQA] small at " + small.transform.position + ", big at " + big.transform.position);

        float smallX = small.transform.position.x, bigX = big.transform.position.x;
        float floorY = small.transform.position.y + .6f;

        // ── 1. Small battery ignores a whole droplet while splitting is locked ──
        // Enter from the spawn (left) side; the big battery's zone starts just right of the small one.
        Place(main, smallX - 1.2f, floorY);
        yield return Seconds(1.2f);
        Require(!main.IsParked && !small.IsRunning && !smallDoor.IsUnlocked,
            "Small battery took a whole droplet while splitting was locked.");
        Debug.Log("[BatteryQA] PASS 1: small battery idle while splitting is locked.");

        // ── 2. Liquid only: a solid body is ignored by the big battery ──
        main.changeBodyState(PlayerBodyState.Solid, new Vector2(bigX - 1.2f, floorY));
        Place(main, bigX - 1.2f, floorY);
        yield return Seconds(1.2f);
        Require(!main.IsParked && !big.IsRunning, "Big battery sucked in a solid body.");
        // Turn back to liquid away from both suction zones (by the spawn).
        main.changeBodyState(PlayerBodyState.Liquid, new Vector2(smallX - 5f, floorY));
        yield return Seconds(.5f);
        Debug.Log("[BatteryQA] PASS 2: solid body ignored.");

        // ── 3. Whole droplet into small battery: auto-split, half parks, half gets control ──
        split.UnlockSplitting();
        Place(main, smallX - 1.2f, floorY);
        yield return Until(() => split.IsSplit && small.IsRunning, 3f);
        Require(split.IsSplit && small.IsRunning, "Small battery did not auto-split and start running.");
        var halves = Halves(split);
        var parked = halves.Single(h => h.IsParked);
        var outside = halves.Single(h => !h.IsParked);
        Require(outside.InputEnabled && !parked.InputEnabled, "Control did not move to the expelled half.");
        Require(outside.Center.x < smallX, "Expelled half did not leave on the entry (left) side: " + outside.Center);
        yield return Seconds(.4f);
        Require(smallDoor.IsUnlocked, "Small battery did not power its door.");
        Require(!bigDoor.IsUnlocked, "Small battery powered the wrong door.");
        Debug.Log("[BatteryQA] PASS 3: auto-split, half parked, other half has control, door powered.");

        // ── 4. Passing half does not auto-merge with the parked half ──
        Place(outside, parked.Center.x + .1f, parked.Center.y);
        yield return Seconds(.6f);
        Require(split.IsSplit && parked.IsParked, "Passing half merged with the parked half.");
        Debug.Log("[BatteryQA] PASS 4: no auto-merge with a parked half.");

        // ── 5. Eject the parked half: launches right and up, door relocks (hold) ──
        Place(outside, smallX - 5f, floorY); // out of every suction zone
        Require(small.TryEject(parked, -1f), "TryEject refused a parked half.");
        Require(!parked.IsParked, "Half still parked after eject.");
        Vector2 launch = parked.CalculateAverageVelocity();
        Require(launch.x < -3f && launch.y > 1f, "Launch velocity not left-and-up: " + launch);
        yield return Seconds(.3f);
        Require(!small.IsRunning && !smallDoor.IsUnlocked, "Small door did not relock after eject.");
        Debug.Log("[BatteryQA] PASS 5: eject launched " + launch + "; hold door relocked.");

        // ── 6. Big battery: one half holds (not running), second half merges and runs ──
        yield return Seconds(1.1f); // let the small battery's cooldown pass
        halves = Halves(split);
        Place(halves[0], bigX - 1.3f, floorY);
        yield return Until(() => halves[0].IsParked, 3f);
        Require(halves[0].IsParked && !big.IsRunning && !bigDoor.IsUnlocked,
            "Big battery ran with only one half inside.");
        Place(halves[1], bigX + .9f, floorY);
        yield return Until(() => !split.IsSplit && main.IsParked, 3f);
        Require(!split.IsSplit && main.IsParked && big.IsRunning, "Big battery did not merge both halves and run.");
        yield return Seconds(.4f);
        Require(bigDoor.IsUnlocked && !smallDoor.IsUnlocked, "Big battery did not power only its own door.");
        Debug.Log("[BatteryQA] PASS 6: big battery held one half, merged the second, powered its door.");

        // ── 7. Eject the whole droplet right (left would fly into the small battery) ──
        Require(big.TryEject(main, 1f), "TryEject refused the merged body.");
        Vector2 bigLaunch = main.CalculateAverageVelocity();
        Require(!main.IsParked && bigLaunch.x > 3f && bigLaunch.y > 1f, "Big battery launch not right-and-up: " + bigLaunch);
        yield return Seconds(.3f);
        Require(!bigDoor.IsUnlocked, "Big door did not relock after eject.");
        Debug.Log("[BatteryQA] PASS 7: whole droplet launched " + bigLaunch + "; door relocked.");

        // ── 8. Whole droplet counts as both halves in the big battery ──
        yield return Seconds(1.1f);
        Place(main, bigX - 1.3f, floorY);
        yield return Until(() => main.IsParked && big.IsRunning, 3f);
        Require(main.IsParked && big.IsRunning, "Whole droplet did not run the big battery.");
        Require(!split.IsSplit, "Big battery split a whole droplet.");
        foreach (var body in FindObjectsByType<SoftBodyPlayer>(FindObjectsSortMode.None))
        {
            bool shown = body.GetComponent<MeshRenderer>().enabled;
            Debug.Log("[BatteryQA] body '" + body.name + "' parked=" + body.IsParked + " visible=" + shown + " at " + body.Center);
            Require(!(body.IsParked && shown), "A parked body is still visible: " + body.name);
        }
        capture = true;
        yield return Seconds(.4f);
        Require(bigDoor.IsUnlocked, "Whole droplet did not power the big door.");
        Debug.Log("[BatteryQA] PASS 8: whole droplet powers the big battery.");

        // ── 9. Cooldown: no immediate recapture after eject ──
        big.TryEject(main, 1f);
        Place(main, bigX + .9f, floorY);
        yield return Seconds(.5f);
        Require(!main.IsParked, "Battery recaptured during cooldown.");
        Debug.Log("[BatteryQA] PASS 9: cooldown prevents immediate recapture.");

        Debug.Log("[BatteryQA] ALL PASS");
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(0);
    }

    private void Update()
    {
        if (deadline > 0 && Time.realtimeSinceStartup > deadline) Require(false, "Probe timed out.");
    }

    // Renders the demo room once a battery is running so the result can be reviewed.
    private void LateUpdate()
    {
        if (!capture) return;
        capture = false;
        var go = new GameObject("BatteryDemoPreviewCamera");
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 3.6f; camera.aspect = 2.4f;
        camera.transform.position = new Vector3(-30f, -3f, -10f);
        var rt = new RenderTexture(1440, 600, 24); camera.targetTexture = rt;
        camera.Render(); camera.Render(); RenderTexture.active = rt;
        var pixels = new Texture2D(1440, 600, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1440, 600), 0, 0); pixels.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", "sandbox-battery-demo.png"), pixels.EncodeToPNG());
        camera.targetTexture = null; RenderTexture.active = null;
        Destroy(pixels); Destroy(rt); Destroy(go);
    }

    private static Door DoorAbove(WaterBattery battery)
    {
        return FindObjectsByType<Door>(FindObjectsSortMode.None)
            .OrderBy(d => Vector2.Distance(d.transform.position, battery.transform.position + Vector3.up * 3f))
            .First();
    }

    private static SoftBodyPlayer[] Halves(PlayerSplitController split)
    {
        var field = typeof(PlayerSplitController).GetField("_droplets",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return ((SoftBodyPlayer[])field.GetValue(split)).Where(d => d != null).ToArray();
    }

    private static void Place(SoftBodyPlayer body, float x, float y)
    {
        if (body.IsParked) return;
        body.Unfreeze();
        body.TeleportTo(new Vector2(x, y), Vector2.zero);
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
        Debug.LogError("[BatteryQA] FAIL: " + message);
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(1);
        throw new InvalidOperationException(message);
    }
}
#endif
