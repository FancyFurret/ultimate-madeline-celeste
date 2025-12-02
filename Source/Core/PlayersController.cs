using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Multiplayer;
using Celeste.Mod.UltimateMadelineCeleste.Networking;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Core;

/// <summary>
/// Manages all players in the session. Handles join/leave logic and related network messages.
/// </summary>
public class PlayersController
{
    public const int MaxPlayers = 4;

    public List<UmcPlayer> All { get; } = new();

    private readonly List<int> _localSlotIndices = new();

    // Pending join request (client-side)
    private Action<bool, int> _pendingJoinCallback;
    private InputDevice _pendingJoinDevice;

    // Convenience accessors
    private MultiplayerController Net => MultiplayerController.Instance;
    private bool IsHost => Net?.IsHost ?? true;
    private ulong LocalClientId => Net?.LocalClientId ?? 1;

    public UmcPlayer GetAtSlot(int slot) => All.FirstOrDefault(p => p.SlotIndex == slot);
    public UmcPlayer Get(ulong clientId, int slot) => All.FirstOrDefault(p => p.ClientId == clientId && p.SlotIndex == slot);
    public IEnumerable<UmcPlayer> GetForClient(ulong clientId) => All.Where(p => p.ClientId == clientId);
    public IEnumerable<UmcPlayer> Local => All.Where(p => p.IsLocal);
    public IEnumerable<UmcPlayer> Remote => All.Where(p => !p.IsLocal);
    public bool IsSlotAvailable(int slot) => slot >= 0 && slot < MaxPlayers && GetAtSlot(slot) == null;
    public bool IsDeviceTaken(InputDevice device) => All.Any(p => p.Device != null && p.Device.Equals(device));

    // ========================================================================
    // Message Registration
    // ========================================================================

    public void RegisterMessages(NetMessageRegistry messages)
    {
        messages.Register<PlayerJoinRequestMessage>(1, HandlePlayerJoinRequest);
        messages.Register<PlayerJoinResponseMessage>(2, HandlePlayerJoinResponse);
        messages.Register<PlayerAddedMessage>(3, HandlePlayerAddedMessage);
        messages.Register<PlayerRemovedMessage>(4, HandlePlayerRemovedMessage);
        messages.Register<LobbyStateMessage>(5, HandleLobbyStateMessage);
    }

    public int FindFirstAvailableSlot()
    {
        for (var i = 0; i < MaxPlayers; i++)
        {
            if (GetAtSlot(i) == null)
                return i;
        }
        return -1;
    }

    public void RequestAddPlayer(InputDevice device, Action<bool, int> onComplete = null)
    {
        if (IsHost)
        {
            var slot = FindFirstAvailableSlot();
            if (slot < 0)
            {
                UmcLogger.Warn("Cannot add player: no available slots");
                onComplete?.Invoke(false, -1);
                return;
            }

            var playerName = GetLocalPlayerName(_localSlotIndices.Count);

            var player = Add(LocalClientId, slot, playerName, isLocal: true, device);
            if (player == null)
            {
                onComplete?.Invoke(false, -1);
                return;
            }

            _localSlotIndices.Add(slot);

            // Spawn the actual entity
            SpawnPlayerEntity(player);

            Net.Messages.Broadcast(new PlayerAddedMessage
            {
                ClientSteamId = LocalClientId,
                PlayerIndex = slot,
                PlayerName = playerName
            });

            onComplete?.Invoke(true, slot);
        }
        else
        {
            if (_pendingJoinCallback != null)
            {
                UmcLogger.Warn("Already have a pending join request");
                onComplete?.Invoke(false, -1);
                return;
            }

            _pendingJoinCallback = onComplete;
            _pendingJoinDevice = device;

            Net.Messages.SendToHost(new PlayerJoinRequestMessage());
            UmcLogger.Info("Sent player join request to host");
        }
    }

    public void RemoveLocalPlayer(int slot)
    {
        if (!_localSlotIndices.Contains(slot)) return;

        var player = Get(LocalClientId, slot);
        if (player != null)
        {
            MultiMadelineManager.Instance?.DespawnPlayer(player);
            Remove(player);
        }

        _localSlotIndices.Remove(slot);

        Net.Messages.Broadcast(new PlayerRemovedMessage
        {
            ClientSteamId = LocalClientId,
            PlayerIndex = slot
        });
    }

