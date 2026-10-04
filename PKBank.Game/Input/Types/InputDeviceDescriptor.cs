using Silk.NET.Input;

namespace PKBank.Game.Input.Types;

public sealed record InputDeviceDescriptor(string Name, int Index)
{
    public InputDeviceDescriptor(IInputDevice device) : this(device.Name, device.Index)
    {
    }
}
