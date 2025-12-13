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

    // ============== BLOCK PROPS ==============

    // Squares
    public static readonly BlockProp Block2x2 = new("block_2x2", "2x2 Block", 16, 16);
    public static readonly BlockProp Block3x3 = new("block_3x3", "3x3 Block", 24, 24);

    // Bars (short=3, med=5, long=8 tiles)
    public static readonly BlockProp Block3x1 = new("block_3x1", "3x1 Block", 24, 8);
    public static readonly BlockProp Block5x1 = new("block_5x1", "5x1 Block", 40, 8);
    public static readonly BlockProp Block8x1 = new("block_8x1", "8x1 Block", 64, 8);

    // L-shape
    public static readonly LBlockProp BlockL = new();

    // ============== HAZARDS ==============

    public static readonly DustSpinnerProp DustSpinner = new();
    public static readonly CrystalSpinnerProp CrystalSpinner = new();
    public static readonly SpikesProp Spikes = new("spikes", "Spikes", 24);
    public static readonly SpikesProp SpikesLong = new("spikes_long", "Long Spikes", 48);
    public static readonly BumperProp Bumper = new();
    public static readonly PufferProp Puffer = new();
    public static readonly KevinProp Kevin = new("kevin", "Kevin", 32, 32);
    public static readonly FireBarProp FireBar = new();

    // ============== DEADLY BLOCKS ==============

    // Lava blocks - squares
    public static readonly LavaBlockProp Lava2x2 = new("lava_2x2", "Lava 2x2", 16, 16);
    public static readonly LavaBlockProp Lava3x3 = new("lava_3x3", "Lava 3x3", 24, 24);

    // Lava blocks - bars (short=3, med=5, long=8 tiles)
    public static readonly LavaBlockProp Lava3x1 = new("lava_3x1", "Lava 3x1", 24, 8);
    public static readonly LavaBlockProp Lava5x1 = new("lava_5x1", "Lava 5x1", 40, 8);
    public static readonly LavaBlockProp Lava8x1 = new("lava_8x1", "Lava 8x1", 64, 8);

    // Ice blocks - squares
    public static readonly IceBlockProp Ice2x2 = new("ice_2x2", "Ice 2x2", 16, 16);
    public static readonly IceBlockProp Ice3x3 = new("ice_3x3", "Ice 3x3", 24, 24);

    // Ice blocks - bars (short=3, med=5, long=8 tiles)
    public static readonly IceBlockProp Ice3x1 = new("ice_3x1", "Ice 3x1", 24, 8);
    public static readonly IceBlockProp Ice5x1 = new("ice_5x1", "Ice 5x1", 40, 8);
    public static readonly IceBlockProp Ice8x1 = new("ice_8x1", "Ice 8x1", 64, 8);

    // ============== MOVEMENT AIDS ==============

    public static readonly SpringProp Spring = new();
    public static readonly GreenBoosterProp GreenBooster = new();
    public static readonly RedBoosterProp RedBooster = new();
    public static readonly DashCrystalProp DashCrystal = new();
    public static readonly DoubleDashCrystalProp DoubleDashCrystal = new();
    public static readonly FeatherProp Feather = new();
    public static readonly BounceBlockProp BounceBlock = new("bounce_block", "Bounce Block", 24, 24);

    // ============== PLATFORMS ==============

    public static readonly JumpThroughProp JumpThrough = new("jumpthrough", "Platform", 48);
    public static readonly CrumbleBlockProp CrumbleBlock = new("crumble_block", "Crumble Platform", 24);
    public static readonly FallingBlockProp FallingBlock = new("falling_block", "Falling Block", 24, 24);
    public static readonly DreamBlockProp DreamBlock = new("dream_block", "Dream Block", 24, 24);
    public static readonly ZipMoverProp ZipMover = new("zip_mover", "Zip Mover", 24, 16);
    public static readonly SwapBlockProp SwapBlock = new("swap_block", "Swap Block", 24, 24);
    public static readonly CloudProp Cloud = new();

    // ============== INTERACTIVE ==============

    public static readonly IntroCarProp IntroCar = new();

    // ============== COLLECTIBLES ==============

    public static readonly BerryProp Berry = new();

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
        // Block props - squares
        Register(Block2x2);
        Register(Block3x3);

        // Block props - bars
        Register(Block3x1);
        Register(Block5x1);
        Register(Block8x1);

        // Block props - special
        Register(BlockL);

        // Hazards
        Register(DustSpinner);
        Register(CrystalSpinner);
        Register(Spikes);
        Register(SpikesLong);
        Register(Bumper);
        Register(Puffer);
        Register(Kevin);
        Register(FireBar);

        // Deadly blocks - Lava
        Register(Lava2x2);
        Register(Lava3x3);
        Register(Lava3x1);
        Register(Lava5x1);
        Register(Lava8x1);

        // Deadly blocks - Ice
        Register(Ice2x2);
        Register(Ice3x3);
        Register(Ice3x1);
        Register(Ice5x1);
        Register(Ice8x1);

        // Movement aids
        Register(Spring);
        Register(GreenBooster);
        Register(RedBooster);
        Register(DashCrystal);
        Register(DoubleDashCrystal);
        Register(Feather);
        Register(BounceBlock);

        // Platforms
        Register(JumpThrough);
        Register(CrumbleBlock);
        Register(FallingBlock);
        Register(DreamBlock);
        Register(ZipMover);
        Register(SwapBlock);
        Register(Cloud);

        // Interactive objects
        Register(IntroCar);

        // Collectibles
        Register(Berry);
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
