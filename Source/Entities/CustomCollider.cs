using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// An invisible solid wall that the player physically collides with, like a tile.
/// </summary>
[CustomEntity("UltimateMadelineCeleste/CustomCollider")]
[Tracked]
public class CustomCollider : Solid
{
    public CustomCollider(EntityData data, Vector2 offset)
        : base(data.Position + offset, data.Width, data.Height, safe: true)
    {
    }
}

