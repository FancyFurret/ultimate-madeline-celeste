using System;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

public class DustSpinnerProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "dust_spinner";
    public override string Name => "Dust Spinner";
    public override PropCategory Category => PropCategory.Deadly;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new DustStaticSpinner(position, attachToSolid: false, ignoreSolids: false);
    }
}

public class CrystalSpinnerProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "crystal_spinner";
    public override string Name => "Crystal Spinner";
    public override PropCategory Category => PropCategory.Deadly;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new CrystalStaticSpinner(position, attachToSolid: false, CrystalColor.Blue);
    }
}

public class SpikesProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _size;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Deadly;

    public SpikesProp(string id, string name, int size)
    {
        _id = id;
        _name = name;
        _size = size;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => rotation switch
    {
        // Offsets tuned to center spike visuals in bounding box
        90f => SpriteInfo.Custom(8, _size, new Vector2(0, 3)),  // Right
        180f => SpriteInfo.Custom(_size, 8, new Vector2(3, 0)), // Down
        270f => SpriteInfo.Custom(8, _size, new Vector2(8, 3)), // Left
        _ => SpriteInfo.Custom(_size, 8, new Vector2(3, 8))     // Up
    };

    public override RotationMode AllowedRotation => RotationMode.Rotate90;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return rotation switch
        {
            90f => new Spikes(position, _size, Spikes.Directions.Right, "default"),
            180f => new Spikes(position, _size, Spikes.Directions.Down, "default"),
            270f => new Spikes(position, _size, Spikes.Directions.Left, "default"),
            _ => new Spikes(position, _size, Spikes.Directions.Up, "default")
        };
    }
}

public class BumperProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(24, 24);

    public override string Id => "bumper";
    public override string Name => "Bumper";
    public override PropCategory Category => PropCategory.Movement;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var bumper = new Bumper(position, null);
        bumper.anchor = position;
        return bumper;
    }

    public override void OnPositionChanged(Entity entity, Vector2 newPosition)
    {
        if (entity is Bumper bumper)
            bumper.anchor = newPosition;
    }
}

public class SpringProp : Prop
{
    private static readonly SpriteInfo _spriteFloor = SpriteInfo.FromEntity(new Spring(Vector2.Zero, Spring.Orientations.Floor, playerCanUse: true));
    private static readonly SpriteInfo _spriteWallLeft = SpriteInfo.FromEntity(new Spring(Vector2.Zero, Spring.Orientations.WallLeft, playerCanUse: true));
    private static readonly SpriteInfo _spriteWallRight = SpriteInfo.FromEntity(new Spring(Vector2.Zero, Spring.Orientations.WallRight, playerCanUse: true));

    public override string Id => "spring";
    public override string Name => "Spring";
    public override PropCategory Category => PropCategory.Movement;

    public override SpriteInfo GetSprite(float rotation = 0f) => rotation switch
    {
        90f => _spriteWallRight,
        270f => _spriteWallLeft,
        _ => _spriteFloor
    };

    public override RotationMode AllowedRotation => RotationMode.Rotate90;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var orientation = rotation switch
        {
            90f => Spring.Orientations.WallRight,
            180f => Spring.Orientations.Floor,
            270f => Spring.Orientations.WallLeft,
            _ => Spring.Orientations.Floor
        };

        return new Spring(position, orientation, playerCanUse: true);
    }
}

public class GreenBoosterProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "booster_green";
    public override string Name => "Green Booster";
    public override PropCategory Category => PropCategory.Movement;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new Booster(position, red: false);
    }
}

public class RedBoosterProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "booster_red";
    public override string Name => "Red Booster";
    public override PropCategory Category => PropCategory.Movement;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new Booster(position, red: true);
    }
}

public class DashCrystalProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "dash_crystal";
    public override string Name => "Dash Crystal";
    public override PropCategory Category => PropCategory.Movement;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new Refill(position, twoDashes: false, oneUse: false);
    }
}

public class DoubleDashCrystalProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "dash_crystal_double";
    public override string Name => "Double Dash Crystal";
    public override PropCategory Category => PropCategory.Movement;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new Refill(position, twoDashes: true, oneUse: false);
    }
}

