using System;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
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
        // Host broadcasts completion to all
        NetworkManager.BroadcastWithSelf(new PlatformingCompleteMessage());
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

        // Record death in stats and consume a life
        var stats = RoundState.Current?.GetPlayerStats(umcPlayer);
        if (stats != null)
        {
            stats.TotalDeaths++;
            stats.DiedThisRound = true;
            stats.LivesRemaining--;
        }

        // Break a heart
        LifeHeartManager.RemoveOneLife(umcPlayer);
        bool shouldRespawn = stats?.LivesRemaining > 0;

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
            UmcLogger.Info($"Player {umcPlayer.Name} died, {stats?.LivesRemaining ?? 0} lives remaining");
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
            _instance._deadPlayers.Add(umcPlayer);
            UmcLogger.Info($"Player {umcPlayer.Name} eliminated (no lives remaining)");

            customDeadBody.OnDeathComplete = () =>
            {
                CameraController.Instance?.UntrackEntity(customDeadBody);
                _instance?.UntrackPlayer(umcPlayer);
            };

            // Check if all players are now dead
            _instance.CheckAllPlayersDead();
        }

        // Berry dropping is handled by UmcBerry.OnLoseLeader

        // If online, broadcast the death
        if (NetworkManager.Instance?.IsOnline == true)
        {
            NetworkManager.Broadcast(new PlayerDeathSyncMessage
            {
                PlayerIndex = umcPlayer.SlotIndex,
                IsEliminated = !shouldRespawn
            });
        }

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
        if (NetworkManager.Instance?.IsOnline == true)
        {
            NetworkManager.Broadcast(new PlayerFinishedSyncMessage
            {
                PlayerIndex = player.SlotIndex
            });
        }

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

        if (message.IsEliminated)
        {
            // Don't double-count if already dead
            if (_deadPlayers.Contains(player)) return;

            _deadPlayers.Add(player);
            UmcLogger.Info($"Synced elimination for player {player.Name}");

            // Despawn the remote player so they're no longer visible or tracked by camera
            PlayerSpawner.Instance?.DespawnPlayer(player);

            // Update state - record death and mark as eliminated
            var stats = RoundState.Current?.GetPlayerStats(player);
            if (stats != null)
            {
                stats.TotalDeaths++;
                stats.DiedThisRound = true;
                stats.LivesRemaining = 0;
            }

            CheckAllPlayersDead();
        }
        else
        {
            // Player died but will respawn - just log it
            UmcLogger.Info($"Synced death for player {player.Name} (will respawn)");

            // Record death and consume a life
            var stats = RoundState.Current?.GetPlayerStats(player);
            if (stats != null)
            {
                stats.TotalDeaths++;
                stats.DiedThisRound = true;
                stats.LivesRemaining--;
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

        // Despawn the remote player
        PlayerSpawner.Instance?.DespawnPlayer(player);

        CheckAllPlayersDead();
    }

    private void HandlePlatformingComplete(PlatformingCompleteMessage message)
    {
        DoComplete();
    }

    #endregion
}

#region Messages

public class PlayerDeathSyncMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public bool IsEliminated { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
        writer.Write(IsEliminated);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        IsEliminated = reader.ReadBoolean();
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
    public void Serialize(BinaryWriter writer) { }
    public void Deserialize(BinaryReader reader) { }
}

#endregion
