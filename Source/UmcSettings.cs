using Microsoft.Xna.Framework.Input;

namespace Celeste.Mod.UltimateMadelineCeleste;


public class UmcSettings : EverestModuleSettings
{
    // Defaults
    private bool _enabled = true;

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
}