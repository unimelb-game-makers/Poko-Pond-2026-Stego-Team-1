using UnityEngine;

// Emits only while the parent prop is powered; live particles finish naturally.
[RequireComponent(typeof(ParticleSystem))]
public class PropParticles : MonoBehaviour
{
    private ParticleSystem _particles;
    private IPropPowered _prop;
    private bool _emitting;

    private void Awake()
    {
        _particles = GetComponent<ParticleSystem>();
        _prop = GetComponentInParent<IPropPowered>();
        _emitting = _particles.isEmitting;
    }

    private void Update()
    {
        if (_prop == null) return;
        bool powered = _prop.IsPowered;
        if (powered == _emitting) return;
        _emitting = powered;
        if (powered)
            _particles.Play();
        else
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
    }
}
