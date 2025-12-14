using Microsoft.Xna.Framework;

namespace Celeste.Mod.UltimateMadelineCeleste.Session;

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

public class UmcPlayer
{
    public ulong ClientId { get; set; }
    public int SlotIndex { get; set; }
    public string Name { get; set; }
    public bool IsLocal { get; set; }
    public InputDevice Device { get; set; }
    public string SkinId { get; set; }
    public int ColorIndex => SlotIndex;
    public RemotePlayerNetworkState LastNetworkState { get; set; }

    /// <summary>
    /// Maximum/starting lives for this player. Set from RoundSettings.DefaultLives.
    /// Used in lobby to display lives. During gameplay, use RoundState stats instead.
    /// </summary>
    public int MaxLives { get; set; }

    /// <summary>
    /// When true, player can change facing direction but cannot move.
    /// </summary>
    public bool InDanceMode { get; private set; }

    /// <summary>
    /// When true, player should be hidden (e.g., after someone else wins).
    /// </summary>
    public bool IsHidden { get; set; }

    public void SetDanceMode(bool enabled)
    {
        InDanceMode = enabled;
    }

    /// <summary>
    /// Resets transient state (dance mode, hidden) for lobby return or new game.
    /// Note: MaxLives is NOT reset here - it persists from balancer settings.
    /// </summary>
    public void ResetState()
    {
        InDanceMode = false;
        IsHidden = false;
    }
}

public class RemotePlayerNetworkState
{
    public Vector2 Position { get; set; }
    public Facings Facing { get; set; }
    public Vector2 Scale { get; set; }
    public Vector2 Speed { get; set; }
    public int AnimationFrame { get; set; }
    public bool Dead { get; set; }
}