    public void RemoveLocalPlayer(UmcPlayer player)
    {
        if (player?.IsLocal == true)
            RemoveLocalPlayer(player.SlotIndex);
    }

    // ========================================================================
    // Message Handlers
    // ========================================================================

    private void HandlePlayerJoinRequest(CSteamID senderId, PlayerJoinRequestMessage message)
    {
        UmcLogger.Info("Got player join request");
        
        if (!IsHost)
        {
            UmcLogger.Warn("Received PlayerJoinRequest but we're not the host");
            return;
        }

        var clientId = senderId.m_SteamID;
        var clientName = SteamManager.GetPlayerName(senderId);

        var slotIndex = FindFirstAvailableSlot();
        if (slotIndex < 0)
        {
            Net.Messages.SendTo(new PlayerJoinResponseMessage
            {
                Success = false,
                ErrorMessage = "No available slots"
            }, senderId);
            return;
        }

        var existingCount = GetForClient(clientId).Count();
        var playerName = existingCount == 0 ? clientName : $"{clientName} ({existingCount + 1})";

        var player = Add(clientId, slotIndex, playerName, isLocal: false);
        if (player == null)
        {
            Net.Messages.SendTo(new PlayerJoinResponseMessage
            {
                Success = false,
                ErrorMessage = "Failed to add player"
            }, senderId);
            return;
        }

        // Spawn remote player entity
        SpawnPlayerEntity(player);

        Net.Messages.SendTo(new PlayerJoinResponseMessage
        {
            Success = true,
            AssignedPlayerIndex = slotIndex
        }, senderId);

        Net.Messages.Broadcast(new PlayerAddedMessage
        {
            ClientSteamId = clientId,
            PlayerIndex = slotIndex,
            PlayerName = playerName
        });

        UmcLogger.Info($"Host: Added player {playerName} at slot {slotIndex} for {clientName}");
    }

    private void HandlePlayerJoinResponse(CSteamID senderId, PlayerJoinResponseMessage message)
    {
        var callback = _pendingJoinCallback;
        var device = _pendingJoinDevice;
        _pendingJoinCallback = null;
        _pendingJoinDevice = null;

        if (message.Success)
        {
            var playerName = GetLocalPlayerName(_localSlotIndices.Count);
            var player = Add(LocalClientId, message.AssignedPlayerIndex, playerName, isLocal: true, device);
            
            if (player != null)
            {
                _localSlotIndices.Add(message.AssignedPlayerIndex);
                SpawnPlayerEntity(player);
                UmcLogger.Info($"Joined as player at slot {message.AssignedPlayerIndex}");
            }
        }
        else
        {
            UmcLogger.Warn($"Join request denied: {message.ErrorMessage}");
        }

        callback?.Invoke(message.Success, message.AssignedPlayerIndex);
    }

    private void HandlePlayerAddedMessage(CSteamID senderId, PlayerAddedMessage message)
    {
        if (Get(message.ClientSteamId, message.PlayerIndex) != null) return;

        var isLocal = message.ClientSteamId == LocalClientId;
        var player = Add(message.ClientSteamId, message.PlayerIndex, message.PlayerName, isLocal);

        // Only spawn remote players here - local players are spawned in HandlePlayerJoinResponse
        if (player != null && !isLocal)
        {
            SpawnPlayerEntity(player);
        }
    }

    private void HandlePlayerRemovedMessage(CSteamID senderId, PlayerRemovedMessage message)
    {
        var player = Get(message.ClientSteamId, message.PlayerIndex);
        if (player != null)
        {
            MultiMadelineManager.Instance?.DespawnPlayer(player);
            Remove(player);
        }
    }

    private void HandleLobbyStateMessage(CSteamID senderId, LobbyStateMessage message)
    {
        UmcLogger.Info($"Received lobby state: {message.Players.Count} players");

        Clear();

        foreach (var playerInfo in message.Players)
        {
            var isLocal = playerInfo.ClientSteamId == LocalClientId;
            var player = Add(playerInfo.ClientSteamId, playerInfo.PlayerIndex, playerInfo.PlayerName, isLocal);

            // Spawn remote player entities (local players will join separately)
            if (player != null && !isLocal)
            {
                SpawnPlayerEntity(player);
            }
        }
    }

