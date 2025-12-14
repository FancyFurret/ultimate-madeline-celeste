using System;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Network.Steam;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;
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

    /// <summary>
    /// True when client is waiting for lobby state from host.
    /// </summary>
    public bool IsWaitingForLobbyState { get; private set; }

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;
    public event Action OnLobbyStateReceived;

    public LobbyController()
    {
        Instance = this;
        NetworkManager.Handle<LobbyStateMessage>(HandleLobbyStateMessage);
        NetworkManager.Handle<RequestLobbyStateMessage>(HandleRequestLobbyState);
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
        IsWaitingForLobbyState = true;
        SteamLobby.Instance?.JoinLobbyByCode(code);
    }

    public void LeaveLobby()
    {
        if (!IsOnline) return;
        UmcLogger.Info("Leaving lobby...");
        IsWaitingForLobbyState = false;
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

    /// <summary>
    /// Called by client when they're ready to receive lobby state (scene is loaded).
    /// </summary>
    public void RequestLobbyState()
    {
        if (IsHost) return;
        if (!IsOnline) return;

        UmcLogger.Info("Scene: " + Engine.Scene);
        UmcLogger.Info("Requesting lobby state from host...");
        IsWaitingForLobbyState = true;
        NetworkManager.SendToHost(new RequestLobbyStateMessage());
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
            IsWaitingForLobbyState = false;
            OnError?.Invoke("Failed to join lobby");
            return;
        }

        UmcLogger.Info($"Joined lobby: {lobbyId}");
        OnConnected?.Invoke();
    }

    private void HandleLobbyLeft()
    {
        UmcLogger.Info("Left lobby");
        IsWaitingForLobbyState = false;
        GameSession.Instance?.Players.ClearRemotePlayers();
        OnDisconnected?.Invoke();
    }

    private void HandleClientJoined(CSteamID steamId, string clientName)
    {
        UmcLogger.Info($"Client joined: {clientName} ({steamId})");
        // Don't send lobby state immediately - wait for client to request it
    }

    /// <summary>
    /// Host receives this when a client is ready for lobby state.
    /// </summary>
    private void HandleRequestLobbyState(CSteamID senderId, RequestLobbyStateMessage message)
    {
        if (!IsHost) return;

        UmcLogger.Info($"Client {SteamManager.GetPlayerName(senderId)} requested lobby state");
        SendLobbyStateTo(senderId);
        NetworkedEntityRegistry.Instance?.SendAllEntitiesTo(senderId);
    }

    /// <summary>
    /// Sends full lobby state to a client.
    /// </summary>
    private void SendLobbyStateTo(CSteamID steamId)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var playerMessages = new List<PlayerAddedMessage>();
        foreach (var p in session.Players.All)
        {
            playerMessages.Add(new PlayerAddedMessage
            {
                ClientSteamId = p.ClientId,
                PlayerIndex = p.SlotIndex,
                PlayerName = p.Name,
                SkinId = p.SkinId,
                MaxLives = p.MaxLives
            });
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
        NetworkedEntityRegistry.Instance?.RemoveEntitiesOwnedBy(steamId.m_SteamID);
    }

    private void HandleLobbyStateMessage(CSteamID senderId, LobbyStateMessage message)
    {
        UmcLogger.Info($"Received lobby state: {message.Players.Count} players");
        IsWaitingForLobbyState = false;

        var players = GameSession.Instance?.Players;
        if (players == null) return;

        players.Clear();

        foreach (var playerInfo in message.Players)
        {
            var isLocal = playerInfo.ClientSteamId == NetworkManager.Instance?.LocalClientId;
            var player = players.Add(playerInfo.ClientSteamId, playerInfo.PlayerIndex, playerInfo.PlayerName, isLocal);

            if (player == null) continue;

            // Apply MaxLives from message
            if (playerInfo.MaxLives > 0)
            {
                player.MaxLives = playerInfo.MaxLives;
            }

            // Set skin ID and spawn remote player if they have a skin selected
            if (!isLocal && !string.IsNullOrEmpty(playerInfo.SkinId))
            {
                player.SkinId = playerInfo.SkinId;
                players.SpawnPlayerEntity(player);
            }
        }

        OnLobbyStateReceived?.Invoke();
    }
}

/// <summary>
/// Client sends this to host when ready to receive lobby state.
/// </summary>
public class RequestLobbyStateMessage : INetMessage
{
    public void Serialize(BinaryWriter writer) { }
    public void Deserialize(BinaryReader reader) { }
}
