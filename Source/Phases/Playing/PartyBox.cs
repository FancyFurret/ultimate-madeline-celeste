using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Scoring;
using Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

public class PartyBox : Entity
{
    private enum BoxState { Waiting, SlidingIn, Shaking, Opening, Open, Closing, SlidingOut }

    private const float SlideDuration = 0.3f;
    private const float ShakeDuration = .65f;
    private const float OpenDuration = .65f;
    private const float CloseDuration = 0.4f;
    private const float ShakeIntensity = 3f;
    private const float ShakeSpeed = 30f;
    private const float BoxSize = 132f;
    private const float SlotOffset = 27f;
    private const float SlideDistance = 250f;

    // HUD rendering constants
    private static float HudScale => ScoringConfig.HudScale;
    private const float NativeWidth = 320f;
    private const float NativeHeight = 180f;

    private const float PropDisplayScale = 2f / 3f;

    // Shadow settings
    private const float PropShadowOffsetX = 2f;
    private const float PropShadowOffsetY = 2f;
    private const float PropShadowAlpha = 0.5f;

    private MTexture _boxInside;
    private MTexture _lidLeftTop;
    private MTexture _lidRightTop;
    private MTexture _lidLeftBottom;
    private MTexture _lidRightBottom;

    private BoxState _state = BoxState.Waiting;
    private float _stateTimer;
    private float _shakeOffset;
    private float _lidProgress;
    private float _slideOffset;
    private float _zoomScale = 1f;

    private readonly List<Prop> _props;
    private readonly List<PropSlot> _slots = new();

    private VirtualRenderTarget _propRenderTarget;
    private Level _fakeLevel;
    private const int RenderTargetSize = 256;

    public event Action OnOpenComplete;
    public event Action OnCloseStart;
    public event Action OnClosed;
    public bool IsOpen => _state == BoxState.Open;
    public int RemainingPropCount => _slots.Count(s => !s.IsTaken);

    private class PropSlot
    {
        public Prop Prop;
        public PropInstance PropInstance;
        public Vector2 LocalOffset;
        public bool IsTaken;
    }

    private const int BoxDepth = -100000; // In front of gameplay, behind props

    public PartyBox(Vector2 position, List<Prop> props)
    {
        Position = position;
        _props = props ?? new List<Prop>();
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
        Depth = BoxDepth;

        _propRenderTarget = VirtualContent.CreateRenderTarget("partybox-props", RenderTargetSize, RenderTargetSize);

        LoadTextures();
        CreateSlots();
    }

    private void LoadTextures()
    {
        _boxInside = GFX.Game["objects/UMC/partyBox/inside"];
        _lidLeftTop = GFX.Game["objects/UMC/partyBox/lidLeftTop"];
        _lidRightTop = GFX.Game["objects/UMC/partyBox/lidRightTop"];
        _lidLeftBottom = GFX.Game["objects/UMC/partyBox/lidLeftBottom"];
        _lidRightBottom = GFX.Game["objects/UMC/partyBox/lidRightBottom"];
    }

    private void CreateSlots()
    {
        _slots.Clear();
        int count = _props.Count;
        if (count == 0) return;

        var positions = new Vector2[]
        {
            new(-SlotOffset, -SlotOffset),
            new(SlotOffset, -SlotOffset),
            new(0, 0),
            new(-SlotOffset, SlotOffset),
            new(SlotOffset, SlotOffset),
        };

        for (int i = 0; i < count && i < 5; i++)
        {
            _slots.Add(new PropSlot
            {
                Prop = _props[i],
                PropInstance = new PropInstance(_props[i]),
                LocalOffset = positions[i]
            });
        }
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        foreach (var slot in _slots)
            slot.PropInstance?.Despawn();

        _propRenderTarget?.Dispose();
        _propRenderTarget = null;
        _fakeLevel = null;
    }

    private void SpawnProps()
    {
        if (Scene is not Level) return;

        // Create fake level on first spawn with vanilla Celeste session
        // to avoid mod-specific entity behavior (e.g. IntroCar adding roads)
        if (_fakeLevel == null)
        {
            var fakeSession = new global::Celeste.Session(new AreaKey(1, AreaMode.Normal));
            _fakeLevel = new Level { Session = fakeSession };
            _fakeLevel.Camera = new Camera(RenderTargetSize, RenderTargetSize);
        }

        foreach (var slot in _slots)
        {
            if (slot.IsTaken || slot.PropInstance.IsSpawned) continue;

            // Position at render target coordinates (centered at 128, 128)
            // Scale positions UP by 1/PropDisplayScale so after render target is scaled down,
            // props end up at the same visual positions (only smaller in size)
            var center = new Vector2(RenderTargetSize / 2f, RenderTargetSize / 2f);
            var slotCenter = center + slot.LocalOffset / PropDisplayScale;
            var topLeft = slotCenter - slot.Prop.GetCenter();

            slot.PropInstance.Spawn(_fakeLevel, topLeft);

            if (slot.PropInstance.Entity != null)
                slot.PropInstance.Entity.Visible = true;
        }

        _fakeLevel.Entities.UpdateLists();
    }

