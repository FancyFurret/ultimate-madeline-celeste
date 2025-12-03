using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Network.Steam;

/// <summary>
/// Manages Steam lobbies for multiplayer matchmaking.
/// </summary>
public class SteamLobby
{
    public static SteamLobby Instance { get; private set; }

    public CSteamID CurrentLobby { get; private set; } = CSteamID.Nil;
    public bool IsHost { get; private set; }
    public bool InLobby => CurrentLobby.IsValid() && CurrentLobby != CSteamID.Nil;
    public string LobbyCode { get; private set; }
    public List<LobbyMember> LobbyMembers { get; } = new();

    public const int MaxLobbyMembers = 4;
    private const string CodeChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
    private const int CodeLength = 11;

    private Callback<LobbyCreated_t> _lobbyCreatedCallback;
    private Callback<LobbyEnter_t> _lobbyEnteredCallback;
    private Callback<LobbyChatUpdate_t> _lobbyChatUpdateCallback;
    private Callback<LobbyDataUpdate_t> _lobbyDataUpdateCallback;
    private Callback<GameLobbyJoinRequested_t> _gameLobbyJoinRequestedCallback;
    private Callback<LobbyMatchList_t> _lobbyMatchListCallback;

    public event Action<CSteamID> OnLobbyCreated;
    public event Action<CSteamID, bool> OnLobbyJoined;
    public event Action OnLobbyLeft;
    public event Action<CSteamID, string> OnPlayerJoined;
    public event Action<CSteamID, string> OnPlayerLeft;
    public event Action<CSteamID> OnLobbyInviteReceived;
    public event Action<string> OnJoinByCodeFailed;

    public SteamLobby()
    {
        Instance = this;
    }

    public void Initialize()
    {
        if (!SteamManager.IsInitialized)
        {
            UmcLogger.Warn("Cannot initialize SteamLobby - Steam not initialized");
            return;
        }

        _lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnLobbyCreatedCallback);
        _lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnLobbyEnteredCallback);
        _lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdateCallback);
        _lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdateCallback);
        _gameLobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequestedCallback);
        _lobbyMatchListCallback = Callback<LobbyMatchList_t>.Create(OnLobbyMatchListCallback);

        UmcLogger.Info("SteamLobby initialized");
    }

    public void Shutdown()
    {
        if (InLobby) LeaveLobby();

        _lobbyCreatedCallback?.Dispose();
        _lobbyEnteredCallback?.Dispose();
        _lobbyChatUpdateCallback?.Dispose();
        _lobbyDataUpdateCallback?.Dispose();
        _gameLobbyJoinRequestedCallback?.Dispose();
        _lobbyMatchListCallback?.Dispose();

        UmcLogger.Info("SteamLobby shut down");
    }

    public void CreateLobby()
    {
        if (!SteamManager.IsInitialized) return;
        if (InLobby) LeaveLobby();

        UmcLogger.Info("Creating Steam lobby...");
        SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeInvisible, MaxLobbyMembers);
    }

    public void JoinLobby(CSteamID lobbyId)
    {
        if (!SteamManager.IsInitialized) return;
        if (InLobby) LeaveLobby();

        UmcLogger.Info($"Joining lobby {lobbyId}...");
        SteamMatchmaking.JoinLobby(lobbyId);
    }

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

    public void JoinLobbyByCode(string code)
    {
        if (!SteamManager.IsInitialized) return;

        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeLength)
        {
            UmcLogger.Warn($"Invalid lobby code: {code}");
            OnJoinByCodeFailed?.Invoke(code);
            return;
        }

        var lobbyId = DecodeLobbyCode(code.Trim());
        if (!lobbyId.HasValue)
        {
            UmcLogger.Warn($"Invalid lobby code format: {code}");
            OnJoinByCodeFailed?.Invoke(code);
            return;
        }

        UmcLogger.Info($"Joining lobby from code: {code}");
        JoinLobby(new CSteamID(lobbyId.Value));
    }

    public void OpenInviteOverlay()
    {
        if (!InLobby) return;
        SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobby);
    }

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

    private static ulong? DecodeLobbyCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code) || code.Length != CodeLength)
            return null;

        ulong result = 0;
        var baseNum = (ulong)CodeChars.Length;

        foreach (var c in code)
        {
            var index = CodeChars.IndexOf(c);
            if (index < 0) return null;
            result = result * baseNum + (ulong)index;
        }

        return result;
    }

    private void RefreshLobbyMembers()
    {
        LobbyMembers.Clear();
        if (!InLobby) return;

        var memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobby);
        var lobbyOwner = SteamMatchmaking.GetLobbyOwner(CurrentLobby);

        for (var i = 0; i < memberCount; i++)
        {
            var memberId = SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobby, i);
            LobbyMembers.Add(new LobbyMember
            {
                SteamId = memberId,
                Name = SteamFriends.GetFriendPersonaName(memberId),
                IsHost = memberId == lobbyOwner,
                IsLocal = memberId == SteamManager.LocalSteamId,
                SlotIndex = i
            });
        }
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
        LobbyCode = EncodeLobbyId(CurrentLobby.m_SteamID);

        SteamMatchmaking.SetLobbyData(CurrentLobby, "umc_game", "UltimateMadelineCeleste");
        SteamMatchmaking.SetLobbyData(CurrentLobby, "umc_host", SteamManager.LocalPlayerName);

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
        LobbyCode = EncodeLobbyId(CurrentLobby.m_SteamID);

        RefreshLobbyMembers();
        UmcLogger.Info($"Joined lobby: {CurrentLobby} (Host: {IsHost}, Code: {LobbyCode})");
        OnLobbyJoined?.Invoke(CurrentLobby, true);
    }

    private void OnLobbyChatUpdateCallback(LobbyChatUpdate_t callback)
    {
        if (new CSteamID(callback.m_ulSteamIDLobby) != CurrentLobby) return;

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

    private void OnLobbyDataUpdateCallback(LobbyDataUpdate_t callback) { }

    private void OnGameLobbyJoinRequestedCallback(GameLobbyJoinRequested_t callback)
    {
        var lobbyId = callback.m_steamIDLobby;
        var friendName = SteamManager.GetPlayerName(callback.m_steamIDFriend);

        UmcLogger.Info($"Received lobby invite from {friendName}");
        OnLobbyInviteReceived?.Invoke(lobbyId);
        JoinLobby(lobbyId);
    }

    private void OnLobbyMatchListCallback(LobbyMatchList_t callback) { }
}

public class LobbyMember
{
    public CSteamID SteamId { get; set; }
    public string Name { get; set; }
    public bool IsHost { get; set; }
    public bool IsLocal { get; set; }
    public int SlotIndex { get; set; }
}
