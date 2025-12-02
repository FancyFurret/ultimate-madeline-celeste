using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Networking;

/// <summary>
/// Manages Steam lobbies for multiplayer matchmaking.
/// Handles lobby creation, joining, leaving, and friend invitations.
/// </summary>
public class SteamLobbyManager
{
    /// <summary>
    /// Singleton instance.
    /// </summary>
    public static SteamLobbyManager Instance { get; private set; }

    /// <summary>
    /// The current lobby we're in, or CSteamID.Nil if not in a lobby.
    /// </summary>
    public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;

    /// <summary>
    /// Whether we are the host of the current lobby.
    /// </summary>
    public bool IsHost { get; private set; }

    /// <summary>
    /// Whether we're currently in a lobby.
    /// </summary>
    public bool InLobby => CurrentLobby.IsValid() && CurrentLobby != CSteamID.Nil;

    /// <summary>
    /// Maximum players allowed in a lobby.
    /// </summary>
    public const int MaxLobbyMembers = 4;

    /// <summary>
    /// Lobby metadata keys.
    /// </summary>
    private const string LobbyDataGameId = "umc_game";
    private const string LobbyDataVersion = "umc_version";
    private const string LobbyDataHostName = "umc_host";
    private const string LobbyDataCurrentLevel = "umc_level";
    private const string MemberDataPlayerCount = "player_count";

    /// <summary>
    /// Characters used for lobby codes - full alphanumeric (62 chars).
    /// </summary>
    private const string CodeChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int CodeLength = 11;

    /// <summary>
    /// The current lobby's join code (encoded from lobby ID).
    /// </summary>
    public string LobbyCode { get; private set; }

    // Steam callbacks
    private Callback<LobbyCreated_t> _lobbyCreatedCallback;
    private Callback<LobbyEnter_t> _lobbyEnteredCallback;
    private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
    private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
    private Callback<LobbyMatchList_t> _lobbyMatchListCallback;

    // Events for the game to subscribe to
    public event Action<CSteamID> OnLobbyCreated;
    public event Action<CSteamID, bool> OnLobbyJoined; // lobbyId, success
    public event Action OnLobbyLeft;
    public event Action<CSteamID, string> OnPlayerJoined; // steamId, playerName
    public event Action<CSteamID, string> OnPlayerLeft; // steamId, playerName
    public event Action<CSteamID> OnLobbyInviteReceived; // lobbyId

    /// <summary>
    /// List of members in the current lobby.
    /// </summary>
    public List<LobbyMember> LobbyMembers { get; } = new();

    public SteamLobbyManager()
    {
        Instance = this;
    }

