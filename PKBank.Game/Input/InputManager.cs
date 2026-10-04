using PKBank.Game.Bootstrap;
using PKBank.Game.Input.Types;
using PKBank.Game.Logging;
using Silk.NET.Input;
using Silk.NET.Windowing;

namespace PKBank.Game.Input;

public class InputManager(
    IWindow window,
    InputEvents inputEvents
) : IInitializable
{
    private readonly Dictionary<IInputDevice, InputDeviceDescriptor> _devices = new(ReferenceEqualityComparer.Instance);
    private readonly IInputContext _inputCtx = window.CreateInput();

    public void Initialize()
    {
        RegisterDevices();
        _inputCtx.ConnectionChanged += OnConnectionChanged;
    }

    private void RegisterDevices()
    {
        foreach (var device in _inputCtx.Keyboards.Cast<IInputDevice>()
                     .Concat(_inputCtx.Gamepads)
                     .Concat(_inputCtx.Joysticks))
            TryRegisterDevice(device);
    }

    private void TryRegisterDevice(IInputDevice device)
    {
        if (device is not (IKeyboard or IGamepad or IJoystick)
            || !device.IsConnected
            || _devices.ContainsKey(device)
            || inputEvents.Subscribe(device) is null
           )
        {
            Logger.Trace($"[{nameof(InputManager)}]: FAILED TO REGISTER: [{device.Index}]: {device.Name}");
            return;
        }

        var descriptor = new InputDeviceDescriptor(device);
        _devices[device] = descriptor;
        Logger.Trace($"[{nameof(InputManager)}]: REGISTERED: [{descriptor.Index}]: {descriptor.Name}");
    }

    private void TryUnregisterDevice(IInputDevice device)
    {
        if (!_devices.Remove(device, out var descriptor))
        {
            Logger.Trace($"[{nameof(InputManager)}]: FAILED TO UNREGISTER: [{device.Index}]: {device.Name}");
            return;
        }

        inputEvents.Unsubscribe(device);
        Logger.Trace($"[{nameof(InputManager)}]: UNREGISTERED: [{descriptor.Index}]: {descriptor.Name}");
    }

    private void OnConnectionChanged(IInputDevice device, bool connected)
    {
        if (connected) TryRegisterDevice(device);
        else TryUnregisterDevice(device);
    }
}
