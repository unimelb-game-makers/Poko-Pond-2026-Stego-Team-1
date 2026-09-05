#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

// Editor-only integration probe, run in an isolated copy of Sandbox.
[DefaultExecutionOrder(10000)]
public class SandboxMechanicsDemoProbe : MonoBehaviour
{
    private const string Key = "SandboxMechanicsDemoProbe.Pending";
    private float deadline;
    private bool capture;
    private SoftBodyPlayer player;
    private bool climbing;
    private int ascentStage;
    private static readonly float[] AscentX = { 16.5f, 19.5f, 22.5f, 27.2f };
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
            new GameObject("SandboxDemoProbe").AddComponent<SandboxMechanicsDemoProbe>();
    }
    private IEnumerator Start()
    {
        Time.captureDeltaTime = 1f / 60f;
        deadline = Time.realtimeSinceStartup + 90f;
        yield return new WaitForSeconds(.5f);
        player = GameObject.FindWithTag("Player").GetComponent<SoftBodyPlayer>();
        var split = player.GetComponent<PlayerSplitController>();
        var machine = FindObjectsByType<SplittingMachine>(FindObjectsSortMode.None).Single();
        var doors = FindObjectsByType<Door>(FindObjectsSortMode.None).OrderBy(d => d.transform.position.x).ToArray();
        Require(doors.Length == 3, "Expected exactly three demo doors.");
        var red = doors[0]; var yellow = doors[1]; var green = doors[2];
        Require(!red.IsUnlocked && !yellow.IsUnlocked && green.IsUnlocked, "Initial red/yellow/green states are wrong.");
        Require(!split.SplittingUnlocked && !machine.IsActivated, "Splitter must start available for demonstration.");
        // One setup teleport to the shaft floor, then horizontal steering only.
        // All vertical movement must come from real spring contact and physics.
        Place(player, 16.5f, -3.5f);
        climbing = true;
        float climbEnd = Time.time + 18f;
        while (Time.time < climbEnd && !(ascentStage == 3 && Center().y > 16.25f && Mathf.Abs(Center().x - AscentX[3]) < .25f && Mathf.Abs(player.CalculateAverageVelocity().y) < .5f))
            yield return new WaitForFixedUpdate();
        Require(ascentStage == 3 && Center().y > 16.25f && Mathf.Abs(Center().x - AscentX[3]) < .25f && Mathf.Abs(player.CalculateAverageVelocity().y) < .5f,
            "Spring climb could not reach gallery: stage=" + ascentStage + " center=" + Center());
        climbing = false;
        Debug.Log("[SandboxDemoQA] PASS: shaft floor to gallery using three actual spring bounces, horizontal steering only.");
        capture = true;
        Place(player, 28.5f, 17f);
        yield return new WaitForSeconds(1.2f);
        Require(split.SplittingUnlocked && machine.IsActivated, "Machine did not unlock splitting from real overlap.");
        var viewport = Camera.main.WorldToViewportPoint(machine.transform.position + Vector3.up);
        Require(viewport.x > 0 && viewport.x < 1 && viewport.y > 0 && viewport.y < 1,
            "Gallery entrance is outside the gameplay camera: " + viewport);
        Place(player, 25.5f, 16.6f);
        yield return new WaitForSeconds(.7f);
        Require(green.IsOpen, "Green did not open on approach.");
        Place(player, 17.2f, 16.6f);
        yield return new WaitForSeconds(.7f);
        Require(!green.IsOpen && !yellow.IsUnlocked, "Green did not close, or yellow unlocked without its plate.");
        Place(player, 20.5f, 16.6f);
        yield return new WaitForSeconds(.5f);
        Require(yellow.IsUnlocked && !red.IsUnlocked && !green.IsOpen, "Yellow plate affected the wrong doors.");
        Place(player, 17.2f, 16.6f);
        yield return new WaitForSeconds(.7f);
        Require(yellow.IsUnlocked && yellow.IsOpen, "Yellow did not stay unlocked/open on approach after plate release.");
        Place(player, 14.5f, 16.6f);
        yield return new WaitForSeconds(.5f);
        Require(red.IsUnlocked, "Red plate did not unlock red.");
        Place(player, 16.5f, 16.6f);
        yield return new WaitForSeconds(.5f);
        Require(!red.IsUnlocked && yellow.IsUnlocked, "Red release did not relock only red.");
        split.StartCoroutine("SplitCoroutine");
        yield return new WaitForSeconds(.15f);
        Require(split.IsSplit, "Unlocked controller did not create split droplets.");
        var droplets = (SoftBodyPlayer[])typeof(PlayerSplitController).GetField("_droplets", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(split);
        Place(droplets[0], 14.5f, 16.5f);
        Place(droplets[1], 11.5f, 16.5f);
        yield return new WaitForSeconds(.7f);
        Require(red.IsUnlocked && red.IsOpen, "A parked split droplet did not hold red open for the other droplet.");
        Place(droplets[0], 16.5f, 16.5f);
        yield return new WaitForSeconds(.5f);
        Require(!red.IsUnlocked && !red.IsOpen && yellow.IsUnlocked, "Red did not relock when the parked droplet left.");
        Debug.Log("[SandboxDemoQA] PASS: camera, splitter unlock/split, green proximity, yellow one-shot latch, red held/released by split droplet; isolated connections.");
        SessionState.SetBool(Key, false);
        EditorApplication.Exit(0);
    }
    private void Update()
    {
        if (deadline > 0 && Time.realtimeSinceStartup > deadline) Require(false, "Probe timed out.");
    }
    private Vector2 Center()
    {
        var result = Vector2.zero;
        foreach (var point in player.Points) result += point.position;
        return result / player.Points.Length;
    }
    private void FixedUpdate()
    {
        if (!climbing) return;
        Vector2 center = Center();
        float velocityY = player.CalculateAverageVelocity().y;
        if (ascentStage < 3 && velocityY > 20f && Mathf.Abs(center.x - AscentX[ascentStage]) < 1f)
        {
            Debug.Log("[SandboxDemoQA] Spring " + ascentStage + " launched at " + center);
            ascentStage++;
        }
        // Well below the normal horizontal speed cap. Never inject vertical speed.
        float speed = Mathf.Clamp((AscentX[ascentStage] - center.x) * 6f, -4f, 4f);
        foreach (var point in player.Points)
            point.linearVelocity = new Vector2(speed, point.linearVelocity.y);
    }
    private static void Place(SoftBodyPlayer body, float x, float y)
    {
        body.Unfreeze(); body.InputEnabled = false;
        body.TeleportTo(new Vector2(x,y), Vector2.zero);
    }
    private void LateUpdate()
    {
        if (!capture) return;
        capture = false;
        var go = new GameObject("GalleryPreviewCamera"); var camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 4.3f; camera.aspect = 2.5f;
        camera.transform.position = new Vector3(20.5f, 19.4f, -10f);
        var rt = new RenderTexture(1800,720,24); camera.targetTexture = rt;
        camera.Render(); camera.Render(); RenderTexture.active=rt;
        var pixels=new Texture2D(1800,720,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,1800,720),0,0);pixels.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"..","sandbox-gallery.png"),pixels.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Destroy(pixels);Destroy(rt);Destroy(go);
        go = new GameObject("SpringRoutePreviewCamera"); camera = go.AddComponent<Camera>();
        camera.orthographic = true; camera.orthographicSize = 15f; camera.aspect = .8f;
        camera.transform.position = new Vector3(21f, 8.5f, -10f);
        rt = new RenderTexture(960,1200,24); camera.targetTexture = rt;
        camera.Render(); camera.Render(); RenderTexture.active = rt;
        pixels = new Texture2D(960,1200,TextureFormat.RGB24,false);
        pixels.ReadPixels(new Rect(0,0,960,1200),0,0); pixels.Apply();
        System.IO.File.WriteAllBytes(System.IO.Path.Combine(Application.dataPath,"..","sandbox-spring-route.png"),pixels.EncodeToPNG());
        camera.targetTexture=null;RenderTexture.active=null;Destroy(pixels);Destroy(rt);Destroy(go);
    }
    private static void Require(bool condition, string message)
    {
        if (condition) return;
        Debug.LogError("[SandboxDemoQA] FAIL: " + message);
        SessionState.SetBool(Key,false); EditorApplication.Exit(1);
        throw new InvalidOperationException(message);
    }
}
#endif
