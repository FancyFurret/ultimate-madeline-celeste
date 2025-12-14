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

    public static void End()
    {
        if (Instance == null) return;

        Instance.Players.Clear();
        Instance = null;
        UmcLogger.Info("GameSession ended");
    }
}
