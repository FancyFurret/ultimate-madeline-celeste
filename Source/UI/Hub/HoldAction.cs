using System;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Hub;

public enum HoldActionType
{
    Leave,
    BackToSelection,
    GiveUp
}

/// <summary>
/// Tracks a hold-to-confirm action for a player.
/// </summary>
public class HoldAction
{
    public const float DefaultDuration = 2f;
    public const float VisualDelay = 0.5f;

    public UmcPlayer Player { get; }
    public HoldActionType ActionType { get; }
    public float Duration { get; }
    public float HoldTime { get; private set; }
    public ButtonBinding Binding { get; }
    public Action OnComplete { get; }

    /// <summary>
    /// Progress from 0 to 1 (includes visual delay period).
    /// </summary>
    public float Progress => Math.Clamp(HoldTime / Duration, 0f, 1f);

    /// <summary>
    /// Visual progress from 0 to 1 (only counts after visual delay).
    /// </summary>
    public float VisualProgress
    {
        get
        {
            if (HoldTime < VisualDelay) return 0f;
            float visualDuration = Duration - VisualDelay;
            return Math.Clamp((HoldTime - VisualDelay) / visualDuration, 0f, 1f);
        }
    }

    /// <summary>
    /// Whether the visual indicator should be shown.
    /// </summary>
    public bool ShowVisual => HoldTime >= VisualDelay;

    /// <summary>
    /// Whether the hold action is complete.
    /// </summary>
    public bool IsComplete => HoldTime >= Duration;

    public HoldAction(UmcPlayer player, HoldActionType actionType, ButtonBinding binding, Action onComplete, float duration = DefaultDuration)
    {
        Player = player;
        ActionType = actionType;
        Binding = binding;
        OnComplete = onComplete;
        Duration = duration;
        HoldTime = 0f;
    }

    /// <summary>
    /// Updates the hold time. Returns true if the action completed this frame.
    /// </summary>
    public bool Update()
    {
        if (IsComplete) return false;

        HoldTime += Engine.DeltaTime;

        if (IsComplete)
        {
            OnComplete?.Invoke();
            return true;
        }

        return false;
    }

    /// <summary>
    /// Checks if the player is still holding the button.
    /// </summary>
    public bool IsStillHolding()
    {
        if (Player.Device == null) return false;

        if (Player.Device.Type == InputDeviceType.Keyboard)
        {
            foreach (var key in Binding.Keys)
                if (MInput.Keyboard.Check(key)) return true;
            return false;
        }

        var gamepad = MInput.GamePads[Player.Device.ControllerIndex];
        foreach (var button in Binding.Buttons)
            if (gamepad.Check(button)) return true;
        return false;
    }

    /// <summary>
    /// Gets the display name for the action type.
    /// </summary>
    public string GetActionName() => ActionType switch
    {
        HoldActionType.Leave => "Leave",
        HoldActionType.BackToSelection => "Change",
        HoldActionType.GiveUp => "Give Up",
        _ => "Hold"
    };
}

