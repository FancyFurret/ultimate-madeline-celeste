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
    /// Handicap: extra lives granted per player slot (1-indexed in the UI).
    /// </summary>
    public int ExtraLivesSlot1 { get; set; } = 0;
    public int ExtraLivesSlot2 { get; set; } = 0;
    public int ExtraLivesSlot3 { get; set; } = 0;
    public int ExtraLivesSlot4 { get; set; } = 0;
    public int ExtraLivesSlot5 { get; set; } = 0;
    public int ExtraLivesSlot6 { get; set; } = 0;
    public int ExtraLivesSlot7 { get; set; } = 0;
    public int ExtraLivesSlot8 { get; set; } = 0;

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
        // slotIndex is 0-based internally, while settings are 1-based for readability.
        int extra = slotIndex switch
        {
            0 => ExtraLivesSlot1,
            1 => ExtraLivesSlot2,
            2 => ExtraLivesSlot3,
            3 => ExtraLivesSlot4,
            4 => ExtraLivesSlot5,
            5 => ExtraLivesSlot6,
            6 => ExtraLivesSlot7,
            7 => ExtraLivesSlot8,
            _ => 0
        };

        return extra < 0 ? 0 : extra;
    }
}