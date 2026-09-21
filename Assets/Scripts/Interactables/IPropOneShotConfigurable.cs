// Implemented by activators whose per-cell configuration can make the first
// activation permanent. Pressure plates use this to create the yellow,
// one-shot door behaviour described by the level design.
public interface IPropOneShotConfigurable
{
    void SetOneShot(bool oneShot);
}
