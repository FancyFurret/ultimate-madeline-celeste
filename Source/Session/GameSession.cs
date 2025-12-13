using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Phases;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;

namespace Celeste.Mod.UltimateMadelineCeleste.Session;

public enum GamePhase
{
    Lobby,
    Playing,
}

/// <summary>
/// Core game session state. Manages session lifecycle and player registry.
/// </summary>
public class GameSession
{
    public static GameSession Instance { get; private set; }
    public static bool Started => Instance != null;

    public PlayersController Players { get; } = new();
    public GamePhase Phase => PhaseManager.Instance.Phase;

    /// <summary>
    /// Host-controlled: default lives each player starts with at the beginning of each round.
    /// </summary>
    public int DefaultLives { get; private set; } = 1;

    /// <summary>
    /// Host-controlled: extra lives per player slot index (0-based).
    /// </summary>
    public Dictionary<int, int> ExtraLivesBySlot { get; private set; } = new();

    private GameSession() { }

    public static void Start()
    {
        if (Instance != null)
        {
            UmcLogger.Error("GameSession already started");
            return;
        }

        Instance = new GameSession();
        UmcLogger.Info("GameSession started");
    }

    public void ConfigureLives(int defaultLives, Dictionary<int, int> extraLivesBySlot)
    {
        DefaultLives = Math.Max(1, defaultLives);
        ExtraLivesBySlot = extraLivesBySlot?
            .Where(kvp => kvp.Key >= 0)
            .ToDictionary(kvp => kvp.Key, kvp => Math.Max(0, kvp.Value))
            ?? new Dictionary<int, int>();
    }

    public void ConfigureLivesFromSettings(UmcSettings settings)
    {
        if (settings == null)
        {
            ConfigureLives(defaultLives: 1, extraLivesBySlot: new Dictionary<int, int>());
            return;
        }

        ConfigureLives(settings.DefaultLives, settings.ExtraLivesBySlot);
    }

    public int GetStartingLivesForSlot(int slotIndex)
    {
        int baseLives = Math.Max(1, DefaultLives);
        int extra = 0;
        if (ExtraLivesBySlot != null && ExtraLivesBySlot.TryGetValue(slotIndex, out var v))
            extra = Math.Max(0, v);
        return baseLives + extra;
    }

    public static void End()
    {
        if (Instance == null) return;

        Instance.Players.Clear();
        Instance = null;
        UmcLogger.Info("GameSession ended");
    }
}
