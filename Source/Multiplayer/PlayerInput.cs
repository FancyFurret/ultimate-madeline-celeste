using Celeste.Mod.UltimateMadelineCeleste.Core;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Multiplayer;

/// <summary>
/// Creates dedicated input objects bound to a specific input device.
/// These use the user's actual bindings but redirect them to a specific gamepad.
/// </summary>
public class PlayerInput
{
    public VirtualIntegerAxis MoveX { get; private set; }
    public VirtualIntegerAxis MoveY { get; private set; }
    public VirtualIntegerAxis GliderMoveY { get; private set; }
    public VirtualButton Jump { get; private set; }
    public VirtualButton Dash { get; private set; }
    public VirtualButton Grab { get; private set; }
    public VirtualButton Talk { get; private set; }
    public VirtualButton Pause { get; private set; }
    public VirtualButton QuickRestart { get; private set; }
    public VirtualButton CrouchDash { get; private set; }
    public VirtualJoystick Aim { get; private set; }
    public VirtualJoystick Feather { get; private set; }
    public VirtualJoystick MountainAim { get; private set; }

    public InputDevice Device { get; }

    public PlayerInput(InputDevice device)
    {
        Device = device;

        // Use gamepad index for controllers, or 0 for keyboard (keyboard bindings ignore gamepad index)
        var gamepadIndex = device.Type == InputDeviceType.Controller ? device.ControllerIndex : 0;
        var deviceType = device.Type;

        // Clone all inputs using the Binding property - redirects to specific gamepad
        // Also filter by device type so keyboard bindings don't affect controller players and vice versa
        MoveX = Input.MoveX.Clone(gamepadIndex, deviceType);
        MoveY = Input.MoveY.Clone(gamepadIndex, deviceType);
        GliderMoveY = Input.GliderMoveY.Clone(gamepadIndex, deviceType);

        Jump = Input.Jump.Clone(gamepadIndex, deviceType);
        Dash = Input.Dash.Clone(gamepadIndex, deviceType);
        Grab = Input.Grab.Clone(gamepadIndex, deviceType);
        Talk = Input.Talk.Clone(gamepadIndex, deviceType);
        CrouchDash = Input.CrouchDash.Clone(gamepadIndex, deviceType);
        Pause = Input.Pause.Clone(gamepadIndex, deviceType);
        QuickRestart = Input.QuickRestart.Clone(gamepadIndex, deviceType);

        Aim = Input.Aim.Clone(gamepadIndex, deviceType);
        Feather = Input.Feather.Clone(gamepadIndex, deviceType);
        MountainAim = Input.MountainAim.Clone(gamepadIndex, deviceType);
    }

    /// <summary>
    /// Deregisters all virtual inputs. Call when the player is removed.
    /// </summary>
    public void Deregister()
    {
        MoveX?.Deregister();
        MoveY?.Deregister();
        Jump?.Deregister();
        Dash?.Deregister();
        Grab?.Deregister();
        Talk?.Deregister();
        Pause?.Deregister();
        QuickRestart?.Deregister();
        CrouchDash?.Deregister();
        Aim?.Deregister();
        MountainAim?.Deregister();
    }
}
