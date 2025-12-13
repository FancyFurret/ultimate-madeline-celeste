using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Scoring;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

/// <summary>
/// Visual scoreboard display - handles all rendering and animations.
/// Used by ScoringPhase for the scoring screen.
/// </summary>
public class Scoreboard : Entity
{
    public enum State
    {
        Hidden,
        SlidingIn,
        ShowingSpecialMessage,
        AnimatingScores,
        Idle,
        SlidingOut
    }

    // Textures - loaded from Graphics/Atlases/Gameplay/objects/UMC/scoring/
    private static MTexture _cardTexture;
    private static MTexture _barFillTexture;
    private static MTexture _bannerTexture;

    // State
    private State _state = State.Hidden;
    private float _stateTimer;

    // Card position (in native 320x180 coordinates)
    private float _cardY;
    private float _cardTargetY;
    private float _cardStartY;

    // Score animation (by segment index across all players)
    private int _currentSegmentIndex;
    private int _maxSegmentCount;
    private float _stepTimer;
    private float _labelAlpha;

    // Special message
    private bool _showTooEasy;
    private bool _showNoWinners;
    private float _specialMessageAlpha;

    // Victory text
    private bool _showVictoryText;
    private float _victoryTextAlpha;
    private string _victorySkinName;

    // Player data for display
    private List<PlayerScoreRow> _playerRows = new();

    // Events
    public event Action OnSlideInComplete;
    public event Action OnSpecialMessageComplete;
    public event Action OnScoreAnimationComplete;
    public event Action OnSlideOutComplete;

    // Shorthand
    private static float Scale => ScoringConfig.HudScale;
    private static float NativeW => ScoringConfig.NativeWidth;
    private static float NativeH => ScoringConfig.NativeHeight;

    // Fixed grid height for 4 players
    private const int MaxPlayerRows = 4;

    public bool IsIdle => _state == State.Idle;
    public bool IsHidden => _state == State.Hidden;

    public static void LoadTextures()
    {
        const string path = "objects/UMC/scoring/";

        _cardTexture = GFX.Game.GetOrDefault(path + "card", null);
        _barFillTexture = GFX.Game.GetOrDefault(path + "bar_fill", null);
        _bannerTexture = GFX.Game.GetOrDefault(path + "banner", null);

        UmcLogger.Info("Scoreboard textures loaded");
    }

    public Scoreboard()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
        Depth = -100000;

