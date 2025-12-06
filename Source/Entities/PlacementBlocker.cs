using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// A trigger zone that blocks prop placement during the placing phase.
/// Place this around spawn areas to prevent players from placing props there.
/// </summary>
[CustomEntity("UltimateMadelineCeleste/PlacementBlocker")]
[Tracked]
public class PlacementBlocker : Trigger
{
    public PlacementBlocker(EntityData data, Vector2 offset)
        : base(data, offset)
    {
        // Trigger base class handles Width/Height and Collider from EntityData
    }

    /// <summary>
    /// Checks if a given rectangle overlaps with this blocker.
    /// </summary>
    public bool OverlapsRect(Rectangle rect)
    {
        var blockerRect = new Rectangle(
            (int)Position.X,
            (int)Position.Y,
            (int)Width,
            (int)Height
        );
        return blockerRect.Intersects(rect);
    }

    /// <summary>
    /// Checks if any placement blocker in the level overlaps with the given rectangle.
    /// </summary>
    public static bool IsBlocked(Level level, Rectangle propRect)
    {
        foreach (var entity in level.Tracker.GetEntities<PlacementBlocker>())
        {
            if (entity is PlacementBlocker blocker && blocker.OverlapsRect(propRect))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Checks if a position with the given size would be blocked.
    /// </summary>
    public static bool IsBlocked(Level level, Vector2 position, int width, int height)
    {
        var rect = new Rectangle((int)position.X, (int)position.Y, width, height);
        return IsBlocked(level, rect);
    }
}

