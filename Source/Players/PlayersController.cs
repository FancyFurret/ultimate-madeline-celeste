using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Network.Steam;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework.Input;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Manages player slots and handles player-related network messages.
/// </summary>
public class PlayersController
{
    public const int MaxPlayers = 4;

    public List<UmcPlayer> All { get; } = new();

    private readonly List<int> _localSlotIndices = new();
    private Action<bool, int> _pendingJoinCallback;
    private InputDevice _pendingJoinDevice;
    private bool _pendingAutoSpawn;

    private NetworkManager Net => NetworkManager.Instance;
    private bool IsHost => LobbyController.Instance?.IsHost ?? true;
    private ulong LocalClientId => Net?.LocalClientId ?? 1;

    public UmcPlayer GetAtSlot(int slot) => All.FirstOrDefault(p => p.SlotIndex == slot);
    public UmcPlayer Get(ulong clientId, int slot) => All.FirstOrDefault(p => p.ClientId == clientId && p.SlotIndex == slot);
    public IEnumerable<UmcPlayer> GetForClient(ulong clientId) => All.Where(p => p.ClientId == clientId);
    public IEnumerable<UmcPlayer> Local => All.Where(p => p.IsLocal);
    public IEnumerable<UmcPlayer> Remote => All.Where(p => !p.IsLocal);
    public bool IsSlotAvailable(int slot) => slot >= 0 && slot < MaxPlayers && GetAtSlot(slot) == null;
    public bool IsDeviceTaken(InputDevice device) => All.Any(p => p.Device != null && p.Device.Equals(device));

    public PlayersController()
    {
        NetworkManager.Handle<PlayerJoinRequestMessage>(HandlePlayerJoinRequest);
        NetworkManager.Handle<PlayerJoinResponseMessage>(HandlePlayerJoinResponse);
        NetworkManager.Handle<PlayerAddedMessage>(HandlePlayerAddedMessage);
        NetworkManager.Handle<PlayerRemovedMessage>(HandlePlayerRemovedMessage);
    }

    public UmcPlayer GetByClientId(ulong clientId) => All.FirstOrDefault(p => p.ClientId == clientId);

    public int FindFirstAvailableSlot()
    {
        for (var i = 0; i < MaxPlayers; i++)
        {
            if (GetAtSlot(i) == null)
                return i;
        }
        return -1;
    }

    public void RequestAddPlayer(InputDevice device, bool autoSpawn = true, Action<bool, int> onComplete = null)
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

            if (autoSpawn)
            {
                SpawnPlayerEntity(player);
            }

            Net.Messages.Broadcast(new PlayerAddedMessage
            {
                ClientSteamId = LocalClientId,
                PlayerIndex = slot,
                PlayerName = playerName,
                SkinId = player.SkinId
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
            _pendingAutoSpawn = autoSpawn;

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
            PlayerSpawner.Instance?.DespawnPlayer(player);
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

    private void HandlePlayerJoinRequest(CSteamID senderId, PlayerJoinRequestMessage message)
    {
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
            PlayerName = playerName,
            SkinId = player.SkinId
        });

        UmcLogger.Info($"Host: Added player {playerName} at slot {slotIndex} for {clientName}");
    }