    /// <summary>
    /// Initializes the lobby manager and registers Steam callbacks.
    /// </summary>
    public void Initialize()
    {
        if (!SteamManager.IsInitialized)
        {
            UmcLogger.Warn("Cannot initialize SteamLobbyManager - Steam not initialized");
            return;
        }

        // Register Steam callbacks
        _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
        _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateCallback);
        _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateCallback);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequestedCallback);
        _lobbyMatchListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyMatchListCallback);

        UmcLogger.Info("SteamLobbyManager initialized");
    }

    /// <summary>
    /// Shuts down the lobby manager and leaves any current lobby.
    /// </summary>
    public void Shutdown()
    {
        if (InLobby)
        {
            LeaveLobby();
        }

        _lobbyCreatedCallback?.Dispose();
        _lobbyEnteredCallback?.Dispose();
        _lobbyChatUpdateCallback?.Dispose();
        _lobbyDataUpdateCallback?.Dispose();
        _gameLobbyJoinRequestedCallback?.Dispose();
        _lobbyMatchListCallback?.Dispose();

        UmcLogger.Info("SteamLobbyManager shut down");
    }

    /// <summary>
    /// Creates a new invisible lobby (only joinable via code).
    /// </summary>
    public void CreateLobby()
    {
        if (!SteamManager.IsInitialized)
        {
            UmcLogger.Error("Cannot create lobby - Steam not initialized");
            return;
        }

        if (InLobby)
        {
            UmcLogger.Warn("Already in a lobby - leaving current lobby first");
            LeaveLobby();
        }

        UmcLogger.Info("Creating Steam lobby...");
        // Use Invisible type - lobby won't appear in any searches, only joinable with direct ID
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeInvisible, MaxLobbyMembers);
    }

    /// <summary>
    /// Joins an existing lobby by its Steam ID.
    /// </summary>
    public void JoinLobby(CSteamID lobbyId)
    {
        if (!SteamManager.IsInitialized)
        {
            UmcLogger.Error("Cannot join lobby - Steam not initialized");
            return;
        }

        if (InLobby)
        {
            UmcLogger.Warn("Already in a lobby - leaving current lobby first");
            LeaveLobby();
        }

        UmcLogger.Info($"Joining lobby {lobbyId}...");
        SteamMatchmaking.JoinLobby(lobbyId);
    }

    /// <summary>
    /// Leaves the current lobby.
    /// </summary>
    public void LeaveLobby()
    {
        if (!InLobby) return;

        UmcLogger.Info($"Leaving lobby {CurrentLobby}");
        SteamMatchmaking.LeaveLobby(CurrentLobby);

        CurrentLobby = CSteamID.Nil;
        IsHost = false;
        LobbyCode = null;
        LobbyMembers.Clear();

        OnLobbyLeft?.Invoke();
    }

    /// <summary>
    /// Invites a friend to the current lobby.
    /// </summary>
    public bool InviteFriend(CSteamID friendId)
    {
        if (!InLobby)
        {
            UmcLogger.Warn("Cannot invite friend - not in a lobby");
            return false;
        }

        var result = SteamMatchmaking.InviteUserToLobby(CurrentLobby, friendId);
        var friendName = SteamManager.GetPlayerName(friendId);

        if (result)
        {
            UmcLogger.Info($"Sent lobby invite to {friendName}");
        }
        else
        {
            UmcLogger.Warn($"Failed to send invite to {friendName}");
        }

        return result;
    }

    /// <summary>
    /// Opens the Steam overlay to invite friends to the current lobby.
    /// </summary>
    public void OpenInviteOverlay()
    {
        if (!InLobby)
        {
            UmcLogger.Warn("Cannot open invite overlay - not in a lobby");
            return;
        }

        SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
        UmcLogger.Info("Opened Steam invite overlay");
    }

    /// <summary>
    /// Gets a list of online friends who could be invited.
    /// </summary>
    public List<FriendInfo> GetOnlineFriends()
    {
        var friends = new List<FriendInfo>();

        if (!SteamManager.IsInitialized) return friends;

        var friendCount = SteamFriends.GetFriendCount(EFriendFlags.k_EFriendFlagImmediate);

        for (var i = 0; i < friendCount; i++)
        {
            var friendId = SteamFriends.GetFriendByIndex(i, EFriendFlags.k_EFriendFlagImmediate);
            var friendState = SteamFriends.GetFriendPersonaState(friendId);

            // Only include friends who are online
            if (friendState != EPersonaState.k_EPersonaStateOffline)
            {
                friends.Add(new FriendInfo
                {
                    SteamId = friendId,
                    Name = SteamFriends.GetFriendPersonaName(friendId),
                    State = friendState
                });
            }
        }

        return friends;
    }

    /// <summary>
    /// Updates lobby data (host only).
    /// </summary>
    public void SetLobbyData(string key, string value)
    {
        if (!InLobby || !IsHost) return;
        SteamMatchmaking.SetLobbyData(CurrentLobby, key, value);
    }

    /// <summary>
    /// Gets lobby data.
    /// </summary>
    public string GetLobbyData(string key)
    {
        if (!InLobby) return null;
        return SteamMatchmaking.GetLobbyData(CurrentLobby, key);
    }

    /// <summary>
    /// Encodes a Steam lobby ID into an 11-character alphanumeric code.
    /// </summary>
    private static string EncodeLobbyId(ulong lobbyId)
    {
        var result = new char[CodeLength];
        var baseNum = (ulong)CodeChars.Length;

        for (var i = CodeLength - 1; i >= 0; i--)
        {
            result[i] = CodeChars[(int)(lobbyId % baseNum)];
            lobbyId /= baseNum;
        }

        return new string(result);
    }

    /// <summary>
    /// Decodes an 11-character alphanumeric code back to a Steam lobby ID.
    /// Returns null if the code is invalid.
    /// </summary>
    private static ulong? DecodeLobbyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeLength)
            return null;

        ulong result = 0;
        var baseNum = (ulong)CodeChars.Length;

        foreach (var c in code)
        {
            var index = CodeChars.IndexOf(c);
            if (index < 0) return null; // Invalid character

            result = result * baseNum + (ulong)index;
        }

        return result;
    }

    /// <summary>
    /// Joins a lobby using the 11-character code (decodes directly to lobby ID).
    /// </summary>
    public void JoinLobbyByCode(string code)
    {
        if (!SteamManager.IsInitialized)
        {
            UmcLogger.Error("Cannot join lobby - Steam not initialized");
            return;
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            UmcLogger.Warn("Invalid lobby code: empty");
            OnJoinByCodeFailed?.Invoke(code);
            return;
        }

        code = code.Trim();

        if (code.Length != CodeLength)
        {
            UmcLogger.Warn($"Invalid lobby code length: {code} ({code.Length} chars, expected {CodeLength})");
            OnJoinByCodeFailed?.Invoke(code);
            return;
        }

        var lobbyId = DecodeLobbyCode(code);
        if (!lobbyId.HasValue)
        {
            UmcLogger.Warn($"Invalid lobby code format: {code}");
            OnJoinByCodeFailed?.Invoke(code);
            return;
        }

        UmcLogger.Info($"Joining lobby from code: {code} -> {lobbyId.Value}");
        JoinLobby(new CSteamID(lobbyId.Value));
    }

    /// <summary>
    /// Updates the local player count that others can see.
    /// </summary>
    public void SetLocalPlayerCount(int count)
    {
        if (!InLobby) return;
        SteamMatchmaking.SetLobbyMemberData(CurrentLobby, MemberDataPlayerCount, count.ToString());
    }

    /// <summary>
    /// Gets a member's player count from lobby member data.
    /// </summary>
    private int GetMemberPlayerCount(CSteamID memberId)
    {
        if (!InLobby) return 1;
        var data = SteamMatchmaking.GetLobbyMemberData(CurrentLobby, memberId, MemberDataPlayerCount);
        return int.TryParse(data, out var count) ? count : 1;
    }

    /// <summary>
    /// Refreshes the list of members in the current lobby.
    /// </summary>
    private void RefreshLobbyMembers()
    {
        LobbyMembers.Clear();

        if (!InLobby) return;

        var memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(CurrentLobby);

        for (var i = 0; i < memberCount; i++)
        {
            var memberId = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
            var memberName = SteamFriends.GetFriendPersonaName(memberId);
            var playerCount = GetMemberPlayerCount(memberId);

            LobbyMembers.Add(new LobbyMember
            {
                SteamId = memberId,
                Name = memberName,
                IsHost = memberId == lobbyOwner,
                IsLocal = memberId == SteamManager.LocalSteamId,
                SlotIndex = i,
                PlayerCount = playerCount
            });
        }

        UmcLogger.Info($"Lobby has {LobbyMembers.Count} members");
    }


    private void OnLobbyCreatedCallback(LobbyCreated_t callback)
    {
        if (callback.m_eResult != EResult.k_EResultOK)
        {
            UmcLogger.Error($"Failed to create lobby: {callback.m_eResult}");
            OnLobbyJoined?.Invoke(CSteamID.Nil, false);
            return;
        }

        CurrentLobby = new CSteamID(callback.m_ulSteamIDLobby);
        IsHost = true;

        // Encode lobby ID into shareable code
        LobbyCode = EncodeLobbyId(CurrentLobby.m_SteamID);

        // Set lobby metadata
        SteamMatchmaking.SetLobbyData(CurrentLobby, LobbyDataGameId, "UltimateMadelineCeleste");
        SteamMatchmaking.SetLobbyData(CurrentLobby, LobbyDataVersion, "1.0.0");
        SteamMatchmaking.SetLobbyData(CurrentLobby, LobbyDataHostName, SteamManager.LocalPlayerName);

        RefreshLobbyMembers();

        UmcLogger.Info($"Created lobby: {CurrentLobby} with code: {LobbyCode}");
        OnLobbyCreated?.Invoke(CurrentLobby);
    }

    private void OnLobbyEnteredCallback(LobbyEnter_t callback)
    {
        var lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        var response = (EChatRoomEnterResponse)callback.m_EChatRoomEnterResponse;

        if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
        {
            UmcLogger.Error($"Failed to join lobby: {response}");
            OnLobbyJoined?.Invoke(lobbyId, false);
            return;
        }

        CurrentLobby = lobbyId;
        IsHost = SteamMatchmaking.GetLobbyOwner(CurrentLobby) == SteamManager.LocalSteamId;

        // Encode lobby ID into shareable code
        LobbyCode = EncodeLobbyId(CurrentLobby.m_SteamID);

        RefreshLobbyMembers();

        UmcLogger.Info($"Joined lobby: {CurrentLobby} (Host: {IsHost}, Code: {LobbyCode})");
        OnLobbyJoined?.Invoke(CurrentLobby, true);
    }

    private void OnLobbyChatUpdateCallback(LobbyChatUpdate_t callback)
    {
        var lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        if (lobbyId != CurrentLobby) return;

        var userId = new CSteamID(callback.m_ulSteamIDUserChanged);
        var userName = SteamManager.GetPlayerName(userId);
        var changeFlags = (EChatMemberStateChange)callback.m_rgfChatMemberStateChange;

        if (changeFlags.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeEntered))
        {
            UmcLogger.Info($"Player joined lobby: {userName}");
            RefreshLobbyMembers();
            OnPlayerJoined?.Invoke(userId, userName);
        }

        if (changeFlags.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeLeft) ||
            changeFlags.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) ||
            changeFlags.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeKicked) ||
            changeFlags.HasFlag(EChatMemberStateChange.k_EChatMemberStateChangeBanned))
        {
            UmcLogger.Info($"Player left lobby: {userName}");
            RefreshLobbyMembers();
            OnPlayerLeft?.Invoke(userId, userName);

            // Check if we need to become the new host
            if (InLobby)
            {
                var newOwner = SteamMatchmaking.GetLobbyOwner(CurrentLobby);
                if (newOwner == SteamManager.LocalSteamId && !IsHost)
                {
                    IsHost = true;
                    UmcLogger.Info("We are now the lobby host");
                }
            }
        }
    }

    private void OnLobbyDataUpdateCallback(LobbyDataUpdate_t callback)
    {
        // Lobby data was updated - could refresh UI here if needed
        var lobbyId = new CSteamID(callback.m_ulSteamIDLobby);
        if (lobbyId == CurrentLobby)
        {
            UmcLogger.Debug("Lobby data updated");
        }
    }

    private void OnGameLobbyJoinRequestedCallback(GameLobbyJoinRequested_t callback)
    {
        var lobbyId = callback.m_steamIDLobby;
        var friendId = callback.m_steamIDFriend;
        var friendName = SteamManager.GetPlayerName(friendId);

        UmcLogger.Info($"Received lobby invite from {friendName}");
        OnLobbyInviteReceived?.Invoke(lobbyId);

        // Auto-join the lobby when receiving an invite
        JoinLobby(lobbyId);
    }

    private void OnLobbyMatchListCallback(LobbyMatchList_t callback)
    {
        UmcLogger.Debug($"Found {callback.m_nLobbiesMatching} lobbies");
    }

    /// <summary>
    /// Event fired when joining by code fails (invalid code or lobby doesn't exist).
    /// </summary>
    public event Action<string> OnJoinByCodeFailed;
}

/// <summary>
/// Represents a member in a Steam lobby.
/// </summary>
public class LobbyMember
{
    public CSteamID SteamId { get; set; }
    public string Name { get; set; }
    public bool IsHost { get; set; }
    public bool IsLocal { get; set; }
    public int SlotIndex { get; set; }
    public int PlayerCount { get; set; } = 1;
}

/// <summary>
/// Represents a Steam friend.
/// </summary>
public class FriendInfo
{
    public CSteamID SteamId { get; set; }
    public string Name { get; set; }
    public EPersonaState State { get; set; }
}

