using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

/// <summary>
/// Helper for calculating prop size and position offset from sprite info.
/// </summary>
public readonly struct SpriteInfo
{
    public Vector2 Size { get; }
    public float Width => Size.X;
    public float Height => Size.Y;
    public Vector2 Offset { get; }

    private SpriteInfo(Vector2 size, Vector2 offset)
    {
        Size = size;
        Offset = offset;
    }

    public static SpriteInfo FromEntity(Entity entity)
    {
        var image = entity.Get<Image>();
        if (image != null)
            return new SpriteInfo(new Vector2(image.Width, image.Height), image.Origin);

        var sprite = entity.Get<Sprite>();
        if (sprite != null)
            return new SpriteInfo(new Vector2(sprite.Width, sprite.Height), sprite.Origin);

        return new SpriteInfo(Vector2.Zero, Vector2.Zero);
    }

    public static SpriteInfo Custom(float width, float height, Vector2 offset)
        => new(new Vector2(width, height), offset);

    public static SpriteInfo Custom(float width, float height)
    {
        var size = new Vector2(width, height);
        return new SpriteInfo(size, size / 2f);
    }
}

/// <summary>
/// Rotation modes for props when placing.
/// </summary>
public enum RotationMode
{
    None,
    Rotate90,
    Rotate180
}

/// <summary>
/// Mirror/flip modes for props when placing.
/// </summary>
public enum MirrorMode
{
    None,
    MirrorX,
    MirrorY,
    MirrorXY
}

/// <summary>
/// Abstract base class for all props. Acts as a stateless template/factory.
/// Use PropInstance to create instances with position/rotation state.
/// </summary>
public abstract class Prop
{
    public abstract string Name { get; }
    public abstract string Id { get; }

    /// <summary>
    /// Gets the sprite info for the given rotation.
    /// Override if sprite changes based on rotation (e.g. Spring).
    /// </summary>
    public abstract SpriteInfo GetSprite(float rotation = 0f);

    /// <summary>
    /// Convenience property for the default (0°) sprite.
    /// </summary>
    public SpriteInfo Sprite => GetSprite(0f);

    public virtual RotationMode AllowedRotation => RotationMode.None;
    public virtual MirrorMode AllowedMirror => MirrorMode.None;
    public virtual MTexture Icon => null;

    /// <summary>
    /// If true, the entity must be rebuilt when moved (e.g. IntroCar with wheels).
    /// </summary>
    public virtual bool RequiresRebuildOnMove => false;

    /// <summary>
    /// Gets the size for the given rotation.
    /// </summary>
    public Vector2 GetSize(float rotation = 0f) => GetSprite(rotation).Size;

    /// <summary>
    /// Gets the center offset for the given rotation.
    /// </summary>
    public Vector2 GetCenter(float rotation = 0f) => GetSize(rotation) / 2f;

    /// <summary>
    /// Clamps rotation to allowed values.
    /// </summary>
    public float ClampRotation(float rotation)
    {
        return AllowedRotation switch
        {
            RotationMode.None => 0f,
            RotationMode.Rotate180 => rotation == 180f ? 180f : 0f,
            RotationMode.Rotate90 => rotation switch
            {
                90f => 90f,
                180f => 180f,
                270f => 270f,
                _ => 0f
            },
            _ => 0f
        };
    }

    /// <summary>
    /// Clamps mirror to allowed values.
    /// </summary>
    public (bool mirrorX, bool mirrorY) ClampMirror(bool mirrorX, bool mirrorY)
    {
        var x = AllowedMirror is MirrorMode.MirrorX or MirrorMode.MirrorXY && mirrorX;
        var y = AllowedMirror is MirrorMode.MirrorY or MirrorMode.MirrorXY && mirrorY;
        return (x, y);
    }

    /// <summary>
    /// Builds the entity with the given transform.
    /// topLeft is the bounding box top-left; this method handles the sprite offset.
    /// </summary>
    public Entity Build(Vector2 topLeft, float rotation = 0f, bool mirrorX = false, bool mirrorY = false)
    {
        var entityPos = topLeft + GetSprite(rotation).Offset;
        return BuildEntity(entityPos, rotation, mirrorX, mirrorY);
    }

    /// <summary>
    /// Updates an entity's position.
    /// topLeft is the bounding box top-left; this method handles the sprite offset.
    /// </summary>
    // public void SetPosition(Entity entity, Vector2 topLeft, float rotation = 0f)
    // {
    //     entity.Position = topLeft + GetSprite(rotation).Offset;
    // }

    /// <summary>
    /// Override to build the entity. Position is already offset (entity position, not top-left).
    /// </summary>
    protected abstract Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY);

    /// <summary>
    /// Called when an entity is about to be despawned/removed.
    /// Override to clean up related entities (e.g. IntroCar wheels).
    /// </summary>
    public virtual void OnDespawn(Entity entity) { }

    /// <summary>
    /// Called after entity depth is set.
    /// Override to sync depth of related entities (e.g. IntroCar wheels).
    /// </summary>
    public virtual void OnDepthChanged(Entity entity, int depth) { }
}