public class FeatherProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(20, 20);

    public override string Id => "feather";
    public override string Name => "Feather";
    public override PropCategory Category => PropCategory.Movement;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new FlyFeather(position, shielded: false, singleUse: false);
    }
}

public class BounceBlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Movement;

    public BounceBlockProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => SpriteInfo.Custom(_width, _height, Vector2.Zero);

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new BounceBlock(position, _width, _height);
    }

    public override void OnPositionChanged(Entity entity, Vector2 newPosition)
    {
        if (entity is BounceBlock bounceBlock)
            bounceBlock.startPos = newPosition;
    }
}

public class JumpThroughProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Platform;

    public JumpThroughProp(string id, string name, int width)
    {
        _id = id;
        _name = name;
        _width = width;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => SpriteInfo.Custom(_width, 8, Vector2.Zero);

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var data = new EntityData
        {
            Position = position,
            Width = _width,
            Values = new System.Collections.Generic.Dictionary<string, object>
            {
                { "texture", "default" }
            }
        };
        return new JumpthruPlatform(data, Vector2.Zero);
    }
}

public class CrumbleBlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Platform;

    public CrumbleBlockProp(string id, string name, int width)
    {
        _id = id;
        _name = name;
        _width = width;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => SpriteInfo.Custom(_width, 8, Vector2.Zero);

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new CrumblePlatform(position, _width);
    }
}

public class FallingBlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;
    private readonly char _tileset;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Platform;

    public FallingBlockProp(string id, string name, int width, int height, char tileset = 'G')
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
        _tileset = tileset;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => SpriteInfo.Custom(_width, _height, Vector2.Zero);

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new FallingBlock(position, _tileset, _width, _height, finalBoss: false, behind: false, climbFall: true);
    }
}

public class DreamBlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Platform;

    public DreamBlockProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => SpriteInfo.Custom(_width, _height, Vector2.Zero);

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new DreamBlock(position, _width, _height, null, fastMoving: false, oneUse: false);
    }
}

public class ZipMoverProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Special;

    public ZipMoverProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => SpriteInfo.Custom(_width, _height + 16, new Vector2(0, 16));
    public override PlacementMode PlacementMode => PlacementMode.TwoStage;
    public override bool RequiresRebuildOnMove => true;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        // No target set yet - show a small track one tile above
        var target = position - new Vector2(0, 16);
        return new ZipMover(position, _width, _height, target, ZipMover.Themes.Normal);
    }

    protected override Entity BuildEntity(Vector2 position, Vector2 target, float rotation, bool mirrorX, bool mirrorY)
    {
        return new ZipMover(position, _width, _height, target, ZipMover.Themes.Normal);
    }
}


public class SwapBlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Special;

    public SwapBlockProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
    }

    public override SpriteInfo GetSprite(float rotation = 0f) => SpriteInfo.Custom(_width, _height + 8, new Vector2(0, 8));
    public override PlacementMode PlacementMode => PlacementMode.TwoStage;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var target = position - new Vector2(0, 8);
        return new SwapBlock(position, _width, _height, target, SwapBlock.Themes.Normal);
    }

    protected override Entity BuildEntity(Vector2 position, Vector2 target, float rotation, bool mirrorX, bool mirrorY)
    {
        return new SwapBlock(position, _width, _height, target, SwapBlock.Themes.Normal);
    }

    public override void OnPositionChanged(Entity entity, Vector2 newPosition)
    {
        if (entity is SwapBlock swapBlock)
        {
            var offset = swapBlock.end - swapBlock.start;
            swapBlock.start = newPosition;
            swapBlock.end = newPosition + offset;
            UpdateSwapBlock(swapBlock);
        }
    }

    public override void OnTargetChanged(Entity entity, Vector2 newTarget)
    {
        if (entity is SwapBlock swapBlock)
        {
            swapBlock.end = newTarget;
            UpdateSwapBlock(swapBlock);
        }
    }

    private static void UpdateSwapBlock(SwapBlock swapBlock)
    {
        // Update move rect
        var x = (int)Math.Min(swapBlock.X, swapBlock.end.X);
        var y = (int)Math.Min(swapBlock.Y, swapBlock.end.Y);
        var x2 = (int)Math.Max(swapBlock.X + swapBlock.Width, swapBlock.end.X + swapBlock.Width);
        var y2 = (int)Math.Max(swapBlock.Y + swapBlock.Height, swapBlock.end.Y + swapBlock.Height);
        swapBlock.moveRect = new Rectangle(x, y, x2 - x, y2 - y);

        // Update speeds based on distance
        var distance = Vector2.Distance(swapBlock.start, swapBlock.end);
        if (distance > 0)
        {
            swapBlock.maxForwardSpeed = 360f / distance;
            swapBlock.maxBackwardSpeed = swapBlock.maxForwardSpeed * 0.4f;
        }
    }

    public override void OnDespawn(Entity entity)
    {
        if (entity is SwapBlock swapBlock)
        {
            swapBlock.path?.RemoveSelf();
        }
    }
}

