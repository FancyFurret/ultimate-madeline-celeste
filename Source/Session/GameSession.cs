using System;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;

namespace Celeste.Mod.UltimateMadelineCeleste.Session;

public enum GamePhase
{
    Lobby,
}

/// <summary>
/// Core game session state. Manages session lifecycle and player registry.
/// </summary>
public class GameSession
{
    public static GameSession Instance { get; private set; }
    public static bool Started => Instance != null;

    public PlayersController Players { get; } = new();
    public GamePhase Phase { get; private set; } = GamePhase.Lobby;

    public event Action<GamePhase> OnPhaseChanged;

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

    public void SetPhase(GamePhase phase)
    {
        if (Phase == phase) return;
        Phase = phase;
        UmcLogger.Info($"Game phase: {phase}");
        OnPhaseChanged?.Invoke(phase);
    }
}