    public void StartAnimation()
    {
        if (_state != BoxState.Waiting) return;
        _state = BoxState.SlidingIn;
        _stateTimer = 0f;
        _slideOffset = -SlideDistance;
        Audio.Play("event:/game/general/seed_poof", Position);
    }

    public override void Update()
    {
        base.Update();
        _stateTimer += Engine.DeltaTime;

        var level = Scene as Level;
        if (level?.Camera != null)
        {
            float visibleWidth = 320f / level.Zoom;
            float visibleHeight = 180f / level.Zoom;
            Position = level.Camera.Position + new Vector2(visibleWidth / 2f, visibleHeight / 2f);
            _zoomScale = 1f / level.Zoom;
        }

        UpdatePropPositions();

        switch (_state)
        {
            case BoxState.SlidingIn:
                UpdateSlidingIn();
                break;
            case BoxState.Shaking:
                UpdateShaking();
                break;
            case BoxState.Opening:
                UpdateOpening();
                break;
            case BoxState.Closing:
                UpdateClosing();
                break;
            case BoxState.SlidingOut:
                UpdateSlidingOut();
                break;
        }
    }

    private void UpdatePropPositions()
    {
        if (_state == BoxState.Opening || _state == BoxState.Open)
            SpawnProps();

        if (_fakeLevel != null)
        {
            // Temporarily move real level's camera to (0,0) so CullHelper.IsVisible passes
            // (entities like LavaRect check visibility in Update and skip if not visible)
            var level = Scene as Level;
            Vector2? savedCameraPos = null;
            if (level?.Camera != null)
            {
                savedCameraPos = level.Camera.Position;
                level.Camera.Position = Vector2.Zero;
            }

            _fakeLevel.Entities.UpdateLists();
            _fakeLevel.Entities.Update();

            // Restore camera position
            if (savedCameraPos.HasValue && level?.Camera != null)
            {
                level.Camera.Position = savedCameraPos.Value;
            }
        }
    }

    private void UpdateSlidingIn()
    {
        float progress = Math.Min(_stateTimer / SlideDuration, 1f);
        float eased = Ease.CubeOut(progress);
        _slideOffset = -SlideDistance * (1f - eased);

        if (_stateTimer >= SlideDuration)
        {
            _state = BoxState.Shaking;
            _stateTimer = 0f;
            _slideOffset = 0f;
        }
    }

    private void UpdateShaking()
    {
        _shakeOffset = (float)Math.Sin(_stateTimer * ShakeSpeed) * ShakeIntensity;

        if (_stateTimer >= ShakeDuration)
        {
            _state = BoxState.Opening;
            _stateTimer = 0f;
            _shakeOffset = 0f;
            Audio.Play("event:/game/general/thing_in", Position);
        }
    }

    private void UpdateOpening()
    {
        _lidProgress = Math.Min(_stateTimer / OpenDuration, 1f);
        _lidProgress = Ease.CubeOut(_lidProgress);

        if (_stateTimer >= OpenDuration)
        {
            _state = BoxState.Open;
            _lidProgress = 1f;
            OnOpenComplete?.Invoke();
        }
    }

    private void UpdateClosing()
    {
        float progress = Math.Min(_stateTimer / CloseDuration, 1f);
        float eased = Ease.CubeIn(progress);
        _lidProgress = 1f - eased;

        if (_stateTimer >= CloseDuration)
        {
            _state = BoxState.SlidingOut;
            _stateTimer = 0f;
            _lidProgress = 0f;
            Audio.Play("event:/game/general/seed_poof", Position);
        }
    }

    private void UpdateSlidingOut()
    {
        float progress = Math.Min(_stateTimer / SlideDuration, 1f);
        float eased = Ease.CubeIn(progress);
        _slideOffset = SlideDistance * eased;

        if (_stateTimer >= SlideDuration)
        {
            OnClosed?.Invoke();
            RemoveSelf();
        }
    }

    /// <summary>Convert native coordinates to HUD coordinates.</summary>
    private Vector2 ToHud(float x, float y) => new Vector2(x * HudScale, y * HudScale);

