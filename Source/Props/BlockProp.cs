using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

/// <summary>
/// A simple rectangular solid block prop.
/// </summary>
public class BlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _baseWidth;
    private readonly int _baseHeight;

    public override string Id => _id;
    public override string Name => _name;

    public override SpriteInfo GetSprite(float rotation = 0f)
    {
        bool swapped = rotation == 90f || rotation == 270f;
        float w = swapped ? _baseHeight : _baseWidth;
        float h = swapped ? _baseWidth : _baseHeight;
        return SpriteInfo.Custom(w, h, Vector2.Zero);
    }

    public override RotationMode AllowedRotation => RotationMode.Rotate90;
    public override MirrorMode AllowedMirror => MirrorMode.MirrorXY;

    public BlockProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _baseWidth = width;
        _baseHeight = height;
    }

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var sprite = GetSprite(rotation);
        return new PlacedBlock(position, (int)sprite.Width, (int)sprite.Height);
    }
}

/// <summary>
/// A simple solid block entity that can be placed.
/// </summary>
public class PlacedBlock : Solid
{
    private readonly int _width;
    private readonly int _height;
    private TileGrid _tiles;

    public PlacedBlock(Vector2 position, int width, int height)
        : base(position, width, height, safe: true)
    {
        _width = width;
        _height = height;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);

        int tilesX = _width / 8;
        int tilesY = _height / 8;

        var generated = GFX.FGAutotiler.GenerateBox('6', tilesX, tilesY);
        _tiles = generated.TileGrid;

        Add(_tiles);
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        _tiles = null;
    }
}

