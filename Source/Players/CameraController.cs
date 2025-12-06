using System;
using System.Collections.Generic;
using Celeste.Mod.MotionSmoothing.Utilities;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Controls the camera in multiplayer to keep all players visible.
/// </summary>
public class CameraController : HookedFeature<CameraController>
{
    private const int MaxBufferDimension = 10000;
    private const float Padding = 80f;
    private const float DefaultZoom = 0.75f;
    private const float MinZoom = 0.4f;
    private static int BufferSizeMultiplier => (int)Math.Ceiling(1f / MinZoom);
    private const float CameraLerpSpeed = 4f;
    private const float ZoomLerpSpeed = 3f;
    private const float ScreenWidth = 320f;
    private const float ScreenHeight = 180f;

    private float _targetZoom = DefaultZoom;
    private float _currentZoom = DefaultZoom;
    private Vector2 _targetCameraPos;
    private bool _isControlling;
    private readonly Dictionary<VirtualRenderTarget, (int Width, int Height)> _scaledTargets = new();

    // Focus override for level selection zoom
    private Vector2? _focusOverride;
    private float _focusZoom = 1f;

    // External entities to track (weak references so we don't prevent GC)
    private readonly List<WeakReference<Entity>> _trackedEntities = new();

    /// <summary>
    /// Sets a focus override that the camera will zoom to.
    /// </summary>
    public void SetFocusTarget(Vector2 position, float zoom = 1f)
    {
        _focusOverride = position;
        _focusZoom = zoom;
    }

    /// <summary>
    /// Clears the focus override.
    /// </summary>
    public void ClearFocusTarget()
    {
        _focusOverride = null;
        _focusZoom = 1f;
    }

    /// <summary>
    /// Whether a focus override is currently active.
    /// </summary>
    public bool HasFocusOverride => _focusOverride.HasValue;

    /// <summary>
    /// Adds an entity for the camera to track. Uses weak reference so it won't prevent GC.
    /// </summary>
    public void TrackEntity(Entity entity)
    {
        if (entity == null) return;

        // Check if already tracking
        foreach (var weakRef in _trackedEntities)
        {
            if (weakRef.TryGetTarget(out var existing) && existing == entity)
                return;
        }

        _trackedEntities.Add(new WeakReference<Entity>(entity));
    }

    /// <summary>
    /// Stops tracking an entity.
    /// </summary>
    public void UntrackEntity(Entity entity)
    {
        if (entity == null) return;

        _trackedEntities.RemoveAll(weakRef =>
            !weakRef.TryGetTarget(out var target) || target == entity);
    }

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

