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
}