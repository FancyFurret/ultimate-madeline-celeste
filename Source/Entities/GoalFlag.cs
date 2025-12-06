using System;
using Celeste.Mod.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// A goal flag entity that players must reach to complete the level.
/// </summary>
[CustomEntity("UltimateMadelineCeleste/GoalFlag")]
[Tracked]
public class GoalFlag : Entity
{
    /// <summary>
    /// Event fired when a player reaches the goal.
    /// </summary>
    public static event Action<UmcPlayer> OnPlayerReachedGoal;

    private float _waveTimer;

    // Flag dimensions
    private const int PoleHeight = 32;
    private const int FlagWidth = 18;
    private const int FlagHeight = 12;

    // Colors
    private static readonly Color PoleColor = new(50, 50, 50);
    private static readonly Color FlagRed = new(220, 40, 40);
    private static readonly Color FlagDarkRed = new(180, 30, 30);

    public GoalFlag(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        // Hitbox for player collision
        Collider = new Hitbox(24, PoleHeight, -4, -PoleHeight);

        Depth = Depths.FGDecals;
    }

    public override void Update()
    {
        base.Update();

        // Animate the flag wave
        _waveTimer += Engine.DeltaTime * 3f;

        // Check for player collision
        CheckPlayerCollision();
    }

    private void CheckPlayerCollision()
    {
        if (Scene is not Level level) return;

        // Check local players
        foreach (var player in level.Tracker.GetEntities<Player>())
        {
            if (player is Player p && CollideCheck(p))
            {
                var spawner = PlayerSpawner.Instance;
                var umcPlayer = spawner?.GetUmcPlayer(p);
                if (umcPlayer != null)
                {
                    TriggerGoalReached(umcPlayer);
                }
            }
        }

        // Check remote players
        foreach (var entity in level.Tracker.GetEntities<RemotePlayer>())
        {
            if (entity is RemotePlayer remote && !remote.Dead && CollideCheck(remote))
            {
                TriggerGoalReached(remote.UmcPlayer);
            }
        }
    }

    private void TriggerGoalReached(UmcPlayer player)
    {
        UmcLogger.Info($"Player {player.Name} reached the goal!");
        OnPlayerReachedGoal?.Invoke(player);
    }

    public override void Render()
    {
        base.Render();
        DrawFlag();
    }

    private void DrawFlag()
    {
        Vector2 basePos = Position;

        // Draw pole (thin black line)
        Draw.Rect(basePos.X - 1, basePos.Y - PoleHeight, 2, PoleHeight, PoleColor);

        // Flag attachment point (top of pole)
        Vector2 flagTop = basePos + new Vector2(1, -PoleHeight + 2);

        // Draw waving triangular pennant
        DrawPennant(flagTop);
    }

    private void DrawPennant(Vector2 attachPoint)
    {
        // Draw triangular pennant flag row by row
        // Triangle: wide at pole, narrows to a point
        for (int x = 0; x < FlagWidth; x++)
        {
            // Wave effect increases toward the tip
            float xFactor = x / (float)FlagWidth;
            float wave = (float)Math.Sin(_waveTimer + x * 0.25f) * xFactor * 1.5f;

            // Triangle height decreases as we go right (toward tip)
            int rowHeight = (int)((1f - xFactor) * FlagHeight);
            if (rowHeight < 1) rowHeight = 1;

            // Center the column vertically
            float startY = -rowHeight / 2f;

            // Shade based on wave position
            float shade = 0.9f + (float)Math.Sin(_waveTimer + x * 0.3f) * 0.1f * xFactor;
            Color col = Color.Lerp(FlagDarkRed, FlagRed, shade);

            for (int y = 0; y < rowHeight; y++)
            {
                Vector2 pixelPos = attachPoint + new Vector2(x, startY + y + wave + FlagHeight / 2f);
                Draw.Rect(pixelPos.X, pixelPos.Y, 1, 1, col);
            }
        }
    }

    /// <summary>
    /// Clears all event handlers. Call when leaving the level.
    /// </summary>
    public static void ClearHandlers()
    {
        OnPlayerReachedGoal = null;
    }
}

