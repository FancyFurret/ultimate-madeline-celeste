using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Scoring;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

public class PlatformingPhase
{
    private const float AllDeadTransitionDelay = 1f;
    private readonly HashSet<UmcPlayerDeadBody> _customDeadBodies = new();
    private readonly HashSet<UmcPlayer> _deadPlayers = new();
    private readonly HashSet<UmcPlayer> _finishedPlayers = new();
    private float _allDeadTimer;
    private static PlatformingPhase _instance;
    private BerryManager _berryManager;

    /// <summary>
    /// Scores received from host (or calculated if we are host).
    /// Passed to ScoringPhase.
    /// </summary>
    public PlatformingCompleteMessage ReceivedScores { get; private set; }

    public event Action OnComplete;

    private bool IsHost => NetworkManager.Instance?.IsHost ?? true;

    public PlatformingPhase()
    {
        // Reset tracking
        _deadPlayers.Clear();
        _finishedPlayers.Clear();
        _allDeadTimer = 0f;
        _instance = this;

        // Register network handlers
        NetworkManager.Handle<PlayerDeathSyncMessage>(HandlePlayerDeathSync);
        NetworkManager.Handle<PlayerFinishedSyncMessage>(HandlePlayerFinishedSync);
        NetworkManager.Handle<PlatformingCompleteMessage>(HandlePlatformingComplete);

        // Subscribe to goal flag events
        GoalFlag.OnPlayerReachedGoal += HandlePlayerReachedGoal;

        // Reset all player states (dance mode, hidden, etc.) before spawning
        var session = GameSession.Instance;
        if (session != null)
        {
            foreach (var player in session.Players.All)
            {
                player.ResetState();
            }
        }

        // Clear any tracked entities from previous phase, spawn all players
        // (spawn methods auto-track with camera)
        CameraController.Instance?.ClearTrackedEntities();
        PlayerSpawner.Instance?.SpawnAllSessionPlayers(Engine.Scene as Level);

        // Initialize berry manager
        var level = Engine.Scene as Level;
        if (level != null)
        {
            _berryManager = new BerryManager(level);
        }

        On.Celeste.Player.Die += OnPlayerDie;

        UmcLogger.Info("Started platforming phase - players spawned");
    }

    public void Update()
    {
        // Only host controls the transition
        if (!IsHost) return;

        int totalPlayers = GameSession.Instance?.Players.All.Count ?? 0;
        if (totalPlayers <= 0) return;

        // Check if all players finished or died
        int activePlayersRemaining = totalPlayers - _deadPlayers.Count - _finishedPlayers.Count;

        if (activePlayersRemaining <= 0)
        {
            _allDeadTimer += Engine.DeltaTime;
            if (_allDeadTimer >= AllDeadTransitionDelay)
            {
                Complete();
            }
        }
    }

    private void Complete()
    {
        // Host calculates scores and broadcasts to all
        var message = new PlatformingCompleteMessage
        {
            RoundNumber = RoundState.Current?.RoundNumber ?? 1,
            PlayerScores = CalculateScores()
        };

        NetworkManager.BroadcastWithSelf(message);
    }

