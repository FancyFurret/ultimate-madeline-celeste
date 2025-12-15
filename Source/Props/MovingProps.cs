using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

/// <summary>
/// Dust spinner that moves along a linear track between two points.
/// Two-stage placement: first click = start, second click = end.
/// </summary>
public class DustTrackSpinnerProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "dust_track_spinner";
    public override string Name => "Dust Track";
    public override PropCategory Category => PropCategory.Deadly;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;
    public override PlacementMode PlacementMode => PlacementMode.TwoStage;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return CreateTrackSpinner(position - new Vector2(8, 0), position + new Vector2(8, 0));
    }

    protected override Entity BuildEntity(Vector2 position, Vector2 target, float rotation, bool mirrorX, bool mirrorY)
    {
        return CreateTrackSpinner(position, target);
    }

    private static Entity CreateTrackSpinner(Vector2 start, Vector2 end)
    {
        var data = new EntityData
        {
            Position = start,
            Nodes = new[] { end },
            Values = new Dictionary<string, object>
            {
                { "speed", TrackSpinner.Speeds.Normal },
                { "startCenter", false }
            }
        };
        return new DustTrackSpinner(data, Vector2.Zero);
    }

    public override void OnPositionChanged(Entity entity, Vector2 newPosition)
    {
        if (entity is TrackSpinner spinner)
        {
            spinner.Start = newPosition;
            spinner.End = newPosition;
            spinner.Position = newPosition;
        }
    }

    public override void OnTargetChanged(Entity entity, Vector2 newTarget)
    {
        (entity as TrackSpinner)?.End = newTarget;
    }
}

/// <summary>
/// Dust spinner that rotates in a circle around a center point.
/// Two-stage placement: first click = center, second click = spinner position (radius = distance).
/// </summary>
public class DustRotateSpinnerProp : Prop
{
    private static readonly SpriteInfo _sprite = SpriteInfo.Custom(16, 16);

    public override string Id => "dust_rotate_spinner";
    public override string Name => "Dust Orbit";
    public override PropCategory Category => PropCategory.Deadly;
    public override SpriteInfo GetSprite(float rotation = 0f) => _sprite;
    public override PlacementMode PlacementMode => PlacementMode.TwoStage;
    public override MirrorMode AllowedMirror => MirrorMode.MirrorX;
    public override string FirstStageLabel => "Select Center";
    public override string SecondStageLabel => "Select Position";

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        var center = position;
        var spinnerPos = position; // Same position = radius 0
        return CreateRotateSpinner(position + new Vector2(12, 0), position, mirrorX);
    }

    protected override Entity BuildEntity(Vector2 position, Vector2 target, float rotation, bool mirrorX, bool mirrorY)
    {
        return CreateRotateSpinner(target, position, mirrorX);
    }

    private static Entity CreateRotateSpinner(Vector2 spinnerPos, Vector2 center, bool clockwise)
    {
        var data = new EntityData
        {
            Position = spinnerPos,
            Nodes = new[] { center },
            Values = new Dictionary<string, object>
            {
                { "clockwise", clockwise }
            }
        };
        return new DustRotateSpinner(data, Vector2.Zero);
    }

    public override void OnPositionChanged(Entity entity, Vector2 newPosition)
    {
        // When the first-stage position (center) moves, move both center and spinner
        if (entity is RotateSpinner spinner)
        {
            spinner.Center = newPosition;
            spinner.center = newPosition;
            spinner.Position = newPosition;
        }
    }

    public override void OnTargetChanged(Entity entity, Vector2 newTarget)
    {
        // newTarget is the spinner position (second click)
        if (entity is RotateSpinner spinner)
        {
            spinner.Position = newTarget;
            var length = (newTarget - spinner.center).Length();
            spinner.length = length;
        }
    }
}
