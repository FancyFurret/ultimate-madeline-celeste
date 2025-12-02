using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.UltimateMadelineCeleste.Core;
using Celeste.Mod.UltimateMadelineCeleste.Multiplayer;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Networking;

public class MultiplayerController
{
    public static MultiplayerController Instance { get; private set; }

    public bool IsActive => UmcSession.Started;
    public bool IsOnline => SteamLobbyManager.Instance?.InLobby == true;
    public bool IsHost => !IsOnline || SteamLobbyManager.Instance?.IsHost == true;

    public NetMessageRegistry Messages { get; } = new();

    public ulong LocalClientId => SteamManager.IsInitialized
        ? SteamManager.LocalSteamId.m_SteamID
        : 1;

    private SteamLobbyManager _lobbyManager;
    private SteamNetworkManager _networkManager;
    
    /// <summary>
    /// Maps animation name to ID for local player(s). Built when player sprite is created.
    /// </summary>
    private readonly Dictionary<int, Dictionary<string, int>> _localAnimationMaps = new();
    
    /// <summary>
    /// Tracks if we've sent graphics for each local player slot.
    /// </summary>
    private readonly HashSet<int> _sentGraphicsFor = new();

    public event Action OnConnected;
    public event Action OnDisconnected;
    public event Action<string> OnError;

    public MultiplayerController()
    {
        Instance = this;
    }

    public void Load()
    {
        SteamManager.Initialize();

        if (SteamManager.IsInitialized)
        {
            _lobbyManager = new SteamLobbyManager();
            _networkManager = new SteamNetworkManager();

            _lobbyManager.Initialize();
            _networkManager.Initialize();

            Messages.Configure(
                sendToClient: (steamId, data, mode) => _networkManager.SendTo(steamId, data, mode),
                broadcast: (data, mode) => _networkManager.Broadcast(data, mode),
                getHostId: () =>
                {
                    var host = _lobbyManager?.LobbyMembers.Find(m => m.IsHost);
                    return host != null ? new CSteamID(host.SteamId.m_SteamID) : null;
                }
            );

            RegisterMessages();

            _lobbyManager.OnLobbyCreated += HandleLobbyCreated;
            _lobbyManager.OnLobbyJoined += HandleLobbyJoined;
            _lobbyManager.OnLobbyLeft += HandleLobbyLeft;
            _lobbyManager.OnPlayerJoined += HandleClientJoined;
            _lobbyManager.OnPlayerLeft += HandleClientLeft;

            _networkManager.OnDataReceived += (sender, data) => Messages.HandleRawMessage(sender, data);
            _networkManager.OnPeerDisconnected += HandlePeerDisconnected;

            UmcLogger.Info("MultiplayerController initialized with Steam");
        }
        else
        {
            UmcLogger.Info("MultiplayerController initialized (local only)");
        }
    }

    private void RegisterMessages()
    {
        // Frame data (per-tick, unreliable) - ID 0
        Messages.Register<ClientPlayersFrameMessage>(0, HandleClientPlayersFrame, checkTimestamp: true);
        
        // Graphics data (sent when player joins/sprite changes) - ID 7
        Messages.Register<PlayerGraphicsMessage>(7, HandlePlayerGraphics);
        
        // Events - ID 6
        Messages.Register<PlayerEventMessage>(6, HandlePlayerEventMessage);
    }

    public void Unload()
    {
        if (_lobbyManager != null)
        {
            _lobbyManager.OnLobbyCreated -= HandleLobbyCreated;
            _lobbyManager.OnLobbyJoined -= HandleLobbyJoined;
            _lobbyManager.OnLobbyLeft -= HandleLobbyLeft;
            _lobbyManager.OnPlayerJoined -= HandleClientJoined;
            _lobbyManager.OnPlayerLeft -= HandleClientLeft;
            _lobbyManager.Shutdown();
        }

        _networkManager?.Shutdown();
        SteamManager.Shutdown();
        Messages.Clear();
    }

    public void Update()
    {
        if (!IsActive) return;

        if (SteamManager.IsInitialized)
        {
            _networkManager?.Update(Engine.DeltaTime);

            if (IsOnline && _networkManager?.ShouldSendStateUpdate() == true)
            {
                BroadcastLocalPlayerStates();
            }
        }
    }

