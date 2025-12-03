using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SkinModHelper;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.UI.Hub;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Hub;

/// <summary>
/// Handles character selection: cursor management, pedestal interaction, and skin selection.
/// </summary>
public class CharacterSelection
{
    private const float SelectionRadius = 40f;
    private const float NetworkSyncInterval = 0.05f;

    private readonly Scene _scene;
    private readonly Dictionary<UmcPlayer, PlayerCursor> _localCursors = new();
    private readonly Dictionary<UmcPlayer, PlayerCursor> _remoteCursors = new();
    private readonly HashSet<UmcPlayer> _selectingPlayers = new();
    private readonly Dictionary<UmcPlayer, SkinPedestal> _hoveredPedestals = new();
    private readonly Dictionary<UmcPlayer, SkinPedestal> _claimedPedestals = new();
    private readonly Dictionary<UmcPlayer, Vector2> _spawnPositions = new();
    private float _networkSyncTimer;
    private PlayersController _players;

    public bool IsSelecting => _selectingPlayers.Count > 0;
    public bool IsPlayerSelecting(UmcPlayer player) => _selectingPlayers.Contains(player);
    public Vector2? GetSpawnPosition(UmcPlayer player) => _spawnPositions.TryGetValue(player, out var pos) ? pos : null;

    public CharacterSelection(Scene scene, PlayersController players)
    {
        _scene = scene;
        _players = players;

        if (_players != null)
            _players.OnPlayerSkinChanged += OnPlayerSkinChanged;
    }

    public void RegisterMessages(MessageRegistry messages)
    {
        messages.Register<CursorPositionMessage>(10, HandleCursorPosition);
    }

    public void Cleanup()
    {
        if (_players != null)
            _players.OnPlayerSkinChanged -= OnPlayerSkinChanged;
        _players = null;

        foreach (var cursor in _localCursors.Values) cursor.RemoveSelf();
        foreach (var cursor in _remoteCursors.Values) cursor.RemoveSelf();
        foreach (var pedestal in _claimedPedestals.Values)
        {
            if (pedestal != null) { pedestal.Visible = true; pedestal.Active = true; }
        }

        _localCursors.Clear();
        _remoteCursors.Clear();
        _selectingPlayers.Clear();
        _hoveredPedestals.Clear();
        _claimedPedestals.Clear();
        _spawnPositions.Clear();
    }

    private void OnPlayerSkinChanged(UmcPlayer player, string oldSkin, string newSkin)
    {
        if (string.IsNullOrEmpty(newSkin))
            HandleSkinCleared(player, oldSkin);
        else
            HandleSkinSelected(player, newSkin);
    }

    public void Update(Level level)
    {
        if (!IsSelecting) return;

        UpdateHoveredPedestals(level);

        _networkSyncTimer += Engine.DeltaTime;
        if (_networkSyncTimer >= NetworkSyncInterval)
        {
            _networkSyncTimer = 0f;
            BroadcastCursorPositions();
        }
    }

    public void CleanupRemovedPlayers(GameSession session)
    {
        foreach (var player in _claimedPedestals.Where(kvp => !session.Players.All.Contains(kvp.Key)).Select(kvp => kvp.Key).ToList())
            ReleasePedestal(player);

        foreach (var player in _remoteCursors.Where(kvp => !session.Players.All.Contains(kvp.Key)).Select(kvp => kvp.Key).ToList())
        {
            RemoveCursor(player);
            _selectingPlayers.Remove(player);
            _hoveredPedestals.Remove(player);
        }
    }

    public void StartSelection(UmcPlayer player)
    {
        if (_selectingPlayers.Contains(player)) return;

        UmcLogger.Info($"Starting character selection for {player.Name}");
        _selectingPlayers.Add(player);
        EnsureAvailablePedestal();

        if (player.IsLocal)
            CreateLocalCursor(player);
    }

    public void ReturnToSelection(UmcPlayer player)
    {
        if (!player.IsLocal || _selectingPlayers.Contains(player)) return;

        UmcLogger.Info($"Player {player.Name} returning to character selection");
        ReleasePedestal(player);
        PlayerSpawner.Instance?.DespawnPlayer(player);
        StartSelection(player);
        _players?.SetPlayerSkin(player, null);

        var net = NetworkManager.Instance;
        if (net?.IsOnline == true)
        {
            net.Messages.Broadcast(new PlayerGraphicsMessage
            {
                PlayerIndex = player.SlotIndex,
                SkinId = null
            });
        }

        Audio.Play("event:/ui/main/button_back");
    }