    /// <summary>
    /// Host calculates all player scores for this round.
    /// </summary>
    private List<PlayerScoreData> CalculateScores()
    {
        var scores = new List<PlayerScoreData>();

        var session = GameSession.Instance;
        var round = RoundState.Current;
        if (session == null || round == null) return scores;

        bool isTooEasy = round.DidEveryoneFinish() && session.Players.All.Count > 1;
        bool isNoWinners = round.DidNoOneFinish();

        int totalPlayers = session.Players.All.Count;
        int finisherCount = round.GetFinisherCount();
        bool isSoloWin = finisherCount == 1 && totalPlayers >= ScoringConfig.SoloMinPlayers;

        foreach (var player in session.Players.All)
        {
            var stats = round.GetPlayerStats(player);

            // Track where new segments start (before adding this round's)
            int roundStartIndex = stats.ScoreSegments.Count;

            bool isUnderdog = round.IsUnderdog(player);
            stats.WasUnderdogThisRound = isUnderdog;

            // Calculate points if not a special condition round
            if (!isTooEasy && !isNoWinners)
            {
                // Finish points
                if (stats.FinishedThisRound)
                {
                    float points = ScoreType.Finish.GetBasePoints();
                    stats.TotalScore += points;
                    stats.AddScoreSegment(ScoreType.Finish, points);
                }

                // First place points (only with 3+ players)
                if (stats.FinishOrder == 0 && totalPlayers >= 3)
                {
                    float points = ScoreType.FirstPlace.GetBasePoints();
                    stats.TotalScore += points;
                    stats.AddScoreSegment(ScoreType.FirstPlace, points);
                }

                // Trap kill points
                if (stats.TrapKillsThisRound > 0)
                {
                    float points = stats.TrapKillsThisRound * ScoreType.TrapKill.GetBasePoints();
                    stats.TotalScore += points;
                    stats.AddScoreSegment(ScoreType.TrapKill, points);
                }

                // Underdog bonus
                if (stats.FinishedThisRound && isUnderdog)
                {
                    float points = ScoreType.UnderdogBonus.GetBasePoints();
                    stats.TotalScore += points;
                    stats.AddScoreSegment(ScoreType.UnderdogBonus, points);
                }

                // Solo bonus
                if (stats.FinishedThisRound && isSoloWin)
                {
                    float points = ScoreType.Solo.GetBasePoints();
                    stats.TotalScore += points;
                    stats.AddScoreSegment(ScoreType.Solo, points);
                }
            }

            // Berry points - each berry is a separate segment, and berries always count (even if "too easy")
            if (stats.BerriesThisRound > 0)
            {
                float pointsPerBerry = ScoreType.Berry.GetBasePoints();
                for (int i = 0; i < stats.BerriesThisRound; i++)
                {
                    stats.TotalScore += pointsPerBerry;
                    stats.AddScoreSegment(ScoreType.Berry, pointsPerBerry);
                }
            }

            // Build score data with ALL segments (cumulative)
            var scoreData = new PlayerScoreData
            {
                SlotIndex = player.SlotIndex,
                TotalScore = stats.TotalScore,
                TotalDeaths = stats.TotalDeaths,
                TotalBerries = stats.TotalBerries,
                TotalTrapKills = stats.TotalTrapKills,
                RoundStartSegmentIndex = roundStartIndex,
                WasUnderdogThisRound = isUnderdog,
                Segments = stats.ScoreSegments.Select(s => new ScoreSegmentData
                {
                    Type = (byte)s.Type,
                    Points = s.Points
                }).ToList()
            };

            scores.Add(scoreData);
        }

        return scores;
    }

    private void DoComplete()
    {
        Cleanup();
        OnComplete?.Invoke();
    }

    public void Cleanup()
    {
        // Don't despawn players here - keep them for scoring phase victory display
        // ScoringPhase.Cleanup will despawn them
        On.Celeste.Player.Die -= OnPlayerDie;

        // Clean up custom dead bodies
        foreach (var deadBody in _customDeadBodies)
        {
            deadBody?.Cleanup();
        }
        _customDeadBodies.Clear();

        // Unsubscribe from goal flag events
        GoalFlag.OnPlayerReachedGoal -= HandlePlayerReachedGoal;
        GoalFlag.ClearHandlers();

        // Cleanup berry manager
        _berryManager?.Cleanup();
        _berryManager = null;

        if (_instance == this)
            _instance = null;

        UmcLogger.Info("Platforming phase cleaned up");
    }

