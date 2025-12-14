using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Scoring;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

/// <summary>
/// Scoring phase controller - manages the scoring logic and controls the Scoreboard display.
/// </summary>
public class ScoringPhase
{
    public event Action OnComplete;
    public event Action<UmcPlayer> OnVictory;

    private enum Phase
    {
        SlideIn,
        ShowSpecialMessage,
        AnimatingScores,
        PostScoreDelay,
        SlideOut,
        VictoryFocus,
        VictoryText,
        Complete
    }

    private Phase _currentPhase = Phase.SlideIn;
    private float _phaseTimer;

    // Scoreboard display entity
    private Scoreboard _scoreboard;
    private Level _level;

    // Special conditions
    private bool _isTooEasy;
    private bool _isNoWinners;

    // Victory
    private UmcPlayer _winner;

    // Player data
    private List<PlayerScoreRow> _playerRows = new();

    public ScoringPhase(Level level)
    {
        _level = level;

        // Build display from already-calculated scores
        BuildScoreDisplay();

        // Create and configure scoreboard
        _scoreboard = new Scoreboard();
        _scoreboard.SetPlayerData(_playerRows);
        _scoreboard.SetSpecialMessage(_isTooEasy, _isNoWinners);
        _scoreboard.SetMaxSegmentCount(GetMaxNewSegmentCount());

        // Subscribe to scoreboard events
        _scoreboard.OnSlideInComplete += HandleSlideInComplete;
        _scoreboard.OnSpecialMessageComplete += HandleSpecialMessageComplete;
        _scoreboard.OnScoreAnimationComplete += HandleScoreAnimationComplete;
        _scoreboard.OnSlideOutComplete += HandleSlideOutComplete;

        // Add scoreboard to scene
        level.Add(_scoreboard);

        // Start the animation
        _scoreboard.SlideIn();
        _currentPhase = Phase.SlideIn;

        UmcLogger.Info("ScoringPhase started");
    }

    /// <summary>
    /// Builds display data from the already-calculated scores in RoundState.
    /// Scores were calculated by host and synced via PlatformingCompleteMessage.
    /// </summary>
    private void BuildScoreDisplay()
    {
        var session = GameSession.Instance;
        var round = RoundState.Current;
        if (session == null || round == null) return;

        _isTooEasy = round.DidEveryoneFinish() && session.Players.All.Count > 1;
        _isNoWinners = round.DidNoOneFinish();

        foreach (var player in session.Players.All)
        {
            var stats = round.GetPlayerStats(player);
            var row = new PlayerScoreRow
            {
                SlotIndex = player.SlotIndex,
                PlayerName = player.Name,
                SkinId = player.SkinId,
                SkinName = SkinHelper.GetDisplayName(player.SkinId),
                Portrait = SkinPortraitHelper.GetPortrait(player.SkinId),
                IsUnderdog = stats.WasUnderdogThisRound
            };

            // Calculate bar progress based on score BEFORE this round's new segments
            float previousScore = 0f;
            for (int i = 0; i < stats.RoundStartSegmentIndex && i < stats.ScoreSegments.Count; i++)
            {
                previousScore += stats.ScoreSegments[i].Points;
            }
            row.CurrentBarProgress = Math.Max(0, previousScore) * ScoringConfig.BarAreaWidth / ScoringConfig.PointsToWin;
            row.AnimatedBarProgress = row.CurrentBarProgress;

            // Copy all segments and track where new ones start
            row.Segments = new List<ScoreSegment>(stats.ScoreSegments);
            row.RoundStartSegmentIndex = stats.RoundStartSegmentIndex;

            _playerRows.Add(row);
        }

        if (_isTooEasy || _isNoWinners)
        {
            UmcLogger.Info(_isTooEasy ? "Too Easy - no points!" : "No Winners - no points!");
        }

        _winner = round.GetWinner();
    }

    private int GetMaxNewSegmentCount()
    {
        if (_isTooEasy || _isNoWinners || _playerRows.Count == 0)
            return 0;

        return _playerRows.Max(r => r.NewSegmentCount);
    }