    public void ReleasePedestal(UmcPlayer player)
    {
        if (_claimedPedestals.TryGetValue(player, out var pedestal))
        {
            pedestal.Visible = true;
            pedestal.Active = true;
            _claimedPedestals.Remove(player);
        }
        _spawnPositions.Remove(player);
    }

    private void UpdateHoveredPedestals(Level level)
    {
        var pedestals = level.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>().ToList();

        foreach (var kvp in _localCursors)
        {
            var player = kvp.Key;
            var cursor = kvp.Value;
            if (!_selectingPlayers.Contains(player)) continue;

            Vector2 worldPos = cursor.GetWorldPosition(level);
            SkinPedestal closest = null;
            float closestDist = SelectionRadius;

            foreach (var pedestal in pedestals)
            {
                if (!pedestal.Active || !pedestal.Visible) continue;
                float dist = Vector2.Distance(worldPos, pedestal.Position);
                if (dist < closestDist) { closestDist = dist; closest = pedestal; }
            }

            _hoveredPedestals[player] = closest;
        }
    }

    private void CreateLocalCursor(UmcPlayer player)
    {
        if (_localCursors.ContainsKey(player)) return;

        var cursor = new PlayerCursor(player) { ScreenPosition = GetSpawnScreenPosition() };
        cursor.OnConfirm += () => OnCursorConfirm(player);
        cursor.OnCancel += () => ReturnToSelection(player);
        _scene.Add(cursor);
        _localCursors[player] = cursor;
    }

    private void CreateRemoteCursor(UmcPlayer player)
    {
        if (_remoteCursors.ContainsKey(player)) return;

        var cursor = new PlayerCursor(player) { ScreenPosition = GetSpawnScreenPosition() };
        _scene.Add(cursor);
        _remoteCursors[player] = cursor;
    }

    private Vector2 GetSpawnScreenPosition()
    {
        var level = _scene as Level;
        if (level == null) return new Vector2(960f, 540f);

        Vector2 worldSpawn = level.Session.RespawnPoint ?? new Vector2(160, 90);
        var playerData = level.Session.LevelData?.Entities?.FirstOrDefault(e => e.Name == "player");
        if (playerData != null) worldSpawn = new Vector2(playerData.Position.X, playerData.Position.Y);

        const float screenScale = 1920f / 320f;
        Vector2 gameScreenPos = (worldSpawn - level.Camera.Position) * level.Zoom;
        Vector2 screenPos = gameScreenPos * screenScale;
        return new Vector2(Math.Clamp(screenPos.X, 50f, 1870f), Math.Clamp(screenPos.Y, 50f, 1030f));
    }

    private void OnCursorConfirm(UmcPlayer player)
    {
        if (!_selectingPlayers.Contains(player)) return;
        if (_hoveredPedestals.TryGetValue(player, out var pedestal) && pedestal != null)
            SelectSkin(player, pedestal);
    }

    private void SelectSkin(UmcPlayer player, SkinPedestal pedestal)
    {
        if (!_selectingPlayers.Contains(player)) return;

        string skinName = pedestal.SkinName;
        UmcLogger.Info($"Player {player.Name} selected skin: {skinName}");

        player.SkinId = skinName;
        _claimedPedestals[player] = pedestal;
        _spawnPositions[player] = pedestal.Position;
        pedestal.Visible = false;
        pedestal.Active = false;
        _selectingPlayers.Remove(player);

        RemoveCursor(player);
        _hoveredPedestals.Remove(player);

        SpawnPlayerAfterSelection(player);
        BroadcastPlayerSelection(player);

        Audio.Play("event:/ui/main/button_select");
    }

    private void BroadcastPlayerSelection(UmcPlayer player)
    {
        var net = NetworkManager.Instance;
        if (net?.IsOnline != true) return;

        var spawner = PlayerSpawner.Instance;
        var stateSync = PlayerStateSync.Instance;
        var playerEntity = spawner?.GetLocalPlayer(player);

        if (playerEntity != null && stateSync != null)
            stateSync.SendPlayerGraphics(player, playerEntity);
    }

    private void SpawnPlayerAfterSelection(UmcPlayer player)
    {
        var level = _scene as Level;
        var spawner = PlayerSpawner.Instance;
        if (level == null || spawner == null) return;

        if (player.IsLocal && !string.IsNullOrEmpty(player.SkinId))
        {
            if (spawner.GetLocalPlayer(player) == null)
            {
                var spawnPos = GetSpawnPosition(player);
                spawner.SpawnLocalPlayer(level, player, spawnPos);
            }
        }
    }

