using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SkinModHelper;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Handles spawning and despawning of player entities (local and remote).
/// </summary>
public class PlayerSpawner : HookedFeature<PlayerSpawner>
{
    private readonly Dictionary<UmcPlayer, Player> _localPlayers = new();
    private readonly Dictionary<UmcPlayer, RemotePlayer> _remotePlayers = new();

    public IReadOnlyDictionary<UmcPlayer, Player> LocalPlayers => _localPlayers;
    public IReadOnlyDictionary<UmcPlayer, RemotePlayer> RemotePlayers => _remotePlayers;

    public bool IsMultiplayerLevel { get; private set; }
    public Vector2 SpawnPosition { get; private set; }

    protected override void Hook()
    {
        base.Hook();
        On.Celeste.Level.LoadLevel += OnLevelLoad;
        On.Celeste.Level.End += OnLevelEnd;
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
        UmcLogger.Info($"ONLEVELLOAD: {self.Session.Area.SID}");
        // We're in a multiplayer level if we're in an active session
        IsMultiplayerLevel = GameSession.Started;
        UmcLogger.Info(IsMultiplayerLevel.ToString());
        InputsFrozen = false;

        // Always calculate spawn position for multiplayer levels
        if (IsMultiplayerLevel)
        {
            UmcLogger.Info($"Found player data {self.Session.LevelData?.Entities?.Count}");
            SpawnPosition = self.Session.LevelData?.DefaultSpawn ??
                            self.Session.LevelData?.Spawns.FirstOrDefault() ??
                            Vector2.Zero;
        }

        orig(self, playerIntro, isFromLoader);

        // Always remove default player when in a multiplayer session
        if (IsMultiplayerLevel)
        {
            RemoveDefaultPlayer(self);

            // Set camera default position around spawn
            CameraController.Instance?.SetDefaultPosition(SpawnPosition);
        }
    }

