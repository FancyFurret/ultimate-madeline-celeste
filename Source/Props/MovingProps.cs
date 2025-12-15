using System;
using System.Collections.Generic;
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

    public override bool PauseDuringPlacing => true;

    public override void SetPaused(Entity entity, bool paused)
    {
        if (entity is TrackSpinner spinner)
        {
            spinner.Moving = !paused;
        }
    }

    public override void RenderPlacingPreview(Entity entity, float pulse)
    {
        if (entity is not TrackSpinner spinner) return;

        var start = spinner.Start;
        var end = spinner.End;

        // Draw the track line
        var lineColor = new Color(255, 150, 50) * (0.5f * pulse);
        Draw.Line(start, end, lineColor, 2f);

        // Draw endpoint markers
        var markerColor = new Color(255, 200, 100) * pulse;
        const float markerSize = 3f;
        Draw.Rect(start.X - markerSize, start.Y - markerSize, markerSize * 2, markerSize * 2, markerColor);
        Draw.Rect(end.X - markerSize, end.Y - markerSize, markerSize * 2, markerSize * 2, markerColor);
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

    public override bool PauseDuringPlacing => true;

    public override void SetPaused(Entity entity, bool paused)
    {
        if (entity is RotateSpinner spinner)
        {
            spinner.Moving = !paused;
        }
    }

    public override void RenderPlacingPreview(Entity entity, float pulse)
    {
        if (entity is not RotateSpinner spinner) return;

        var center = spinner.center;
        var radius = spinner.length;

        if (radius <= 0) return;

        // Draw the orbit circle
        var lineColor = new Color(255, 150, 50) * (0.5f * pulse);
        DrawCircle(center, radius, lineColor, 32);

        // Draw center marker
        var markerColor = new Color(255, 200, 100) * pulse;
        const float markerSize = 3f;
        Draw.Rect(center.X - markerSize, center.Y - markerSize, markerSize * 2, markerSize * 2, markerColor);
    }

    private static void DrawCircle(Vector2 center, float radius, Color color, int segments)
    {
        for (int i = 0; i < segments; i++)
        {
            float a1 = i / (float)segments * MathF.PI * 2f;
            float a2 = (i + 1) / (float)segments * MathF.PI * 2f;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            var p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            Draw.Line(p1, p2, color, 1f);
        }
    }
}
