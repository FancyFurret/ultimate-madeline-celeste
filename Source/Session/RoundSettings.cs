using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Scoring;

namespace Celeste.Mod.UltimateMadelineCeleste.Session;

/// <summary>
/// A weighted prop entry for the prop pool.
/// </summary>
public class WeightedProp
{
    public Prop Prop { get; set; }
    public float Weight { get; set; }

    public WeightedProp(Prop prop, float weight = 1f)
    {
        Prop = prop;
        Weight = weight;
    }
}

/// <summary>
/// Point values for each score type.
/// </summary>
public class ScoreValues
{
    public float Finish { get; set; } = 5.0f;
    public float FirstPlace { get; set; } = 0.2f;
    public float TrapKill { get; set; } = 0.2f;
    public float Berry { get; set; } = 0.6f;
    public float UnderdogBonus { get; set; } = 0.6f;
    public float Solo { get; set; } = 0.6f;

    /// <summary>
    /// Gets the point value for a given score type.
    /// </summary>
    public float GetPoints(ScoreType type) => type switch
    {
        ScoreType.Finish => Finish,
        ScoreType.FirstPlace => FirstPlace,
        ScoreType.TrapKill => TrapKill,
        ScoreType.Berry => Berry,
        ScoreType.UnderdogBonus => UnderdogBonus,
        ScoreType.Solo => Solo,
        _ => 0f
    };
}

/// <summary>
/// Settings for a round/match. Can be configured per-game.
/// </summary>
public class RoundSettings
{
    /// <summary>
    /// Current active settings for the game.
    /// </summary>
    public static RoundSettings Current { get; private set; } = Default;

    /// <summary>
    /// Name of this preset.
    /// </summary>
    public string Name { get; set; } = "Default";

    // ============== WIN CONDITIONS ==============

    /// <summary>
    /// Points required to win the game.
    /// </summary>
    public float PointsToWin { get; set; } = 5.0f;

    // ============== LIVES ==============

    /// <summary>
    /// Default number of lives per player each round (0 = no respawns).
    /// </summary>
    public int DefaultLives { get; set; } = 0;

    /// <summary>
    /// Point difference required to be considered an underdog.
    /// </summary>
    public float UnderdogThreshold { get; set; } = 1.5f;

    /// <summary>
    /// Minimum players for solo bonus to apply.
    /// </summary>
    public int SoloMinPlayers { get; set; } = 3;
    public int FirstMinPlayers { get; set; } = 3;

    /// <summary>
    /// Total number of rounds to play (0 = unlimited, play until points reached).
    /// </summary>
    public int MaxRounds { get; set; } = 0;

    // ============== SCORING ==============

    /// <summary>
    /// Point values for each score type.
    /// </summary>
    public ScoreValues ScoreValues { get; set; } = new();

    // ============== PARTY BOX ==============

    /// <summary>
    /// Total number of props in the party box.
    /// </summary>
    public int PartyBoxItemCount { get; set; } = 5;

    /// <summary>
    /// Minimum guaranteed props per category.
    /// E.g., { Deadly: 2, Block: 1 } means at least 2 deadly and 1 block.
    /// Remaining slots are filled with weighted random from all props.
    /// </summary>
    public Dictionary<PropCategory, int> MinimumPerCategory { get; set; } = new();

    // ============== PROPS ==============

    /// <summary>
    /// Available props with their weights for random selection.
    /// </summary>
    public List<WeightedProp> AvailableProps { get; set; } = new();

    // ============== STATIC PRESETS ==============

