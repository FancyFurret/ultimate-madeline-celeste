using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Core;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Utilities;

public static class VirtualInputExtensions
{
    public static VirtualJoystick Clone(this VirtualJoystick joystick, int gamepadIndex, InputDeviceType deviceType)
    {
        return new VirtualJoystick(
            FilterBinding(joystick.Up, deviceType),
            FilterBinding(joystick.UpAlt, deviceType),
            FilterBinding(joystick.Down, deviceType),
            FilterBinding(joystick.DownAlt, deviceType),
            FilterBinding(joystick.Left, deviceType),
            FilterBinding(joystick.LeftAlt, deviceType),
            FilterBinding(joystick.Right, deviceType),
            FilterBinding(joystick.RightAlt, deviceType),
            gamepadIndex,
            joystick.Threshold,
            joystick.OverlapBehavior);
    }

    public static VirtualButton Clone(this VirtualButton button, int gamepadIndex, InputDeviceType deviceType)
    {
        return new VirtualButton(FilterBinding(button.Binding, deviceType), gamepadIndex, button.BufferTime, button.Threshold);
    }

    public static VirtualIntegerAxis Clone(this VirtualIntegerAxis axis, int gamepadIndex, InputDeviceType deviceType)
    {
        return new VirtualIntegerAxis(
            FilterBinding(axis.Negative, deviceType),
            FilterBinding(axis.NegativeAlt, deviceType),
            FilterBinding(axis.Positive, deviceType),
            FilterBinding(axis.PositiveAlt, deviceType),
            gamepadIndex,
            axis.Threshold);
    }

    /// <summary>
    /// Creates a new Binding containing only the bindings for the specified device type.
    /// </summary>
    private static Binding FilterBinding(Binding original, InputDeviceType deviceType)
    {
        if (original == null) return null;
        
        var filtered = new Binding();

        if (deviceType == InputDeviceType.Keyboard)
        {
            // Only copy keyboard bindings
            foreach (var key in original.Keyboard)
                filtered.Keyboard.Add(key);
        }
        else
        {
            // Only copy controller bindings
            foreach (var button in original.Controller)
                filtered.Controller.Add(button);
        }

        return filtered;
    }
}