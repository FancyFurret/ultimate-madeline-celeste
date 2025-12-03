using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Network.Steam;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Network;

/// <summary>
/// Handles Steam lobby/connection management. Tracks which clients are connected (not players).
/// </summary>
public class LobbyController
{
    public static LobbyController Instance { get; private set; }

    public bool IsOnline => SteamLobby.Instance?.InLobby == true;
    public bool IsHost => !IsOnline || SteamLobby.Instance?.IsHost == true;
    public string LobbyCode => SteamLobby.Instance?.LobbyCode;
    public List<LobbyMember> Members => SteamLobby.Instance?.LobbyMembers;

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public LobbyController()
    {
        Instance = this;
    }

    public void RegisterMessages(MessageRegistry messages)
    {
        messages.Register<LobbyStateMessage>(5, HandleLobbyStateMessage);
    }

    public void Initialize(SteamLobby lobbyManager)
    {
        if (lobbyManager == null) return;

        lobbyManager.OnLobbyCreated += HandleLobbyCreated;
        lobbyManager.OnLobbyJoined += HandleLobbyJoined;
        lobbyManager.OnLobbyLeft += HandleLobbyLeft;
        lobbyManager.OnPlayerJoined += HandleClientJoined;
        lobbyManager.OnPlayerLeft += HandleClientLeft;
    }

    public void Shutdown(SteamLobby lobbyManager)
    {
        if (lobbyManager == null) return;

        lobbyManager.OnLobbyCreated -= HandleLobbyCreated;
        lobbyManager.OnLobbyJoined -= HandleLobbyJoined;
        lobbyManager.OnLobbyLeft -= HandleLobbyLeft;
        lobbyManager.OnPlayerJoined -= HandleClientJoined;
        lobbyManager.OnPlayerLeft -= HandleClientLeft;
    }

    public void HostOnlineGame()
    {
        if (!SteamManager.IsInitialized)
        {
            OnError?.Invoke("Steam is not available");
            return;
        }

        UmcLogger.Info("Hosting online game...");
        SteamLobby.Instance?.CreateLobby();
    }

    public void JoinOnlineGame(string code)
    {
        if (!SteamManager.IsInitialized)
        {
            OnError?.Invoke("Steam is not available");
            return;
        }

        UmcLogger.Info($"Joining online game: {code}");
        SteamLobby.Instance?.JoinLobbyByCode(code);
    }

    public void LeaveLobby()
    {
        if (!IsOnline) return;
        UmcLogger.Info("Leaving lobby...");
        SteamLobby.Instance?.LeaveLobby();
    }

    public void InviteFriends()
    {
        if (!IsOnline)
        {
            OnError?.Invoke("Not in an online lobby");
            return;
        }
        SteamLobby.Instance?.OpenInviteOverlay();
    }

    private void HandleLobbyCreated(CSteamID lobbyId)
    {
        UmcLogger.Info($"Lobby created: {lobbyId}");
        OnConnected?.Invoke();
    }

    private void HandleLobbyJoined(CSteamID lobbyId, bool success)
    {
        if (!success)
        {
            UmcLogger.Error("Failed to join lobby");
            OnError?.Invoke("Failed to join lobby");
            return;
        }

        UmcLogger.Info($"Joined lobby: {lobbyId}");
        OnConnected?.Invoke();
    }

    private void HandleLobbyLeft()
    {
        UmcLogger.Info("Left lobby");
        GameSession.Instance?.Players.ClearRemotePlayers();
        OnDisconnected?.Invoke();
    }

    private void HandleClientJoined(CSteamID steamId, string clientName)
    {
        UmcLogger.Info($"Client joined: {clientName} ({steamId})");

        if (IsHost)
        {
            SendLobbyStateTo(steamId);
        }
    }

    /// <summary>
    /// Sends full lobby state to a client when they join.
    /// </summary>
    private void SendLobbyStateTo(CSteamID steamId)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var spawner = PlayerSpawner.Instance;
        var stateSync = PlayerStateSync.Instance;

        var playerMessages = new List<PlayerAddedMessage>();
        foreach (var p in session.Players.All)
        {
            var msg = new PlayerAddedMessage
            {
                ClientSteamId = p.ClientId,
                PlayerIndex = p.SlotIndex,
                PlayerName = p.Name
            };

            // Include graphics if player is spawned
            if (spawner != null && stateSync != null)
            {
                var playerEntity = spawner.GetLocalPlayer(p);
                if (playerEntity != null)
                    msg.Graphics = stateSync.BuildPlayerGraphics(p, playerEntity);
            }

            playerMessages.Add(msg);
        }

        var message = new LobbyStateMessage { Players = playerMessages };
        NetworkManager.Instance?.Messages.SendTo(message, steamId);
        UmcLogger.Info($"Sent lobby state to {SteamManager.GetPlayerName(steamId)}");
    }

    private void HandleClientLeft(CSteamID steamId, string clientName)
    {
        UmcLogger.Info($"Client left: {clientName} ({steamId})");
        GameSession.Instance?.Players.HandleClientLeft(steamId.m_SteamID);
        NetworkManager.Instance?.Messages.ClearPeerState(steamId);
    }

    private void HandleLobbyStateMessage(CSteamID senderId, LobbyStateMessage message)
    {
        UmcLogger.Info($"Received lobby state: {message.Players.Count} players");

        var players = GameSession.Instance?.Players;
        var spawner = PlayerSpawner.Instance;
        if (players == null) return;

        players.Clear();

        foreach (var playerInfo in message.Players)
        {
            var isLocal = playerInfo.ClientSteamId == NetworkManager.Instance?.LocalClientId;
            var player = players.Add(playerInfo.ClientSteamId, playerInfo.PlayerIndex, playerInfo.PlayerName, isLocal);

            if (player == null) continue;

            if (!string.IsNullOrEmpty(playerInfo.Graphics?.SkinId))
                players.SetPlayerSkin(player, playerInfo.Graphics.SkinId);

            // Apply additional graphics if provided
            if (!isLocal && playerInfo.Graphics != null)
            {
                var remotePlayer = spawner?.GetRemotePlayer(player);
                remotePlayer?.UpdateGraphics(playerInfo.Graphics);
            }
        }
    }
}

