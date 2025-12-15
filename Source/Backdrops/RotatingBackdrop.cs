using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Backdrops;

[CustomBackdrop("UltimateMadelineCeleste/RotatingBackdrop")]
public class RotatingBackdrop : Backdrop
{
    private readonly MTexture _texture;
    private readonly Vector2 _center;
    private readonly float _rotationSpeed;
    private readonly float _scale;
    private readonly float _opacity;
    private float _rotation;

    public RotatingBackdrop(BinaryPacker.Element data)
    {
        // Get the texture path from the map data
        string texturePath = data.Attr("texture", "bgs/UMC/stars");
        _texture = GFX.Game[texturePath];

        // Center point for rotation (defaults to center of screen 160, 90)
        float centerX = data.AttrFloat("centerX", 160f);
        float centerY = data.AttrFloat("centerY", 90f);
        _center = new Vector2(centerX, centerY);

        // Rotation speed in degrees per second (positive = clockwise)
        _rotationSpeed = MathHelper.ToRadians(data.AttrFloat("rotationSpeed", 5f));

        // Scale of the texture
        _scale = data.AttrFloat("scale", 1f);

        // Opacity (0-1)
        _opacity = MathHelper.Clamp(data.AttrFloat("opacity", 1f), 0f, 1f);

        // Initial rotation in degrees
        _rotation = MathHelper.ToRadians(data.AttrFloat("startRotation", 0f));
    }

    public override void Update(Scene scene)
    {
        base.Update(scene);
        _rotation += _rotationSpeed * Engine.DeltaTime;
    }

    public override void Render(Scene scene)
    {
        Level level = scene as Level;
        if (level == null)
            return;

        Vector2 cameraPos = level.Camera.Position;
        Vector2 renderPos = _center - cameraPos * Scroll;

        Color drawColor = Color * FadeAlphaMultiplier * _opacity;
        if (drawColor.A <= 1)
            return;

        _texture.DrawCentered(renderPos, drawColor, _scale, _rotation);
    }
}