    private void OnLevelEnd(On.Celeste.Level.orig_End orig, Level self)
    {
        if (IsMultiplayerLevel)
        {
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

    public void SpawnAllSessionPlayers(Level level, Vector2? overrideSpawnPosition = null)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var spawnPos = overrideSpawnPosition ?? SpawnPosition;

        foreach (var umcPlayer in session.Players.All)
        {
            if (string.IsNullOrEmpty(umcPlayer.SkinId))
            {
                UmcLogger.Info($"Skipping spawn for {umcPlayer.Name} - no skin selected yet");
                continue;
            }

            // Only spawn local players - remote players spawn via NetworkedEntity factory
            // when the remote client spawns their local player
            if (umcPlayer.IsLocal)
            {
                SpawnLocalPlayer(level, umcPlayer, spawnPos);
            }
        }
    }

    public void DespawnAllSessionPlayers()
    {
        // Despawn all local players - their NetworkedPlayerComponent will broadcast despawn
        // Remote players auto-despawn when they receive the DespawnEntityMessage
        foreach (var kvp in _localPlayers.ToList())
        {
            kvp.Value?.RemoveSelf();
        }
        _localPlayers.Clear();

        // Also clear any remaining remote player references (they should auto-remove)
        foreach (var kvp in _remotePlayers.ToList())
        {
            kvp.Value?.RemoveSelf();
        }
        _remotePlayers.Clear();
    }

    public void RespawnAllSessionPlayers(Level level, Vector2? spawnPosition = null)
    {
        DespawnAllSessionPlayers();
        SpawnAllSessionPlayers(level, spawnPosition);
    }

    public void RespawnPlayer(Level level, UmcPlayer umcPlayer, Vector2? spawnPosition = null)
    {
        DespawnPlayer(umcPlayer);

        var spawnPos = spawnPosition ?? SpawnPosition;

        // Only respawn local players - remote players spawn via NetworkedEntity factory
        if (umcPlayer.IsLocal)
        {
            SpawnLocalPlayer(level, umcPlayer, spawnPos);
        }
    }

    public Player SpawnLocalPlayer(Level level, UmcPlayer umcPlayer, Vector2? spawnPosition = null)
    {
        if (_localPlayers.TryGetValue(umcPlayer, out var localPlayer))
        {
            UmcLogger.Warn($"Player {umcPlayer.Name} already has a local player entity");
            return localPlayer;
        }

        var spawnPos = spawnPosition ?? SpawnPosition;
        var spriteMode = level.Session.Inventory.Backpack
            ? PlayerSpriteMode.Madeline
            : PlayerSpriteMode.MadelineNoBackpack;

        var player = new Player(spawnPos, spriteMode);
        var controller = new LocalPlayerController(umcPlayer);
        player.Add(controller);

        // Add networked player component for automatic spawn/despawn and frame sync
        var netPlayer = new NetworkedPlayerComponent(umcPlayer, spawnPos);
        player.Add(netPlayer);

        player.Tag |= Tags.Persistent;

        level.Add(player);
        _localPlayers[umcPlayer] = player;
        UmcLogger.Info($"Spawned local player for {umcPlayer.Name} at {spawnPos}");

        // Track with camera
        CameraController.Instance?.TrackEntity(player);

        // Send player graphics after entity is added to scene (so the net component is ready)
        level.OnEndOfFrame += () => netPlayer.SendGraphics();

        // Attach life hearts if lives system is enabled
        int lives = RoundState.Current?.GetPlayerStats(umcPlayer)?.LivesRemaining ?? umcPlayer.MaxLives;
        if (lives > 0)
        {
            LifeHeartManager.AttachToPlayer(player, umcPlayer, lives);
        }

        return player;
    }

    /// <summary>
    /// Manually spawns a remote player (for cases where NetworkedEntity isn't used).
    /// Prefer using the NetworkedEntity system which auto-spawns via factory.
    /// </summary>
    public RemotePlayer SpawnRemotePlayer(Level level, UmcPlayer umcPlayer, Vector2? spawnPosition = null)
    {
        if (_remotePlayers.TryGetValue(umcPlayer, out var player))
        {
            UmcLogger.Warn($"Player {umcPlayer.Name} already has a remote player entity");
            return player;
        }

        if (string.IsNullOrEmpty(umcPlayer.SkinId))
        {
            UmcLogger.Warn($"Player {umcPlayer.Name} doesn't have a skin selected yet");
            return null;
        }

        var spawnPos = spawnPosition ?? SpawnPosition;
        var remotePlayer = new RemotePlayer(umcPlayer, spawnPos);
        level.Add(remotePlayer);

        _remotePlayers[umcPlayer] = remotePlayer;
        UmcLogger.Info($"Spawned remote player for {umcPlayer.Name} at {spawnPos}");

        if (SkinsSystem.skinConfigs != null &&
            SkinsSystem.skinConfigs.TryGetValue(umcPlayer.SkinId, out var config) &&
            !string.IsNullOrEmpty(config.Character_ID))
        {
            remotePlayer.ApplySkin(config.Character_ID);
        }

        // Track with camera
        CameraController.Instance?.TrackEntity(remotePlayer);

        return remotePlayer;
    }

    /// <summary>
    /// Registers a remote player that was created by the NetworkedEntity factory.
    /// </summary>
    public void RegisterRemotePlayer(UmcPlayer umcPlayer, RemotePlayer remotePlayer)
    {
        if (_remotePlayers.TryGetValue(umcPlayer, out var existing))
        {
            // Remove old entity if it still exists
            if (existing.Scene != null)
            {
                existing.RemoveSelf();
            }
            UmcLogger.Info($"Replacing existing remote player for {umcPlayer.Name}");
        }

        _remotePlayers[umcPlayer] = remotePlayer;
        UmcLogger.Info($"Registered remote player for {umcPlayer.Name}");
    }

    /// <summary>
    /// Unregisters a remote player (called when the entity is removed).
    /// </summary>
    public void UnregisterRemotePlayer(UmcPlayer umcPlayer)
    {
        if (_remotePlayers.Remove(umcPlayer))
        {
            UmcLogger.Info($"Unregistered remote player for {umcPlayer.Name}");
        }
    }

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

    public Player GetLocalPlayer(UmcPlayer umcPlayer) => _localPlayers.GetValueOrDefault(umcPlayer);
    public RemotePlayer GetRemotePlayer(UmcPlayer umcPlayer) => _remotePlayers.GetValueOrDefault(umcPlayer);

    public UmcPlayer GetUmcPlayer(Player player)
    {
        foreach (var kvp in _localPlayers)
        {
            if (kvp.Value == player)
                return kvp.Key;
        }
        return null;
    }

    /// <summary>
    /// When true, all player inputs are disabled and players are frozen.
    /// </summary>
    public bool InputsFrozen { get; set; }

    /// <summary>
    /// Swaps in player-specific inputs before each player's update, then restores after.
    /// </summary>
    private void OnPlayerUpdate(On.Celeste.Player.orig_Update orig, Player self)
    {
        // If inputs are frozen, completely freeze the player
        if (InputsFrozen)
        {
            self.Speed = Vector2.Zero;
            return;
        }

        var controller = self.Get<LocalPlayerController>();
        var umcPlayer = controller?.UmcPlayer ?? GetUmcPlayer(self);

        // Hide player if flagged
        if (umcPlayer?.IsHidden == true)
        {
            self.Visible = false;
            self.Collidable = false;
            return;
        }

        if (umcPlayer?.InDanceMode == true)
        {
            var savedX = self.Position.X;
            var savedSpeedX = self.Speed.X;

            if (controller != null)
            {
                controller.SwapInputsIn();
                try { orig(self); }
                finally { controller.SwapInputsOut(); }
            }
            else
            {
                orig(self);
            }

            self.Position.X = savedX;
            self.Speed.X = 0;
            return;
        }

        if (controller != null)
        {
            controller.SwapInputsIn();
            try { orig(self); }
            finally { controller.SwapInputsOut(); }
        }
        else
        {
            orig(self);
        }
    }
}
