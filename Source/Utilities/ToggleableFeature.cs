using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.MotionSmoothing.Utilities;

public abstract class ToggleableFeature<T>: HookedFeature<T> where T : class 
{
    public bool Enabled { get; private set; }

    private bool _hooked;
    private readonly HashSet<Hook> _hooks = new();
    private readonly HashSet<ILHook> _ilHooks = new();

    public override void Load()
    {
    }

    public override void Unload()
    {
        Disable();
        base.Unload();
    }


    public virtual void Enable()
    {
        if (!_hooked)
        {
            Hook();
            _hooked = true;
        }

        Enabled = true;
    }

    public virtual void Disable()
    {
        if (_hooked)
        {
            Unhook();
            _hooked = false;
        }

        Enabled = false;
    }
}