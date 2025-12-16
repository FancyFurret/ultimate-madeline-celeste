using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// An invisible death plane that kills the player on contact.
/// </summary>
[CustomEntity("UltimateMadelineCeleste/DeathPlane")]
[Tracked]
public class DeathPlane : Entity
{
    public DeathPlane(EntityData data, Vector2 offset)
        : base(data.Position + offset)
    {
        Collider = new Hitbox(data.Width, data.Height);
    }

    public override void Update()
    {
        base.Update();
        Player player = CollideFirst<Player>();
        if (player != null)
        {
            player.Die((player.Center - Center).SafeNormalize());
        }
    }
}

