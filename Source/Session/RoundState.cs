using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Scoring;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;

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
    /// Props placed this round, with their owner's slot index.
    /// </summary>
    public List<PlacedProp> PlacedProps { get; } = new();

    /// <summary>
    /// Whether the round has started (players can move).
    /// </summary>
    public bool HasStarted { get; private set; }

    /// <summary>
    /// Whether the round has ended (win/lose condition met).
    /// </summary>
    public bool HasEnded { get; private set; }

    /// <summary>
    /// The current round number (1-indexed).
    /// </summary>
    public int RoundNumber { get; private set; } = 1;

    /// <summary>
    /// Tracks the order players finished (first finisher = 0).
    /// </summary>
    private int _finishOrderCounter = 0;


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
    /// Advances to the next round.
    /// </summary>
    public void NextRound()
    {
        RoundNumber++;
        _finishOrderCounter = 0;

        // Reset round-specific stats but keep cumulative scores
        // Use player's MaxLives (from balancer) instead of default
        var session = GameSession.Instance;
        foreach (var kvp in PlayerStats)
        {
            var player = session?.Players.GetAtSlot(kvp.Key);
            int lives = player?.MaxLives ?? RoundSettings.Current.DefaultLives;
            kvp.Value.ResetForNewRound(lives);
        }

        // Clear placed props for new round
        PlacedProps.Clear();

        HasEnded = false;
        HasStarted = true;
        UmcLogger.Info($"Starting round {RoundNumber}");
    }

    /// <summary>
    /// Gets or creates stats for a player.
    /// </summary>
    public PlayerRoundStats GetPlayerStats(UmcPlayer player)
    {
        if (!PlayerStats.TryGetValue(player.SlotIndex, out var stats))
        {
            stats = new PlayerRoundStats(player.SlotIndex, player.MaxLives);
            PlayerStats[player.SlotIndex] = stats;
        }
        return stats;
    }

    /// <summary>
    /// Records a trap kill for a player.
    /// </summary>
    public void RecordTrapKill(UmcPlayer killer)
    {
        if (killer == null) return;

        var stats = GetPlayerStats(killer);
        stats.TrapKillsThisRound++;
        stats.TotalTrapKills++;
        UmcLogger.Info($"{killer.Name} got a trap kill! (total: {stats.TotalTrapKills}, this round: {stats.TrapKillsThisRound})");
    }

    /// <summary>
    /// Records that a player finished the level.
    /// </summary>
    public void RecordPlayerFinished(UmcPlayer player)
    {
        var stats = GetPlayerStats(player);
        if (stats.FinishedThisRound) return;

        stats.FinishedThisRound = true;
        stats.FinishOrder = _finishOrderCounter++;
        UmcLogger.Info($"Player {player.Name} finished (order: {stats.FinishOrder})");
    }

    /// <summary>
    /// Records that a player collected a strawberry.
    /// </summary>
    public void RecordBerryCollected(UmcPlayer player)
    {
        var stats = GetPlayerStats(player);
        stats.BerriesThisRound++;
        stats.TotalBerries++;
        UmcLogger.Info($"Player {player.Name} collected a berry! (total: {stats.TotalBerries}, this round: {stats.BerriesThisRound})");
    }

    /// <summary>
    /// Returns true if everyone who didn't die finished the level.
    /// Used for "Too Easy! No Points!" detection.
    /// </summary>
    public bool DidEveryoneFinish()
    {
        var players = GameSession.Instance?.Players.All;
        if (players == null || players.Count == 0) return false;
        return PlayerStats.Values.All(s => s.FinishedThisRound);
    }

    /// <summary>
    /// Returns true if nobody finished the level.
    /// Used for "No Winners! No Points!" detection.
    /// </summary>
    public bool DidNoOneFinish()
    {
        return !PlayerStats.Values.Any(s => s.FinishedThisRound);
    }

    /// <summary>
    /// Returns all players who finished, ordered by finish order.
    /// </summary>
    public IEnumerable<(UmcPlayer player, PlayerRoundStats stats)> GetFinishers()
    {
        var session = GameSession.Instance;
        if (session == null) yield break;

        var finishedStats = PlayerStats.Values
            .Where(s => s.FinishedThisRound)
            .OrderBy(s => s.FinishOrder);

        foreach (var stats in finishedStats)
        {
            var player = session.Players.GetAtSlot(stats.SlotIndex);
            if (player != null)
                yield return (player, stats);
        }
    }

    /// <summary>
    /// Returns the count of players who finished this round.
    /// </summary>
    public int GetFinisherCount()
    {
        return PlayerStats.Values.Count(s => s.FinishedThisRound);
    }

    /// <summary>
    /// Returns the first player to finish, or null if nobody finished.
    /// </summary>
    public UmcPlayer GetFirstFinisher()
    {
        return GetFinishers().FirstOrDefault().player;
    }

    /// <summary>
    /// Checks if a player is an underdog (significantly behind the leader).
    /// </summary>
    public bool IsUnderdog(UmcPlayer player)
    {
        var stats = GetPlayerStats(player);
        float leaderScore = PlayerStats.Values.Max(s => s.TotalScore);
        return leaderScore - stats.TotalScore >= ScoringConfig.UnderdogThreshold;
    }

    /// <summary>
    /// Checks if any player has won (reached points to win).
    /// </summary>
    public UmcPlayer GetWinner()
    {
        var session = GameSession.Instance;
        if (session == null) return null;

        foreach (var stats in PlayerStats.Values)
        {
            if (stats.TotalScore >= ScoringConfig.PointsToWin)
            {
                return session.Players.GetAtSlot(stats.SlotIndex);
            }
        }
        return null;
    }

    /// <summary>
    /// Registers a placed prop with its owner.
    /// </summary>
    public void RegisterPlacedProp(PropInstance prop, UmcPlayer owner)
    {
        if (prop?.Entity == null || owner == null) return;

        PlacedProps.Add(new PlacedProp
        {
            Prop = prop,
            Entity = prop.Entity,
            Owner = owner
        });
        UmcLogger.Info($"Registered prop: {prop.Prop.Name} placed by {owner.Name}");
    }

    /// <summary>
    /// Gets the owner of a placed prop entity, or null if not found.
    /// </summary>
    public UmcPlayer GetPropOwner(Entity entity)
    {
        if (entity == null) return null;

        var placed = PlacedProps.FirstOrDefault(p => p.Entity == entity);
        return placed?.Owner;
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
/// A prop placed during a round.
/// </summary>
public class PlacedProp
{
    public PropInstance Prop { get; set; }
    public Entity Entity { get; set; }
    public UmcPlayer Owner { get; set; }
}

/// <summary>
/// Per-player statistics for a round.
/// </summary>
public class PlayerRoundStats
{
    public int SlotIndex { get; }

    // Lives
    public int LivesRemaining { get; set; }

    // Cumulative stats (persist across rounds)
    public float TotalScore { get; set; }
    public int TotalDeaths { get; set; }
    public int TotalBerries { get; set; }
    public int TotalTrapKills { get; set; }

    // All score segments earned (persists across rounds for display)
    public List<ScoreSegment> ScoreSegments { get; } = new();

    // Index where this round's new segments start (for animation)
    public int RoundStartSegmentIndex { get; set; }

    // Per-round stats (reset each round)
    public bool FinishedThisRound { get; set; }
    public bool DiedThisRound { get; set; }
    public int FinishOrder { get; set; } = -1;
    public int BerriesThisRound { get; set; }
    public int TrapKillsThisRound { get; set; }
    public bool WasUnderdogThisRound { get; set; }

    public PlayerRoundStats(int slotIndex, int startingLives)
    {
        SlotIndex = slotIndex;
        LivesRemaining = startingLives;
    }

    /// <summary>
    /// Adds a score segment to the history.
    /// </summary>
    public void AddScoreSegment(ScoreType type, float points)
    {
        if (points > 0)
            ScoreSegments.Add(new ScoreSegment(type, points));
    }

    /// <summary>
    /// Resets per-round stats for a new round.
    /// </summary>
    public void ResetForNewRound(int startingLives)
    {
        FinishedThisRound = false;
        DiedThisRound = false;
        FinishOrder = -1;
        BerriesThisRound = 0;
        TrapKillsThisRound = 0;
        WasUnderdogThisRound = false;
        LivesRemaining = startingLives;
    }
}
