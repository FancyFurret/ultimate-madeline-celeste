using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Creates dedicated input objects bound to a specific input device.
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
    public VirtualButton MenuConfirm { get; private set; }
    public VirtualButton MenuCancel { get; private set; }
    public VirtualButton CursorSprint { get; private set; }

    public InputDevice Device { get; }

    public PlayerInput(InputDevice device)
    {
        Device = device;

        var gamepadIndex = device.Type == InputDeviceType.Controller ? device.ControllerIndex : 0;
        var deviceType = device.Type;

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

        MenuConfirm = Input.MenuConfirm.Clone(gamepadIndex, deviceType);
        MenuCancel = Input.MenuCancel.Clone(gamepadIndex, deviceType);
        CursorSprint = UmcModule.Settings.ButtonCursorSprint.Button.Clone(gamepadIndex, deviceType);
    }

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
        MenuConfirm?.Deregister();
        MenuCancel?.Deregister();
        CursorSprint?.Deregister();
    }
}
