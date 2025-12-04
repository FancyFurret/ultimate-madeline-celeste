using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

/// <summary>
/// Phase controller for when players are actively playing a level.
/// Handles player spawning at level start positions and respawning.
/// </summary>
public class PlayingPhase : Entity
{
    public static PlayingPhase Instance { get; private set; }

    public string CurrentLevelSID { get; private set; }

    /// <summary>
    /// The current round state (score, lives, etc.)
    /// </summary>
    public RoundState Round => RoundState.Current;

    private bool IsHost => NetworkManager.Instance?.IsHost ?? true;

    /// <summary>
    /// Delay in seconds before respawning a player after death.
    /// </summary>
    private const float RespawnDelay = 1f;

    /// <summary>
    /// Players waiting to respawn with their remaining delay time.
    /// </summary>
    private readonly Dictionary<UmcPlayer, float> _pendingRespawns = new();

    /// <summary>
    /// Tracks PlayerDeadBody instances that belong to our managed players.
    /// </summary>
    private readonly HashSet<PlayerDeadBody> _managedDeadBodies = new();

    public PlayingPhase(string levelSID)
    {
        Instance = this;
        CurrentLevelSID = levelSID;
        Tag = Tags.Global | Tags.PauseUpdate;
        Depth = 0;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Instance = this;

        var net = NetworkManager.Instance;
        if (net != null)
            RegisterMessages(net.Messages);

        On.Celeste.Player.Die += OnPlayerDie;
        On.Celeste.PlayerDeadBody.End += OnPlayerDeadBodyEnd;

        RoundState.Start(CurrentLevelSID);
        PlayerSpawner.Instance?.SpawnAllSessionPlayers(Scene as Level);
        UmcLogger.Info($"PlayingPhase started for level: {CurrentLevelSID}");
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);

        On.Celeste.Player.Die -= OnPlayerDie;
        On.Celeste.PlayerDeadBody.End -= OnPlayerDeadBodyEnd;
        _pendingRespawns.Clear();
        _managedDeadBodies.Clear();

        // Clear round state
        RoundState.Clear();

        if (Instance == this) Instance = null;

        UmcLogger.Info("PlayingPhase ended");
    }

    public void RegisterMessages(MessageRegistry messages)
    {
        messages.Register<ReturnToLobbyMessage>(23, HandleReturnToLobby);
    }

    private static PlayerDeadBody OnPlayerDie(On.Celeste.Player.orig_Die orig, Player self, Vector2 direction, bool evenIfInvincible, bool registerDeathInStats)
    {
        var spawner = PlayerSpawner.Instance;
        var umcPlayer = spawner?.GetUmcPlayer(self);

        // If this isn't one of our managed players, let vanilla handle it
        if (umcPlayer == null)
            return orig(self, direction, evenIfInvincible, registerDeathInStats);

        // Call original but we'll override the death action
        var deadBody = orig(self, direction, evenIfInvincible, registerDeathInStats);

        // Only handle death if it actually happened
        if (deadBody == null) return deadBody;

        // Track this dead body so we can intercept its End call
        Instance._managedDeadBodies.Add(deadBody);

        // Record the death
        Instance.Round?.RecordPlayerDeath(umcPlayer);
        UmcLogger.Info($"Player {umcPlayer.Name} died, will respawn in {RespawnDelay}s");

        // Queue respawn after delay
        Instance._pendingRespawns[umcPlayer] = RespawnDelay;

        return deadBody;
    }

    private void OnPlayerDeadBodyEnd(On.Celeste.PlayerDeadBody.orig_End orig, PlayerDeadBody self)
    {
        // If this is one of our managed dead bodies, don't let it trigger a reload
        if (_managedDeadBodies.Remove(self))
        {
            // Just remove the dead body without triggering any reload
            self.RemoveSelf();
            return;
        }

        // Let vanilla handle non-managed dead bodies
        orig(self);
    }

    public void RespawnPlayer(UmcPlayer umcPlayer)
    {
        var level = Scene as Level;
        var spawner = PlayerSpawner.Instance;
        if (level == null || spawner == null) return;

        spawner.RespawnPlayer(level, umcPlayer);
    }

    /// <summary>
    /// Respawns all players at the level start position.
    /// </summary>
    public void RespawnAllPlayers()
    {
        var level = Scene as Level;
        var spawner = PlayerSpawner.Instance;
        if (level == null || spawner == null) return;

        spawner.RespawnAllSessionPlayers(level);
    }

    public override void Update()
    {
        base.Update();

        var level = Scene as Level;
        if (level == null || level.Paused) return;

        // Process pending respawns
        if (_pendingRespawns.Count > 0)
        {
            var toRespawn = new List<UmcPlayer>();
            var updates = new Dictionary<UmcPlayer, float>();

            foreach (var kvp in _pendingRespawns)
            {
                var remaining = kvp.Value - Engine.DeltaTime;
                if (remaining <= 0)
                    toRespawn.Add(kvp.Key);
                else
                    updates[kvp.Key] = remaining;
            }

            // Apply updates
            foreach (var kvp in updates)
                _pendingRespawns[kvp.Key] = kvp.Value;

            // Process respawns
            foreach (var player in toRespawn)
            {
                _pendingRespawns.Remove(player);
                RespawnPlayer(player);
            }
        }
    }

    private void HandleReturnToLobby(CSteamID sender, ReturnToLobbyMessage message)
    {
        // Only clients handle this - host sends it
        if (IsHost) return;

        UmcLogger.Info("Received return to lobby message from host");
        ReturnToLobbyInternal();
    }

    /// <summary>
    /// Returns all players to the lobby. Host only.
    /// </summary>
    public void ReturnToLobby()
    {
        if (!IsHost)
        {
            UmcLogger.Warn("Only host can return to lobby");
            return;
        }

        // Broadcast to clients
        var net = NetworkManager.Instance;
        if (net?.IsOnline == true)
        {
            net.Messages.Broadcast(new ReturnToLobbyMessage());
        }

        ReturnToLobbyInternal();
    }

    private void ReturnToLobbyInternal()
    {
        UmcLogger.Info("Returning to lobby...");

        // Despawn all players
        PlayerSpawner.Instance?.DespawnAllSessionPlayers();

        // Use PhaseManager to transition back to lobby
        PhaseManager.Instance?.TransitionToLobby();
    }
}

