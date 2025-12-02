using System;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Core;

public enum InputDeviceType
{
    Keyboard,
    Controller
}

public class InputDevice
{
    public InputDeviceType Type { get; init; }
    public int ControllerIndex { get; init; }

    public static InputDevice Keyboard => new() { Type = InputDeviceType.Keyboard, ControllerIndex = -1 };
    public static InputDevice Controller(int index) => new() { Type = InputDeviceType.Controller, ControllerIndex = index };

    public override bool Equals(object obj)
    {
        if (obj is not InputDevice other) return false;
        return Type == other.Type && ControllerIndex == other.ControllerIndex;
    }

    public override int GetHashCode() => (Type, ControllerIndex).GetHashCode();
}

public enum GamePhase
{
    /// <summary>In lobby, players joining/leaving.</summary>
    Lobby,
}

public class UmcSession
{
    public static UmcSession Instance { get; private set; }
    public static bool Started => Instance != null;

    public PlayersController Players { get; } = new();

    public GamePhase Phase { get; private set; } = GamePhase.Lobby;

    // Events
    public event Action<GamePhase> OnPhaseChanged;

    private UmcSession() { }

    public static void Start()
    {
        if (Instance != null)
        {
            UmcLogger.Error("UmcSession already started");
            return;
        }

        Instance = new UmcSession();
        UmcLogger.Info("UmcSession started");
    }

    public static void End()
    {
        if (Instance == null) return;

        Instance.Players.Clear();
        Instance = null;
        UmcLogger.Info("UmcSession ended");
    }

    /// <summary>
    /// Sets the game phase.
    /// </summary>
    public void SetPhase(GamePhase phase)
    {
        if (Phase == phase) return;
        Phase = phase;
        UmcLogger.Info($"Game phase: {phase}");
        OnPhaseChanged?.Invoke(phase);
    }

    // ========================================================================
    // Input Detection (convenience methods)
    // ========================================================================

    /// <summary>
    /// Detects which input device pressed confirm.
    /// Only returns devices that are not already taken.
    /// </summary>
    public InputDevice DetectJoinInput()
    {
        // Check keyboard
        if (MInput.Keyboard.Pressed(Keys.J))
        {
            var keyboardDevice = InputDevice.Keyboard;
            if (!Players.IsDeviceTaken(keyboardDevice))
                return keyboardDevice;
        }

        // Check gamepads
        for (var i = 0; i < 4; i++)
        {
            var gamepad = MInput.GamePads[i];
            if (!gamepad.Attached) continue;

            if (gamepad.Pressed(Buttons.A) || gamepad.Pressed(Buttons.Start))
            {
                var controllerDevice = InputDevice.Controller(i);
                if (!Players.IsDeviceTaken(controllerDevice))
                    return controllerDevice;
            }
        }

        return null;
    }
}
