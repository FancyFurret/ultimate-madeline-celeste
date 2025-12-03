using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SkinModHelper;
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
        IsMultiplayerLevel = self.Session.Area.SID == HubStageId;

        if (IsMultiplayerLevel)
        {
            var playerData = self.Session.LevelData?.Entities?.FirstOrDefault(e => e.Name == "player");
            _spawnPosition = playerData != null
                ? new Vector2(playerData.Position.X, playerData.Position.Y)
                : self.Session.RespawnPoint ?? new Vector2(160, 90);
        }

        orig(self, playerIntro, isFromLoader);

        if (IsMultiplayerLevel)
        {
            RemoveDefaultPlayer(self);
            RespawnAllSessionPlayers(self);
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

    private void RespawnAllSessionPlayers(Level level)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        foreach (var umcPlayer in session.Players.All)
        {
            if (string.IsNullOrEmpty(umcPlayer.SkinId))
            {
                UmcLogger.Info($"Skipping spawn for {umcPlayer.Name} - no skin selected yet");
                continue;
            }

            if (umcPlayer.IsLocal)
                SpawnLocalPlayer(level, umcPlayer);
            else
                SpawnRemotePlayer(level, umcPlayer);
        }
    }

    public Player SpawnLocalPlayer(Level level, UmcPlayer umcPlayer, Vector2? spawnPosition = null)
    {
        if (_localPlayers.TryGetValue(umcPlayer, out var localPlayer))
        {
            UmcLogger.Warn($"Player {umcPlayer.Name} already has a local player entity");
            return localPlayer;
        }

        var spawnPos = spawnPosition ?? _spawnPosition ?? level.Session.RespawnPoint ?? new Vector2(160, 90);
        var spriteMode = level.Session.Inventory.Backpack
            ? PlayerSpriteMode.Madeline
            : PlayerSpriteMode.MadelineNoBackpack;

        var player = new Player(spawnPos, spriteMode);
        var controller = new LocalPlayerController(umcPlayer);
        player.Add(controller);
        player.Tag |= Tags.Persistent;

        level.Add(player);
        _localPlayers[umcPlayer] = player;
        UmcLogger.Info($"Spawned local player for {umcPlayer.Name} at {spawnPos}");

        return player;
    }

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

        var spawnPos = spawnPosition ?? _spawnPosition ?? level.Session.RespawnPoint ?? new Vector2(160, 90);
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

        return remotePlayer;
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
