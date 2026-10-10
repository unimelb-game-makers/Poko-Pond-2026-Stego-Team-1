using UnityEngine;
using UnityEngine.Rendering.Universal;

// Fades a light between on/off looks based on the parent prop's powered state.
[RequireComponent(typeof(Light2D))]
public class PropLight : MonoBehaviour
{
    public Color onColor = new Color(1f, 0.62f, 0.3f);
    public float onIntensity = 1.2f;
    public Color offColor = new Color(1f, 0.62f, 0.3f);
    public float offIntensity = 0f;
    [Tooltip("Seconds to blend between states.")]
    public float fadeTime = 0.2f;
    [Tooltip("Brief overshoot when switching on.")]
    public float powerUpFlash = 0.6f;

    private Light2D _light;
    private IPropPowered _prop;
    private float _blend;
    private float _flash;
    private bool _wasPowered;

    private void Awake()
    {
        _light = GetComponent<Light2D>();
        _prop = GetComponentInParent<IPropPowered>();
        _wasPowered = _prop != null && _prop.IsPowered;
        _blend = _wasPowered ? 1f : 0f;
        Apply();
    }

    private void Update()
    {
        if (_prop == null) return;
        bool powered = _prop.IsPowered;
        if (powered && !_wasPowered)
            _flash = powerUpFlash;
        _wasPowered = powered;

        float step = fadeTime > 0f ? Time.deltaTime / fadeTime : 1f;
        _blend = Mathf.MoveTowards(_blend, powered ? 1f : 0f, step);
        _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 3f);
        Apply();
    }

    private void Apply()
    {
        _light.color = Color.Lerp(offColor, onColor, _blend);
        _light.intensity = Mathf.Lerp(offIntensity, onIntensity, _blend) + _flash;
    }
}
