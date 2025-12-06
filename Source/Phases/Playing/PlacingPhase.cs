using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

public class PlacingPhase
{
    public event Action OnComplete;

    private readonly Dictionary<UmcPlayer, PropInstance> _playerProps = new();
    private readonly Dictionary<UmcPlayer, Vector2> _targetPositions = new();
    private readonly HashSet<UmcPlayer> _placedPlayers = new();
    private readonly HashSet<UmcPlayer> _needsSpawn = new();
    private readonly Level _level;
    private PlayerCursors _cursors;

    private const float PositionLerpSpeed = 30f;

    private bool IsHost => NetworkManager.Instance?.IsHost ?? true;

    /// <summary>
    /// Calculates the top-left position for a prop so its bottom-left corner is at the cursor position.
    /// </summary>
    private static Vector2 GetTopLeftFromCursor(Vector2 cursorPos, PropInstance propInstance)
    {
        var size = propInstance.Prop.GetSize(propInstance.Rotation);
        return new Vector2(cursorPos.X, cursorPos.Y - size.Y);
    }

    public PlacingPhase(Level level, Dictionary<UmcPlayer, Prop> playerSelections)
    {
        _level = level;
        _cursors = new PlayerCursors(level, onConfirm: HandlePlacingConfirm);

        // Register network handlers
        NetworkManager.Handle<PropPlacedMessage>(HandlePropPlaced);
        NetworkManager.Handle<PlacingCompleteMessage>(HandlePlacingComplete);

        // Setup props for each player
        foreach (var kvp in playerSelections)
        {
            var propInstance = new PropInstance(kvp.Value);
            _playerProps[kvp.Key] = propInstance;
            _needsSpawn.Add(kvp.Key);
        }

        // Spawn cursors for local players
        _cursors.SpawnForLocalPlayers();
    }

    private void SpawnPreview(UmcPlayer player, PropInstance propInstance)
    {
        var cursorPos = _cursors.GetWorldPosition(player);

        // Snap to grid
        var snappedPos = new Vector2(
            (float)Math.Round(cursorPos.X / 8f) * 8f,
            (float)Math.Round(cursorPos.Y / 8f) * 8f
        );

        var topLeft = GetTopLeftFromCursor(snappedPos, propInstance);

        propInstance.Spawn(_level, topLeft);
        _targetPositions[player] = topLeft;

        UmcLogger.Info($"Created preview for {player.Name}: {propInstance.Prop.Name}");
    }

    public void Update()
    {
        // Spawn previews for players that need them (wait for cursor to exist)
        foreach (var player in _needsSpawn.ToList())
        {
            if (!_cursors.Has(player)) continue;

            if (_playerProps.TryGetValue(player, out var propInstance))
            {
                SpawnPreview(player, propInstance);
                _needsSpawn.Remove(player);
            }
        }

        foreach (var kvp in _playerProps)
        {
            var player = kvp.Key;
            var propInstance = kvp.Value;

            if (_placedPlayers.Contains(player)) continue;
            if (!propInstance.IsSpawned) continue;
            if (!_cursors.Has(player)) continue;

            // Calculate the snapped target position
            var worldPos = _cursors.GetWorldPosition(player);
            var snappedPos = new Vector2(
                (float)Math.Round(worldPos.X / 8f) * 8f,
                (float)Math.Round(worldPos.Y / 8f) * 8f
            );

            var targetTopLeft = GetTopLeftFromCursor(snappedPos, propInstance);
            _targetPositions[player] = targetTopLeft;

            // Smoothly interpolate to the target position
            var currentPos = propInstance.Position;
            var t = 1f - (float)Math.Exp(-PositionLerpSpeed * Engine.DeltaTime);
            var smoothedPos = Vector2.Lerp(currentPos, targetTopLeft, t);

            propInstance.SetPosition(smoothedPos);
        }
    }

    private void HandlePlacingConfirm(UmcPlayer player, Vector2 position)
    {
        if (_placedPlayers.Contains(player))
        {
            UmcLogger.Info($"Player {player.Name} already placed their prop");
            return;
        }

        if (!_playerProps.TryGetValue(player, out var propInstance))
        {
            UmcLogger.Warn($"Player {player.Name} has no selected prop to place");
            return;
        }

        var snappedPos = new Vector2(
            (float)Math.Round(position.X / 8f) * 8f,
            (float)Math.Round(position.Y / 8f) * 8f
        );

        // If online, send to host for validation; otherwise process locally
        if (NetworkManager.Instance?.IsOnline == true && !IsHost)
        {
            NetworkManager.SendToHost(new PropPlacedMessage
            {
                PlayerIndex = player.SlotIndex,
                PositionX = snappedPos.X,
                PositionY = snappedPos.Y
            });
        }
        else
        {
            // Host or local - process and broadcast
            ProcessPropPlacement(player, snappedPos, propInstance);

            if (NetworkManager.Instance?.IsOnline == true)
            {
                NetworkManager.Broadcast(new PropPlacedMessage
                {
                    PlayerIndex = player.SlotIndex,
                    PositionX = snappedPos.X,
                    PositionY = snappedPos.Y
                });
            }
        }
    }

    private void ProcessPropPlacement(UmcPlayer player, Vector2 snappedPos, PropInstance propInstance)
    {
        if (_placedPlayers.Contains(player)) return;

        var topLeft = GetTopLeftFromCursor(snappedPos, propInstance);
        propInstance.SetPosition(topLeft);

        _placedPlayers.Add(player);
        Audio.Play("event:/ui/main/button_select");
        UmcLogger.Info($"Player {player.Name} placed {propInstance.Prop.Name} at {topLeft}");

        _cursors.Remove(player);
        CheckAllPlayersPlaced();
    }

    private void CheckAllPlayersPlaced()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        int totalPlayers = session.Players.All.Count;
        int placedCount = _placedPlayers.Count;

        UmcLogger.Info($"Placed: {placedCount}/{totalPlayers}");

        if (placedCount >= totalPlayers)
        {
            // Host triggers completion
            if (IsHost)
            {
                NetworkManager.BroadcastWithSelf(new PlacingCompleteMessage());
            }
        }
    }

    #region Network Handlers

    private void HandlePropPlaced(CSteamID sender, PropPlacedMessage message)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var player = session.Players.GetAtSlot(message.PlayerIndex);
        if (player == null) return;

        if (!_playerProps.TryGetValue(player, out var propInstance)) return;

        var snappedPos = new Vector2(message.PositionX, message.PositionY);

        // If we're host and received from client, validate and rebroadcast
        if (IsHost && sender.m_SteamID != NetworkManager.Instance.LocalClientId)
        {
            ProcessPropPlacement(player, snappedPos, propInstance);
            NetworkManager.Broadcast(new PropPlacedMessage
            {
                PlayerIndex = message.PlayerIndex,
                PositionX = message.PositionX,
                PositionY = message.PositionY
            });
        }
        else if (!IsHost)
        {
            // Client receiving from host
            ProcessPropPlacement(player, snappedPos, propInstance);
        }
    }

    private void HandlePlacingComplete(PlacingCompleteMessage message)
    {
        OnComplete?.Invoke();
    }

    #endregion
}

#region Messages

public class PropPlacedMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public float PositionX { get; set; }
    public float PositionY { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
        writer.Write(PositionX);
        writer.Write(PositionY);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        PositionX = reader.ReadSingle();
        PositionY = reader.ReadSingle();
    }
}

public class PlacingCompleteMessage : INetMessage
{
    public void Serialize(BinaryWriter writer) { }
    public void Deserialize(BinaryReader reader) { }
}

#endregion
