using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// Manages all UmcBerry entities during gameplay.
/// Handles berry pickup, drop on death, and collection at goal.
/// Also handles network synchronization for berries.
/// </summary>
public class BerryManager
{
    public static BerryManager Instance { get; private set; }

    private readonly List<UmcBerry> _berries = new();
    private Level _level;

    private bool IsHost => NetworkManager.Instance?.IsHost ?? true;

    public BerryManager(Level level)
    {
        Instance = this;
        _level = level;

        // Subscribe to goal events
        GoalFlag.OnPlayerReachedGoal += HandlePlayerReachedGoal;
        UmcBerry.OnBerryCollected += HandleBerryCollected;

        // Register network handlers
        NetworkManager.Handle<BerryPickedMessage>(HandleBerryPickedMessage);
        NetworkManager.Handle<BerryCollectedMessage>(HandleBerryCollectedMessage);
        NetworkManager.Handle<BerryDroppedMessage>(HandleBerryDroppedMessage);

        // Find all existing UmcBerry entities in the level
        RefreshBerryList();

        UmcLogger.Info($"BerryManager initialized with {_berries.Count} berries");
    }

    /// <summary>
    /// Refreshes the list of berries from the current scene.
    /// </summary>
    public void RefreshBerryList()
    {
        _berries.Clear();
        if (_level == null) return;

        foreach (var entity in _level.Tracker.GetEntities<UmcBerry>())
        {
            if (entity is UmcBerry berry)
            {
                _berries.Add(berry);
            }
        }
    }

    /// <summary>
    /// Registers a newly spawned berry.
    /// </summary>
    public void RegisterBerry(UmcBerry berry)
    {
        if (!_berries.Contains(berry))
        {
            _berries.Add(berry);
            UmcLogger.Info($"Registered berry at {berry.SpawnPosition}");
        }
    }

    /// <summary>
    /// Called when a player reaches the goal. Collects their carried berry.
    /// </summary>
    private void HandlePlayerReachedGoal(UmcPlayer player)
    {
        foreach (var berry in _berries)
        {
            if (berry.IsCollected) continue;
            if (berry.Carrier != player) continue;

            berry.Collect(player);
        }
    }

    /// <summary>
    /// Called when a berry is collected. Records the score and broadcasts.
    /// </summary>
    private void HandleBerryCollected(UmcBerry berry, UmcPlayer player)
    {
        RoundState.Current?.RecordBerryCollected(player);

        // Broadcast collection to other clients (visual sync)
        NetworkManager.Broadcast(new BerryCollectedMessage
        {
            PlayerIndex = player.SlotIndex,
            BerryX = berry.SpawnPosition.X,
            BerryY = berry.SpawnPosition.Y
        });
    }

    /// <summary>
    /// Called when a local player picks up a berry. Sends to host for validation.
    /// </summary>
    public void RequestBerryPickup(UmcBerry berry, UmcPlayer player)
    {
        if (IsHost)
        {
            // Host validates and broadcasts immediately
            NetworkManager.Broadcast(new BerryPickedMessage
            {
                PlayerIndex = player.SlotIndex,
                BerryX = berry.SpawnPosition.X,
                BerryY = berry.SpawnPosition.Y
            });
        }
        else
        {
            // Client sends to host for validation
            NetworkManager.SendToHost(new BerryPickedMessage
            {
                PlayerIndex = player.SlotIndex,
                BerryX = berry.SpawnPosition.X,
                BerryY = berry.SpawnPosition.Y
            });
        }
    }

    /// <summary>
    /// Called when a local player drops a berry (on death).
    /// </summary>
    public void BroadcastBerryDropped(UmcBerry berry, Vector2 dropPosition)
    {
        NetworkManager.Broadcast(new BerryDroppedMessage
        {
            BerryX = berry.SpawnPosition.X,
            BerryY = berry.SpawnPosition.Y,
            DropX = dropPosition.X,
            DropY = dropPosition.Y
        });
    }

    /// <summary>
    /// Finds a berry by its spawn position.
    /// </summary>
    private UmcBerry FindBerryBySpawnPosition(float x, float y)
    {
        const float tolerance = 1f;
        return _berries.FirstOrDefault(b =>
            System.Math.Abs(b.SpawnPosition.X - x) < tolerance &&
            System.Math.Abs(b.SpawnPosition.Y - y) < tolerance);
    }

    #region Network Handlers

    private void HandleBerryPickedMessage(CSteamID sender, BerryPickedMessage message)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var player = session.Players.GetAtSlot(message.PlayerIndex);
        if (player == null) return;

        var berry = FindBerryBySpawnPosition(message.BerryX, message.BerryY);
        if (berry == null || berry.IsCarried || berry.IsCollected) return;

        // If we're host and received from client, validate and rebroadcast
        if (IsHost && sender.m_SteamID != NetworkManager.Instance.LocalClientId)
        {
            // Validate: berry exists and is available
            NetworkManager.Broadcast(new BerryPickedMessage
            {
                PlayerIndex = message.PlayerIndex,
                BerryX = message.BerryX,
                BerryY = message.BerryY
            });
        }

        // Don't apply to our own local players (they already picked up locally)
        if (player.IsLocal) return;

        // Apply pickup for remote player
        var spawner = PlayerSpawner.Instance;
        if (spawner?.RemotePlayers.TryGetValue(player, out var remotePlayer) == true)
        {
            berry.PickupByRemote(player);
            UmcLogger.Info($"Synced berry pickup for player {player.Name}");
        }
    }

    private void HandleBerryCollectedMessage(CSteamID sender, BerryCollectedMessage message)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var player = session.Players.GetAtSlot(message.PlayerIndex);
        if (player == null || player.IsLocal) return;

        var berry = FindBerryBySpawnPosition(message.BerryX, message.BerryY);
        if (berry == null || berry.IsCollected) return;

        // Play collection effects without recording score (host already did)
        berry.CollectVisualOnly();
        UmcLogger.Info($"Synced berry collection for player {player.Name}");
    }

    private void HandleBerryDroppedMessage(CSteamID sender, BerryDroppedMessage message)
    {
        var berry = FindBerryBySpawnPosition(message.BerryX, message.BerryY);
        if (berry == null || berry.IsCollected) return;

        // Drop the berry at the specified position
        berry.Drop(new Vector2(message.DropX, message.DropY));
        UmcLogger.Info($"Synced berry drop at ({message.DropX}, {message.DropY})");
    }

    #endregion

    /// <summary>
    /// Cleans up the manager.
    /// </summary>
    public void Cleanup()
    {
        GoalFlag.OnPlayerReachedGoal -= HandlePlayerReachedGoal;
        UmcBerry.OnBerryCollected -= HandleBerryCollected;
        UmcBerry.ClearHandlers();
        _berries.Clear();

        if (Instance == this)
            Instance = null;

        UmcLogger.Info("BerryManager cleaned up");
    }
}

