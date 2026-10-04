using PKBank.Game.Logging;
using Silk.NET.Input;

namespace PKBank.Game.Input;

public class InputEvents
{
    public IInputDevice? Unsubscribe(IInputDevice device)
    {
        switch (device)
        {
            case IKeyboard k:
                k.KeyDown -= KeyDown;
                k.KeyUp -= KeyUp;
                return k;
            case IGamepad g:
                g.ButtonDown -= ButtonDown;
                g.ButtonUp -= ButtonUp;
                return g;
            case IJoystick j:
                j.ButtonDown -= ButtonDown;
                j.ButtonUp -= ButtonUp;
                return j;
        }

        return null;
    }

    public IInputDevice? Subscribe(IInputDevice device)
    {
        switch (device)
        {
            case IKeyboard k:
                k.KeyDown += KeyDown;
                k.KeyUp += KeyUp;
                return k;
            case IGamepad g:
                g.ButtonDown += ButtonDown;
                g.ButtonUp += ButtonUp;
                return g;
            case IJoystick j:
                j.ButtonDown += ButtonDown;
                j.ButtonUp += ButtonUp;
                return j;
        }

        return null;
    }

    private void KeyDown(IKeyboard keyboard, Key key, int keyCode) =>
        Logger.Trace($"[{nameof(InputManager)}]: PRESSED KEY    : [{keyboard.Index}]: {key}");

    private void KeyUp(IKeyboard keyboard, Key key, int keyCode)
    {
    }

    private void ButtonDown(IInputDevice gamepad, Button buttonKey) =>
        Logger.Trace($"[{nameof(InputManager)}]: PRESSED BUTTON : [{gamepad.Index}]: {buttonKey.Name}");

    private void ButtonUp(IInputDevice gamepad, Button buttonKey)
    {
    }
}
