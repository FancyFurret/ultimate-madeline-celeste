using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

/// <summary>
/// Visual overlay for the placing phase - darkens the background,
/// shows a placement grid, and provides smooth transitions.
/// Inspired by Ultimate Chicken Horse's placement phase aesthetic.
/// </summary>
public class PlacingOverlay : Entity
{
    private const float FadeInDuration = 0.2f;
    private const float GridSize = 8f;
    private const float OverlayAlpha = 0.55f;

    // Grid colors - subtle cyan/teal aesthetic
    private static readonly Color GridLineColor = new Color(80, 200, 220);
    private static readonly Color GridLineMajorColor = new Color(120, 240, 255);
    private static readonly Color OverlayColor = new Color(10, 15, 25);

    // Blocked area colors
    private static readonly Color BlockedFillColor = new Color(180, 50, 50);
    private static readonly Color BlockedLineColor = new Color(255, 80, 80);

    private float _fadeProgress;
    private float _gridPulse;
    private bool _isActive = true;

    // Cached level bounds for grid rendering
    private Rectangle _levelBounds;
    private Level _level;

    // Props currently being placed (not yet confirmed)
    private Dictionary<UmcPlayer, PropInstance> _placingProps;

    public PlacingOverlay()
    {
        // Render behind most gameplay but above background
        Depth = 9500;
        Tag = Tags.Global | Tags.PauseUpdate;
    }

    /// <summary>
    /// Sets the props currently being placed (for preview rendering).
    /// </summary>
    public void SetPlacingProps(Dictionary<UmcPlayer, PropInstance> placingProps)
    {
        _placingProps = placingProps;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        _level = scene as Level;

        if (_level != null)
        {
            // Get the current room bounds
            var levelData = _level.Session.LevelData;
            _levelBounds = new Rectangle(
                levelData.Bounds.X,
                levelData.Bounds.Y,
                levelData.Bounds.Width,
                levelData.Bounds.Height
            );
        }

        _fadeProgress = 0f;

        // Pause props that need pausing during placing
        SetPropsPaused(true);
    }

    public override void Removed(Scene scene)
    {
        // Unpause props when overlay is removed
        SetPropsPaused(false);
        base.Removed(scene);
    }

    private void SetPropsPaused(bool paused)
    {
        var roundState = RoundState.Current;
        if (roundState == null) return;

        foreach (var placedProp in roundState.PlacedProps)
        {
            if (placedProp.Entity == null) continue;
            if (!placedProp.Prop.Prop.PauseDuringPlacing) continue;

            placedProp.Prop.Prop.SetPaused(placedProp.Entity, paused);
        }
    }

    public override void Update()
    {
        base.Update();

        if (_isActive && _fadeProgress < 1f)
        {
            _fadeProgress = Math.Min(1f, _fadeProgress + Engine.DeltaTime / FadeInDuration);
        }
        else if (!_isActive && _fadeProgress > 0f)
        {
            _fadeProgress = Math.Max(0f, _fadeProgress - Engine.DeltaTime / FadeInDuration);
            if (_fadeProgress <= 0f)
                RemoveSelf();
        }

        // Subtle pulse animation for grid
        _gridPulse += Engine.DeltaTime * 2f;
    }

    /// <summary>
    /// Start fading out the overlay.
    /// </summary>
    public void FadeOut()
    {
        _isActive = false;
    }

    public override void Render()
    {
        base.Render();

        if (_fadeProgress <= 0f || _level == null) return;

        var camera = _level.Camera;
        if (camera == null) return;

        // Get visible area
        float visibleWidth = 320f / _level.Zoom;
        float visibleHeight = 180f / _level.Zoom;
        var visibleArea = new Rectangle(
            (int)camera.Position.X,
            (int)camera.Position.Y,
            (int)visibleWidth + 1,
            (int)visibleHeight + 1
        );

        // Calculate eased fade
        float ease = Ease.SineOut(_fadeProgress);

        // Draw dark overlay
        DrawOverlay(visibleArea, ease);

        // Draw placement grid
        DrawGrid(visibleArea, ease);

        // Draw blocked areas
        DrawBlockedAreas(ease);

        // Draw prop previews (tracks, orbits, etc.)
        DrawPropPreviews(ease);
    }

    private void DrawOverlay(Rectangle visibleArea, float ease)
    {
        // Dark semi-transparent overlay
        Draw.Rect(
            visibleArea.X - 10,
            visibleArea.Y - 10,
            visibleArea.Width + 20,
            visibleArea.Height + 20,
            OverlayColor * (OverlayAlpha * ease)
        );
    }