    private void RemoveCursor(UmcPlayer player)
    {
        if (_localCursors.TryGetValue(player, out var lc))
        {
            lc.IsActive = false;
            lc.RemoveSelf();
            _localCursors.Remove(player);
        }
        if (_remoteCursors.TryGetValue(player, out var rc))
        {
            rc.IsActive = false;
            rc.RemoveSelf();
            _remoteCursors.Remove(player);
        }
    }

    private void EnsureAvailablePedestal()
    {
        var level = _scene as Level;
        if (level == null) return;

        var pedestals = level.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>().ToList();
        if (pedestals.Any(p => p.Active && p.Visible)) return;

        var defaultPedestal = pedestals.FirstOrDefault(p => p.SkinName == SkinsSystem.DEFAULT);
        if (defaultPedestal != null)
        {
            defaultPedestal.Visible = true;
            defaultPedestal.Active = true;
            var claimingPlayer = _claimedPedestals.FirstOrDefault(kvp => kvp.Value == defaultPedestal).Key;
            if (claimingPlayer != null)
            {
                _claimedPedestals.Remove(claimingPlayer);
                _spawnPositions.Remove(claimingPlayer);
            }
        }
    }

    #region Network

    private void BroadcastCursorPositions()
    {
        var net = NetworkManager.Instance;
        if (net?.IsOnline != true) return;

        foreach (var kvp in _localCursors)
        {
            if (!_selectingPlayers.Contains(kvp.Key)) continue;
            net.Messages.Broadcast(new CursorPositionMessage
            {
                PlayerIndex = kvp.Key.SlotIndex,
                ScreenPosition = kvp.Value.ScreenPosition
            }, SendMode.Unreliable);
        }
    }

    private void HandleCursorPosition(CSteamID senderId, CursorPositionMessage message)
    {
        var players = GameSession.Instance?.Players;
        if (players == null) return;

        var player = players.Get(senderId.m_SteamID, message.PlayerIndex);
        if (player == null || player.IsLocal) return;

        if (!_remoteCursors.ContainsKey(player) && string.IsNullOrEmpty(player.SkinId))
        {
            _selectingPlayers.Add(player);
            CreateRemoteCursor(player);
        }

        if (_remoteCursors.TryGetValue(player, out var cursor))
            cursor.SetPosition(message.ScreenPosition);
    }

    /// <summary>
    /// Called when a player selects a skin (via OnSkinChanged event).
    /// Handles pedestal claiming and cursor cleanup.
    /// </summary>
    private void HandleSkinSelected(UmcPlayer player, string skinId)
    {
        if (player == null) return;

        UmcLogger.Info($"Player {player.Name} skin changed to: {skinId}");

        // Claim the pedestal for this skin
        var pedestal = FindAndClaimPedestalBySkin(skinId);
        if (pedestal != null)
        {
            _claimedPedestals[player] = pedestal;
            _spawnPositions[player] = pedestal.Position;
        }

        // No longer selecting
        _selectingPlayers.Remove(player);
        RemoveCursor(player);
        _hoveredPedestals.Remove(player);

        // Spawn remote player if needed
        if (!player.IsLocal)
            PlayerSpawner.Instance?.SpawnRemotePlayer(_scene as Level, player);
    }

    /// <summary>
    /// Called when a player's skin is cleared (returning to selection).
    /// </summary>
    private void HandleSkinCleared(UmcPlayer player, string oldSkinId)
    {
        if (player == null) return;

        UmcLogger.Info($"Player {player.Name} returning to character selection");

        // Release the pedestal
        if (_claimedPedestals.TryGetValue(player, out var cp))
        {
            cp.Visible = true;
            cp.Active = true;
            _claimedPedestals.Remove(player);
        }
        else if (!string.IsNullOrEmpty(oldSkinId))
        {
            ReleasePedestalBySkin(oldSkinId);
        }

        _spawnPositions.Remove(player);

        // Despawn remote player
        if (!player.IsLocal)
            PlayerSpawner.Instance?.DespawnPlayer(player);

        // Add to selecting players
        _selectingPlayers.Add(player);
    }

    private SkinPedestal FindAndClaimPedestalBySkin(string skinName)
    {
        var level = _scene as Level;
        if (level == null) return null;

        foreach (var pedestal in level.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>())
        {
            if (pedestal.SkinName == skinName && pedestal.Active && pedestal.Visible)
            {
                pedestal.Visible = false;
                pedestal.Active = false;
                return pedestal;
            }
        }
        return null;
    }

    private void ReleasePedestalBySkin(string skinName)
    {
        var level = _scene as Level;
        if (level == null) return;

        foreach (var pedestal in level.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>())
        {
            if (pedestal.SkinName == skinName && !pedestal.Active)
            {
                pedestal.Visible = true;
                pedestal.Active = true;
                return;
            }
        }
    }

    #endregion
}

