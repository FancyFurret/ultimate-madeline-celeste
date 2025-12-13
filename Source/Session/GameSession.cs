using System;
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
    public int[] ExtraLivesBySlot { get; private set; } = new int[8];

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

    public void ConfigureLives(int defaultLives, int[] extraLivesBySlot)
    {
        DefaultLives = Math.Max(1, defaultLives);
        ExtraLivesBySlot = new int[extraLivesBySlot?.Length ?? 0];
        if (extraLivesBySlot != null)
        {
            for (int i = 0; i < extraLivesBySlot.Length; i++)
            {
                ExtraLivesBySlot[i] = Math.Max(0, extraLivesBySlot[i]);
            }
        }
    }

    public void ConfigureLivesFromSettings(UmcSettings settings)
    {
        if (settings == null)
        {
            ConfigureLives(defaultLives: 1, extraLivesBySlot: Array.Empty<int>());
            return;
        }

        var extras = new int[8];
        for (int i = 0; i < extras.Length; i++)
            extras[i] = settings.GetExtraLivesForSlot(i);

        ConfigureLives(settings.DefaultLives, extras);
    }

    public int GetStartingLivesForSlot(int slotIndex)
    {
        int baseLives = Math.Max(1, DefaultLives);
        int extra = 0;
        if (ExtraLivesBySlot != null && slotIndex >= 0 && slotIndex < ExtraLivesBySlot.Length)
            extra = Math.Max(0, ExtraLivesBySlot[slotIndex]);
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
