using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

/// <summary>
/// Registry for all available props.
/// </summary>
public static class PropRegistry
{
    private static readonly Dictionary<string, Prop> _props = new();
    private static readonly List<Prop> _propList = new();

    /// <summary>
    /// All registered props.
    /// </summary>
    public static IReadOnlyList<Prop> All => _propList;

    /// <summary>
    /// Registers a prop type.
    /// </summary>
    public static void Register(Prop prop)
    {
        if (_props.ContainsKey(prop.Id))
        {
            UmcLogger.Warn($"Prop '{prop.Id}' is already registered, skipping");
            return;
        }

        _props[prop.Id] = prop;
        _propList.Add(prop);
        UmcLogger.Info($"Registered prop: {prop.Id} ({prop.Name})");
    }

    /// <summary>
    /// Gets a prop by its ID.
    /// </summary>
    public static Prop Get(string id)
    {
        return _props.GetValueOrDefault(id);
    }

    /// <summary>
    /// Registers all built-in props.
    /// </summary>
    public static void RegisterBuiltIn()
    {
        // Block props
        Register(new BlockProp("block_1x1", "1x1 Block", 8, 8));
        Register(new BlockProp("block_1x5", "1x5 Block", 8, 40));
        Register(new BlockProp("block_4x4", "4x4 Block", 32, 32));
        Register(new LBlockProp());

        // Celeste props
        Register(new DustSpinnerProp());
        Register(new IntroCarProp());
        Register(new SpringProp());
    }

    /// <summary>
    /// Clears all registered props.
    /// </summary>
    public static void Clear()
    {
        _props.Clear();
        _propList.Clear();
    }
}