    private static PlayerDeadBody OnPlayerDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
    {
        var spawner = PlayerSpawner.Instance;
        var umcPlayer = spawner?.GetUmcPlayer(self);

        // If this isn't one of our managed players, let vanilla handle it
        if (umcPlayer == null)
            return orig(self, direction, evenIfInvincible, registerDeathInStats);

        // Players in dance mode are invincible
        if (umcPlayer.InDanceMode)
            return null;

        // Safety check - if platforming phase isn't active, let vanilla handle it
        if (_instance == null)
            return orig(self, direction, evenIfInvincible, registerDeathInStats);

        // Calculate new lives and apply death
        var stats = RoundState.Current?.GetPlayerStats(umcPlayer);
        int livesRemaining = (stats?.LivesRemaining ?? 1) - 1;
        bool isEliminated = _instance.ApplyPlayerDeath(umcPlayer, livesRemaining, null);
        bool shouldRespawn = !isEliminated;

        // Release followers (berries) before removing the player
        // This triggers OnLoseLeader on each follower, allowing berries to drop properly
        self.Leader?.LoseFollowers();

        // Don't call orig - create our own custom dead body instead
        var customDeadBody = new UmcPlayerDeadBody(self, direction, umcPlayer);
        self.Scene?.Add(customDeadBody);

        // Remove the player from scene
        self.RemoveSelf();

        // Track the custom dead body
        _instance._customDeadBodies.Add(customDeadBody);

        // Track the dead body with camera
        CameraController.Instance?.TrackEntity(customDeadBody);

        if (shouldRespawn)
        {
            // Player has lives remaining - respawn after death animation
            UmcLogger.Info($"Player {umcPlayer.Name} died, {livesRemaining} lives remaining");
            customDeadBody.OnDeathComplete = () =>
            {
                CameraController.Instance?.UntrackEntity(customDeadBody);
                customDeadBody.Cleanup();
                _instance?._customDeadBodies.Remove(customDeadBody);

                // Respawn the player
                _instance?.RespawnPlayer(umcPlayer);
            };
        }
        else
        {
            // Player is eliminated (no lives remaining)
            UmcLogger.Info($"Player {umcPlayer.Name} eliminated (no lives remaining)");

            customDeadBody.OnDeathComplete = () =>
            {
                CameraController.Instance?.UntrackEntity(customDeadBody);
                _instance?.UntrackPlayer(umcPlayer);
            };
        }

        // Broadcast the death with explicit lives remaining
        NetworkManager.Broadcast(new PlayerDeathSyncMessage
        {
            PlayerIndex = umcPlayer.SlotIndex,
            LivesRemaining = livesRemaining,
            KillerSlotIndex = -1 // TODO: Track killer from trap
        });

        // Return null since we didn't create a vanilla PlayerDeadBody
        return null;
    }

    public void RespawnPlayer(UmcPlayer umcPlayer)
    {
        var level = Engine.Scene as Level;
        var spawner = PlayerSpawner.Instance;
        if (level == null || spawner == null) return;

        spawner.RespawnPlayer(level, umcPlayer);
    }

    /// <summary>
    /// Applies death stats and effects for a player (shared between local and remote deaths).
    /// Returns true if the player was eliminated (no lives remaining).
    /// </summary>
    private bool ApplyPlayerDeath(UmcPlayer player, int livesRemaining, int? killerSlotIndex)
    {
        var stats = RoundState.Current?.GetPlayerStats(player);
        if (stats != null)
        {
            stats.TotalDeaths++;
            stats.LivesRemaining = livesRemaining;
            if (livesRemaining <= 0)
                stats.DiedThisRound = true;
        }

        // Record trap kill if killed by another player
        if (killerSlotIndex.HasValue && killerSlotIndex.Value >= 0 && killerSlotIndex.Value != player.SlotIndex)
        {
            RoundState.Current?.RecordTrapKill(killerSlotIndex.Value);
        }

        // Break a heart
        LifeHeartManager.RemoveOneLife(player);

        bool isEliminated = livesRemaining <= 0;
        if (isEliminated && !_deadPlayers.Contains(player))
        {
            _deadPlayers.Add(player);
            CheckAllPlayersDead();
        }

        return isEliminated;
    }

    /// <summary>
    /// Respawns all players at the level start position.
    /// </summary>
    public void RespawnAllPlayers()
    {
        var level = Engine.Scene as Level;
        var spawner = PlayerSpawner.Instance;
        if (level == null || spawner == null) return;

        spawner.RespawnAllSessionPlayers(level);
    }


    private void CheckAllPlayersDead()
    {
        int totalPlayers = GameSession.Instance?.Players.All.Count ?? 0;
        int deadCount = _deadPlayers.Count;
        int finishedCount = _finishedPlayers.Count;

        UmcLogger.Info($"Dead: {deadCount}, Finished: {finishedCount}, Total: {totalPlayers}");

        if (deadCount + finishedCount >= totalPlayers)
        {
            _allDeadTimer = 0f;
            UmcLogger.Info("All players done! Transitioning to scoring...");

            // All players are done - re-track finished players so camera focuses on them
            TrackFinishedPlayers();
        }
    }

    private void TrackFinishedPlayers()
    {
        var spawner = PlayerSpawner.Instance;
        var camera = CameraController.Instance;
        if (spawner == null || camera == null) return;

        foreach (var player in _finishedPlayers)
        {
            if (spawner.LocalPlayers.TryGetValue(player, out var localPlayer))
                camera.TrackEntity(localPlayer);
            if (spawner.RemotePlayers.TryGetValue(player, out var remotePlayer))
                camera.TrackEntity(remotePlayer);
        }
    }

