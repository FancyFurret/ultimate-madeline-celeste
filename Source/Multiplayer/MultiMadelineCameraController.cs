using System;
using System.Collections.Generic;
using Celeste.Mod.MotionSmoothing.Utilities;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Multiplayer;

/// <summary>
/// Controls the camera in multiplayer mode to keep all players visible.
/// Calculates zoom ourselves and sets Level.Zoom - ExCameraDynamics allows zoom-out.
/// Also hooks GameplayBuffers to support zoomed-out rendering.
/// </summary>
public class MultiMadelineCameraController : HookedFeature<MultiMadelineCameraController>
{
    /// <summary>
    /// Maximum buffer dimension - don't scale anything already larger than this.
    /// </summary>
    private const int MaxBufferDimension = 10000;

    /// <summary>
    /// Padding in pixels around players that should remain visible.
    /// </summary>
    private const float Padding = 80f;

    /// <summary>
    /// Default game zoom level.
    /// </summary>
    private const float DefaultZoom = 0.75f;

    /// <summary>
    /// Minimum zoom level (maximum zoom out).
    /// </summary>
    private const float MinZoom = 0.4f;

    /// <summary>
    /// Buffer size multiplier calculated from MinZoom.
    /// At MinZoom, we need (1/MinZoom) times the base 320x180 resolution.
    /// </summary>
    private static int BufferSizeMultiplier => (int)Math.Ceiling(1f / MinZoom);

    /// <summary>
    /// How quickly the camera moves to its target position.
    /// </summary>
    private const float CameraLerpSpeed = 4f;

    /// <summary>
    /// How quickly the zoom changes.
    /// </summary>
    private const float ZoomLerpSpeed = 3f;

    /// <summary>
    /// Screen dimensions at default zoom.
    /// </summary>
    private const float ScreenWidth = 320f;
    private const float ScreenHeight = 180f;

    /// <summary>
    /// Current target zoom level.
    /// </summary>
    private float _targetZoom = DefaultZoom;

    /// <summary>
    /// Current smoothed zoom level.
    /// </summary>
    private float _currentZoom = DefaultZoom;

    /// <summary>
    /// Current target camera position.
    /// </summary>
    private Vector2 _targetCameraPos;

    /// <summary>
    /// Whether the camera is actively being controlled.
    /// </summary>
    private bool _isControlling;

    /// <summary>
    /// Tracks scaled render targets and their expected dimensions.
    /// </summary>
    private readonly Dictionary<VirtualRenderTarget, (int Width, int Height)> _scaledTargets = new();

    protected override void Hook()
    {
        base.Hook();

        On.Monocle.VirtualRenderTarget.Reload += OnVirtualRenderTargetReload;
        On.Celeste.Level.Update += OnLevelUpdate;
    }

    protected override void Unhook()
    {
        On.Monocle.VirtualRenderTarget.Reload -= OnVirtualRenderTargetReload;
        On.Celeste.Level.Update -= OnLevelUpdate;

        _scaledTargets.Clear();

        base.Unhook();
    }

    /// <summary>
    /// Hook to fix buffer sizes before Reload is called.
    /// Scales new targets and restores dimensions if another mod reset them.
    /// </summary>
    private void OnVirtualRenderTargetReload(On.Monocle.VirtualRenderTarget.orig_Reload orig, VirtualRenderTarget self)
    {
        if (_scaledTargets.TryGetValue(self, out var expectedSize))
        {
            // Already scaled - check if dimensions were reset and restore if needed
            if (self.Width != expectedSize.Width || self.Height != expectedSize.Height)
            {
                self.Width = expectedSize.Width;
                self.Height = expectedSize.Height;
            }
        }
        else if (self.Width * BufferSizeMultiplier < MaxBufferDimension && self.Height * BufferSizeMultiplier < MaxBufferDimension)
        {
            // New target under max size - scale it
            int scaledWidth = self.Width * BufferSizeMultiplier;
            int scaledHeight = self.Height * BufferSizeMultiplier;

            self.Width = scaledWidth;
            self.Height = scaledHeight;

            _scaledTargets[self] = (scaledWidth, scaledHeight);
        }

        orig(self);
    }

    private void OnLevelUpdate(On.Celeste.Level.orig_Update orig, Level self)
    {
        orig(self);

        // Only control camera in multiplayer levels
        if (!MultiMadelineManager.Instance?.IsMultiplayerLevel ?? true)
        {
            if (_isControlling)
            {
                // Reset zoom when leaving multiplayer
                self.Zoom = DefaultZoom;
                _currentZoom = DefaultZoom;
                _isControlling = false;
            }
            return;
        }

        UpdateCamera(self);
    }