    public void Update()
    {
        _phaseTimer += Engine.DeltaTime;

        switch (_currentPhase)
        {
            case Phase.PostScoreDelay:
                UpdatePostScoreDelay();
                break;
            case Phase.VictoryFocus:
                UpdateVictoryFocus();
                break;
            case Phase.VictoryText:
                UpdateVictoryText();
                break;
            case Phase.Complete:
                Complete();
                break;
        }
    }


    private void HandleSlideInComplete()
    {
        _phaseTimer = 0f;

        if (_isTooEasy || _isNoWinners)
        {
            _currentPhase = Phase.ShowSpecialMessage;
            _scoreboard.ShowSpecialMessage();
        }
        else
        {
            _currentPhase = Phase.AnimatingScores;
            _scoreboard.StartScoreAnimation();
        }
    }

    private void HandleSpecialMessageComplete()
    {
        _phaseTimer = 0f;
        _currentPhase = Phase.PostScoreDelay;
    }

    private void HandleScoreAnimationComplete()
    {
        _phaseTimer = 0f;
        _currentPhase = Phase.PostScoreDelay;
    }

    private void UpdatePostScoreDelay()
    {
        if (_phaseTimer >= ScoringConfig.PostScoreDelay)
        {
            _phaseTimer = 0f;

            if (_winner != null)
            {
                _currentPhase = Phase.VictoryFocus;
                _scoreboard.SlideOut();

                // Hide all players except the winner
                HideNonWinners();
            }
            else
            {
                _currentPhase = Phase.SlideOut;
                _scoreboard.SlideOut();
            }
        }
    }

    private void UpdateVictoryFocus()
    {
        // Zoom camera to the goal flag
        var goalFlag = _level.Tracker.GetEntity<GoalFlag>();
        if (goalFlag != null)
        {
            CameraController.Instance?.SetFocusTarget(goalFlag.Position, 1.5f);
        }

        if (_phaseTimer >= ScoringConfig.VictoryFocusDuration)
        {
            _phaseTimer = 0f;
            _currentPhase = Phase.VictoryText;

            // Start showing victory text on scoreboard
            string skinName = SkinHelper.GetDisplayName(_winner.SkinId);
            _scoreboard?.ShowVictoryText(skinName);
        }
    }

    private void UpdateVictoryText()
    {
        float t = _phaseTimer / ScoringConfig.VictoryTextDuration;

        // Only fade in, then stay at full opacity
        float alpha = t < 0.2f ? Ease.CubeOut(t / 0.2f) : 1f;
        _scoreboard?.UpdateVictoryTextAlpha(alpha);

        if (t >= 1f)
        {
            _currentPhase = Phase.Complete;
            Cleanup(); // Must cleanup before OnVictory nulls the reference
            OnVictory?.Invoke(_winner);
        }
    }

    private void HandleSlideOutComplete()
    {
        if (_currentPhase == Phase.SlideOut)
        {
            _currentPhase = Phase.Complete;
        }
    }

    private void Complete()
    {
        UmcLogger.Info("ScoringPhase complete");
        Cleanup();
        OnComplete?.Invoke();
    }

    public void Cleanup()
    {
        // Clear camera focus override and tracked entities
        var camera = CameraController.Instance;
        camera?.ClearFocusTarget();
        camera?.ClearTrackedEntities();

        // Despawn all players (kept alive from platforming phase for victory display)
        PlayerSpawner.Instance?.DespawnAllSessionPlayers();

        if (_scoreboard != null)
        {
            _scoreboard.OnSlideInComplete -= HandleSlideInComplete;
            _scoreboard.OnSpecialMessageComplete -= HandleSpecialMessageComplete;
            _scoreboard.OnScoreAnimationComplete -= HandleScoreAnimationComplete;
            _scoreboard.OnSlideOutComplete -= HandleSlideOutComplete;
            _scoreboard.RemoveSelf();
            _scoreboard = null;
        }
    }

    private void HideNonWinners()
    {
        var session = GameSession.Instance;
        if (session == null || _winner == null) return;

        foreach (var player in session.Players.All)
        {
            if (player != _winner)
            {
                player.IsHidden = true;
            }
        }
    }
}
