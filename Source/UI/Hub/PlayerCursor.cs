using System;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Hub;

/// <summary>
/// Cursor entity for character selection.
/// </summary>
public class PlayerCursor : Entity
{
    public const float CursorScale = 0.6f;
    public const float MoveSpeed = 500f;
    public const float FastMoveSpeed = 900f;
    public const float InterpolationSpeed = 15f;
    public const float SnapDistanceThreshold = 200f;

    private static MTexture _cursorTexture;

    public UmcPlayer Player { get; }
    public Color CursorColor { get; set; }
    public bool IsLocal => Player?.IsLocal ?? false;
    public Vector2 ScreenPosition { get; private set; }
    public bool IsActive { get; set; } = true;

    public event Action OnConfirm;
    public event Action OnCancel;
    public event Action<Vector2> OnWorldPositionChanged;

    private float _animTimer;
    private const float BobAmount = 4f;
    private PlayerInput _input;

    // Interpolation for remote cursors
    private Vector2 _targetScreenPosition;
    private bool _hasReceivedPosition;
    private bool _isPaused;

    public PlayerCursor(UmcPlayer player)
    {
        Player = player;
        CursorColor = HubLobbyUi.PlayerColors[player.SlotIndex % HubLobbyUi.PlayerColors.Length];
        _cursorTexture ??= GFX.Gui["umc/cursor"];

        if (player.Device != null)
            _input = new PlayerInput(player.Device);

        // Start cursor at center of screen
        ScreenPosition = new Vector2(CoordinateUtils.HudWidth / 2f, CoordinateUtils.HudHeight / 2f);
        _targetScreenPosition = ScreenPosition;
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
        Depth = -20000;
    }

    /// <summary>
    /// Gets the cursor's current world position.
    /// </summary>
    public Vector2 GetWorldPosition()
    {
        return CoordinateUtils.ScreenToWorld(ScreenPosition);
    }

    /// <summary>
    /// Sets the cursor position from world coordinates.
    /// For remote players, this sets the target for interpolation.
    /// </summary>
    public void SetWorldPosition(Vector2 worldPos)
    {
        var screenPos = CoordinateUtils.WorldToScreen(worldPos);

        if (IsLocal)
        {
            ScreenPosition = screenPos;
            _targetScreenPosition = screenPos;
        }
        else
        {
            // For remote players, set target position for interpolation
            if (!_hasReceivedPosition)
            {
                // Snap to first received position
                ScreenPosition = screenPos;
                _hasReceivedPosition = true;
            }
            _targetScreenPosition = screenPos;
        }
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        _input?.Deregister();
        _input = null;
    }

    public override void Update()
    {
        base.Update();
        if (!IsActive) return;

        var level = SceneAs<Level>();
        _isPaused = level != null && level.Paused;

        _animTimer += Engine.DeltaTime * 3f;

        if (IsLocal)
        {
            if (!_isPaused && _input != null) UpdateInput();
        }
        else
        {
            UpdateInterpolation();
        }
    }

    private void UpdateInterpolation()
    {
        float distanceToTarget = Vector2.Distance(ScreenPosition, _targetScreenPosition);

        if (distanceToTarget > SnapDistanceThreshold)
        {
            // Snap if too far away
            ScreenPosition = _targetScreenPosition;
        }
        else if (distanceToTarget > 0.5f)
        {
            // Smooth exponential interpolation
            float t = 1f - (float)Math.Exp(-InterpolationSpeed * Engine.DeltaTime);
            ScreenPosition = Vector2.Lerp(ScreenPosition, _targetScreenPosition, t);
        }
    }

    private void UpdateInput()
    {
        Vector2 moveDir = _input.Feather.Value;

        if (moveDir != Vector2.Zero)
        {
            if (moveDir.Length() > 1f) moveDir.Normalize();

            // Use fast speed when holding cursor sprint
            float speed = _input.CursorSprint.Check ? FastMoveSpeed : MoveSpeed;

            var oldPos = ScreenPosition;
            ScreenPosition += moveDir * speed * Engine.DeltaTime;
            ScreenPosition = CoordinateUtils.ClampToScreen(ScreenPosition);

            if (Vector2.Distance(oldPos, ScreenPosition) > 1f)
                OnWorldPositionChanged?.Invoke(GetWorldPosition());
        }

        if (_input.MenuConfirm.Pressed) OnConfirm?.Invoke();
        if (_input.MenuCancel.Pressed) OnCancel?.Invoke();
    }

    public override void Render()
    {
        if (!IsActive || _cursorTexture == null) return;
        if (_isPaused) return;

        base.Render();

        float bob = (float)Math.Sin(_animTimer) * BobAmount;
        Vector2 renderPos = ScreenPosition + new Vector2(0, bob);

        _cursorTexture.Draw(renderPos + new Vector2(4, 4), new Vector2(1f, 0f), Color.Black * 0.5f, CursorScale);
        _cursorTexture.Draw(renderPos, new Vector2(1f, 0f), CursorColor, CursorScale);
    }
}