    private void HandlePlayerJoinResponse(CSteamID senderId, PlayerJoinResponseMessage message)
    {
        var callback = _pendingJoinCallback;
        var device = _pendingJoinDevice;
        var autoSpawn = _pendingAutoSpawn;
        _pendingJoinCallback = null;
        _pendingJoinDevice = null;
        _pendingAutoSpawn = true;

        if (message.Success)
        {
            var playerName = GetLocalPlayerName(_localSlotIndices.Count);
            var player = Add(LocalClientId, message.AssignedPlayerIndex, playerName, isLocal: true, device);

            if (player != null)
            {
                _localSlotIndices.Add(message.AssignedPlayerIndex);

                if (autoSpawn)
                {
                    SpawnPlayerEntity(player);
                }

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
        // Only handle new players - skin changes go through UpdateSkinMessage
        if (Get(message.ClientSteamId, message.PlayerIndex) != null) return;

        var isLocal = message.ClientSteamId == LocalClientId;
        var player = Add(message.ClientSteamId, message.PlayerIndex, message.PlayerName, isLocal);
        if (player == null) return;

        // Apply skin if provided (for lobby state sync)
        if (!isLocal && !string.IsNullOrEmpty(message.SkinId))
        {
            player.SkinId = message.SkinId;
            SpawnPlayerEntity(player);
        }
    }

    private void HandlePlayerRemovedMessage(CSteamID senderId, PlayerRemovedMessage message)
    {
        var player = Get(message.ClientSteamId, message.PlayerIndex);
        if (player != null)
        {
            PlayerSpawner.Instance?.DespawnPlayer(player);
            Remove(player);
        }
    }

    public void HandleClientLeft(ulong clientId)
    {
        foreach (var player in GetForClient(clientId).ToList())
        {
            PlayerSpawner.Instance?.DespawnPlayer(player);
            Remove(player);
        }
    }

    internal void SpawnPlayerEntity(UmcPlayer player)
    {
        var spawner = PlayerSpawner.Instance;
        if (spawner == null || !spawner.IsMultiplayerLevel) return;

        var level = Engine.Scene as Level;
        if (level == null) return;

        // Don't spawn players without a skin - they need to select first
        if (string.IsNullOrEmpty(player.SkinId))
        {
            UmcLogger.Info($"Skipping spawn for {player.Name} - awaiting character selection");
            return;
        }

        if (player.IsLocal)
            spawner.SpawnLocalPlayer(level, player);
        else
            spawner.SpawnRemotePlayer(level, player);
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
            Device = device,
            MaxLives = RoundSettings.Current.DefaultLives
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
            PlayerSpawner.Instance?.DespawnPlayer(player);
            Remove(player);
        }
        _localSlotIndices.Clear();
        _pendingJoinCallback = null;
        _pendingJoinDevice = null;
        _pendingAutoSpawn = true;
    }

    public void ClearRemotePlayers()
    {
        foreach (var player in Remote.ToList())
        {
            PlayerSpawner.Instance?.DespawnPlayer(player);
            Remove(player);
        }
        _pendingJoinCallback = null;
        _pendingJoinDevice = null;
        _pendingAutoSpawn = true;
    }

    private string GetLocalPlayerName(int localIndex)
    {
        var baseName = SteamManager.IsInitialized ? SteamManager.LocalPlayerName : "Player";
        return localIndex == 0 ? baseName : $"{baseName} ({localIndex + 1})";
    }

    public InputDevice DetectJoinInput()
    {
        var binding = UmcModule.Settings.ButtonJoin;

        // Check keyboard
        foreach (var key in binding.Keys)
        {
            if (MInput.Keyboard.Pressed(key))
            {
                var keyboardDevice = InputDevice.Keyboard;
                if (!IsDeviceTaken(keyboardDevice))
                    return keyboardDevice;
            }
        }

        // Check controllers
        for (var i = 0; i < 4; i++)
        {
            var gamepad = MInput.GamePads[i];
            if (!gamepad.Attached) continue;

            foreach (var button in binding.Buttons)
            {
                if (gamepad.Pressed(button))
                {
                    var controllerDevice = InputDevice.Controller(i);
                    if (!IsDeviceTaken(controllerDevice))
                        return controllerDevice;
                }
            }
        }

        return null;
    }

    public UmcPlayer DetectLeaveInput()
    {
        var binding = UmcModule.Settings.ButtonLeave;

        // Check keyboard
        foreach (var key in binding.Keys)
        {
            if (MInput.Keyboard.Pressed(key))
            {
                var keyboardDevice = InputDevice.Keyboard;
                var player = Local.FirstOrDefault(p => p.Device?.Equals(keyboardDevice) == true);
                if (player != null)
                    return player;
            }
        }

        // Check controllers
        for (var i = 0; i < 4; i++)
        {
            var gamepad = MInput.GamePads[i];
            if (!gamepad.Attached) continue;

            foreach (var button in binding.Buttons)
            {
                if (gamepad.Pressed(button))
                {
                    var controllerDevice = InputDevice.Controller(i);
                    var player = Local.FirstOrDefault(p => p.Device?.Equals(controllerDevice) == true);
                    if (player != null)
                        return player;
                }
            }
        }

        return null;
    }
}
