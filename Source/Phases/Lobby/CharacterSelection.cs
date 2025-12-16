using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.SkinModHelper;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Lobby;

/// <summary>
/// Handles character selection: cursor management, pedestal interaction, and skin selection.
/// </summary>
public class CharacterSelection
{
    private const float SelectionRadius = 40f;

    private readonly LobbyPhase _lobby;
    private PlayerCursors _cursors;
    private readonly HashSet<UmcPlayer> _selectingPlayers = new();
    private readonly Dictionary<UmcPlayer, SkinPedestal> _hoveredPedestals = new();
    private readonly Dictionary<UmcPlayer, SkinPedestal> _claimedPedestals = new();
    private readonly Dictionary<UmcPlayer, Vector2> _spawnPositions = new();

    public bool IsSelecting => _selectingPlayers.Count > 0;
    public bool IsPlayerSelecting(UmcPlayer player) => _selectingPlayers.Contains(player);
    public Vector2? GetSpawnPosition(UmcPlayer player) => _spawnPositions.TryGetValue(player, out var pos) ? pos : null;

    public CharacterSelection(LobbyPhase lobby, Scene scene)
    {
        _lobby = lobby;
        _cursors = new PlayerCursors(
            scene,
            onConfirm: (player, _) => OnCursorConfirm(player),
            onCancel: ReturnToSelection
        );
        NetworkManager.Handle<UpdateSkinMessage>(HandleUpdateSkin);
    }

    public void Cleanup()
    {
        _cursors?.RemoveAll();
        foreach (var pedestal in _claimedPedestals.Values)
        {
            if (pedestal != null) { pedestal.Visible = true; pedestal.Active = true; }
        }

        _selectingPlayers.Clear();
        _hoveredPedestals.Clear();
        _claimedPedestals.Clear();
        _spawnPositions.Clear();
    }

    private void HandleUpdateSkin(CSteamID senderId, UpdateSkinMessage message)
    {
        var players = GameSession.Instance?.Players;
        if (players == null) return;

        var player = players.Get(senderId.m_SteamID, message.PlayerIndex);
        if (player == null) return;

        var oldSkin = player.SkinId;
        player.SkinId = message.SkinId;

        if (string.IsNullOrEmpty(message.SkinId))
            HandleSkinCleared(player, oldSkin);
        else
            HandleSkinSelected(player, message.SkinId);
    }

    public void Update(Level level)
    {
        // Start selection for any local players without skins who aren't already selecting
        var session = GameSession.Instance;
        if (session != null)
        {
            foreach (var player in session.Players.All)
            {
                if (string.IsNullOrEmpty(player.SkinId) && !_selectingPlayers.Contains(player))
                {
                    StartSelection(player);
                }
            }
        }

        if (!IsSelecting) return;
        UpdateHoveredPedestals(level);
    }

    public void CleanupRemovedPlayers(GameSession session)
    {
        foreach (var player in _claimedPedestals.Where(kvp => !session.Players.All.Contains(kvp.Key)).Select(kvp => kvp.Key).ToList())
            ReleasePedestal(player);

        var playersToRemove = _cursors.All.Keys
            .Where(p => !session.Players.All.Contains(p))
            .ToList();

        foreach (var player in playersToRemove)
        {
            _cursors.Remove(player);
            _selectingPlayers.Remove(player);
            _hoveredPedestals.Remove(player);
        }
    }

    public void StartSelection(UmcPlayer player)
    {
        if (_selectingPlayers.Contains(player)) return;

        UmcLogger.Info($"Starting character selection for {player.Name}");

        bool wasEmpty = _selectingPlayers.Count == 0;
        _selectingPlayers.Add(player);
        EnsureAvailablePedestal();

        // Track pedestals when first player starts selecting
        if (wasEmpty)
        {
            foreach (var pedestal in _lobby.Scene.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>())
            {
                if (pedestal.Active && pedestal.Visible)
                    CameraController.Instance?.TrackEntity(pedestal);
            }
        }

        if (player.IsLocal)
            _cursors.Spawn(player);
    }

    public void ReturnToSelection(UmcPlayer player)
    {
        if (!player.IsLocal || _selectingPlayers.Contains(player)) return;

        NetworkManager.BroadcastWithSelf(new UpdateSkinMessage
        {
            PlayerIndex = player.SlotIndex,
            SkinId = null
        });

        Audio.Play("event:/ui/main/button_back");
    }

    public void CancelSelection(UmcPlayer player)
    {
        if (!_selectingPlayers.Contains(player)) return;

        _selectingPlayers.Remove(player);
        _cursors.Remove(player);
        _hoveredPedestals.Remove(player);
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

        foreach (var kvp in _cursors.All)
        {
            var player = kvp.Key;
            var cursor = kvp.Value;
            if (!_selectingPlayers.Contains(player)) continue;

            Vector2 worldPos = cursor.WorldPosition;
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

    private void OnCursorConfirm(UmcPlayer player)
    {
        if (!_selectingPlayers.Contains(player)) return;
        if (_hoveredPedestals.TryGetValue(player, out var pedestal) && pedestal != null)
            SelectSkin(player, pedestal);
    }

    private void SelectSkin(UmcPlayer player, SkinPedestal pedestal)
    {
        if (!_selectingPlayers.Contains(player)) return;

        NetworkManager.BroadcastWithSelf(new UpdateSkinMessage
        {
            PlayerIndex = player.SlotIndex,
            SkinId = pedestal.SkinName
        });

        Audio.Play("event:/ui/main/button_select");
    }

    private void EnsureAvailablePedestal()
    {
        var pedestals = _lobby.Scene.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>().ToList();
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

    private void HandleSkinSelected(UmcPlayer player, string skinId)
    {
        if (player == null) return;

        UmcLogger.Info($"Player {player.Name} selected skin: {skinId}");

        // Claim the pedestal for this skin
        var pedestal = FindAndClaimPedestalBySkin(skinId);
        if (pedestal != null)
        {
            _claimedPedestals[player] = pedestal;
            _spawnPositions[player] = pedestal.Position;
        }

        // No longer selecting
        _selectingPlayers.Remove(player);
        _cursors.Remove(player);
        _hoveredPedestals.Remove(player);

        // Spawn player
        var spawner = PlayerSpawner.Instance;
        if (spawner == null) return;

        if (player.IsLocal)
        {
            if (spawner.GetLocalPlayer(player) == null)
            {
                var spawnPos = GetSpawnPosition(player);
                spawner.SpawnLocalPlayer(_lobby.Scene as Level, player, spawnPos);
            }
        }
        // Remote players spawn via NetworkedEntity factory when they select a skin
    }

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
        PlayerSpawner.Instance?.DespawnPlayer(player);

        // Start selection (creates cursor for local players)
        StartSelection(player);
    }

    private SkinPedestal FindAndClaimPedestalBySkin(string skinName)
    {
        foreach (var pedestal in _lobby.Scene.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>())
        {
            if (pedestal.SkinName == skinName && pedestal.Active && pedestal.Visible)
            {
                // Only hide non-default pedestals - default/Madeline should always be available
                if (skinName != SkinsSystem.DEFAULT)
                {
                    pedestal.Visible = false;
                    pedestal.Active = false;
                }
                return pedestal;
            }
        }
        return null;
    }

    private void ReleasePedestalBySkin(string skinName)
    {
        foreach (var pedestal in _lobby.Scene.Tracker.GetEntities<SkinPedestal>().Cast<SkinPedestal>())
        {
            if (pedestal.SkinName == skinName && !pedestal.Active)
            {
                pedestal.Visible = true;
                pedestal.Active = true;
                return;
            }
        }
    }
}

