using System;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Hub;

/// <summary>
/// Cursor entity for character selection.
/// </summary>
public class PlayerCursor : Entity
{
    public const float CursorScale = 0.6f;
    public const float MoveSpeed = 600f;

    private static MTexture _cursorTexture;

    public UmcPlayer Player { get; }
    public Color CursorColor { get; set; }
    public bool IsLocal => Player?.IsLocal ?? false;
    public Vector2 ScreenPosition { get; set; }
    public bool IsActive { get; set; } = true;

    public event Action OnConfirm;
    public event Action OnCancel;
    public event Action<Vector2> OnPositionChanged;

    private float _animTimer;
    private const float BobAmount = 4f;
    private PlayerInput _input;

    public PlayerCursor(UmcPlayer player)
    {
        Player = player;
        CursorColor = HubLobbyUi.PlayerColors[player.SlotIndex % HubLobbyUi.PlayerColors.Length];
        _cursorTexture ??= GFX.Gui["umc/cursor"];

        if (player.Device != null)
            _input = new PlayerInput(player.Device);

        ScreenPosition = new Vector2(960f, 540f);
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
        Depth = -20000;
    }

    public void SetPosition(Vector2 screenPos) => ScreenPosition = screenPos;

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

        _animTimer += Engine.DeltaTime * 3f;

        if (IsLocal && _input != null)
            UpdateInput();
    }

    private void UpdateInput()
    {
        Vector2 moveDir = _input.Feather.Value;

        if (moveDir != Vector2.Zero)
        {
            if (moveDir.Length() > 1f) moveDir.Normalize();

            var oldPos = ScreenPosition;
            ScreenPosition += moveDir * MoveSpeed * Engine.DeltaTime;
            ScreenPosition = new Vector2(
                Math.Clamp(ScreenPosition.X, 20f, 1900f),
                Math.Clamp(ScreenPosition.Y, 20f, 1060f)
            );

            if (Vector2.Distance(oldPos, ScreenPosition) > 1f)
                OnPositionChanged?.Invoke(ScreenPosition);
        }

        if (_input.MenuConfirm.Pressed) OnConfirm?.Invoke();
        if (_input.MenuCancel.Pressed) OnCancel?.Invoke();
    }

    public override void Render()
    {
        if (!IsActive || _cursorTexture == null) return;

        base.Render();

        float bob = (float)Math.Sin(_animTimer) * BobAmount;
        Vector2 renderPos = ScreenPosition + new Vector2(0, bob);

        _cursorTexture.Draw(renderPos + new Vector2(4, 4), new Vector2(1f, 0f), Color.Black * 0.5f, CursorScale);
        _cursorTexture.Draw(renderPos, new Vector2(1f, 0f), CursorColor, CursorScale);
    }

    public Vector2 GetWorldPosition(Level level)
    {
        if (level?.Camera == null) return Vector2.Zero;
        float screenScale = 1920f / 320f;
        Vector2 gameScreenPos = ScreenPosition / screenScale;
        return level.Camera.Position + gameScreenPos / level.Zoom;
    }
}

