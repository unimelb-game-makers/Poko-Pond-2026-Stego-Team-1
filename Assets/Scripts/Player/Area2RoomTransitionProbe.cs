#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Isolated Play Mode regression. Teleports set up gate/plate cases; crossing
// uses real player physics and the production scene-loading component.
[DefaultExecutionOrder(10000)]
public class Area2RoomTransitionProbe : MonoBehaviour
{
    private const string Pending = "Area2Rooms.TransitionProbe";
    private SoftBodyPlayer walkingPlayer;
    private float deadline;

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeChanged;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        EditorSceneManager.OpenScene("Assets/Scenes/Area2-1.unity", OpenSceneMode.Single);
        SessionState.SetBool(Pending, true);
        EditorApplication.isPlaying = true;
    }

    private static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        new GameObject("Area2RoomTransitionProbe").AddComponent<Area2RoomTransitionProbe>();
    }

    private IEnumerator Start()
    {
        DontDestroyOnLoad(gameObject);
        Time.captureDeltaTime = 1f / 60f;
        deadline = Time.realtimeSinceStartup + 150f;
        for (int i = 0; i < 3; i++)
        {
            string name = $"Area2-{i + 1}";
            yield return new WaitForSeconds(0.6f);
            Require(SceneManager.GetActiveScene().name == name, "Wrong scene at room entry.");
            SoftBodyPlayer player = FindObjectsByType<SoftBodyPlayer>(FindObjectsSortMode.None).Single();
            Require(player.getBodyState() == PlayerBodyState.Liquid, name + " did not start as liquid.");
            Require(Mathf.Abs(player.Center.x - (i == 2 ? 3f : 4f)) < 0.5f && player.Center.y > 1f,
                name + " has an unsafe spawn.");
            Require(FindObjectsByType<Condenser>(FindObjectsSortMode.None).Length == 1, "Foreign room freezer retained.");
            SceneDoorExit exit = FindObjectsByType<SceneDoorExit>(FindObjectsSortMode.None).Single();
            Door door = exit.GetComponent<Door>();
            Require(!door.IsUnlocked, "Exit starts unlocked.");
            Capture("room-" + (i + 1) + "-overview.png", new Vector3((door.transform.position.x + 3f) * 0.5f, 7f, -10f), 12f);

            if (i == 1)
            {
                // Exercise the actual retry button path before solving room 2.
                player.GetComponent<PlayerLife>().Kill();
                Require(GameStateManager.Instance.State == GameState.GameOver, "Death did not show game over.");
                FindObjectsByType<GameOverUI>(FindObjectsSortMode.None).Single().Restart();
                yield return new WaitForSeconds(0.6f);
                Require(SceneManager.GetActiveScene().name == name && Time.timeScale == 1f, "Retry changed rooms or stayed paused.");
                player = FindObjectsByType<SoftBodyPlayer>(FindObjectsSortMode.None).Single();
                exit = FindObjectsByType<SceneDoorExit>(FindObjectsSortMode.None).Single();
                door = exit.GetComponent<Door>();
                Require(player.getBodyState() == PlayerBodyState.Liquid && !door.IsUnlocked, "Retry retained puzzle state.");
            }

            // Even an actor beyond the threshold must not load a locked exit.
            player.TeleportTo((Vector2)door.transform.position + new Vector2(1f, 0.7f), Vector2.zero);
            yield return new WaitForSeconds(0.25f);
            Require(SceneManager.GetActiveScene().name == name && !door.IsUnlocked, "Locked gate allowed progression.");

            Condenser freezer = FindObjectsByType<Condenser>(FindObjectsSortMode.None).Single();
            Bounds footprint = freezer.GetComponent<Collider2D>().bounds;
            player.TeleportTo(new Vector2(footprint.min.x + 0.1f, footprint.center.y), Vector2.zero);
            yield return WaitUntil(() => player.getBodyState() == PlayerBodyState.Solid, "Freezer did not work in " + name);
            yield return new WaitForSeconds(0.25f);
            Require(!door.IsUnlocked, "Freezer unexpectedly unlocked the gate.");
            PressurePlate plate = FindObjectsByType<PressurePlate>(FindObjectsSortMode.None).Single();
            player.TeleportTo((Vector2)plate.transform.position + new Vector2(0f, 0.8f), Vector2.zero);
            yield return WaitUntil(() => door.IsUnlocked, "Solid plate did not unlock " + name);

            player.TeleportTo((Vector2)door.transform.position + new Vector2(-1.1f, 0.7f), Vector2.zero);
            yield return new WaitForSeconds(1f);
            Require(SceneManager.GetActiveScene().name == name && door.IsOpen, "Approaching the open door loaded too early.");
            Capture("room-" + (i + 1) + "-exit.png", Camera.main.transform.position, Camera.main.orthographicSize);
            walkingPlayer = player;
            string destination = i == 2 ? "Area3-1" : $"Area2-{i + 2}";
            yield return WaitUntil(() => SceneManager.GetActiveScene().name == destination, "Crossing failed: " + name);
            walkingPlayer = null;
            Debug.Log("[Area2RoomsQA] PASS " + name + ": spawn, locked gate, freezer, plate, open gate, crossing -> " + destination);
        }
        yield return new WaitForSeconds(0.7f);
        Require(SceneManager.GetActiveScene().name == "Area3-1", "Final destination not reached.");
        Require(FindObjectsByType<SoftBodyPlayer>(FindObjectsSortMode.None).Single().getBodyState() == PlayerBodyState.Liquid,
            "Area3 entry did not reset to liquid.");
        Capture("area3-entry.png", Camera.main.transform.position, Camera.main.orthographicSize);
        yield return null;
        Debug.Log("[Area2RoomsQA] PASS all transitions and room-2 death/retry.");
        EditorApplication.Exit(0);
    }

    private void FixedUpdate()
    {
        if (walkingPlayer == null) return;
        foreach (Rigidbody2D point in walkingPlayer.Points)
            point.linearVelocity = new Vector2(3f, point.linearVelocity.y);
    }

    private static void Capture(string name, Vector3 position, float size)
    {
        var cameraObject = new GameObject("RoomQA_Camera");
        Camera camera = cameraObject.AddComponent<Camera>();
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
        Destroy(pixels);
        Destroy(target);
        Destroy(cameraObject);
    }

    private void Update()
    {
        if (Time.realtimeSinceStartup > deadline) Fail("Overall timeout.");
    }

    private static IEnumerator WaitUntil(Func<bool> condition, string error)
    {
        float end = Time.realtimeSinceStartup + 10f;
        while (!condition())
        {
            if (Time.realtimeSinceStartup > end) Fail(error);
            yield return null;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) Fail(message);
    }

    private static void Fail(string message)
    {
        Debug.LogError("[Area2RoomsQA] FAIL " + message);
        EditorApplication.Exit(1);
        throw new InvalidOperationException(message);
    }
}
#endif
