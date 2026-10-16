// Implemented by props whose on/off state should drive nearby visuals such as PropLight.
public interface IPropPowered
{
    bool IsPowered { get; }
}
