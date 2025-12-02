using System.Collections.Generic;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.UltimateMadelineCeleste.Utilities;

public abstract class HookedFeature<T> where T : class
{
    public static T Instance { get; private set; }

    private bool _hooked;
    private readonly HashSet<Hook> _hooks = new();
    private readonly HashSet<ILHook> _ilHooks = new();

    protected HookedFeature()
    {
        Instance = this as T;
    }

    public virtual void Load()
    {
        if (_hooked) return;
        
        Hook();
        _hooked = true;
    }

    public virtual void Unload()
    {
        if (!_hooked) return;
        
        Unhook();
        _hooked = false;
    }


    protected virtual void Hook()
    {
    }

    protected virtual void Unhook()
    {
        foreach (var hook in _hooks)
            hook.Dispose();
        foreach (var ilHook in _ilHooks)
            ilHook.Dispose();

        _hooks.Clear();
        _ilHooks.Clear();
    }

    protected void AddHook(Hook hook)
    {
        _hooks.Add(hook);
    }

    protected void AddHook(ILHook ilHook)
    {
        _ilHooks.Add(ilHook);
    }
}