    public void StartLocalSession()
    {
        if (UmcSession.Started)
        {
            UmcLogger.Warn("Session already active");
            return;
        }

        UmcLogger.Info("Starting local session");
        UmcSession.Start();
        UmcSession.Instance.Players.RegisterMessages(Messages);
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

        UmcLogger.Info("Hosting online game...");
        _lobbyManager.CreateLobby();
    }

    public void JoinOnlineGame(string code)
    {
        if (!SteamManager.IsInitialized)
        {
            OnError?.Invoke("Steam is not available");
            return;
        }

        UmcLogger.Info($"Joining online game: {code}");
        SteamLobbyManager.Instance?.JoinLobbyByCode(code);
    }

    public void InviteFriends()
    {
        if (!IsOnline)
        {
            OnError?.Invoke("Not in an online lobby");
            return;
        }
        _lobbyManager.OpenInviteOverlay();
    }

    public void LeaveSession()
    {
        if (!IsActive) return;

        UmcLogger.Info("Leaving session...");

        if (IsOnline)
        {
            _lobbyManager.LeaveLobby();
        }
    }

    public void BroadcastEvent(int slotIndex, PlayerEventType eventType, string data = null)
    {
        if (!IsActive || !IsOnline) return;
        Messages.Broadcast(new PlayerEventMessage
        {
            PlayerIndex = slotIndex,
            EventType = eventType,
            Data = data
        });
    }

    private void BroadcastLocalPlayerStates()
    {
        var players = UmcSession.Instance?.Players;
        var multiplayerManager = MultiMadelineManager.Instance;
        if (players == null || multiplayerManager == null) return;

        var frames = new List<PlayerFrameMessage>();

        foreach (var player in players.Local)
        {
            var playerEntity = multiplayerManager.GetLocalPlayer(player);
            if (playerEntity?.Scene == null || playerEntity.Sprite == null || playerEntity.Hair == null)
                continue;
                
            // Send graphics if we haven't yet
            if (!_sentGraphicsFor.Contains(player.SlotIndex))
            {
                SendPlayerGraphics(player, playerEntity);
                _sentGraphicsFor.Add(player.SlotIndex);
            }
            
            // Get animation ID from our map
            int animId = -1;
            if (_localAnimationMaps.TryGetValue(player.SlotIndex, out var animMap))
            {
                animMap.TryGetValue(playerEntity.Sprite.CurrentAnimationID ?? "idle", out animId);
            }
            
            // Gather hair colors for each segment
            int hairCount = Math.Min(playerEntity.Sprite.HairCount, RemotePlayer.MaxHairLength);
            var hairColors = new Color[hairCount];
            for (int i = 0; i < hairCount; i++)
            {
                hairColors[i] = playerEntity.Hair.GetHairColor(i);
            }
            
            frames.Add(new PlayerFrameMessage
            {
                PlayerIndex = player.SlotIndex,
                Position = playerEntity.Position,
                Scale = playerEntity.Sprite.Scale,
                SpriteColor = playerEntity.Sprite.Color,
                Facing = playerEntity.Facing,
                Speed = playerEntity.Speed,
                CurrentAnimationID = animId,
                CurrentAnimationFrame = playerEntity.Sprite.CurrentAnimationFrame,
                HairColors = hairColors,
                HairSimulateMotion = playerEntity.Hair.SimulateMotion,
                Dead = playerEntity.Dead,
                Dashing = playerEntity.StateMachine.State == Player.StDash,
                DashWasB = playerEntity.GetWasDashB() ?? false,
                DashDir = playerEntity.StateMachine.State == Player.StDash ? playerEntity.DashDir : null
            });
        }

        if (frames.Count > 0)
        {
            Messages.Broadcast(new ClientPlayersFrameMessage { Frames = frames }, SendMode.Unreliable);
        }
    }
    
