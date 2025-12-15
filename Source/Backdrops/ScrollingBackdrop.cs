using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Backdrops;

[CustomBackdrop("UltimateMadelineCeleste/ScrollingBackdrop")]
public class ScrollingBackdrop : Backdrop
{
    private readonly MTexture _texture;
    private readonly float _scrollSpeedX;
    private readonly float _yPosition;
    private readonly float _scale;
    private readonly float _opacity;
    private float _scrollOffsetX;

    public ScrollingBackdrop(BinaryPacker.Element data)
    {
        // Get the texture path from the map data
        string texturePath = data.Attr("texture", "bgs/UMC/stars");
        _texture = GFX.Game[texturePath];

        // Scroll speed in pixels per second (horizontal only)
        _scrollSpeedX = data.AttrFloat("scrollSpeedX", 10f);

        // Y position (fixed world position, not affected by camera)
        _yPosition = data.AttrFloat("yPosition", 0f);

        // Scale of the texture
        _scale = data.AttrFloat("scale", 1f);

        // Opacity (0-1)
        _opacity = MathHelper.Clamp(data.AttrFloat("opacity", 1f), 0f, 1f);

        // Initial scroll offset
        _scrollOffsetX = 0f;
    }

    public override void Update(Scene scene)
    {
        base.Update(scene);
        _scrollOffsetX += _scrollSpeedX * Engine.DeltaTime;
    }

    public override void Render(Scene scene)
    {
        Level level = scene as Level;
        if (level == null)
            return;

        Vector2 cameraPos = level.Camera.Position;
        Color drawColor = Color * FadeAlphaMultiplier * _opacity;
        if (drawColor.A <= 1)
            return;

        // Get texture dimensions
        int textureWidth = (int)(_texture.Width * _scale);

        // Calculate the starting position for horizontal tiling
        // Account for camera X position and scroll offset
        float baseX = cameraPos.X * Scroll.X;
        float offsetX = (baseX + _scrollOffsetX) % textureWidth;
        if (offsetX < 0) offsetX += textureWidth;

        // Y position is fixed world position - convert to screen space like RotatingBackdrop
        float drawY = _yPosition - cameraPos.Y * Scroll.Y;

        // Calculate how many tiles we need to draw horizontally to cover the screen
        int screenWidth = Engine.Width;
        int tilesX = (int)Math.Ceiling((float)screenWidth / textureWidth) + 2;

        // Draw the texture tiled only horizontally
        for (int x = -1; x <= tilesX; x++)
        {
            Vector2 drawPos = new Vector2(
                x * textureWidth - offsetX,
                drawY
            );

            _texture.Draw(drawPos, Vector2.Zero, drawColor, _scale);
        }
    }
}

