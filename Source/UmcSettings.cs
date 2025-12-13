using System.Collections.Generic;
using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.UltimateMadelineCeleste;


public class UmcSettings : EverestModuleSettings
{
    // Defaults
    private bool _enabled = true;

    /// <summary>
    /// Default lives each player starts with at the beginning of each round.
    /// </summary>
    public int DefaultLives { get; set; } = 1;

    /// <summary>
    /// Handicap: extra lives granted per player slot index (0-based).
    /// Keys are player slot indices; values are additional lives (clamped to >= 0).
    /// </summary>
    public Dictionary<int, int> ExtraLivesBySlot { get; set; } = new();

    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
        }
    }

    [DefaultButtonBinding(Buttons.A, Keys.C)]
    public ButtonBinding ButtonJoin { get; set; }

    [DefaultButtonBinding(Buttons.Back, Keys.Back)]
    public ButtonBinding ButtonLeave { get; set; }

    [DefaultButtonBinding(Buttons.RightTrigger, Keys.LeftShift)]
    public ButtonBinding ButtonCursorSprint { get; set; }

    [DefaultButtonBinding(Buttons.RightShoulder, Keys.E)]
    public ButtonBinding ButtonRotateRight { get; set; }

    [DefaultButtonBinding(Buttons.LeftShoulder, Keys.Q)]
    public ButtonBinding ButtonRotateLeft { get; set; }

    public int GetExtraLivesForSlot(int slotIndex)
    {
        if (ExtraLivesBySlot == null) return 0;
        if (!ExtraLivesBySlot.TryGetValue(slotIndex, out var extra)) return 0;
        return extra < 0 ? 0 : extra;
    }
}