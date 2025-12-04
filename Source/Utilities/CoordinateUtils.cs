using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Utilities;

public static class CoordinateUtils
{
    public const float HudWidth = 1920f;
    public const float HudHeight = 1080f;
    public const float GameWidth = 320f;
    public const float GameHeight = 180f;
    public const float ScreenScale = HudWidth / GameWidth; // 6.0

    public static Level GetLevel() => Engine.Scene as Level;

    public static Vector2 WorldToScreen(Vector2 worldPos, Level level = null)
    {
        level ??= GetLevel();
        if (level?.Camera == null) return new Vector2(HudWidth / 2f, HudHeight / 2f);

        // World position relative to camera
        Vector2 relativePos = worldPos - level.Camera.Position;

        // Apply zoom
        Vector2 gameScreenPos = relativePos * level.Zoom;

        // Scale to HUD coordinates
        return gameScreenPos * ScreenScale;
    }

    public static Vector2 ScreenToWorld(Vector2 screenPos, Level level = null)
    {
        level ??= GetLevel();
        if (level?.Camera == null) return Vector2.Zero;

        // Scale from HUD to game coordinates
        Vector2 gameScreenPos = screenPos / ScreenScale;

        // Remove zoom
        Vector2 relativePos = gameScreenPos / level.Zoom;

        // Add camera position to get world position
        return level.Camera.Position + relativePos;
    }

    public static Vector2 ClampToScreen(Vector2 screenPos, float padding = 20f)
    {
        return new Vector2(
            MathHelper.Clamp(screenPos.X, padding, HudWidth - padding),
            MathHelper.Clamp(screenPos.Y, padding, HudHeight - padding)
        );
    }

    public static Vector2 ClampToLevel(Vector2 worldPos, Level level = null, float padding = 8f)
    {
        level ??= GetLevel();
        if (level == null) return worldPos;

        var bounds = level.Bounds;
        return new Vector2(
            MathHelper.Clamp(worldPos.X, bounds.Left + padding, bounds.Right - padding),
            MathHelper.Clamp(worldPos.Y, bounds.Top + padding, bounds.Bottom - padding)
        );
    }
}

