// How a trigger (pressure plate, lever, button) affects a linked prop.
public enum ConnectionMode
{
    // Prop state matches the trigger state: active while trigger is held, reverts when released.
    Hold,
    // Each trigger press flips the prop state. Trigger release has no effect.
    Toggle,
}
