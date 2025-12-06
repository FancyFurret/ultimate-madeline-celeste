using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

using System.Linq;

/// <summary>
/// An L-shaped block prop (5 tiles total in an L shape).
/// </summary>
public class LBlockProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(24, 24, Vector2.Zero);

    public override string Id => "block_l";
    public override string Name => "L Block";
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    public override RotationMode AllowedRotation => RotationMode.Rotate90;
    public override MirrorMode AllowedMirror => MirrorMode.MirrorXY;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new PlacedLBlock(position, rotation, mirrorX, mirrorY);
    }
}

/// <summary>
/// A placed L-shaped block entity.
/// </summary>
public class PlacedLBlock : Solid
{
    private const char TileId = '4';

    private static readonly Point[] BaseTiles =
    {
        new(0, 0),
        new(0, 1),
        new(0, 2),
        new(1, 2),
        new(2, 2),
    };

    private readonly Point[] _tiles;
    private TileGrid _tileGrid;

    public PlacedLBlock(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
        : base(position, 24, 24, safe: true)
    {
        _tiles = new Point[5];

        for (int i = 0; i < BaseTiles.Length; i++)
        {
            var tile = BaseTiles[i];

            if (mirrorX) tile.X = 2 - tile.X;
            if (mirrorY) tile.Y = 2 - tile.Y;

            tile = RotateTile(tile, rotation);
            _tiles[i] = tile;
        }

        // Create proper L-shaped collider from the tiles
        var hitboxes = _tiles.Select(t => new Hitbox(8, 8, t.X * 8, t.Y * 8)).ToArray();
        Collider = new ColliderList(hitboxes);
    }

    private static Point RotateTile(Point tile, float rotation)
    {
        int cx = 1, cy = 1;
        int x = tile.X - cx;
        int y = tile.Y - cy;

        return rotation switch
        {
            90f => new Point(cy - y + cx - 1, x + cy),
            180f => new Point(cx - x, cy - y),
            270f => new Point(y + cx, cx - x + cy - 1),
            _ => tile
        };
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);

        var tileMap = new VirtualMap<char>(3, 3, '0');
        foreach (var tile in _tiles)
            tileMap[tile.X, tile.Y] = TileId;

        var generated = GFX.FGAutotiler.GenerateMap(tileMap, paddingIgnoreOutOfLevel: true);
        _tileGrid = generated.TileGrid;

        Add(_tileGrid);
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        _tileGrid = null;
    }
}