    private void DrawGrid(Rectangle visibleArea, float ease)
    {
        // Calculate grid line alpha with subtle pulse
        float pulseAmount = 0.15f + (float)Math.Sin(_gridPulse) * 0.03f;
        float gridAlpha = pulseAmount * ease;
        float majorGridAlpha = (pulseAmount + 0.2f) * ease;

        // Snap to grid boundaries
        int startX = (int)(Math.Floor(visibleArea.X / GridSize) * GridSize);
        int startY = (int)(Math.Floor(visibleArea.Y / GridSize) * GridSize);
        int endX = visibleArea.Right + (int)GridSize;
        int endY = visibleArea.Bottom + (int)GridSize;

        // Clamp to level bounds
        startX = Math.Max(startX, _levelBounds.X);
        startY = Math.Max(startY, _levelBounds.Y);
        endX = Math.Min(endX, _levelBounds.Right);
        endY = Math.Min(endY, _levelBounds.Bottom);

        // Draw vertical lines
        for (float x = startX; x <= endX; x += GridSize)
        {
            bool isMajor = (int)x % 16 == 0;
            var color = (isMajor ? GridLineMajorColor : GridLineColor) * (isMajor ? majorGridAlpha : gridAlpha);

            Draw.Line(
                new Vector2(x, startY),
                new Vector2(x, endY),
                color
            );
        }

        // Draw horizontal lines
        for (float y = startY; y <= endY; y += GridSize)
        {
            bool isMajor = (int)y % 16 == 0;
            var color = (isMajor ? GridLineMajorColor : GridLineColor) * (isMajor ? majorGridAlpha : gridAlpha);

            Draw.Line(
                new Vector2(startX, y),
                new Vector2(endX, y),
                color
            );
        }

        DrawLevelBoundsGlow(ease);
    }

    private void DrawLevelBoundsGlow(float ease)
    {
        float glowAlpha = 0.3f * ease;
        float pulseGlow = 0.1f + (float)Math.Sin(_gridPulse * 1.5f) * 0.05f;
        var boundsColor = GridLineMajorColor * (glowAlpha + pulseGlow);

        // Outer glow (larger, more transparent)
        Draw.HollowRect(
            _levelBounds.X - 2,
            _levelBounds.Y - 2,
            _levelBounds.Width + 4,
            _levelBounds.Height + 4,
            boundsColor * 0.2f
        );

        Draw.HollowRect(
            _levelBounds.X - 1,
            _levelBounds.Y - 1,
            _levelBounds.Width + 2,
            _levelBounds.Height + 2,
            boundsColor * 0.4f
        );

        // Inner sharp line
        Draw.HollowRect(
            _levelBounds.X,
            _levelBounds.Y,
            _levelBounds.Width,
            _levelBounds.Height,
            boundsColor
        );
    }

    private void DrawBlockedAreas(float ease)
    {
        if (_level == null) return;

        float fillAlpha = 0.25f * ease;
        float lineAlpha = 0.6f * ease;
        float pulseAmount = (float)Math.Sin(_gridPulse * 2f) * 0.1f;

        foreach (var entity in _level.Tracker.GetEntities<PlacementBlocker>())
        {
            if (entity is not PlacementBlocker blocker) continue;

            var rect = new Rectangle(
                (int)blocker.Position.X,
                (int)blocker.Position.Y,
                (int)blocker.Width,
                (int)blocker.Height
            );

            // Fill with pulsing red
            Draw.Rect(
                rect.X,
                rect.Y,
                rect.Width,
                rect.Height,
                BlockedFillColor * (fillAlpha + pulseAmount)
            );

            // Border
            Draw.HollowRect(rect.X, rect.Y, rect.Width, rect.Height, BlockedLineColor * lineAlpha);
        }
    }

    private void DrawPropPreviews(float ease)
    {
        // Calculate pulse for animations (0.4 to 1.0)
        float pulse = 0.7f + (float)Math.Sin(_gridPulse * 3f) * 0.3f;
        pulse *= ease;

        // Render previews for already-placed props
        var roundState = RoundState.Current;
        if (roundState != null)
        {
            foreach (var placedProp in roundState.PlacedProps)
            {
                if (placedProp.Entity == null) continue;
                if (placedProp.Entity.Scene == null) continue;

                // Let each prop render its own preview
                placedProp.Prop.Prop.RenderPlacingPreview(placedProp.Entity, pulse);
            }
        }

        // Render previews for props currently being placed
        if (_placingProps != null)
        {
            foreach (var kvp in _placingProps)
            {
                var propInstance = kvp.Value;
                if (propInstance.Entity == null) continue;
                if (propInstance.Entity.Scene == null) continue;

                // Let each prop render its own preview
                propInstance.Prop.RenderPlacingPreview(propInstance.Entity, pulse);
            }
        }
    }
}

