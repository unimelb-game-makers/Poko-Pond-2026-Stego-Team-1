using UnityEngine;

// Spawns self-contained, self-destroying water-drop particle bursts at a world position.
// All configuration is done in code — no prefabs or assets required.
public static class WaterParticleEffect
{
    private static Texture2D _dropTex;

    // ── Public API ────────────────────────────────────────────────────────

    // Split: two side-spraying fans (left + right) for a cartoonish tear effect
    public static void PlaySplit(Vector2 pos, Color color,
                                 string sortingLayer = "Default", int sortingOrder = 1)
    {
        SpawnSplitFan(pos, color, sortingLayer, sortingOrder, goingLeft: true);
        SpawnSplitFan(pos, color, sortingLayer, sortingOrder, goingLeft: false);
    }

    // Merge: radial burst in all directions
    public static void PlayMerge(Vector2 pos, Color color,
                                 string sortingLayer = "Default", int sortingOrder = 1)
    {
        var go = NewGO("[FX_Merge]", pos, 1.0f);
        var ps   = go.AddComponent<ParticleSystem>();
        SetupRenderer(go, color, sortingLayer, sortingOrder);
        ConfigureMerge(ps, color);
        ps.Play();
    }

    // ── Split internals ───────────────────────────────────────────────────

    private static void SpawnSplitFan(Vector2 pos, Color color,
                                       string sortingLayer, int sortingOrder, bool goingLeft)
    {
        var go = NewGO("[FX_Split]", pos, 1.0f);
        var ps   = go.AddComponent<ParticleSystem>();
        SetupRenderer(go, color, sortingLayer, sortingOrder);
        ConfigureSplitFan(ps, color, goingLeft);
        ps.Play();
    }

    private static void ConfigureSplitFan(ParticleSystem ps, Color color, bool goingLeft)
    {
        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.stopAction      = ParticleSystemStopAction.Destroy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.5f,  6.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.08f, 0.20f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   Color.Lerp(color, Color.white, 0.20f),
                                   Color.Lerp(color, Color.white, 0.65f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.maxParticles    = 16;

        // 8-12 particles per side
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 8, 12) });

        // Cone pointing left or right — default cone opens toward +Y,
        // so rotate ±90° around Z to aim sideways
        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Cone;
        sh.angle     = 50f;
        sh.radius    = 0.05f;
        sh.rotation  = goingLeft ? new Vector3(0f, 0f, 90f) : new Vector3(0f, 0f, -90f);

        // Pop then shrink — fast falloff for a snappy cartoon look
        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f,  0f,   -2.5f),
            new Keyframe(1f, 0f, -2.5f,  0f)));
    }

    // ── Merge internals ───────────────────────────────────────────────────

    private static void ConfigureMerge(ParticleSystem ps, Color color)
    {
        var main = ps.main;
        main.loop            = false;
        main.playOnAwake     = false;
        main.stopAction      = ParticleSystemStopAction.Destroy;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(0.15f, 0.32f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(2.0f,  5.5f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.09f, 0.22f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
                                   color,
                                   Color.Lerp(color, Color.white, 0.65f));
        main.gravityModifier = new ParticleSystem.MinMaxCurve(3.0f, 5.0f);
        main.startRotation   = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
        main.maxParticles    = 30;

        // 18-24 particles in a radial burst
        var em = ps.emission;
        em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18, 24) });

        // Full sphere — radial burst in all directions
        var sh = ps.shape;
        sh.enabled   = true;
        sh.shapeType = ParticleSystemShapeType.Sphere;
        sh.radius    = 0.06f;

        var sol = ps.sizeOverLifetime;
        sol.enabled = true;
        sol.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 1f,  0f,   -2.5f),
            new Keyframe(1f, 0f, -2.5f,  0f)));
    }

    // ── Shared helpers ────────────────────────────────────────────────────

    private static GameObject NewGO(string name, Vector2 pos, float destroyAfter)
    {
        var go = new GameObject(name);
        go.hideFlags       = HideFlags.HideInHierarchy;
        go.transform.position = pos;
        Object.Destroy(go, destroyAfter); // failsafe — fires even if stopAction somehow skips
        return go;
    }

    private static void SetupRenderer(GameObject go, Color color,
                                       string sortingLayer, int sortingOrder)
    {
        var rend = go.GetComponent<ParticleSystemRenderer>();
        var mat  = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
        mat.mainTexture       = GetDropTex();
        mat.color             = Color.white;
        rend.material         = mat;
        rend.sortingLayerName = sortingLayer;
        rend.sortingOrder     = sortingOrder;
        rend.renderMode       = ParticleSystemRenderMode.Billboard;
    }

    // Procedural soft-circle texture — cached, 32×32 RGBA
    private static Texture2D GetDropTex()
    {
        if (_dropTex != null) return _dropTex;

        const int sz = 32;
        _dropTex = new Texture2D(sz, sz, TextureFormat.RGBA32, false);
        float c   = sz * 0.5f;
        var   px  = new Color32[sz * sz];

        for (int y = 0; y < sz; y++)
        for (int x = 0; x < sz; x++)
        {
            float d = Mathf.Sqrt((x - c) * (x - c) + (y - c) * (y - c));
            float a = Mathf.Clamp01(1f - d / (c - 0.5f));
            a = a * a;
            px[y * sz + x] = new Color32(255, 255, 255, (byte)(a * 255));
        }

        _dropTex.SetPixels32(px);
        _dropTex.Apply(false);
        _dropTex.filterMode = FilterMode.Bilinear;
        _dropTex.wrapMode   = TextureWrapMode.Clamp;
        return _dropTex;
    }
}
