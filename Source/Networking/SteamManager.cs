using System;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Networking;

/// <summary>
/// Core Steam integration manager. Handles Steam initialization and provides
/// access to Steam APIs for the multiplayer system.
/// </summary>
public static class SteamManager
{
    /// <summary>
    /// Whether Steam is properly initialized and available.
    /// </summary>
    public static bool IsInitialized { get; private set; }

    /// <summary>
    /// The local player's Steam ID.
    /// </summary>
    public static CSteamID LocalSteamId { get; private set; }

    /// <summary>
    /// The local player's Steam persona name.
    /// </summary>
    public static string LocalPlayerName { get; private set; }

    /// <summary>
    /// Initializes the Steam manager. Should be called when the mod loads.
    /// Note: Celeste already initializes Steam, so we just verify it's ready.
    /// </summary>
    public static void Initialize()
    {
        try
        {
            // Celeste should already have Steam initialized
            // We just need to verify and grab our references
            if (!SteamAPI.IsSteamRunning())
            {
                UmcLogger.Warn("Steam is not running - online multiplayer will be unavailable");
                IsInitialized = false;
                return;
            }

            // Try to get the local user's Steam ID
            LocalSteamId = SteamUser.GetSteamID();
            LocalPlayerName = SteamFriends.GetPersonaName();

            if (!LocalSteamId.IsValid())
            {
                UmcLogger.Warn("Could not get valid Steam ID - online multiplayer will be unavailable");
                IsInitialized = false;
                return;
            }

            IsInitialized = true;
            UmcLogger.Info($"Steam initialized successfully - Player: {LocalPlayerName} (ID: {LocalSteamId})");
        }
        catch (Exception ex)
        {
            UmcLogger.Error($"Failed to initialize Steam: {ex.Message}");
            IsInitialized = false;
        }
    }

    /// <summary>
    /// Shuts down the Steam manager.
    /// Note: We don't shut down Steam itself since Celeste owns that.
    /// </summary>
    public static void Shutdown()
    {
        IsInitialized = false;
        UmcLogger.Info("Steam manager shut down");
    }

    /// <summary>
    /// Gets the persona name for a given Steam ID.
    /// </summary>
    public static string GetPlayerName(CSteamID steamId)
    {
        if (!IsInitialized) return "Unknown";
        return SteamFriends.GetFriendPersonaName(steamId);
    }

    /// <summary>
    /// Converts a Steam ID to a string for network identification.
    /// </summary>
    public static string SteamIdToString(CSteamID steamId)
    {
        return steamId.m_SteamID.ToString();
    }

    /// <summary>
    /// Converts a string back to a Steam ID.
    /// </summary>
    public static CSteamID StringToSteamId(string steamIdString)
    {
        if (ulong.TryParse(steamIdString, out var id))
        {
            return new CSteamID(id);
        }
        return CSteamID.Nil;
    }
}