    /// <summary>Starts SpriteBatch with PointClamp (does not end existing batch).</summary>
    private void StartPointClamp()
    {
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

    /// <summary>Switches to PointClamp for crisp pixel art textures (ends current batch first).</summary>
    private void SwitchToPointClamp()
    {
        Draw.SpriteBatch.End();
        StartPointClamp();
    }

    public override void Render()
    {
        base.Render();

        // Switch to PointClamp for crisp pixel art
        SwitchToPointClamp();

        // Center of screen in native coordinates, with shake/slide offset
        float nativeX = NativeWidth / 2f;
        float nativeY = NativeHeight / 2f + _shakeOffset + _slideOffset;

        // Convert to HUD coordinates
        Vector2 renderPos = ToHud(nativeX, nativeY);

        DrawBoxInside(renderPos);

        // Render props on top of box inside
        if (_state == BoxState.Open || _state == BoxState.Opening)
        {
            RenderPropsToTarget();
            DrawPropsFromTarget(renderPos);
        }

        // Draw lids on top of everything
        if (_state == BoxState.Waiting || _state == BoxState.SlidingIn || _state == BoxState.Shaking || _state == BoxState.SlidingOut)
            DrawClosedLids(renderPos);
        else if (_state == BoxState.Opening || _state == BoxState.Open)
            DrawOpeningLids(renderPos, _lidProgress);
        else if (_state == BoxState.Closing)
            DrawOpeningLids(renderPos, _lidProgress);

        // DrawDebugPropSizes(renderPos);
    }

    public override void DebugRender(Camera camera)
    {
        base.DebugRender(camera);

        // Debug render in world coordinates for hit-testing visualization
        Vector2 worldCenter = new(
            (float)Math.Round(Position.X),
            (float)Math.Round(Position.Y + (_shakeOffset + _slideOffset) * _zoomScale)
        );

        DrawDebugPropSizes(worldCenter);
    }

    private void DrawDebugPropSizes(Vector2 boxPos)
    {
        foreach (var slot in _slots)
        {
            if (slot.Prop == null) continue;

            // Same as hit testing: positions at original offset, sizes scaled by PropDisplayScale
            var slotCenter = boxPos + slot.LocalOffset * _zoomScale;
            var size = slot.Prop.GetSize() * PropDisplayScale * _zoomScale;
            var topLeft = slotCenter - size / 2f;

            Draw.HollowRect(topLeft.X, topLeft.Y, size.X, size.Y, Color.Lime * 0.8f);

            float crossSize = 4f * _zoomScale;
            Draw.Line(slotCenter - new Vector2(crossSize, 0), slotCenter + new Vector2(crossSize, 0), Color.Red);
            Draw.Line(slotCenter - new Vector2(0, crossSize), slotCenter + new Vector2(0, crossSize), Color.Red);

            ActiveFont.Draw(
                slot.Prop.Name,
                slotCenter + new Vector2(0, -size.Y / 2f - 8f * _zoomScale),
                new Vector2(0.5f, 1f),
                Vector2.One * 0.3f * _zoomScale,
                Color.White
            );
        }
    }

    private void DrawBoxInside(Vector2 pos)
    {
        if (_boxInside != null)
        {
            _boxInside.DrawCentered(pos, Color.White, HudScale);
        }
        else
        {
            float size = BoxSize * HudScale;
            Draw.Rect(pos.X - size / 2, pos.Y - size / 2, size, size, new Color(139, 90, 43));
            float border = 4 * HudScale;
            Draw.Rect(pos.X - size / 2 + border, pos.Y - size / 2 + border, size - border * 2, size - border * 2, new Color(101, 67, 33));
        }
    }

    private void DrawClosedLids(Vector2 pos)
    {
        if (_lidLeftTop == null || _lidRightTop == null) return;

        float halfBox = (BoxSize / 2f) * HudScale;
        _lidLeftTop.Draw(pos + new Vector2(-halfBox, -halfBox), Vector2.Zero, Color.White, HudScale);
        _lidRightTop.Draw(pos + new Vector2(0, -halfBox), Vector2.Zero, Color.White, HudScale);
    }

    private void DrawOpeningLids(Vector2 pos, float progress)
    {
        float halfBox = (BoxSize / 2f) * HudScale;

        if (progress < 0.5f)
        {
            float topProgress = progress * 2f;
            float scaleX = 1f - topProgress;

            if (scaleX > 0.01f && _lidLeftTop != null && _lidRightTop != null)
            {
                _lidLeftTop.Draw(pos + new Vector2(-halfBox, -halfBox), Vector2.Zero, Color.White, new Vector2(scaleX * HudScale, HudScale));
                _lidRightTop.Draw(pos + new Vector2(halfBox, -halfBox), new Vector2(_lidRightTop.Width, 0), Color.White, new Vector2(scaleX * HudScale, HudScale));
            }
        }
        else
        {
            float bottomProgress = (progress - 0.5f) * 2f;
            float scaleX = bottomProgress;
            float inset = 6f * HudScale;
            float upOffset = 3f * HudScale;

            if (scaleX > 0.01f && _lidLeftBottom != null && _lidRightBottom != null)
            {
                _lidLeftBottom.Draw(pos + new Vector2(-halfBox + inset, -halfBox - upOffset), new Vector2(_lidLeftBottom.Width, 0), Color.White, new Vector2(scaleX * HudScale, HudScale));
                _lidRightBottom.Draw(pos + new Vector2(halfBox - inset, -halfBox - upOffset), Vector2.Zero, Color.White, new Vector2(scaleX * HudScale, HudScale));
            }
        }
    }

    private void RenderPropsToTarget()
    {
        if (_propRenderTarget == null || _fakeLevel == null) return;
        var level = Scene as Level;
        if (level == null) return;

        var engine = Engine.Instance;
        var previousRenderTarget = engine.GraphicsDevice.GetRenderTargets();

        // End current HUD SpriteBatch
        Draw.SpriteBatch.End();

        engine.GraphicsDevice.SetRenderTarget(_propRenderTarget);
        engine.GraphicsDevice.Clear(Color.Transparent);

        // Temporarily move REAL level's camera to (0,0) so CullHelper.IsRectangleVisible works
        // (CullHelper uses Engine.Scene.Camera, not the entity's scene camera)
        var savedCameraPos = level.Camera.Position;
        level.Camera.Position = Vector2.Zero;

        // Position fake level's camera for proper matrix calculations
        _fakeLevel.Camera.Position = Vector2.Zero;

        // Start GameplayRenderer so entities like LavaRect that call GameplayRenderer.End/Begin work
        GameplayRenderer.Begin();

        foreach (var entity in _fakeLevel.Entities)
        {
            if (entity.Visible)
                entity.Render();
        }

        GameplayRenderer.End();

        // Restore real camera position
        level.Camera.Position = savedCameraPos;

        if (previousRenderTarget.Length > 0)
            engine.GraphicsDevice.SetRenderTargets(previousRenderTarget);
        else
            engine.GraphicsDevice.SetRenderTarget(null);

        // Resume HUD SpriteBatch with PointClamp
        StartPointClamp();
    }

    private void DrawPropsFromTarget(Vector2 boxPos)
    {
        if (_propRenderTarget == null) return;

        var targetCenter = new Vector2(RenderTargetSize / 2f, RenderTargetSize / 2f);
        float scale = HudScale * PropDisplayScale;
        var shadowOffset = new Vector2(PropShadowOffsetX, PropShadowOffsetY) * scale;

        // Draw shadow (offset, black tint)
        Draw.SpriteBatch.Draw(
            _propRenderTarget,
            boxPos + shadowOffset,
            null,
            Color.Black * PropShadowAlpha,
            0f,
            targetCenter,
            scale,
            SpriteEffects.None,
            0f
        );

        // Draw actual props
        Draw.SpriteBatch.Draw(
            _propRenderTarget,
            boxPos,
            null,
            Color.White,
            0f,
            targetCenter,
            scale,
            SpriteEffects.None,
            0f
        );
    }

    /// <summary>
    /// Gets the slot index and prop at the given world position.
    /// </summary>
    public (int SlotIndex, Prop Prop)? GetSlotAtPosition(Vector2 worldPos)
    {
        if (_state != BoxState.Open) return null;

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.IsTaken || slot.Prop == null) continue;

            // Position is screen center in world coordinates
            // Slot positions are at original offsets (not scaled by PropDisplayScale)
            // Prop sizes are scaled down by PropDisplayScale
            var slotCenter = Position + slot.LocalOffset * _zoomScale;
            var size = slot.Prop.GetSize() * PropDisplayScale * _zoomScale;
            var topLeft = slotCenter - size / 2f;

            if (worldPos.X >= topLeft.X && worldPos.X <= topLeft.X + size.X &&
                worldPos.Y >= topLeft.Y && worldPos.Y <= topLeft.Y + size.Y)
            {
                return (i, slot.Prop);
            }
        }

        return null;
    }

    /// <summary>
    /// Removes the prop at the given slot index.
    /// </summary>
    public bool RemoveSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return false;

        var slot = _slots[slotIndex];
        if (slot.IsTaken) return false;

        slot.IsTaken = true;
        slot.PropInstance?.Despawn();

        UmcLogger.Info($"Removed prop from slot {slotIndex}: {slot.Prop?.Name}");
        return true;
    }

    /// <summary>
    /// Gets the prop at the given slot index.
    /// </summary>
    public Prop GetPropAtSlot(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count) return null;
        return _slots[slotIndex].Prop;
    }

    public void StartCloseAnimation()
    {
        if (_state != BoxState.Open) return;

        // Despawn remaining props before closing
        foreach (var slot in _slots)
            slot.PropInstance?.Despawn();

        _state = BoxState.Closing;
        _stateTimer = 0f;
        OnCloseStart?.Invoke();
        Audio.Play("event:/game/general/thing_in", Position);
    }
}