    private void UpdateCamera(Level level)
    {
        var positions = GetAllPlayerPositions();

        // Need at least one player to control camera
        if (positions.Count == 0)
        {
            _isControlling = false;
            return;
        }

        // Calculate bounds encompassing all players
        var bounds = CalculatePlayerBounds(positions);

        // Calculate target zoom to fit all players
        _targetZoom = CalculateRequiredZoom(bounds);

        // Smoothly interpolate zoom
        _currentZoom = MathHelper.Lerp(_currentZoom, _targetZoom, ZoomLerpSpeed * Engine.DeltaTime);

        // Calculate target camera position
        _targetCameraPos = CalculateCameraPosition(bounds, level);

        // Apply camera position smoothly
        Vector2 currentPos = level.Camera.Position;
        Vector2 newPos;

        if (!_isControlling)
        {
            // First frame - lerp faster
            newPos = Vector2.Lerp(currentPos, _targetCameraPos, 0.5f);
        }
        else
        {
            newPos = Vector2.Lerp(currentPos, _targetCameraPos, CameraLerpSpeed * Engine.DeltaTime);
        }

        level.Camera.Position = newPos;

        // Apply zoom - ExCameraDynamics patches Level.Zoom to allow < 1.0
        level.Zoom = _currentZoom;

        _isControlling = true;
    }

    /// <summary>
    /// Collects positions of all active players (local and remote).
    /// </summary>
    private List<Vector2> GetAllPlayerPositions()
    {
        var positions = new List<Vector2>();
        var manager = MultiMadelineManager.Instance;

        if (manager == null) return positions;

        // Add local players
        foreach (var kvp in manager.LocalPlayers)
        {
            var player = kvp.Value;
            if (player != null && player.Scene != null)
            {
                positions.Add(player.Center);
            }
        }

        // Add remote players
        foreach (var kvp in manager.RemotePlayers)
        {
            var remote = kvp.Value;
            if (remote != null && remote.Scene != null)
            {
                positions.Add(remote.Position);
            }
        }

        return positions;
    }

    /// <summary>
    /// Calculates a bounding rectangle around all player positions with padding.
    /// </summary>
    private static RectangleF CalculatePlayerBounds(List<Vector2> positions)
    {
        if (positions.Count == 0)
        {
            return new RectangleF(0, 0, ScreenWidth, ScreenHeight);
        }

        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;

        foreach (var pos in positions)
        {
            minX = Math.Min(minX, pos.X);
            minY = Math.Min(minY, pos.Y);
            maxX = Math.Max(maxX, pos.X);
            maxY = Math.Max(maxY, pos.Y);
        }

        // Add padding
        minX -= Padding;
        minY -= Padding;
        maxX += Padding;
        maxY += Padding;

        return new RectangleF(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>
    /// Calculates the zoom level needed to fit all players on screen.
    /// </summary>
    private static float CalculateRequiredZoom(RectangleF bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return DefaultZoom;
        }

        // Calculate the zoom needed to fit the bounds on screen
        float zoomX = ScreenWidth / bounds.Width;
        float zoomY = ScreenHeight / bounds.Height;

        // Use the smaller zoom to ensure both dimensions fit
        float requiredZoom = Math.Min(zoomX, zoomY);

        // Clamp to valid range
        return Math.Clamp(requiredZoom, MinZoom, DefaultZoom);
    }

    /// <summary>
    /// Calculates the camera position to center on all players.
    /// </summary>
    private Vector2 CalculateCameraPosition(RectangleF bounds, Level level)
    {
        // Calculate center of player bounds
        float centerX = bounds.X + bounds.Width / 2f;
        float centerY = bounds.Y + bounds.Height / 2f;

        // Calculate visible screen size at current zoom
        float visibleWidth = ScreenWidth / _currentZoom;
        float visibleHeight = ScreenHeight / _currentZoom;

        // Camera position is top-left, offset from center
        float cameraX = centerX - visibleWidth / 2f;
        float cameraY = centerY - visibleHeight / 2f;

        // Clamp to level bounds
        var levelBounds = level.Bounds;
        cameraX = Math.Clamp(cameraX, levelBounds.Left, Math.Max(levelBounds.Left, levelBounds.Right - visibleWidth));
        cameraY = Math.Clamp(cameraY, levelBounds.Top, Math.Max(levelBounds.Top, levelBounds.Bottom - visibleHeight));

        return new Vector2(cameraX, cameraY);
    }

    /// <summary>
    /// Simple float rectangle for bounds calculation.
    /// </summary>
    private struct RectangleF
    {
        public float X, Y, Width, Height;

        public RectangleF(float x, float y, float width, float height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }
    }
}
