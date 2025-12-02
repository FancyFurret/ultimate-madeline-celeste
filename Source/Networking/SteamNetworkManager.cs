using System;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Networking;

/// <summary>
/// Low-level Steam P2P networking transport.
/// Handles sending/receiving raw bytes over Steam's P2P system.
/// </summary>
public class SteamNetworkManager
{
    public static SteamNetworkManager Instance { get; private set; }

    private const int ChannelReliable = 0;
    private const int ChannelUnreliable = 1;
    private const int MaxPacketSize = 1200;

    /// <summary>
    /// How often to send player state updates (in seconds).
    /// </summary>
    public const float StateUpdateInterval = 1f / 30f; // 30 Hz

    private Callback<P2PSessionRequest_t> _p2pSessionRequestCallback;
    private Callback<P2PSessionConnectFail_t> _p2pSessionConnectFailCallback;

    private float _timeSinceLastUpdate;

    public event Action<CSteamID, byte[]> OnDataReceived;
    public event Action<CSteamID> OnPeerConnected;
    public event Action<CSteamID> OnPeerDisconnected;

    public SteamNetworkManager()
    {
        Instance = this;
    }

    public void Initialize()
    {
        if (!SteamManager.IsInitialized)
        {
            UmcLogger.Warn("Cannot initialize SteamNetworkManager - Steam not initialized");
            return;
        }

        _p2pSessionRequestCallback = Callback<P2PSessionRequest_t>.Create(OnP2PSessionRequest);
        _p2pSessionConnectFailCallback = Callback<P2PSessionConnectFail_t>.Create(OnP2PSessionConnectFail);

        UmcLogger.Info("SteamNetworkManager initialized");
    }

    public void Shutdown()
    {
        if (SteamLobbyManager.Instance?.InLobby == true)
        {
            foreach (var member in SteamLobbyManager.Instance.LobbyMembers)
            {
                if (!member.IsLocal)
                {
                    SteamNetworking.CloseP2PSessionWithUser(member.SteamId);
                }
            }
        }

        _p2pSessionRequestCallback?.Dispose();
        _p2pSessionConnectFailCallback?.Dispose();

        UmcLogger.Info("SteamNetworkManager shut down");
    }

    public void Update(float deltaTime)
    {
        if (!SteamManager.IsInitialized) return;

        ReceiveMessages();
        _timeSinceLastUpdate += deltaTime;
    }

    public bool ShouldSendStateUpdate()
    {
        if (_timeSinceLastUpdate >= StateUpdateInterval)
        {
            _timeSinceLastUpdate = 0;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Sends data to a specific user.
    /// </summary>
    public void SendTo(CSteamID userId, byte[] data, SendMode mode)
    {
        if (!SteamManager.IsInitialized) return;
        if (data.Length > MaxPacketSize)
        {
            UmcLogger.Warn($"Packet too large: {data.Length} bytes (max: {MaxPacketSize})");
            return;
        }

        var sendType = mode == SendMode.Reliable
            ? EP2PSend.k_EP2PSendReliable
            : EP2PSend.k_EP2PSendUnreliableNoDelay;

        var channel = mode == SendMode.Reliable ? ChannelReliable : ChannelUnreliable;

        var success = SteamNetworking.SendP2PPacket(userId, data, (uint)data.Length, sendType, channel);
        if (!success)
        {
            UmcLogger.Warn($"Failed to send packet to {SteamManager.GetPlayerName(userId)}");
        }
    }

    /// <summary>
    /// Broadcasts data to all lobby members.
    /// </summary>
    public void Broadcast(byte[] data, SendMode mode)
    {
        if (SteamLobbyManager.Instance?.InLobby != true) return;

        foreach (var member in SteamLobbyManager.Instance.LobbyMembers)
        {
            if (!member.IsLocal)
            {
                SendTo(member.SteamId, data, mode);
            }
        }
    }

    private void ReceiveMessages()
    {
        ReceiveMessagesOnChannel(ChannelReliable);
        ReceiveMessagesOnChannel(ChannelUnreliable);
    }

    private void ReceiveMessagesOnChannel(int channel)
    {
        while (SteamNetworking.IsP2PPacketAvailable(out var packetSize, channel))
        {
            var buffer = new byte[packetSize];
            if (SteamNetworking.ReadP2PPacket(buffer, packetSize, out _, out var senderId, channel))
            {
                OnDataReceived?.Invoke(senderId, buffer);
            }
        }
    }

    private void OnP2PSessionRequest(P2PSessionRequest_t callback)
    {
        var requesterId = callback.m_steamIDRemote;

        if (SteamLobbyManager.Instance?.InLobby == true)
        {
            foreach (var member in SteamLobbyManager.Instance.LobbyMembers)
            {
                if (member.SteamId == requesterId)
                {
                    SteamNetworking.AcceptP2PSessionWithUser(requesterId);
                    UmcLogger.Info($"Accepted P2P session with {SteamManager.GetPlayerName(requesterId)}");
                    OnPeerConnected?.Invoke(requesterId);
                    return;
                }
            }
        }

        UmcLogger.Warn($"Rejected P2P session request from {SteamManager.GetPlayerName(requesterId)} - not in lobby");
    }

    private void OnP2PSessionConnectFail(P2PSessionConnectFail_t callback)
    {
        var userId = callback.m_steamIDRemote;
        var error = (EP2PSessionError)callback.m_eP2PSessionError;

        UmcLogger.Warn($"P2P connection failed with {SteamManager.GetPlayerName(userId)}: {error}");
        OnPeerDisconnected?.Invoke(userId);
    }
}