    /// <summary>
    /// Default preset with standard settings.
    /// </summary>
    public static RoundSettings Default => new()
    {
        Name = "Default",
        PointsToWin = 5.0f,
        DefaultLives = 1,
        UnderdogThreshold = 1.5f,
        SoloMinPlayers = 3,
        MaxRounds = 0,
        ScoreValues = new ScoreValues
        {
            Finish = 1.0f,
            FirstPlace = 0.2f,
            TrapKill = 0.2f,
            Berry = 0.6f,
            UnderdogBonus = 0.6f,
            Solo = 0.6f
        },
        PartyBoxItemCount = 5,
        MinimumPerCategory = new Dictionary<PropCategory, int>
        {
            { PropCategory.Deadly, 1 },
            { PropCategory.Block, 1 },
            { PropCategory.Movement, 1 }
        },
        AvailableProps = new List<WeightedProp>
        {
            // ============== DEADLY ==============
            // Spinners
            new(PropRegistry.DustSpinner, 1.0f),
            new(PropRegistry.CrystalSpinner, 1.0f),

            // Spikes
            new(PropRegistry.Spikes, 1.5f),
            new(PropRegistry.SpikesLong, 1.2f),

            // Deadly blocks - Lava
            // new(PropRegistry.Lava2x2, 1.0f),
            // new(PropRegistry.Lava3x3, 0.8f),
            new(PropRegistry.Lava3x1, 1.5f),
            new(PropRegistry.Lava5x1, 1.2f),
            new(PropRegistry.Lava8x1, 0.8f),

            // Deadly blocks - Ice
            new(PropRegistry.Ice2x2, 1.0f),
            new(PropRegistry.Ice3x3, 0.8f),
            // new(PropRegistry.Ice3x1, 1.5f),
            // new(PropRegistry.Ice5x1, 1.2f),
            // new(PropRegistry.Ice8x1, 0.8f),

            // Other hazards
            new(PropRegistry.FireBar, 0.8f),

            // ============== BLOCKS ==============
            // Squares
            new(PropRegistry.Block2x2, 1.5f),
            new(PropRegistry.Block3x3, 1.0f),

            // Bars
            new(PropRegistry.Block3x1, 1.5f),
            new(PropRegistry.Block5x1, 1.2f),
            new(PropRegistry.Block8x1, 0.8f),

            // Special
            new(PropRegistry.BlockL, 0.8f),

            // ============== MOVEMENT ==============
            new(PropRegistry.Bumper, 0.8f),
            new(PropRegistry.Kevin, 0.6f),
            new(PropRegistry.Spring, 1.5f),
            new(PropRegistry.GreenBooster, 1.0f),
            new(PropRegistry.RedBooster, 0.8f),
            new(PropRegistry.DashCrystal, 1.0f),
            new(PropRegistry.DoubleDashCrystal, 0.5f),
            new(PropRegistry.Feather, 0.6f),
            new(PropRegistry.BounceBlock, 0.8f),
            new(PropRegistry.Puffer, 0.8f),

            // ============== PLATFORMS ==============
            new(PropRegistry.JumpThrough, 1.0f),
            new(PropRegistry.CrumbleBlock, 0.8f),
            new(PropRegistry.FallingBlock, 0.6f),
            new(PropRegistry.DreamBlock, 0.5f),
            new(PropRegistry.Cloud, 0.8f),

            // ============== SPECIAL ==============
            new(PropRegistry.ZipMover, 0.4f),
            new(PropRegistry.SwapBlock, 0.4f),
            new(PropRegistry.IntroCar, 0.2f),

            // ============== COLLECTIBLES ==============
            new(PropRegistry.Berry, 1.0f)
        }
    };

    // ============== METHODS ==============

    /// <summary>
    /// Sets the current active settings.
    /// </summary>
    public static void SetCurrent(RoundSettings settings)
    {
        Current = settings ?? Default;
    }

    /// <summary>
    /// Resets to default settings.
    /// </summary>
    public static void ResetToDefault()
    {
        Current = Default;
    }

    /// <summary>
    /// Selects random props respecting minimum category requirements.
    /// First fulfills minimums, then fills remaining slots with weighted random.
    /// Results are shuffled so guaranteed items aren't always first.
    /// </summary>
    /// <param name="random">Random instance to use.</param>
    /// <returns>List of selected props.</returns>
    public List<Prop> SelectRandomProps(Random random = null)
    {
        random ??= new Random();

        var validProps = AvailableProps
            .Where(wp => wp.Prop != null)
            .ToList();

        if (validProps.Count == 0)
        {
            return PropRegistry.All
                .OrderBy(_ => random.Next())
                .Take(PartyBoxItemCount)
                .ToList();
        }

        var result = new List<Prop>();
        var usedProps = new HashSet<Prop>(); // Track used props to avoid exact duplicates

        // Step 1: Fulfill minimum requirements per category
        foreach (var (category, minCount) in MinimumPerCategory)
        {
            var categoryProps = validProps
                .Where(wp => wp.Prop.Category == category && !usedProps.Contains(wp.Prop))
                .ToList();

            for (int i = 0; i < minCount && result.Count < PartyBoxItemCount; i++)
            {
                var prop = SelectWeightedProp(categoryProps, random);
                if (prop != null)
                {
                    result.Add(prop);
                    usedProps.Add(prop);
                }
                else if (categoryProps.Count > 0)
                {
                    // Fallback: allow duplicates if we run out
                    categoryProps = validProps.Where(wp => wp.Prop.Category == category).ToList();
                    prop = SelectWeightedProp(categoryProps, random);
                    if (prop != null) result.Add(prop);
                }
            }
        }

        // Step 2: Fill remaining slots with weighted random from all props
        while (result.Count < PartyBoxItemCount)
        {
            var prop = SelectWeightedProp(validProps, random);
            if (prop != null)
                result.Add(prop);
            else
                break;
        }

        // Step 3: Shuffle so guaranteed items aren't always in the same positions
        Shuffle(result, random);

        return result;
    }

    private Prop SelectWeightedProp(List<WeightedProp> props, Random random)
    {
        if (props.Count == 0) return null;

        var totalWeight = props.Sum(wp => wp.Weight);
        if (totalWeight <= 0) return props[random.Next(props.Count)].Prop;

        var roll = (float)(random.NextDouble() * totalWeight);
        var cumulative = 0f;

        foreach (var wp in props)
        {
            cumulative += wp.Weight;
            if (roll <= cumulative)
                return wp.Prop;
        }

        return props.LastOrDefault()?.Prop;
    }

    private static void Shuffle<T>(List<T> list, Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    /// <summary>
    /// Gets props filtered by category.
    /// </summary>
    public List<WeightedProp> GetPropsInCategory(PropCategory category)
    {
        return AvailableProps
            .Where(wp => wp.Prop?.Category == category)
            .ToList();
    }

    /// <summary>
    /// Gets the point value for a score type using current settings.
    /// </summary>
    public float GetScorePoints(ScoreType type) => ScoreValues.GetPoints(type);
}
