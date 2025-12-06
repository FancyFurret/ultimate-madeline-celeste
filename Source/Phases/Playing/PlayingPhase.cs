using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

public enum PlayingSubPhase
{
    Picking,
    Placing,
    Platforming,
    Scoring
}

public class PlayingPhase : Entity
{
    public static PlayingPhase Instance { get; private set; }

    public string CurrentLevelSid { get; private set; }

    public PlayingSubPhase SubPhase { get; private set; } = PlayingSubPhase.Picking;

    public RoundState Round => RoundState.Current;
    private bool IsHost => NetworkManager.Instance?.IsHost ?? true;

    private PickingPhase _pickingPhase;
    private PlacingPhase _placingPhase;
    private PlatformingPhase _platformingPhase;
    private ScoringPhase _scoringPhase;

    public PlayingPhase(string levelSid)
    {
        Instance = this;
        CurrentLevelSid = levelSid;
        Tag = Tags.Global | Tags.PauseUpdate;
        Depth = 0;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Instance = this;

        NetworkManager.Handle<ReturnToLobbyMessage>(HandleReturnToLobby);

        RoundState.Start(CurrentLevelSid);
        StartPickingPhase();
        UmcLogger.Info($"PlayingPhase started for level: {CurrentLevelSid}");
    }

    private void StartPickingPhase()
    {
        SubPhase = PlayingSubPhase.Picking;

        var level = Scene as Level;
        var boxPosition = PlayerSpawner.Instance?.SpawnPosition ?? level?.Session.LevelData?.DefaultSpawn ?? Vector2.Zero;
        boxPosition.Y -= 50f;

        _pickingPhase = new PickingPhase(level, boxPosition);
        _pickingPhase.OnComplete += OnPickingComplete;
        UmcLogger.Info("Started picking phase");
    }

    private void OnPickingComplete(Dictionary<UmcPlayer, Prop> playerSelections)
    {
        UmcLogger.Info("Picking complete - transitioning to placing phase");
        _pickingPhase?.Cleanup();
        _pickingPhase = null;

        // Both host and client have the same selections from picking phase
        StartPlacingPhase(playerSelections);
    }

    private void StartPlacingPhase(Dictionary<UmcPlayer, Prop> playerSelections)
    {
        SubPhase = PlayingSubPhase.Placing;
        _placingPhase = new PlacingPhase(Scene as Level, playerSelections);
        _placingPhase.OnComplete += OnPlacingPhaseComplete;
    }

    private void OnPlacingPhaseComplete()
    {
        UmcLogger.Info("Placing complete - transitioning to platforming phase");
        _placingPhase = null;
        StartPlatformingPhase();
    }

    private void StartPlatformingPhase()
    {
        SubPhase = PlayingSubPhase.Platforming;
        _platformingPhase = new PlatformingPhase();
        _platformingPhase.OnComplete += OnPlatformingPhaseComplete;
    }

    private void OnPlatformingPhaseComplete()
    {
        UmcLogger.Info("Platforming complete - transitioning to picking phase");
        CleanupAllPhases();
        StartPickingPhase();
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        CleanupAllPhases();
        if (Instance == this) Instance = null;
        UmcLogger.Info("PlayingPhase ended");
    }

    private void CleanupAllPhases()
    {
        _pickingPhase?.Cleanup();
        _pickingPhase = null;

        _placingPhase = null;

        _platformingPhase?.Cleanup();
        _platformingPhase = null;

        _scoringPhase = null;

        UmcLogger.Info("All sub-phases cleaned up");
    }

    public override void Update()
    {
        base.Update();

        var level = Scene as Level;
        if (level == null || level.Paused) return;

        switch (SubPhase)
        {
            case PlayingSubPhase.Picking:
                _pickingPhase?.Update();
                break;
            case PlayingSubPhase.Placing:
                _placingPhase?.Update();
                break;
            case PlayingSubPhase.Platforming:
                _platformingPhase?.Update();
                break;
        }
    }

    public void ReturnToLobby()
    {
        if (!IsHost)
        {
            UmcLogger.Warn("Only host can return to lobby");
            return;
        }

        NetworkManager.BroadcastWithSelf(new ReturnToLobbyMessage());
    }

    private void HandleReturnToLobby(CSteamID sender, ReturnToLobbyMessage message)
    {
        UmcLogger.Info("Returning to lobby...");
        CleanupAllPhases();
        PlayerSpawner.Instance?.DespawnAllSessionPlayers();
        PhaseManager.Instance?.TransitionToLobby();
    }
}
