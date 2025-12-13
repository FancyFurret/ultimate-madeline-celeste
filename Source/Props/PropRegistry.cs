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
        // Register(new BlockProp("block_1x1", "1x1 Block", 8, 8));
        // Register(new BlockProp("block_1x2", "1x2 Block", 8, 16));
        // Register(new BlockProp("block_1x5", "1x5 Block", 8, 40));
        Register(new BlockProp("block_2x2", "2x2 Block", 16, 16));
        // Register(new BlockProp("block_3x1", "3x1 Block", 24, 8));
        // Register(new BlockProp("block_4x4", "4x4 Block", 32, 32));
        // Register(new BlockProp("block_6x1", "6x1 Block", 48, 8));
        // Register(new LBlockProp());

        // Hazards
        // Register(new DustSpinnerProp());
        Register(new CrystalSpinnerProp());
        // Register(new SpikesProp("spikes", "Spikes", 36));
        // Register(new BumperProp());
        Register(new PufferProp());
        Register(new KevinProp("kevin", "Kevin", 32, 32));

        // Movement aids
        // Register(new SpringProp());
        // Register(new GreenBoosterProp());
        /////////////// Register(new RedBoosterProp());
        // Register(new DashCrystalProp());
        /////////////// Register(new DoubleDashCrystalProp());
        // Register(new FeatherProp());
        // Register(new BounceBlockProp("bounce_block", "Bounce Block", 24, 24));

        // Platforms
        // Register(new JumpThroughProp("jumpthrough", "Platform", 48));
        // Register(new CrumbleBlockProp("crumble_block", "Crumble Platform", 24));
        // Register(new FallingBlockProp("falling_block", "Falling Block", 24, 24));
        // Register(new DreamBlockProp("dream_block", "Dream Block", 24, 24));
        // Register(new ZipMoverProp("zip_mover", "Zip Mover", 24, 16));
        // Register(new SwapBlockProp("swap_block", "Swap Block", 24, 24));
        // Register(new CloudProp());
        //
        // // Interactive objects
        // Register(new IntroCarProp());

        // Collectibles
        Register(new BerryProp());
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