    // ========================================================================
    // Called by MultiplayerController for lobby events
    // ========================================================================

    public void SendLobbyStateTo(CSteamID steamId)
    {
        Net.Messages.SendTo(new LobbyStateMessage
        {
            Players = All.Select(p => new PlayerAddedMessage
            {
                ClientSteamId = p.ClientId,
                PlayerIndex = p.SlotIndex,
                PlayerName = p.Name
            }).ToList()
        }, steamId);
    }

    public void HandleClientLeft(ulong clientId)
    {
        foreach (var player in GetForClient(clientId).ToList())
        {
            MultiMadelineManager.Instance?.DespawnPlayer(player);
            Remove(player);
        }
    }

    private void SpawnPlayerEntity(UmcPlayer player)
    {
        var manager = MultiMadelineManager.Instance;
        if (manager == null || !manager.IsMultiplayerLevel) return;

        var level = Engine.Scene as Level;
        if (level == null) return;

        if (player.IsLocal)
        {
            manager.SpawnLocalPlayer(level, player);
        }
        else
        {
            manager.SpawnRemotePlayer(level, player);
        }
    }

    internal UmcPlayer Add(ulong clientId, int slot, string name, bool isLocal, InputDevice device = null)
    {
        if (!IsSlotAvailable(slot))
        {
            UmcLogger.Warn($"Cannot add player: slot {slot} is not available");
            return null;
        }

        if (isLocal && device != null && IsDeviceTaken(device))
        {
            UmcLogger.Warn("Cannot add player: device already taken");
            return null;
        }

        var player = new UmcPlayer
        {
            ClientId = clientId,
            SlotIndex = slot,
            Name = name ?? $"Player {slot + 1}",
            IsLocal = isLocal,
            Device = device
        };

        All.Add(player);

        var deviceInfo = isLocal && device != null
            ? $" using {(device.Type == InputDeviceType.Keyboard ? "Keyboard" : $"Controller {device.ControllerIndex + 1}")}"
            : "";
        UmcLogger.Info($"Player '{player.Name}' added at slot {slot}{deviceInfo} (local={isLocal})");

        return player;
    }

    internal bool Remove(UmcPlayer player)
    {
        if (player == null) return false;

        if (All.Remove(player))
        {
            UmcLogger.Info($"Player '{player.Name}' removed from slot {player.SlotIndex}");
            return true;
        }
        return false;
    }

    internal void Clear()
    {
        foreach (var player in All.ToList())
        {
            MultiMadelineManager.Instance?.DespawnPlayer(player);
            Remove(player);
        }
        _localSlotIndices.Clear();
        _pendingJoinCallback = null;
        _pendingJoinDevice = null;
    }

    public void ClearRemotePlayers()
    {
        foreach (var player in Remote.ToList())
        {
            MultiMadelineManager.Instance?.DespawnPlayer(player);
            Remove(player);
        }
        _pendingJoinCallback = null;
        _pendingJoinDevice = null;
    }

    private string GetLocalPlayerName(int localIndex)
    {
        var baseName = SteamManager.IsInitialized ? SteamManager.LocalPlayerName : "Player";
        return localIndex == 0 ? baseName : $"{baseName} ({localIndex + 1})";
    }
}

public class UmcPlayer
{
    public ulong ClientId { get; set; }
    public int SlotIndex { get; set; }
    public string Name { get; set; }
    public bool IsLocal { get; set; }
    public InputDevice Device { get; set; }
    public string SkinId { get; set; }
    public int ColorIndex => SlotIndex;
    public RemotePlayerNetworkState LastNetworkState { get; set; }
}

public class RemotePlayerNetworkState
{
    public Vector2 Position { get; set; }
    public Facings Facing { get; set; }
    public Vector2 Scale { get; set; }
    public Vector2 Speed { get; set; }
    public int AnimationFrame { get; set; }
    public bool Dead { get; set; }
}
