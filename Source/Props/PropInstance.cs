using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

/// <summary>
/// An instance of a prop with position, rotation, and entity state.
/// Wraps a Prop template and manages its entity lifecycle.
/// </summary>
public class PropInstance
{
    /// <summary>
    /// The prop template this instance is based on.
    /// </summary>
    public Prop Prop { get; }

    /// <summary>
    /// The current Monocle entity (null if not spawned).
    /// </summary>
    public Entity Entity { get; private set; }

    /// <summary>
    /// Current top-left position.
    /// </summary>
    public Vector2 Position { get; private set; }

    /// <summary>
    /// Current rotation in degrees (0, 90, 180, 270).
    /// </summary>
    public float Rotation { get; private set; }

    /// <summary>
    /// Whether currently mirrored horizontally.
    /// </summary>
    public bool MirrorX { get; private set; }

    /// <summary>
    /// Whether currently mirrored vertically.
    /// </summary>
    public bool MirrorY { get; private set; }

    /// <summary>
    /// Whether this prop instance is spawned in a scene.
    /// </summary>
    public bool IsSpawned => Entity is { Scene: not null };

    public PropInstance(Prop prop)
    {
        Prop = prop;
    }

    /// <summary>
    /// Spawns the entity in the scene at the given position.
    /// </summary>
    public void Spawn(Scene scene, Vector2 topLeft)
    {
        if (IsSpawned)
            Despawn();

        Position = topLeft;
        Entity = Prop.Build(topLeft, Rotation, MirrorX, MirrorY);
        scene.Add(Entity);
    }

    /// <summary>
    /// Removes the entity from its scene.
    /// </summary>
    public void Despawn()
    {
        if (Entity != null)
        {
            UmcLogger.Info($"Despawning prop: {Prop.Name}");
            Prop.OnDespawn(Entity);
            Entity.RemoveSelf();
            Entity = null;
        }
    }

    /// <summary>
    /// Updates the entity's position.
    /// </summary>
    public void SetPosition(Vector2 topLeft)
    {
        if (topLeft == Position) return;

        Position = topLeft;

        if (!IsSpawned) return;

        if (Prop.RequiresRebuildOnMove)
            RebuildIfSpawned();
        else
            Entity.Position = topLeft + Prop.GetSprite(Rotation).Offset;
    }

    /// <summary>
    /// Sets the rotation. Auto-rebuilds if spawned and rotation changed.
    /// </summary>
    public void SetRotation(float rotation)
    {
        var clamped = Prop.ClampRotation(rotation);
        if (clamped == Rotation) return;

        Rotation = clamped;
        RebuildIfSpawned();
    }

    /// <summary>
    /// Sets the mirror state. Auto-rebuilds if spawned and mirror changed.
    /// </summary>
    public void SetMirror(bool mirrorX, bool mirrorY)
    {
        var (clampedX, clampedY) = Prop.ClampMirror(mirrorX, mirrorY);
        if (clampedX == MirrorX && clampedY == MirrorY) return;

        MirrorX = clampedX;
        MirrorY = clampedY;
        RebuildIfSpawned();
    }

    private void RebuildIfSpawned()
    {
        if (!IsSpawned) return;

        var scene = Entity.Scene;

        Prop.OnDespawn(Entity);
        Entity.RemoveSelf();

        Entity = Prop.Build(Position, Rotation, MirrorX, MirrorY);
        scene.Add(Entity);
    }
}