        _cardStartY = NativeH + 10;
        _cardTargetY = (NativeH - ScoringConfig.CardHeight) / 2f;
        _cardY = _cardStartY;
    }

    /// <summary>
    /// Sets the player data to display.
    /// </summary>
    public void SetPlayerData(List<PlayerScoreRow> rows)
    {
        _playerRows = rows ?? new List<PlayerScoreRow>();
    }

    /// <summary>
    /// Sets the max number of segments to animate.
    /// </summary>
    public void SetMaxSegmentCount(int count)
    {
        _maxSegmentCount = count;
    }

    /// <summary>
    /// Configures special message display.
    /// </summary>
    public void SetSpecialMessage(bool tooEasy, bool noWinners)
    {
        _showTooEasy = tooEasy;
        _showNoWinners = noWinners;
    }

    /// <summary>
    /// Shows the victory text overlay.
    /// </summary>
    public void ShowVictoryText(string skinName)
    {
        _showVictoryText = true;
        _victoryTextAlpha = 0f;
        _victorySkinName = skinName;
    }

    /// <summary>
    /// Updates the victory text alpha for fade in/out.
    /// </summary>
    public void UpdateVictoryTextAlpha(float alpha)
    {
        _victoryTextAlpha = alpha;
    }

    /// <summary>
    /// Hides the victory text.
    /// </summary>
    public void HideVictoryText()
    {
        _showVictoryText = false;
    }

    /// <summary>
    /// Starts the slide-in animation.
    /// </summary>
    public void SlideIn()
    {
        if (_state != State.Hidden) return;
        _state = State.SlidingIn;
        _stateTimer = 0f;
        _cardY = _cardStartY;
    }

    /// <summary>
    /// Shows the special message (Too Easy / No Winners).
    /// </summary>
    public void ShowSpecialMessage()
    {
        _state = State.ShowingSpecialMessage;
        _stateTimer = 0f;
        _specialMessageAlpha = 0f;
    }

    /// <summary>
    /// Starts animating the scores.
    /// </summary>
    public void StartScoreAnimation()
    {
        _state = State.AnimatingScores;
        _currentSegmentIndex = 0;
        _stepTimer = 0f;
        if (_maxSegmentCount > 0)
            StartSegmentAnimation();
    }

    /// <summary>
    /// Sets the scoreboard to idle state.
    /// </summary>
    public void SetIdle()
    {
        _state = State.Idle;
    }

    /// <summary>
    /// Starts the slide-out animation.
    /// </summary>
    public void SlideOut()
    {
        if (_state == State.Hidden || _state == State.SlidingOut) return;
        _state = State.SlidingOut;
        _stateTimer = 0f;
    }

    public override void Update()
    {
        base.Update();

        _stateTimer += Engine.DeltaTime;

        switch (_state)
        {
            case State.SlidingIn:
                UpdateSlidingIn();
                break;
            case State.ShowingSpecialMessage:
                UpdateSpecialMessage();
                break;
            case State.AnimatingScores:
                UpdateScoreAnimation();
                break;
            case State.SlidingOut:
                UpdateSlidingOut();
                break;
        }
    }

    private void UpdateSlidingIn()
    {
        float t = Math.Min(1f, _stateTimer / ScoringConfig.CardSlideInDuration);
        float ease = Ease.CubeOut(t);
        _cardY = MathHelper.Lerp(_cardStartY, _cardTargetY, ease);

        if (t >= 1f)
        {
            _cardY = _cardTargetY;
            _state = State.Idle;
            OnSlideInComplete?.Invoke();
        }
    }

    private void UpdateSpecialMessage()
    {
        float t = _stateTimer / ScoringConfig.SpecialMessageDuration;

        if (t < 0.2f)
            _specialMessageAlpha = Ease.CubeOut(t / 0.2f);
        else if (t > 0.8f)
            _specialMessageAlpha = Ease.CubeIn(1f - (t - 0.8f) / 0.2f);
        else
            _specialMessageAlpha = 1f;

        if (t >= 1f)
        {
            _state = State.Idle;
            OnSpecialMessageComplete?.Invoke();
        }
    }

    private void StartSegmentAnimation()
    {
        if (_currentSegmentIndex >= _maxSegmentCount) return;

        _labelAlpha = 0f;

        // Set target progress for each player based on their segment at this index
        foreach (var row in _playerRows)
        {
            var segment = row.GetNewSegment(_currentSegmentIndex);
            float points = segment?.Points ?? 0f;
            row.TargetBarProgress = row.CurrentBarProgress +
                (points * ScoringConfig.BarAreaWidth / ScoringConfig.PointsToWin);
        }
    }

    private void UpdateScoreAnimation()
    {
        if (_currentSegmentIndex >= _maxSegmentCount)
        {
            _state = State.Idle;
            OnScoreAnimationComplete?.Invoke();
            return;
        }

        float totalStepDuration = ScoringConfig.LabelFadeInDuration +
            ScoringConfig.ScoreBarAnimDuration +
            ScoringConfig.LabelFadeOutDuration +
            ScoringConfig.ScoreTypeDelay;

        float t = _stepTimer / totalStepDuration;

        float labelFadeInEnd = ScoringConfig.LabelFadeInDuration / totalStepDuration;
        float barAnimEnd = (ScoringConfig.LabelFadeInDuration + ScoringConfig.ScoreBarAnimDuration) / totalStepDuration;
        float labelFadeOutEnd = (ScoringConfig.LabelFadeInDuration + ScoringConfig.ScoreBarAnimDuration + ScoringConfig.LabelFadeOutDuration) / totalStepDuration;

        if (t < labelFadeInEnd)
        {
            _labelAlpha = Ease.CubeOut(t / labelFadeInEnd);
        }
        else if (t < barAnimEnd)
        {
            _labelAlpha = 1f;
            float barT = (t - labelFadeInEnd) / (barAnimEnd - labelFadeInEnd);
            float barEase = Ease.CubeOut(barT);

            foreach (var row in _playerRows)
            {
                row.AnimatedBarProgress = MathHelper.Lerp(row.CurrentBarProgress, row.TargetBarProgress, barEase);
            }
        }
        else if (t < labelFadeOutEnd)
        {
            _labelAlpha = Ease.CubeIn(1f - (t - barAnimEnd) / (labelFadeOutEnd - barAnimEnd));

            foreach (var row in _playerRows)
            {
                row.AnimatedBarProgress = row.TargetBarProgress;
                row.CurrentBarProgress = row.TargetBarProgress;
            }
        }
        else
        {
            _labelAlpha = 0f;
        }

        _stepTimer += Engine.DeltaTime;

        if (t >= 1f)
        {
            _currentSegmentIndex++;
            _stepTimer = 0f;

            if (_currentSegmentIndex < _maxSegmentCount)
            {
                StartSegmentAnimation();
            }
            else
            {
                _state = State.Idle;
                OnScoreAnimationComplete?.Invoke();
            }
        }
    }

    private void UpdateSlidingOut()
    {
        float t = Math.Min(1f, _stateTimer / ScoringConfig.CardSlideOutDuration);
        float ease = Ease.CubeIn(t);
        _cardY = MathHelper.Lerp(_cardTargetY, _cardStartY, ease);

        if (t >= 1f)
        {
            _state = State.Hidden;
            OnSlideOutComplete?.Invoke();
        }
    }

    public override void Render()
    {
        base.Render();

        // Victory text overlay (renders even when card is hidden/offscreen)
        if (_showVictoryText && _victoryTextAlpha > 0f)
        {
            RenderVictoryOverlay();
        }

        if (_state == State.Hidden) return;
        if (_cardY >= NativeH) return;

        RenderCard();
    }

    private void RenderVictoryOverlay()
    {
        BeginLinearClamp();

        float centerX = NativeW * Scale / 2f;
        float baseY = NativeH * Scale * 0.65f;
        float lineSpacing = 80f;
        Color textColor = Color.White * _victoryTextAlpha;
        Color outlineColor = Color.Black * _victoryTextAlpha;

        ActiveFont.DrawOutline(
            "You're the",
            new Vector2(centerX, baseY),
            new Vector2(0.5f, 0.5f),
            Vector2.One * 1.2f,
            textColor,
            2f,
            outlineColor
        );

        ActiveFont.DrawOutline(
            "ULTIMATE",
            new Vector2(centerX, baseY + lineSpacing),
            new Vector2(0.5f, 0.5f),
            Vector2.One * 1.8f,
            textColor,
            2f,
            outlineColor
        );

        ActiveFont.DrawOutline(
            _victorySkinName.ToUpperInvariant(),
            new Vector2(centerX, baseY + lineSpacing * 2.3f),
            new Vector2(0.5f, 0.5f),
            Vector2.One * 3.0f,
            textColor,
            2f,
            outlineColor
        );
    }

    /// <summary>
    /// Switches to PointClamp for crisp pixel art textures.
    /// </summary>
    private void BeginPointClamp()
    {
        Draw.SpriteBatch.End();
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.Identity
        );
    }

    /// <summary>
    /// Switches to LinearClamp for smooth HD text.
    /// </summary>
    private void BeginLinearClamp()
    {
        Draw.SpriteBatch.End();
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.Identity
        );
    }

    private Vector2 ToHud(float x, float y) => new Vector2(x * Scale, y * Scale);

    private void RenderCard()
    {
        float cardX = (NativeW - ScoringConfig.CardWidth) / 2f;
        var textColor = ScoringConfig.GridColor;

        // Calculate layout - always use space for MaxPlayerRows
        float barAreaX = cardX + ScoringConfig.PlayerLabelWidth;
        float rowStartY = _cardY + ScoringConfig.CardPadding + 18f;
        float rowSpacing = ScoringConfig.RowHeight;
        float axisY = rowStartY + MaxPlayerRows * rowSpacing + 4f;

        // === TEXTURES (PointClamp for crisp pixel art) ===
        BeginPointClamp();

        // Card background
        if (_cardTexture != null)
        {
            _cardTexture.Draw(ToHud(cardX, _cardY), Vector2.Zero, Color.White, Scale);
        }

        // Grid lines (not text)
        RenderGridLines(barAreaX, rowStartY, axisY);

        // Player bar fills
        for (int i = 0; i < _playerRows.Count; i++)
        {
            var row = _playerRows[i];
            float rowY = rowStartY + i * rowSpacing;
            RenderPlayerBar(rowY, row, barAreaX, cardX);
        }

        // Player portraits with grayscale shader
        RenderPortraitsGrayscale(cardX, rowStartY, rowSpacing);

        // Special message banner texture
        if (_state == State.ShowingSpecialMessage && _bannerTexture != null)
        {
            var bannerPos = ToHud(
                cardX + ScoringConfig.CardWidth / 2f,
                _cardY + ScoringConfig.CardHeight / 2f
            );
            _bannerTexture.DrawCentered(
                bannerPos,
                ScoringConfig.BannerColor * _specialMessageAlpha,
                Scale,
                ScoringConfig.BannerRotation
            );
        }

        // === TEXT (LinearClamp for smooth HD fonts) ===
        BeginLinearClamp();

        // Title centered - "Ultimate Madeline Celeste"
        ActiveFont.Draw(
            "Ultimate Madeline Celeste",
            ToHud(cardX + ScoringConfig.CardWidth / 2f, _cardY + 12f),
            new Vector2(0.5f, 0.5f),
            Vector2.One * 1.0f,
            textColor
        );

        // Round # in top left
        string roundText = RoundState.Current != null ? $"Round {RoundState.Current.RoundNumber}" : "";
        ActiveFont.Draw(
            roundText,
            ToHud(cardX + 8f, _cardY + 12f),
            new Vector2(0f, 0.5f),
            Vector2.One * 0.55f,
            textColor * 0.7f
        );

        // Grid numbers
        RenderGridNumbers(barAreaX, axisY);

        // WIN! text
        float winX = barAreaX + ScoringConfig.BarAreaWidth;
        float gridTop = rowStartY - 2f;
        ActiveFont.Draw(
            "WIN!",
            ToHud(winX, gridTop - 4f),
            new Vector2(0.5f, 1f),
            Vector2.One * 0.65f,
            textColor
        );

        // Player names
        for (int i = 0; i < _playerRows.Count; i++)
        {
            var row = _playerRows[i];
            float rowY = rowStartY + i * rowSpacing;
            RenderPlayerText(cardX, rowY, row, barAreaX);
        }

        // Special message text (rotated to match banner)
        if (_state == State.ShowingSpecialMessage)
        {
            string text = _showTooEasy ? ScoringConfig.TooEasyLabel : ScoringConfig.NoWinnersLabel;
            var bannerPos = ToHud(
                cardX + ScoringConfig.CardWidth / 2f,
                _cardY + ScoringConfig.CardHeight / 2f
            );

            // Apply rotation transform around the banner center
            Draw.SpriteBatch.End();
            Draw.SpriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Matrix.CreateTranslation(-bannerPos.X, -bannerPos.Y, 0f) *
                Matrix.CreateRotationZ(ScoringConfig.SpecialMessageTextRotation) *
                Matrix.CreateTranslation(bannerPos.X, bannerPos.Y, 0f)
            );

            ActiveFont.Draw(
                text,
                bannerPos,
                new Vector2(0.5f, 0.5f),
                Vector2.One * 1.5f,
                Color.White * _specialMessageAlpha
            );

            // Restore normal transform
            BeginLinearClamp();
        }

    }

    private void RenderGridLines(float barAreaX, float rowStartY, float axisY)
    {
        float pointsToWin = ScoringConfig.PointsToWin;
        float barWidth = ScoringConfig.BarAreaWidth;
        float gridTop = rowStartY - 2f;
        float gridBottom = axisY - 2f;
        var lineColor = ScoringConfig.GridColor;

        // Zero line (solid)
        Draw.Line(ToHud(barAreaX, gridTop), ToHud(barAreaX, gridBottom), lineColor * 0.6f, Scale * 1.5f);

        // Dotted lines for 1 to pointsToWin-1
        for (int i = 1; i < (int)pointsToWin; i++)
        {
            float x = barAreaX + (i / pointsToWin) * barWidth;
            float dashLength = 2f;
            float gapLength = 2f;
            float y = gridTop;
            while (y < gridBottom)
            {
                float dashEnd = Math.Min(y + dashLength, gridBottom);
                Draw.Line(ToHud(x, y), ToHud(x, dashEnd), lineColor * 0.4f, Scale);
                y += dashLength + gapLength;
            }
        }

        // WIN line (solid)
        float winX = barAreaX + barWidth;
        Draw.Line(ToHud(winX, gridTop), ToHud(winX, gridBottom), lineColor * 0.8f, Scale * 1.5f);
    }

    private void RenderGridNumbers(float barAreaX, float axisY)
    {
        float pointsToWin = ScoringConfig.PointsToWin;
        float barWidth = ScoringConfig.BarAreaWidth;
        var lineColor = ScoringConfig.GridColor;

        for (int i = 1; i < (int)pointsToWin; i++)
        {
            float x = barAreaX + (i / pointsToWin) * barWidth;
            ActiveFont.Draw(
                i.ToString(),
                ToHud(x, axisY - 1f),
                new Vector2(0.5f, 0f),
                Vector2.One * 0.5f,
                lineColor * 0.8f
            );
        }
    }

    private void RenderPlayerBar(float rowY, PlayerScoreRow row, float barAreaX, float cardX)
    {
        if (_barFillTexture == null) return;

        // Center the bar vertically within the row
        float barY = rowY + (ScoringConfig.RowHeight - ScoringConfig.ScoreBarHeight) / 2f;
        float pixelsPerPoint = ScoringConfig.BarAreaWidth / ScoringConfig.PointsToWin;

        // Calculate how much of the bar to show based on animation progress
        float totalAnimatedPoints = row.AnimatedBarProgress / pixelsPerPoint;
        float drawnPoints = 0f;
        float currentX = barAreaX;

        // Draw each segment from history using the bar texture
        foreach (var segment in row.Segments)
        {
            if (drawnPoints >= totalAnimatedPoints) break;

            float segmentPoints = Math.Min(segment.Points, totalAnimatedPoints - drawnPoints);
            float segmentWidth = segmentPoints * pixelsPerPoint;

            if (segmentWidth > 0.5f)
            {
                var color = segment.Type.GetColor();
                var segmentPos = ToHud(currentX, barY);
                float width = segmentWidth * Scale;
                float height = ScoringConfig.ScoreBarHeight * Scale;
                float borderSize = 1f * Scale;

                // Darken the color slightly for the border (0.85 = 15% darker)
                var borderColor = new Color(
                    (int)(color.R * 0.85f),
                    (int)(color.G * 0.85f),
                    (int)(color.B * 0.85f),
                    color.A
                );

                // Draw fill FIRST (slightly smaller to prevent overflow)
                float texWidthScaled = _barFillTexture.Width * Scale;
                float drawX = segmentPos.X;
                float remainingWidth = width - Scale; // 1 pixel smaller

                // Calculate clip dimensions - use floor and reduce by 1 to avoid overflow
                int clipHeight = (int)Math.Floor(height / Scale);
                clipHeight = Math.Max(1, Math.Min(clipHeight, _barFillTexture.Height));

                while (remainingWidth > 0.5f)
                {
                    float drawWidth = Math.Min(remainingWidth, texWidthScaled);
                    int clipWidth = (int)Math.Floor(drawWidth / Scale);
                    clipWidth = Math.Max(1, Math.Min(clipWidth, _barFillTexture.Width));

                    var clipRect = new Rectangle(0, 0, clipWidth, clipHeight);
                    _barFillTexture.Draw(new Vector2(drawX, segmentPos.Y), Vector2.Zero, color, new Vector2(Scale), 0f, clipRect);

                    drawX += clipWidth * Scale;
                    remainingWidth -= clipWidth * Scale;
                }

                // Draw border ON TOP of fill
                // Top border
                Draw.Rect(segmentPos.X, segmentPos.Y, width, borderSize, borderColor);
                // Bottom border
                Draw.Rect(segmentPos.X, segmentPos.Y + height - borderSize, width, borderSize, borderColor);
                // Left border
                Draw.Rect(segmentPos.X, segmentPos.Y, borderSize, height, borderColor);
                // Right border
                Draw.Rect(segmentPos.X + width - borderSize, segmentPos.Y, borderSize, height, borderColor);
            }

            // Add spacing between segments
            float spacing = 0.25f; // 1/4 native pixel spacing
            currentX += segmentWidth + spacing;
            drawnPoints += segmentPoints;
        }
    }

    private void RenderPortraitsGrayscale(float cardX, float rowStartY, float rowSpacing)
    {
        // Switch to grayscale shader
        Draw.SpriteBatch.End();
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            GrayscaleEffect.Effect,
            Matrix.Identity
        );

        // Draw all portraits
        for (int i = 0; i < _playerRows.Count; i++)
        {
            var row = _playerRows[i];
            if (row.Portrait == null) continue;

            float rowY = rowStartY + i * rowSpacing;
            float portraitScale = 5.0f;
            float portraitX = cardX + 18;
            float portraitY = rowY + ScoringConfig.RowHeight / 2f - 6f;

            row.Portrait.DrawCentered(ToHud(portraitX, portraitY), Color.White, portraitScale);
        }

        // Back to normal rendering
        Draw.SpriteBatch.End();
        Draw.SpriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            RasterizerState.CullNone,
            null,
            Matrix.Identity
        );
    }

    private void RenderPlayerText(float cardX, float rowY, PlayerScoreRow row, float barAreaX)
    {
        var textColor = ScoringConfig.GridColor;

        // Player name (skin name) - right aligned to the 0 line
        float nameX = barAreaX - 4f;
        float nameY = rowY + ScoringConfig.RowHeight / 2f;

        // If underdog, shift name up slightly to make room for "Underdog" text below
        if (row.IsUnderdog)
        {
            nameY -= 2f;
        }

        ActiveFont.Draw(
            TruncateName(row.SkinName, 10),
            ToHud(nameX, nameY),
            new Vector2(1f, 0.5f),
            Vector2.One * 0.6f,
            textColor
        );

        // Underdog text underneath player name
        if (row.IsUnderdog)
        {
            ActiveFont.Draw(
                "Underdog",
                ToHud(nameX, nameY + 5f),
                new Vector2(1f, 0.5f),
                Vector2.One * 0.35f,
                ScoreType.UnderdogBonus.GetColor()
            );
        }

        // Score type label - appears to the right of this player's bar as it animates
        if (_labelAlpha > 0f && _state == State.AnimatingScores)
        {
            var segment = row.GetNewSegment(_currentSegmentIndex);
            if (segment != null)
            {
                string label = segment.Type.GetLabel();
                Color labelColor = segment.Type.GetColor();

                // Position label at the right edge of the animated bar
                float labelX = barAreaX + row.AnimatedBarProgress + 4f;
                ActiveFont.Draw(
                    label,
                    ToHud(labelX, rowY + ScoringConfig.RowHeight / 2f),
                    new Vector2(0f, 0.5f),
                    Vector2.One * 0.8f,
                    labelColor * _labelAlpha
                );
            }
        }
    }


    private static string TruncateName(string name, int maxLength)
    {
        if (string.IsNullOrEmpty(name)) return "";
        if (name.Length <= maxLength) return name;
        return name.Substring(0, maxLength - 1) + "…";
    }

}

/// <summary>
/// Data for a single player row on the scoreboard.
/// </summary>
public class PlayerScoreRow
{
    public int SlotIndex { get; set; }
    public string PlayerName { get; set; }
    public string SkinId { get; set; }
    public string SkinName { get; set; }
    public MTexture Portrait { get; set; }
    public bool IsUnderdog { get; set; }
    public float CurrentBarProgress { get; set; }
    public float TargetBarProgress { get; set; }
    public float AnimatedBarProgress { get; set; }

    // All score segments (historical + this round)
    public List<ScoreSegment> Segments { get; set; } = new();

    // Index where this round's segments start (segments before this are from previous rounds)
    public int RoundStartSegmentIndex { get; set; }

    /// <summary>Gets the number of new segments earned this round.</summary>
    public int NewSegmentCount => Segments.Count - RoundStartSegmentIndex;

    /// <summary>Gets a segment by its round-relative index (0 = first segment this round).</summary>
    public ScoreSegment GetNewSegment(int index)
    {
        int actualIndex = RoundStartSegmentIndex + index;
        return actualIndex < Segments.Count ? Segments[actualIndex] : null;
    }
}
