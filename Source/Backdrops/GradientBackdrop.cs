using System;
using Celeste.Mod.Backdrops;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Backdrops;

public enum GradientDirection
{
    Vertical,
    Horizontal
}

[CustomBackdrop("UltimateMadelineCeleste/GradientBackdrop")]
public class GradientBackdrop : Backdrop
{
    private readonly float _startPos;
    private readonly float _endPos;
    private readonly GradientDirection _direction;
    private readonly Color _color;
    private readonly float _colorOpacity;
    private readonly bool _middleMode;
    private readonly MTexture _texture;

    private const float FullExtent = 10000f;

    public GradientBackdrop(BinaryPacker.Element data)
    {
        _startPos = data.AttrFloat("startPos", 0f);
        _endPos = data.AttrFloat("endPos", 180f);

        string directionStr = data.Attr("direction", "Vertical");
        _direction = Enum.TryParse(directionStr, out GradientDirection dir) ? dir : GradientDirection.Vertical;

        _color = Calc.HexToColor(data.Attr("colorHex", "000000"));
        _colorOpacity = MathHelper.Clamp(data.AttrFloat("colorOpacity", 1f), 0f, 1f);

        _middleMode = data.AttrBool("middleMode", false);

        // Gradient texture: transparent at top -> white at bottom
        string texturePath = data.Attr("texture", "bgs/UMC/gradient");
        _texture = GFX.Game[texturePath];
    }

    public override void Render(Scene scene)
    {
        Level level = scene as Level;
        if (level == null)
            return;

        float alpha = FadeAlphaMultiplier;
        if (alpha <= 0.01f)
            return;

        Vector2 cameraPos = level.Camera.Position;
        Color color = _color * (_colorOpacity * alpha);

        // Determine if we need to flip based on start > end
        float minPos = Math.Min(_startPos, _endPos);
        float maxPos = Math.Max(_startPos, _endPos);
        float length = maxPos - minPos;
        bool flip = _startPos < _endPos;

        if (_direction == GradientDirection.Vertical)
        {
            float screenY = minPos - cameraPos.Y;

            if (_middleMode)
            {
                // Middle mode: transparent at edges, color in middle
                float halfLength = length / 2f;
                DrawVertical(-FullExtent, screenY, FullExtent * 2, halfLength, color, false);  // transparent top, color middle
                DrawVertical(-FullExtent, screenY + halfLength, FullExtent * 2, halfLength, color, true);  // color middle, transparent bottom
            }
            else
            {
                DrawVertical(-FullExtent, screenY, FullExtent * 2, length, color, flip);
            }
        }
        else
        {
            float screenX = minPos - cameraPos.X;

            if (_middleMode)
            {
                // Middle mode: transparent at edges, color in middle
                float halfLength = length / 2f;
                DrawHorizontal(screenX, -FullExtent, halfLength, FullExtent * 2, color, false);  // transparent left, color middle
                DrawHorizontal(screenX + halfLength, -FullExtent, halfLength, FullExtent * 2, color, true);  // color middle, transparent right
            }
            else
            {
                DrawHorizontal(screenX, -FullExtent, length, FullExtent * 2, color, flip);
            }
        }
    }

    private void DrawVertical(float x, float y, float width, float height, Color color, bool flip)
    {
        Vector2 scale = new Vector2(width / _texture.Width, height / _texture.Height);

        if (flip)
            _texture.Draw(new Vector2(x, y + height), Vector2.Zero, color, new Vector2(scale.X, -scale.Y));
        else
            _texture.Draw(new Vector2(x, y), Vector2.Zero, color, scale);
    }

    private void DrawHorizontal(float x, float y, float width, float height, Color color, bool flip)
    {
        Vector2 scale = new Vector2(height / _texture.Width, width / _texture.Height);

        if (flip)
            _texture.Draw(new Vector2(x + width, y + height), Vector2.Zero, color, new Vector2(-scale.X, scale.Y), -MathHelper.PiOver2);
        else
            _texture.Draw(new Vector2(x, y + height), Vector2.Zero, color, scale, -MathHelper.PiOver2);
    }
}
