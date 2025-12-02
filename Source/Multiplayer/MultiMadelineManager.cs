using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.MotionSmoothing.Utilities;
using Celeste.Mod.UltimateMadelineCeleste.Core;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Multiplayer;

/// <summary>
/// Manages multiplayer functionality including player spawning, input routing,
/// and coordination between local and remote players.
/// </summary>
public class MultiMadelineManager : HookedFeature<MultiMadelineManager>
{
    private const string HubStageId = "FancyFurret/UltimateMadelineCeleste/Hub";

    private readonly Dictionary<UmcPlayer, Player> _localPlayers = new();
    private readonly Dictionary<UmcPlayer, RemotePlayer> _remotePlayers = new();
    public IReadOnlyDictionary<UmcPlayer, Player> LocalPlayers => _localPlayers;
    public IReadOnlyDictionary<UmcPlayer, RemotePlayer> RemotePlayers => _remotePlayers;

    public bool IsMultiplayerLevel { get; private set; }

    private Vector2? _spawnPosition;

    protected override void Hook()
    {
        base.Hook();

        // Hook level loading to prevent default player spawn
        On.Celeste.Level.LoadLevel += OnLevelLoad;
        On.Celeste.Level.End += OnLevelEnd;

        // Hook player update to swap inputs per-player
        On.Celeste.Player.Update += OnPlayerUpdate;
    }

    protected override void Unhook()
    {
        On.Celeste.Level.LoadLevel -= OnLevelLoad;
        On.Celeste.Level.End -= OnLevelEnd;
        On.Celeste.Player.Update -= OnPlayerUpdate;

        base.Unhook();
    }

    private void OnLevelLoad(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        IsMultiplayerLevel = self.Session.Area.SID == HubStageId;

        if (IsMultiplayerLevel)
        {
            // Store spawn position from level data before calling orig
            // The default player will spawn at this position
            var playerData = self.Session.LevelData?.Entities?.FirstOrDefault(e => e.Name == "player");
            if (playerData != null)
            {
                _spawnPosition = new Vector2(playerData.Position.X, playerData.Position.Y);
            }
            else
            {
                _spawnPosition = self.Session.RespawnPoint ?? new Vector2(160, 90);
            }
        }

        orig(self, playerIntro, isFromLoader);

        if (IsMultiplayerLevel)
        {
            // Remove the default player that was spawned
            RemoveDefaultPlayer(self);

            // Respawn all session players
            RespawnAllSessionPlayers(self);
        }
    }

    private void OnLevelEnd(On.Celeste.Level.orig_End orig, Level self)
    {
        if (IsMultiplayerLevel)
        {
            // Clear all player references when leaving level
            _localPlayers.Clear();
            _remotePlayers.Clear();
        }

        orig(self);
    }

    private void RemoveDefaultPlayer(Level level)
    {
        var defaultPlayer = level.Tracker.GetEntity<Player>();
        if (defaultPlayer != null)
        {
            UmcLogger.Info("Removing default player entity for multiplayer");
            defaultPlayer.RemoveSelf();
        }
    }

    /// <summary>
    /// Respawns all players that are registered in the UmcSession.
    /// Called when entering a multiplayer level.
    /// </summary>
    private void RespawnAllSessionPlayers(Level level)
    {
        var session = UmcSession.Instance;
        if (session == null) return;

        foreach (var umcPlayer in session.Players.All)
        {
            if (umcPlayer.IsLocal)
                SpawnLocalPlayer(level, umcPlayer);
            else
                SpawnRemotePlayer(level, umcPlayer);
        }

    }

    /// <summary>
    /// Spawns a local player entity for the given UmcPlayer.
    /// </summary>
    public Player SpawnLocalPlayer(Level level, UmcPlayer umcPlayer)
    {
        if (_localPlayers.TryGetValue(umcPlayer, out var localPlayer))
        {
            UmcLogger.Warn($"Player {umcPlayer.Name} already has a local player entity");
            return localPlayer;
        }

        var spawnPos = _spawnPosition ?? level.Session.RespawnPoint ?? new Vector2(160, 90);

        // Determine sprite mode (could be customized per player later)
        var spriteMode = level.Session.Inventory.Backpack
            ? PlayerSpriteMode.Madeline
            : PlayerSpriteMode.MadelineNoBackpack;

        // Create the native Player entity
        var player = new Player(spawnPos, spriteMode);

        // Add our controller component to bind input
        var controller = new LocalPlayerController(umcPlayer);
        player.Add(controller);

        // Tag for identification
        player.Tag |= Tags.Persistent;

        // Add to level
        level.Add(player);

        _localPlayers[umcPlayer] = player;
        UmcLogger.Info($"Spawned local player for {umcPlayer.Name} at {spawnPos}");

        return player;
    }

    /// <summary>
    /// Spawns a remote player entity for the given UmcPlayer.
    /// </summary>
    public RemotePlayer SpawnRemotePlayer(Level level, UmcPlayer umcPlayer)
    {
        if (_remotePlayers.TryGetValue(umcPlayer, out var player))
        {
            UmcLogger.Warn($"Player {umcPlayer.Name} already has a remote player entity");
            return player;
        }

        var spawnPos = _spawnPosition ?? level.Session.RespawnPoint ?? new Vector2(160, 90);

        var remotePlayer = new RemotePlayer(umcPlayer, spawnPos);
        level.Add(remotePlayer);

        _remotePlayers[umcPlayer] = remotePlayer;
        UmcLogger.Info($"Spawned remote player for {umcPlayer.Name} at {spawnPos}");

        return remotePlayer;
    }

    /// <summary>
    /// Removes a player (local or remote) from the level.
    /// </summary>
    public void DespawnPlayer(UmcPlayer umcPlayer)
    {
        if (_localPlayers.TryGetValue(umcPlayer, out var localPlayer))
        {
            localPlayer.RemoveSelf();
            _localPlayers.Remove(umcPlayer);
            UmcLogger.Info($"Despawned local player for {umcPlayer.Name}");
        }

        if (_remotePlayers.TryGetValue(umcPlayer, out var remotePlayer))
        {
            remotePlayer.RemoveSelf();
            _remotePlayers.Remove(umcPlayer);
            UmcLogger.Info($"Despawned remote player for {umcPlayer.Name}");
        }
    }

    /// <summary>
    /// Gets the Player entity for a given UmcPlayer, if they are local.
    /// </summary>
    public Player GetLocalPlayer(UmcPlayer umcPlayer)
    {
        return _localPlayers.GetValueOrDefault(umcPlayer);
    }

    /// <summary>
    /// Gets the RemotePlayer entity for a given UmcPlayer, if they are remote.
    /// </summary>
    public RemotePlayer GetRemotePlayer(UmcPlayer umcPlayer)
    {
        return _remotePlayers.GetValueOrDefault(umcPlayer);
    }

    /// <summary>
    /// Gets the UmcPlayer associated with a Player entity.
    /// </summary>
    public UmcPlayer GetUmcPlayer(Player player)
    {
        foreach (var kvp in _localPlayers)
        {
            if (kvp.Value == player)
                return kvp.Key;
        }
        return null;
    }

    private void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self)
    {
        // Find which UmcPlayer this Player belongs to
        var controller = self.Get<LocalPlayerController>();

        if (controller != null)
        {
            // Swap in player-specific inputs before update
            controller.SwapInputsIn();

            try
            {
                orig(self);
            }
            finally
            {
                // Always restore original inputs, even if update throws
                controller.SwapInputsOut();
            }
        }
        else
        {
            // No controller - this is either a vanilla player or something else
            // Just call the original update
            orig(self);
        }
    }
}

