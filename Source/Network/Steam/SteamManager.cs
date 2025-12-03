using System;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Network.Steam;

/// <summary>
/// Core Steam integration. Handles Steam initialization and provides access to Steam APIs.
/// </summary>
public static class SteamManager
{
    public static bool IsInitialized { get; private set; }
    public static CSteamID LocalSteamId { get; private set; }
    public static string LocalPlayerName { get; private set; }

    public static void Initialize()
    {
        try
        {
            if (!SteamAPI.IsSteamRunning())
            {
                UmcLogger.Warn("Steam is not running - online multiplayer unavailable");
                IsInitialized = false;
                return;
            }

            LocalSteamId = SteamUser.GetSteamID();
            LocalPlayerName = SteamFriends.GetPersonaName();

            if (!LocalSteamId.IsValid())
            {
                UmcLogger.Warn("Could not get valid Steam ID - online multiplayer unavailable");
                IsInitialized = false;
                return;
            }

            IsInitialized = true;
            UmcLogger.Info($"Steam initialized - Player: {LocalPlayerName} (ID: {LocalSteamId})");
        }
        catch (Exception ex)
        {
            UmcLogger.Error($"Failed to initialize Steam: {ex.Message}");
            IsInitialized = false;
        }
    }

    public static void Shutdown()
    {
        IsInitialized = false;
        UmcLogger.Info("Steam manager shut down");
    }

    public static string GetPlayerName(CSteamID steamId)
    {
        if (!IsInitialized) return "Unknown";
        return SteamFriends.GetFriendPersonaName(steamId);
    }
}
