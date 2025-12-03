using System;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Network.Steam;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Network;

/// <summary>
/// Core network transport and message routing. Pure networking, no game logic.
/// </summary>
public class NetworkManager
{
    public static NetworkManager Instance { get; private set; }

    public bool IsActive => GameSession.Started;
    public bool IsOnline => LobbyController.Instance?.IsOnline == true;
    public bool IsHost => LobbyController.Instance?.IsHost ?? true;

    public MessageRegistry Messages { get; } = new();

    public ulong LocalClientId => SteamManager.IsInitialized
        ? SteamManager.LocalSteamId.m_SteamID
        : 1;

    private SteamLobby _steamLobby;
    private SteamTransport _steamTransport;
    private LobbyController _lobbyController;
    private PlayerStateSync _playerStateSync;

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public NetworkManager()
    {
        Instance = this;
    }

    public void Load()
    {
        SteamManager.Initialize();

        _lobbyController = new LobbyController();
        _playerStateSync = new PlayerStateSync();

        _lobbyController.OnConnected += () => OnConnected?.Invoke();
        _lobbyController.OnDisconnected += () => OnDisconnected?.Invoke();
        _lobbyController.OnError += (err) => OnError?.Invoke(err);

        if (SteamManager.IsInitialized)
        {
            _steamLobby = new SteamLobby();
            _steamTransport = new SteamTransport();

            _steamLobby.Initialize();
            _steamTransport.Initialize();
            _lobbyController.Initialize(_steamLobby);

            Messages.Configure(
                sendToClient: (steamId, data, mode) => _steamTransport.SendTo(steamId, data, mode),
                broadcast: (data, mode) => _steamTransport.Broadcast(data, mode),
                getHostId: () =>
                {
                    var host = _steamLobby?.LobbyMembers.Find(m => m.IsHost);
                    return host != null ? new CSteamID(host.SteamId.m_SteamID) : null;
                }
            );

            _steamTransport.OnDataReceived += (sender, data) => Messages.HandleRawMessage(sender, data);
            _steamTransport.OnPeerDisconnected += HandlePeerDisconnected;

            _playerStateSync.RegisterMessages(Messages);

            UmcLogger.Info("NetworkManager initialized with Steam");
        }
        else
        {
            UmcLogger.Info("NetworkManager initialized (local only)");
        }
    }

    public void Unload()
    {
        _lobbyController?.Shutdown(_steamLobby);
        _steamLobby?.Shutdown();
        _steamTransport?.Shutdown();
        _playerStateSync?.Clear();
        SteamManager.Shutdown();
        Messages.Clear();
    }

    public void Update()
    {
        if (!IsActive) return;

        if (SteamManager.IsInitialized)
        {
            _steamTransport?.Update(Engine.DeltaTime);
        }
    }

    public bool ShouldSendStateUpdate()
    {
        return IsOnline && (_steamTransport?.ShouldSendStateUpdate() ?? false);
    }

    public void StartLocalSession()
    {
        if (GameSession.Started)
        {
            UmcLogger.Warn("Session already active");
            return;
        }

        UmcLogger.Info("Starting local session");
        GameSession.Start();
        GameSession.Instance.Players.RegisterMessages(Messages);
        LobbyController.Instance.RegisterMessages(Messages);
        OnConnected?.Invoke();
    }

    public void HostOnlineGame()
    {
        if (!SteamManager.IsInitialized)
        {
            OnError?.Invoke("Steam is not available");
            StartLocalSession();
            return;
        }
        _lobbyController.HostOnlineGame();
    }

    public void JoinOnlineGame(string code)
    {
        _lobbyController.JoinOnlineGame(code);
    }

    public void InviteFriends()
    {
        _lobbyController.InviteFriends();
    }

    public void LeaveSession()
    {
        if (!IsActive) return;
        UmcLogger.Info("Leaving session...");
        _lobbyController.LeaveLobby();
    }

    private void HandlePeerDisconnected(CSteamID steamId)
    {
        GameSession.Instance?.Players.HandleClientLeft(steamId.m_SteamID);
        Messages.ClearPeerState(steamId);
    }
}
