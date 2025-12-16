using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// Helper class for spawning, tracking, and managing player cursors.
/// Handles both local and remote cursors in a unified way.
/// </summary>
public class PlayerCursors
{
    /// <summary>
    /// The currently active PlayerCursors instance. Used by remote cursor factory.
    /// </summary>
    public static PlayerCursors ActiveInstance { get; private set; }

    private readonly Scene _scene;
    private readonly Dictionary<UmcPlayer, PlayerCursor> _cursors = new();
    private readonly Action<UmcPlayer, Vector2> _onConfirm;
    private readonly Action<UmcPlayer> _onCancel;

    public IReadOnlyDictionary<UmcPlayer, PlayerCursor> All => _cursors;
    public int Count => _cursors.Count;

    /// <summary>
    /// Whether cursors should be tracked by the camera. Default is true.
    /// </summary>
    public bool TrackWithCamera { get; set; } = true;

    public PlayerCursors(Scene scene, Action<UmcPlayer, Vector2> onConfirm = null, Action<UmcPlayer> onCancel = null, bool trackWithCamera = true)
    {
        _scene = scene ?? throw new ArgumentNullException(nameof(scene));
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        TrackWithCamera = trackWithCamera;

        // Set as active instance so remote cursor factory can find us
        ActiveInstance = this;
    }

    /// <summary>
    /// Spawns cursors for all local players in the session.
    /// Remote cursors are automatically created via NetworkedEntityRegistry.
    /// </summary>
    public void SpawnForLocalPlayers()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        foreach (var player in session.Players.All)
        {
            if (player.IsLocal)
                Spawn(player);
        }
    }

    /// <summary>
    /// Spawns a cursor for a specific player.
    /// </summary>
    public PlayerCursor Spawn(UmcPlayer player, bool isRemote = false)
    {
        if (player == null) return null;
        if (_cursors.ContainsKey(player)) return _cursors[player];

        var cursor = new PlayerCursor(
            player,
            isRemote,
            pos => _onConfirm?.Invoke(player, pos),
            () => _onCancel?.Invoke(player)
        );
        _scene.Add(cursor);
        _cursors[player] = cursor;

        if (TrackWithCamera)
            CameraController.Instance?.TrackEntity(cursor);

        UmcLogger.Info($"Spawned {(isRemote ? "remote" : "local")} cursor for {player.Name}");
        return cursor;
    }

    /// <summary>
    /// Registers a remote cursor that was spawned via the network factory.
    /// </summary>
    public void RegisterRemoteCursor(UmcPlayer player, PlayerCursor cursor)
    {
        if (player == null || cursor == null) return;
        if (_cursors.ContainsKey(player))
        {
            UmcLogger.Warn($"Remote cursor for {player.Name} already registered");
            return;
        }

        _cursors[player] = cursor;

        if (TrackWithCamera)
            CameraController.Instance?.TrackEntity(cursor);

        UmcLogger.Info($"Registered remote cursor for {player.Name}");
    }

    /// <summary>
    /// Gets the cursor for a specific player, if it exists.
    /// </summary>
    public PlayerCursor GetFor(UmcPlayer player)
    {
        return player != null && _cursors.TryGetValue(player, out var cursor) ? cursor : null;
    }

    /// <summary>
    /// Gets the world position of a player's cursor, or a fallback position if not found.
    /// </summary>
    public Vector2 GetWorldPosition(UmcPlayer player, Vector2 fallback = default)
    {
        var cursor = GetFor(player);
        return cursor != null ? cursor.WorldPosition : fallback;
    }

    /// <summary>
    /// Checks if a cursor exists for the given player.
    /// </summary>
    public bool Has(UmcPlayer player) => player != null && _cursors.ContainsKey(player);

    /// <summary>
    /// Removes and despawns the cursor for a specific player.
    /// </summary>
    public void Remove(UmcPlayer player)
    {
        if (player == null) return;
        if (!_cursors.TryGetValue(player, out var cursor)) return;

        cursor.IsActive = false;
        _cursors.Remove(player);
        if (cursor.IsLocal)
            cursor.RemoveSelf();

        UmcLogger.Info($"Removed cursor for {player.Name}");
    }

    /// <summary>
    /// Removes and despawns all cursors.
    /// </summary>
    public void RemoveAll()
    {
        foreach (var cursor in _cursors.Values)
        {
            cursor.IsActive = false;
            cursor.RemoveSelf();
        }
        _cursors.Clear();

        // Clear active instance when all cursors are removed
        if (ActiveInstance == this)
            ActiveInstance = null;
    }

    /// <summary>
    /// Cleans up cursors for players that are no longer in the session.
    /// </summary>
    public void CleanupRemovedPlayers()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var playersToRemove = _cursors.Keys
            .Where(p => !session.Players.All.Contains(p))
            .ToList();

        foreach (var player in playersToRemove)
            Remove(player);
    }
}