public class CloudProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(32, 8);

    public override string Id => "cloud";
    public override string Name => "Cloud";
    public override PropCategory Category => PropCategory.Platform;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new Cloud(position, fragile: false);
    }
}


public class IntroCarProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.FromEntity(new IntroCar(Vector2.Zero));

    public override string Id => "intro_car";
    public override string Name => "Car";
    public override PropCategory Category => PropCategory.Special;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;
    public override MirrorMode AllowedMirror => MirrorMode.MirrorX;
    public override bool RequiresRebuildOnMove => true;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var car = new IntroCar(position);
        return car;
    }

    public override void OnDespawn(Entity entity)
    {
        if (entity is IntroCar car)
            car.wheels?.RemoveSelf();
    }

    public override void OnDepthChanged(Entity entity, int depth)
    {
        if (entity is IntroCar car && car.wheels != null)
            car.wheels.Depth = depth + 1;
    }
}


/// <summary>
/// Kevin (crushing block) prop - chases and crushes player.
/// </summary>
public class KevinProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Movement;
    public override bool NeedsReset => true;

    public KevinProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
    }

    public override SpriteInfo GetSprite(float rotation = 0f)
    {
        bool swapped = rotation == 90f || rotation == 270f;
        float w = swapped ? _height : _width;
        float h = swapped ? _width : _height;
        return SpriteInfo.Custom(w, h, Vector2.Zero);
    }

    public override RotationMode AllowedRotation => RotationMode.Rotate90;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var sprite = GetSprite(rotation);
        var axes = rotation switch
        {
            90f or 270f => CrushBlock.Axes.Horizontal,
            _ => CrushBlock.Axes.Vertical
        };
        return new CrushBlock(position, (int)sprite.Width, (int)sprite.Height, axes, chillOut: false);
    }
}


/// <summary>
/// Puffer prop - explodes when player is nearby, launching them.
/// </summary>
public class PufferProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "puffer";
    public override string Name => "Puffer";
    public override PropCategory Category => PropCategory.Movement;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;
    public override MirrorMode AllowedMirror => MirrorMode.MirrorX;
    public override bool NeedsReset => true;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new Puffer(position, mirrorX);
    }

    public override void OnPositionChanged(Entity entity, Vector2 newPosition)
    {
        // (entity as Puffer)?.anchorPosition = newPosition;
        (entity as Puffer)?.startPosition = newPosition;
        (entity as Puffer)?.Position = newPosition;
    }
}

/// <summary>
/// Berry prop - can be picked up by players and collected at the goal for bonus points.
/// </summary>
public class BerryProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(14, 14);

    public override string Id => "berry";
    public override string Name => "Berry";
    public override PropCategory Category => PropCategory.Collectible;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var berry = new UmcBerry(position);
        // Register with the berry manager if it exists
        BerryManager.Instance?.RegisterBerry(berry);
        return berry;
    }

    public override void OnPositionChanged(Entity entity, Vector2 newPosition)
    {
        if (entity is UmcBerry berry)
            berry.SpawnPosition = newPosition;
    }
}