    private void HandlePlayerReachedGoal(UmcPlayer player)
    {
        // Don't double-count
        if (_finishedPlayers.Contains(player) || _deadPlayers.Contains(player))
            return;

        _finishedPlayers.Add(player);
        Audio.Play("event:/game/07_summit/checkpoint_confetti");
        UmcLogger.Info($"Player {player.Name} reached the goal! ({_finishedPlayers.Count} finished)");

        // Record in round state for scoring
        RoundState.Current?.RecordPlayerFinished(player);

        // Player is now in dance mode (set by GoalFlag) and invincible
        // Remove from camera tracking so camera focuses on remaining active players
        // (finished players get re-tracked when all players are done)
        UntrackPlayer(player);

        // Broadcast to network
        NetworkManager.Broadcast(new PlayerFinishedSyncMessage
        {
            PlayerIndex = player.SlotIndex
        });

        CheckAllPlayersDead();
    }

    private void UntrackPlayer(UmcPlayer player)
    {
        var spawner = PlayerSpawner.Instance;
        var camera = CameraController.Instance;
        if (spawner == null || camera == null) return;

        if (spawner.LocalPlayers.TryGetValue(player, out var localPlayer))
            camera.UntrackEntity(localPlayer);
        if (spawner.RemotePlayers.TryGetValue(player, out var remotePlayer))
            camera.UntrackEntity(remotePlayer);
    }

    #region Network Handlers

    private void HandlePlayerDeathSync(CSteamID sender, PlayerDeathSyncMessage message)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var player = session.Players.GetAtSlot(message.PlayerIndex);
        if (player == null) return;

        // Don't double-count if already dead
        if (_deadPlayers.Contains(player)) return;

        var spawner = PlayerSpawner.Instance;
        var level = Engine.Scene as Level;
        var remotePlayer = spawner?.GetRemotePlayer(player);

        // Apply death stats, break heart, and handle elimination
        int? killerSlot = message.KillerSlotIndex >= 0 ? message.KillerSlotIndex : null;
        bool isEliminated = ApplyPlayerDeath(player, message.LivesRemaining, killerSlot);

        // Create death animation if we have the remote player entity
        if (remotePlayer != null && level != null)
        {
            // Random death direction (we don't have the actual direction from the killer)
            var direction = new Vector2(Calc.Random.Choose(-1, 1), -1f);
            direction.Normalize();

            // Release followers (berries) before removing the remote player
            remotePlayer.Leader?.LoseFollowers();

            var deadBody = new UmcPlayerDeadBody(remotePlayer, direction, player);
            level.Add(deadBody);
            _customDeadBodies.Add(deadBody);

            // Track dead body with camera
            CameraController.Instance?.TrackEntity(deadBody);

            // Remove the remote player entity from scene (dead body takes over visuals)
            remotePlayer.RemoveSelf();

            if (isEliminated)
            {
                UmcLogger.Info($"Synced elimination for player {player.Name}");
                deadBody.OnDeathComplete = () =>
                {
                    CameraController.Instance?.UntrackEntity(deadBody);
                    UntrackPlayer(player);
                };
            }
            else
            {
                UmcLogger.Info($"Synced death for player {player.Name} ({message.LivesRemaining} lives remaining)");
                deadBody.OnDeathComplete = () =>
                {
                    CameraController.Instance?.UntrackEntity(deadBody);
                    deadBody.Cleanup();
                    _customDeadBodies.Remove(deadBody);
                };
            }
        }
        else
        {
            // Fallback if no remote player entity found
            if (isEliminated)
            {
                UmcLogger.Info($"Synced elimination for player {player.Name} (no entity)");
                spawner?.DespawnPlayer(player);
            }
            else
            {
                UmcLogger.Info($"Synced death for player {player.Name} ({message.LivesRemaining} lives remaining, no entity)");
            }
        }
    }

    private void HandlePlayerFinishedSync(CSteamID sender, PlayerFinishedSyncMessage message)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var player = session.Players.GetAtSlot(message.PlayerIndex);
        if (player == null) return;

        // Don't double-count
        if (_finishedPlayers.Contains(player)) return;

        _finishedPlayers.Add(player);
        UmcLogger.Info($"Synced finish for player {player.Name}");

        // Record in round state for scoring
        RoundState.Current?.RecordPlayerFinished(player);

        // Untrack from camera so it focuses on remaining active players
        // (but keep the remote player entity visible for dance animation)
        UntrackPlayer(player);

        CheckAllPlayersDead();
    }

    private void HandlePlatformingComplete(PlatformingCompleteMessage message)
    {
        // Apply scores from host to our local state
        ApplyReceivedScores(message);
        ReceivedScores = message;
        DoComplete();
    }

    /// <summary>
    /// Applies scores received from host to local RoundState.
    /// </summary>
    private void ApplyReceivedScores(PlatformingCompleteMessage message)
    {
        var round = RoundState.Current;
        if (round == null) return;

        foreach (var scoreData in message.PlayerScores)
        {
            var session = GameSession.Instance;
            var player = session?.Players.GetAtSlot(scoreData.SlotIndex);
            if (player == null) continue;

            var stats = round.GetPlayerStats(player);

            // Apply the authoritative values from host
            stats.TotalScore = scoreData.TotalScore;
            stats.TotalDeaths = scoreData.TotalDeaths;
            stats.TotalBerries = scoreData.TotalBerries;
            stats.TotalTrapKills = scoreData.TotalTrapKills;
            stats.RoundStartSegmentIndex = scoreData.RoundStartSegmentIndex;
            stats.WasUnderdogThisRound = scoreData.WasUnderdogThisRound;

            // Replace all segments with authoritative list from host
            stats.ScoreSegments.Clear();
            foreach (var segData in scoreData.Segments)
            {
                stats.ScoreSegments.Add(new ScoreSegment((ScoreType)segData.Type, segData.Points));
            }
        }

        UmcLogger.Info($"Applied scores from host for round {message.RoundNumber}");
    }

    #endregion
}

