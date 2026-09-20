#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Batch Play Mode regression: real freezer detection, transformation, physics,
// plate activation and platform dropping. Horizontal velocity replaces keyboard
// input only; the probe never jumps or teleports during a tested route.
[DefaultExecutionOrder(10000)]
public class Area2FreezerRouteProbe : MonoBehaviour
{
    private const string PendingKey = "Area2FreezerRouteValidation.Pending";
    private readonly HashSet<string> activated = new HashSet<string>();
    private SoftBodyPlayer player;
    // Use authoritative physics positions, not the renderer's interpolated root.
    private Vector2 PlayerPosition => player.Points.Aggregate(Vector2.zero, (sum, point) => sum + point.position) / player.Points.Length;
    private float? targetX;
    private bool dropping;
    private float deadline;
    private static readonly MethodInfo DropProbe = typeof(PlatformDropThrough)
        .GetMethod("ProbeAndDisable", BindingFlags.Instance | BindingFlags.NonPublic);

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void BeginTest()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException("Run this automated traversal in an isolated batch project.");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredPlayMode && SessionState.GetBool(PendingKey, false))
            new GameObject("FreezerRouteProbe").AddComponent<Area2FreezerRouteProbe>();
    }

    private IEnumerator Start()
    {
        // Keep physics/input sampling deterministic despite batch shader/import stalls.
        Time.captureDeltaTime = 1f / 60f;
        deadline = Time.realtimeSinceStartup + 100f;
        EventManager.OnPressurePlateActivated += OnActivated;
        yield return new WaitForSeconds(0.5f);
        player = GameObject.FindWithTag("Player").GetComponent<SoftBodyPlayer>();
        player.InputEnabled = false;

        Condenser[] freezers = FindObjectsByType<Condenser>(FindObjectsSortMode.None)
            .OrderBy(freezer => freezer.transform.position.x).ToArray();
        Require(freezers.Length == 3, "Expected all three Area2 freezers.");
        for (int i = 0; i < freezers.Length; i++)
        {
            Condenser freezer = freezers[i];
            Collider2D footprint = freezer.GetComponent<Collider2D>();
            Require(footprint.isTrigger, "Freezer body still blocks the ice route.");
            Vector2 exit = freezer.GetSolidExitPosition(player.pointRadius);
            float halfExtent = 0.5f + player.pointRadius;
            Require(exit.x + halfExtent < footprint.bounds.min.x, "Output overlaps the machine.");
            RaycastHit2D floor = Physics2D.Raycast(exit, Vector2.down, halfExtent + 0.1f,
                LayerMask.GetMask("Ground", "Platform"));
            Require(floor.collider != null && Mathf.Abs(exit.y - halfExtent - floor.point.y - 0.05f) < 0.01f,
                "Output is not supported at floor level.");

            // Setup each independent room as liquid at the real intake.
            player.Unfreeze();
            Vector2 intake = new Vector2(footprint.bounds.min.x + 0.1f, footprint.bounds.center.y);
            player.changeBodyState(PlayerBodyState.Liquid, intake, Vector2.zero);
            player.TeleportTo(intake, Vector2.zero);
            float timeout = Time.realtimeSinceStartup + 3f;
            while (player.getBodyState() != PlayerBodyState.Solid && Time.realtimeSinceStartup < timeout)
                yield return null;
            Require(player.getBodyState() == PlayerBodyState.Solid, "Intake did not freeze the player.");
            Require(Vector2.Distance(PlayerPosition, exit) < 0.25f, "Freezer did not release on its return side.");
            Require(player.solidJumpForce == 0f, "Test requires non-jumping ice.");
            yield return new WaitForSeconds(0.8f);
            Debug.Log("[FreezerRouteQA] Settled " + i + " at " + PlayerPosition +
                " point range=" + player.Points.Min(p => p.position.y) + ".." + player.Points.Max(p => p.position.y));
            Capture("freezer-route-" + i + ".png");
            Require(Mathf.Abs(PlayerPosition.y - exit.y) < 0.3f,
                "Released ice fell through its supporting floor: " + PlayerPosition + " expected " + exit);

            string connection = i == 0 ? "intro_door" : i == 1 ? "familiarity_crushers" : "challenge_exit";
            Require(!activated.Contains(connection), "Freezing activated the plate before the return journey.");
            if (i < 2)
            {
                yield return MoveTo(i == 0 ? 9.5f : 56.5f);
                Require(activated.Contains(connection), "Frozen return did not activate " + connection);
                yield return MoveTo(footprint.bounds.max.x + 0.75f);
                Require(player.getBodyState() == PlayerBodyState.Solid, "Crossing the freezer changed the body unexpectedly.");
            }
            else
            {
                // The station is passable in ice. Cross its landing, then descend
                // to the switch with the same S/Down handler as the player.
                yield return MoveTo(82.5f);
                dropping = true;
                while (!activated.Contains(connection) && PlayerPosition.y > 2f) yield return null;
                dropping = false;
                Require(activated.Contains(connection), "No-jump descent missed the challenge switch at " + PlayerPosition);
                // Leave the bottom landing and pass the divider to its exit side.
                dropping = true;
                while (PlayerPosition.y > 2.2f) yield return null;
                dropping = false;
                yield return MoveTo(87.5f);
            }
            Debug.Log("[FreezerRouteQA] PASS " + connection + ": froze, returned/dropped to plate, continued without jumping.");
        }
        EventManager.OnPressurePlateActivated -= OnActivated;
        SessionState.SetBool(PendingKey, false);
        Debug.Log("[FreezerRouteQA] PASS all three no-jump freezer routes.");
        EditorApplication.Exit(0);
    }

    private IEnumerator MoveTo(float x)
    {
        targetX = x;
        float timeout = Time.realtimeSinceStartup + 15f;
        while (Mathf.Abs(PlayerPosition.x - x) > 0.25f && Time.realtimeSinceStartup < timeout)
            yield return null;
        Require(Mathf.Abs(PlayerPosition.x - x) <= 0.25f,
            "Blocked moving to x=" + x + " from " + PlayerPosition);
        targetX = null;
        yield return new WaitForSeconds(0.25f);
    }

    private void FixedUpdate()
    {
        if (player == null) return;
        float speed = targetX.HasValue ? Mathf.Clamp((targetX.Value - PlayerPosition.x) * 5f, -3f, 3f) : 0f;
        foreach (Rigidbody2D point in player.Points)
            point.linearVelocity = new Vector2(speed, point.linearVelocity.y);
        if (dropping)
            DropProbe.Invoke(player.GetComponent<PlatformDropThrough>(), new object[] { Vector2.down });
    }

    private void Update()
    {
        if (deadline > 0f && Time.realtimeSinceStartup > deadline)
            Require(false, "Traversal timed out at " + (player != null ? PlayerPosition.ToString() : "startup"));
    }

    private void OnActivated(string id) => activated.Add(id);

    private void Capture(string name)
    {
        var go = new GameObject("FreezerQA_Camera");
        var camera = go.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 4f;
        camera.transform.position = new Vector3(PlayerPosition.x, PlayerPosition.y + 1f, -10f);
        var rt = new RenderTexture(1200, 700, 24);
        camera.targetTexture = rt;
        camera.aspect = 1200f / 700f;
        camera.Render();
        RenderTexture.active = rt;
        var pixels = new Texture2D(1200, 700, TextureFormat.RGB24, false);
        pixels.ReadPixels(new Rect(0, 0, 1200, 700), 0, 0);
        pixels.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath, "..", name), pixels.EncodeToPNG());
        RenderTexture.active = null;
        camera.targetTexture = null;
        Destroy(pixels);
        Destroy(rt);
        Destroy(go);
    }

    private static void Require(bool condition, string message)
    {
        if (condition) return;
        Debug.LogError("[FreezerRouteQA] FAIL: " + message);
        SessionState.SetBool(PendingKey, false);
        EditorApplication.Exit(1);
        throw new InvalidOperationException(message);
    }
}
#endif
