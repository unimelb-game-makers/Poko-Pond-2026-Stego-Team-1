using UnityEngine;
using UnityEngine.Rendering.Universal;

// Noisy intensity wobble with occasional dropouts, for faulty factory lamps.
[RequireComponent(typeof(Light2D))]
public class LightFlicker : MonoBehaviour
{
    [Tooltip("Fraction of base intensity the noise can remove.")]
    [Range(0f, 1f)] public float noiseAmount = 0.15f;
    public float noiseSpeed = 3f;
    [Tooltip("Average dropouts per second. 0 disables them.")]
    public float dropoutRate = 0.1f;
    [Range(0f, 1f)] public float dropoutIntensity = 0.1f;
    public float dropoutDuration = 0.08f;

    private Light2D _light;
    private float _baseIntensity;
    private float _seed;
    private float _dropoutUntil;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _baseIntensity = _light.intensity;
        _seed = Random.value * 100f;
    }

    private void Update()
    {
        if (dropoutRate > 0f && Random.value < dropoutRate * Time.deltaTime)
            _dropoutUntil = Time.time + dropoutDuration * Random.Range(0.5f, 1.5f);

        float noise = Mathf.PerlinNoise(_seed, Time.time * noiseSpeed);
        float intensity = _baseIntensity * (1f - noiseAmount * noise);
        if (Time.time < _dropoutUntil)
            intensity *= dropoutIntensity;
        _light.intensity = intensity;
    }
}