#region Messages

public class PlayerDeathSyncMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public int LivesRemaining { get; set; }
    public int KillerSlotIndex { get; set; } = -1; // -1 = no killer (self/environment)

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
        writer.Write((sbyte)LivesRemaining);
        writer.Write((sbyte)KillerSlotIndex);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        LivesRemaining = reader.ReadSByte();
        KillerSlotIndex = reader.ReadSByte();
    }
}

public class PlayerFinishedSyncMessage : INetMessage
{
    public int PlayerIndex { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
    }
}

public class PlatformingCompleteMessage : INetMessage
{
    public int RoundNumber { get; set; }
    public List<PlayerScoreData> PlayerScores { get; set; } = new();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)RoundNumber);
        writer.Write((byte)PlayerScores.Count);
        foreach (var score in PlayerScores)
        {
            score.Serialize(writer);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        RoundNumber = reader.ReadByte();
        int count = reader.ReadByte();
        PlayerScores = new List<PlayerScoreData>(count);
        for (int i = 0; i < count; i++)
        {
            var score = new PlayerScoreData();
            score.Deserialize(reader);
            PlayerScores.Add(score);
        }
    }
}

public class PlayerScoreData : INetMessage
{
    public int SlotIndex { get; set; }
    public float TotalScore { get; set; }
    public int TotalDeaths { get; set; }
    public int TotalBerries { get; set; }
    public int TotalTrapKills { get; set; }
    public int RoundStartSegmentIndex { get; set; } // Index where this round's new segments start
    public bool WasUnderdogThisRound { get; set; }
    public List<ScoreSegmentData> Segments { get; set; } = new();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)SlotIndex);
        writer.Write(TotalScore);
        writer.Write((byte)TotalDeaths);
        writer.Write((byte)TotalBerries);
        writer.Write((byte)TotalTrapKills);
        writer.Write((byte)RoundStartSegmentIndex);
        writer.Write(WasUnderdogThisRound);
        writer.Write((byte)Segments.Count);
        foreach (var seg in Segments)
        {
            seg.Serialize(writer);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        SlotIndex = reader.ReadByte();
        TotalScore = reader.ReadSingle();
        TotalDeaths = reader.ReadByte();
        TotalBerries = reader.ReadByte();
        TotalTrapKills = reader.ReadByte();
        RoundStartSegmentIndex = reader.ReadByte();
        WasUnderdogThisRound = reader.ReadBoolean();
        int segCount = reader.ReadByte();
        Segments = new List<ScoreSegmentData>(segCount);
        for (int i = 0; i < segCount; i++)
        {
            var seg = new ScoreSegmentData();
            seg.Deserialize(reader);
            Segments.Add(seg);
        }
    }
}

public class ScoreSegmentData : INetMessage
{
    public byte Type { get; set; }
    public float Points { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Type);
        writer.Write(Points);
    }

    public void Deserialize(BinaryReader reader)
    {
        Type = reader.ReadByte();
        Points = reader.ReadSingle();
    }
}

#endregion
