using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

/// <summary>
/// Dust spinner (static) prop - a hazard that kills on contact.
/// </summary>
public class DustSpinnerProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "dust_spinner";
    public override string Name => "Dust Spinner";
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new DustStaticSpinner(position, attachToSolid: false, ignoreSolids: false);
    }
}

/// <summary>
/// Intro car prop - a movable car that can be pushed.
/// </summary>
public class IntroCarProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.FromEntity(new IntroCar(Vector2.Zero));

    public override string Id => "intro_car";
    public override string Name => "Car";
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;
    public override MirrorMode AllowedMirror => MirrorMode.MirrorX;
    public override bool RequiresRebuildOnMove => true;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        // UmcLogger.Info("BUILDING CAR");
        var car = new IntroCar(position);
        // TODO: Handle MirrorX for car facing direction
        return car;
    }

    public override void OnDespawn(Entity entity)
    {
        if (entity is IntroCar car)
            car.wheels?.RemoveSelf();
    }

    public override void OnDepthChanged(Entity entity, int depth)
    {
        // Wheels should be slightly behind the car body
        if (entity is IntroCar car && car.wheels != null)
            car.wheels.Depth = depth + 1;
    }
}

/// <summary>
/// Spring prop - bounces the player.
/// </summary>
public class SpringProp : Prop
{
    private static readonly SpriteInfo _spriteFloor = SpriteInfo.FromEntity(new Spring(Vector2.Zero, Spring.Orientations.Floor, playerCanUse: true));
    private static readonly SpriteInfo _spriteWallLeft = SpriteInfo.FromEntity(new Spring(Vector2.Zero, Spring.Orientations.WallLeft, playerCanUse: true));
    private static readonly SpriteInfo _spriteWallRight = SpriteInfo.FromEntity(new Spring(Vector2.Zero, Spring.Orientations.WallRight, playerCanUse: true));

    public override string Id => "spring";
    public override string Name => "Spring";

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