/// <summary>
/// A deadly lava block that kills players on touch.
/// </summary>
public class LavaBlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Deadly;

    public LavaBlockProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
    }

    public override SpriteInfo GetSprite(float rotation = 0f)
    {
        bool swapped = rotation == 90f || rotation == 270f;
        float w = swapped ? _height : _width;
        float h = swapped ? _width : _height;
        return SpriteInfo.Custom(w, h, Vector2.Zero);
    }

    public override RotationMode AllowedRotation => RotationMode.Rotate90;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var sprite = GetSprite(rotation);
        return new PlacedLavaBlock(position, (int)sprite.Width, (int)sprite.Height);
    }
}

/// <summary>
/// A placed lava block entity that kills on contact.
/// </summary>
public class PlacedLavaBlock : Entity
{
    private readonly int _width;
    private readonly int _height;
    private LavaRect _lava;

    public PlacedLavaBlock(Vector2 position, int width, int height) : base(position)
    {
        _width = width;
        _height = height;
        Collider = new Hitbox(width, height);
        Depth = -8500;

        Add(new PlayerCollider(OnPlayer));
        Add(_lava = new LavaRect(width, height, 2));

        // Lava visual settings
        _lava.SurfaceColor = Calc.HexToColor("ff8933");
        _lava.EdgeColor = Calc.HexToColor("f25e29");
        _lava.CenterColor = Calc.HexToColor("d01c01");
        _lava.SmallWaveAmplitude = 2f;
        _lava.BigWaveAmplitude = 2f;
        _lava.CurveAmplitude = 2f;
    }

    private void OnPlayer(Player player)
    {
        player.Die((player.Center - Center).SafeNormalize());
    }
}

/// <summary>
/// An ice/cold block that kills players on touch.
/// </summary>
public class IceBlockProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _width;
    private readonly int _height;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Deadly;

    public IceBlockProp(string id, string name, int width, int height)
    {
        _id = id;
        _name = name;
        _width = width;
        _height = height;
    }

    public override SpriteInfo GetSprite(float rotation = 0f)
    {
        bool swapped = rotation == 90f || rotation == 270f;
        float w = swapped ? _height : _width;
        float h = swapped ? _width : _height;
        return SpriteInfo.Custom(w, h, Vector2.Zero);
    }

    public override RotationMode AllowedRotation => RotationMode.Rotate90;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var sprite = GetSprite(rotation);
        return new PlacedIceBlock(position, (int)sprite.Width, (int)sprite.Height);
    }
}

/// <summary>
/// A placed ice block entity that kills on contact.
/// </summary>
public class PlacedIceBlock : Entity
{
    private readonly int _width;
    private readonly int _height;
    private LavaRect _lava;

    public PlacedIceBlock(Vector2 position, int width, int height) : base(position)
    {
        _width = width;
        _height = height;
        Collider = new Hitbox(width, height);
        Depth = -8500;

        Add(new PlayerCollider(OnPlayer));
        Add(_lava = new LavaRect(width, height, 2));

        // Ice visual settings
        _lava.SurfaceColor = Calc.HexToColor("a6fff4");
        _lava.EdgeColor = Calc.HexToColor("6cd6eb");
        _lava.CenterColor = Calc.HexToColor("4ca8d6");
        _lava.SmallWaveAmplitude = 1f;
        _lava.BigWaveAmplitude = 1f;
        _lava.CurveAmplitude = 1f;
        _lava.UpdateMultiplier = 0.3f;
    }

    private void OnPlayer(Player player)
    {
        player.Die((player.Center - Center).SafeNormalize());
    }
}

/// <summary>
/// A fire bar/rotating hazard prop.
/// </summary>
public class FireBarProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(24, 24);

    public override string Id => "fire_bar";
    public override string Name => "Fire Spinner";
    public override PropCategory Category => PropCategory.Deadly;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        // RotateSpinner needs Nodes[0] as center and Position as the spinner location
        // The spinner rotates around the center point
        var center = position; // Center of rotation
        var spinnerPos = position + new Vector2(12, 0); // Offset spinner from center

        var data = new EntityData
        {
            Position = spinnerPos,
            Nodes = new[] { center },
            Values = new System.Collections.Generic.Dictionary<string, object>
            {
                { "clockwise", !mirrorX }
            }
        };

        return new RotateSpinner(data, Vector2.Zero);
    }
}

