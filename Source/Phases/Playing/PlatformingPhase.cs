using System;
using System.Collections.Generic;
using System.IO;
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
    private readonly HashSet<PlayerDeadBody> _managedDeadBodies = new();
    private readonly HashSet<UmcPlayer> _deadPlayers = new();
    private float _allDeadTimer;
    private static PlatformingPhase _instance;

    public event Action OnComplete;

    private bool IsHost => NetworkManager.Instance?.IsHost ?? true;

    public PlatformingPhase()
    {
        // Reset dead player tracking
        _deadPlayers.Clear();
        _allDeadTimer = 0f;
        _instance = this;

        // Register network handlers
        NetworkManager.Handle<PlayerDeathSyncMessage>(HandlePlayerDeathSync);
        NetworkManager.Handle<PlatformingCompleteMessage>(HandlePlatformingComplete);

        // Now spawn all players
        PlayerSpawner.Instance?.SpawnAllSessionPlayers(Engine.Scene as Level);

        On.Celeste.Player.Die += OnPlayerDie;
        On.Celeste.PlayerDeadBody.End += OnPlayerDeadBodyEnd;

        UmcLogger.Info("Started platforming phase - players spawned");
    }

    public void Update()
    {
        // Only host controls the transition
        if (!IsHost) return;

        // Only count down if all players are dead
        int totalPlayers = GameSession.Instance?.Players.All.Count ?? 0;
        if (totalPlayers > 0 && _deadPlayers.Count >= totalPlayers)
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
        PlayerSpawner.Instance?.DespawnAllSessionPlayers();
        On.Celeste.Player.Die -= OnPlayerDie;
        On.Celeste.PlayerDeadBody.End -= OnPlayerDeadBodyEnd;

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

        // Call original but we'll override the death action
        var deadBody = orig(self, direction, evenIfInvincible, registerDeathInStats);

        // Only handle death if it actually happened
        if (deadBody == null) return deadBody;

        // Track this dead body so we can intercept its End call
        _instance._managedDeadBodies.Add(deadBody);

        // Mark player as dead
        _instance._deadPlayers.Add(umcPlayer);
        UmcLogger.Info($"Player {umcPlayer.Name} died");

        // If online, broadcast the death
        if (NetworkManager.Instance?.IsOnline == true)
        {
            // Local death - broadcast to others
            NetworkManager.Broadcast(new PlayerDeathSyncMessage
            {
                PlayerIndex = umcPlayer.SlotIndex
            });
        }

        // Check if all players are now dead
        _instance.CheckAllPlayersDead();

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

        UmcLogger.Info($"Dead players: {deadCount}/{totalPlayers}");

        if (deadCount >= totalPlayers)
        {
            _allDeadTimer = 0f;
            UmcLogger.Info("All players dead! Transitioning back to picking phase...");
        }
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

        _deadPlayers.Add(player);
        UmcLogger.Info($"Synced death for player {player.Name}");

        // Despawn the remote player so they're no longer visible or tracked by camera
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
