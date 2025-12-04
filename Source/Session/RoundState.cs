using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;

namespace Celeste.Mod.UltimateMadelineCeleste.Session;

/// <summary>
/// Tracks state for the current round/game being played.
/// Created when entering a level, cleared when returning to lobby.
/// </summary>
public class RoundState
{
    public static RoundState Current { get; private set; }

    /// <summary>
    /// The map SID being played.
    /// </summary>
    public string MapSID { get; init; }

    /// <summary>
    /// Per-player stats for the round.
    /// </summary>
    public Dictionary<int, PlayerRoundStats> PlayerStats { get; } = new();

    /// <summary>
    /// Whether the round has started (players can move).
    /// </summary>
    public bool HasStarted { get; private set; }

    /// <summary>
    /// Whether the round has ended (win/lose condition met).
    /// </summary>
    public bool HasEnded { get; private set; }


    public static void Start(string mapSID)
    {
        Current = new RoundState()
        {
            MapSID = mapSID,
            HasStarted = true,
            HasEnded = false,
        };
        UmcLogger.Info("Round started");
    }

    /// <summary>
    /// Ends the round.
    /// </summary>
    public void End()
    {
        if (HasEnded) return;
        HasEnded = true;
    }

    /// <summary>
    /// Gets or creates stats for a player.
    /// </summary>
    public PlayerRoundStats GetPlayerStats(UmcPlayer player)
    {
        if (!PlayerStats.TryGetValue(player.SlotIndex, out var stats))
        {
            stats = new PlayerRoundStats(player.SlotIndex);
            PlayerStats[player.SlotIndex] = stats;
        }
        return stats;
    }

    /// <summary>
    /// Records a death for a player.
    /// </summary>
    public void RecordPlayerDeath(UmcPlayer player)
    {
        var stats = GetPlayerStats(player);
        stats.Deaths++;
        UmcLogger.Info($"Player {player.Name} died (total deaths: {stats.Deaths})");
    }

    /// <summary>
    /// Clears the current round state.
    /// </summary>
    public static void Clear()
    {
        if (Current != null)
        {
            UmcLogger.Info("RoundState cleared");
            Current = null;
        }
    }
}

/// <summary>
/// Per-player statistics for a round.
/// </summary>
public class PlayerRoundStats
{
    public int SlotIndex { get; }
    public int Deaths { get; set; }
    public int Score { get; set; }

    public PlayerRoundStats(int slotIndex)
    {
        SlotIndex = slotIndex;
        Deaths = 0;
        Score = 0;
    }
}