    /// <summary>
    /// Sends player graphics data (animations, hair setup, etc).
    /// Called once when player joins and when sprite changes.
    /// </summary>
    private void SendPlayerGraphics(UmcPlayer umcPlayer, Player playerEntity)
    {
        if (playerEntity?.Sprite == null || playerEntity.Hair == null) return;
        
        // Build animation map
        var animMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var animations = new string[playerEntity.Sprite.Animations.Count];
        int idx = 0;
        foreach (var animKey in playerEntity.Sprite.Animations.Keys)
        {
            animMap[animKey] = idx;
            animations[idx] = animKey;
            idx++;
        }
        _localAnimationMaps[umcPlayer.SlotIndex] = animMap;
        
        // Hair scales
        int hairCount = Math.Min(playerEntity.Sprite.HairCount, RemotePlayer.MaxHairLength);
        var hairScales = new Vector2[hairCount];
        for (int i = 0; i < hairCount; i++)
        {
            hairScales[i] = playerEntity.Hair.GetHairScale(i) * 
                new Vector2((i == 0 ? (int)playerEntity.Hair.Facing : 1) / Math.Abs(playerEntity.Sprite.Scale.X), 1);
        }
        
        var graphics = new PlayerGraphicsMessage
        {
            PlayerIndex = umcPlayer.SlotIndex,
            Depth = playerEntity.Depth,
            SpriteMode = playerEntity.Sprite.Mode,
            SpriteRate = playerEntity.Sprite.Rate,
            Animations = animations,
            HairCount = (byte)hairCount,
            HairScales = hairScales
        };
        
        Messages.Broadcast(graphics, SendMode.Reliable);
        UmcLogger.Debug($"Sent graphics for player {umcPlayer.SlotIndex}: {animations.Length} animations, {hairCount} hair");
    }
    
    /// <summary>
    /// Forces re-sending graphics for a player (call when sprite mode changes).
    /// </summary>
    public void InvalidatePlayerGraphics(int slotIndex)
    {
        _sentGraphicsFor.Remove(slotIndex);
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
        UmcLogger.Info("Left lobby - switching to local session");
        
        // Clear graphics state
        _sentGraphicsFor.Clear();
        _localAnimationMaps.Clear();
        
        // Remove remote players but keep local ones
        UmcSession.Instance?.Players.ClearRemotePlayers();
        
        OnDisconnected?.Invoke();
    }

    private void HandleClientJoined(CSteamID steamId, string clientName)
    {
        UmcLogger.Info($"Client joined: {clientName} ({steamId})");

        if (IsHost)
        {
            UmcSession.Instance?.Players.SendLobbyStateTo(steamId);
            UmcLogger.Info($"Sent lobby state to {clientName}");
        }
    }

    private void HandleClientLeft(CSteamID steamId, string clientName)
    {
        UmcLogger.Info($"Client left: {clientName} ({steamId})");
        UmcSession.Instance?.Players.HandleClientLeft(steamId.m_SteamID);
        Messages.ClearPeerState(steamId);
    }

    private void HandlePeerDisconnected(CSteamID steamId)
    {
        HandleClientLeft(steamId, SteamManager.GetPlayerName(steamId));
    }

    private void HandleClientPlayersFrame(CSteamID senderId, ClientPlayersFrameMessage message)
    {
        var players = UmcSession.Instance?.Players;
        var multiplayerManager = MultiMadelineManager.Instance;
        if (players == null) return;

        foreach (var frame in message.Frames)
        {
            var player = players.Get(senderId.m_SteamID, frame.PlayerIndex);
            if (player == null) continue;
            
            // Update cached network state
            player.LastNetworkState = new RemotePlayerNetworkState
            {
                Position = frame.Position,
                Facing = frame.Facing,
                Scale = frame.Scale,
                Speed = frame.Speed,
                AnimationFrame = frame.CurrentAnimationFrame,
                Dead = frame.Dead
            };

            // Apply to remote player entity
            var remotePlayer = multiplayerManager?.GetRemotePlayer(player);
            remotePlayer?.ApplyFrame(frame);
        }
    }
    
    private void HandlePlayerGraphics(CSteamID senderId, PlayerGraphicsMessage message)
    {
        var players = UmcSession.Instance?.Players;
        var multiplayerManager = MultiMadelineManager.Instance;
        if (players == null) return;
        
        var player = players.Get(senderId.m_SteamID, message.PlayerIndex);
        if (player == null) return;
        
        var remotePlayer = multiplayerManager?.GetRemotePlayer(player);
        remotePlayer?.UpdateGraphics(message);
        
        UmcLogger.Debug($"Received graphics for player {message.PlayerIndex} from {senderId}: {message.Animations?.Length ?? 0} animations");
    }

    private void HandlePlayerEventMessage(CSteamID senderId, PlayerEventMessage message)
    {
        UmcLogger.Debug($"Player event: {message.EventType} for slot {message.PlayerIndex}");
    }
}