    private void OnVirtualRenderTargetReload(On.Monocle.VirtualRenderTarget.orig_Reload orig, VirtualRenderTarget self)
    {
        if (_scaledTargets.TryGetValue(self, out var expectedSize))
        {
            if (self.Width != expectedSize.Width || self.Height != expectedSize.Height)
            {
                self.Width = expectedSize.Width;
                self.Height = expectedSize.Height;
            }
        }
        else if (self.Width * BufferSizeMultiplier < MaxBufferDimension && self.Height * BufferSizeMultiplier < MaxBufferDimension)
        {
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

        if (!PlayerSpawner.Instance?.IsMultiplayerLevel ?? true)
        {
            if (_isControlling)
            {
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
        // If focus override is set, zoom to that target
        if (_focusOverride.HasValue)
        {
            _targetZoom = _focusZoom;
            _currentZoom = MathHelper.Lerp(_currentZoom, _targetZoom, ZoomLerpSpeed * Engine.DeltaTime);

            // Calculate camera position to center on focus target
            float visibleWidth = ScreenWidth / _currentZoom;
            float visibleHeight = ScreenHeight / _currentZoom;
            _targetCameraPos = _focusOverride.Value - new Vector2(visibleWidth / 2f, visibleHeight / 2f);

            Vector2 currentCameraPos = level.Camera.Position;
            Vector2 newCameraPos = Vector2.Lerp(currentCameraPos, _targetCameraPos, CameraLerpSpeed * Engine.DeltaTime);

            level.Camera.Position = newCameraPos;
            level.Zoom = _currentZoom;
            _isControlling = true;
            return;
        }

        var positions = GetAllCameraTargets(level);

        if (positions.Count == 0)
        {
            _isControlling = false;
            return;
        }

        var bounds = CalculatePlayerBounds(positions);
        _targetZoom = CalculateRequiredZoom(bounds);
        _currentZoom = MathHelper.Lerp(_currentZoom, _targetZoom, ZoomLerpSpeed * Engine.DeltaTime);
        _targetCameraPos = CalculateCameraPosition(bounds, level);

        Vector2 currentPos = level.Camera.Position;
        Vector2 newPos = _isControlling
            ? Vector2.Lerp(currentPos, _targetCameraPos, CameraLerpSpeed * Engine.DeltaTime)
            : Vector2.Lerp(currentPos, _targetCameraPos, 0.5f);

        level.Camera.Position = newPos;
        level.Zoom = _currentZoom;
        _isControlling = true;
    }

    private List<Vector2> GetAllCameraTargets(Level level)
    {
        var positions = new List<Vector2>();
        var spawner = PlayerSpawner.Instance;

        if (spawner == null) return positions;

        foreach (var kvp in spawner.LocalPlayers)
        {
            var player = kvp.Value;
            if (player?.Scene != null)
                positions.Add(player.Center);
        }

        foreach (var kvp in spawner.RemotePlayers)
        {
            var remote = kvp.Value;
            if (remote?.Scene != null)
                positions.Add(remote.Position);
        }

        // Get positions from tracked entities (clean up dead refs as we go)
        for (int i = _trackedEntities.Count - 1; i >= 0; i--)
        {
            if (_trackedEntities[i].TryGetTarget(out var entity))
            {
                if (entity.Scene != null && entity.Visible && entity.Active)
                    positions.Add(entity.Position);
            }
            else
                _trackedEntities.RemoveAt(i);
        }

        return positions;
    }

    private static RectangleF CalculatePlayerBounds(List<Vector2> positions)
    {
        if (positions.Count == 0)
            return new RectangleF(0, 0, ScreenWidth, ScreenHeight);

        float minX = float.MaxValue, minY = float.MaxValue;
        float maxX = float.MinValue, maxY = float.MinValue;

        foreach (var pos in positions)
        {
            minX = Math.Min(minX, pos.X);
            minY = Math.Min(minY, pos.Y);
            maxX = Math.Max(maxX, pos.X);
            maxY = Math.Max(maxY, pos.Y);
        }

        return new RectangleF(minX - Padding, minY - Padding, maxX - minX + Padding * 2, maxY - minY + Padding * 2);
    }

    private static float CalculateRequiredZoom(RectangleF bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0)
            return DefaultZoom;

        float zoomX = ScreenWidth / bounds.Width;
        float zoomY = ScreenHeight / bounds.Height;
        return Math.Clamp(Math.Min(zoomX, zoomY), MinZoom, DefaultZoom);
    }

    private Vector2 CalculateCameraPosition(RectangleF bounds, Level level)
    {
        float centerX = bounds.X + bounds.Width / 2f;
        float centerY = bounds.Y + bounds.Height / 2f;
        float visibleWidth = ScreenWidth / _currentZoom;
        float visibleHeight = ScreenHeight / _currentZoom;

        float cameraX = centerX - visibleWidth / 2f;
        float cameraY = centerY - visibleHeight / 2f;

        var levelBounds = level.Bounds;
        cameraX = Math.Clamp(cameraX, levelBounds.Left, Math.Max(levelBounds.Left, levelBounds.Right - visibleWidth));
        cameraY = Math.Clamp(cameraY, levelBounds.Top, Math.Max(levelBounds.Top, levelBounds.Bottom - visibleHeight));

        return new Vector2(cameraX, cameraY);
    }

    private struct RectangleF
    {
        public float X, Y, Width, Height;
        public RectangleF(float x, float y, float width, float height) { X = x; Y = y; Width = width; Height = height; }
    }
}
