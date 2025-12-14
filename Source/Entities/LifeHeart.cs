using System;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

[Tracked]
public class LifeHeart : Entity
{
    public UmcPlayer Owner { get; }
    public Player TrackedPlayer { get; private set; }
    public int LifeIndex { get; }
    public int TotalLives { get; } // Total hearts in this group for centering

    private readonly Sprite _sprite;
    private float _wobble;
    private bool _breaking;
    private float _visibleTimer;
    private float _alpha = 1f;

    // Layout settings
    private const float HeadOffset = -15f; // How far above head
    private const float Spacing = 8f; // Horizontal spacing between hearts
    private const float VisibleDuration = 2.5f; // How long to stay fully visible
    private const float FadeDuration = 0.5f; // How long to fade out

    public LifeHeart(UmcPlayer owner, Player player, int lifeIndex, int totalLives) : base(player.Position)
    {
        Owner = owner;
        TrackedPlayer = player;
        LifeIndex = lifeIndex;
        TotalLives = totalLives;
        Depth = Depths.Top - 10;

        // Create sprite
        _sprite = new Sprite(GFX.Game, "objects/UMC/life/");
        _sprite.AddLoop("idle", "idle", 0.5f);
        _sprite.Play("idle");
        _sprite.CenterOrigin();
        Add(_sprite);

        // Red color for all lives
        _sprite.Color = Calc.HexToColor("E74C3C");
    }

    public override void Update()
    {
        base.Update();

        if (_breaking) return;

        // If player is gone, remove self
        if (TrackedPlayer == null || TrackedPlayer.Scene == null)
        {
            RemoveSelf();
            return;
        }

        // Hide if only 1 life (no need to show indicator)
        if (TotalLives <= 1)
        {
            Visible = false;
            return;
        }
        Visible = true;

        // Handle visibility timer and fade out
        _visibleTimer += Engine.DeltaTime;
        if (_visibleTimer > VisibleDuration)
        {
            float fadeProgress = (_visibleTimer - VisibleDuration) / FadeDuration;
            _alpha = Math.Max(0f, 1f - fadeProgress);
            _sprite.Color = Calc.HexToColor("E74C3C") * _alpha;
        }

        // Position above player's head, centered based on total hearts in group
        float totalWidth = (TotalLives - 1) * Spacing;
        float startX = -totalWidth / 2f;
        float xOffset = startX + (LifeIndex * Spacing);

        // Simple bob: 2 pixels up and down, same rate for all
        _wobble += Engine.DeltaTime * 2f;
        float yFloat = (float)Math.Sin(_wobble) * 2f;

        // Position relative to player center (not feet)
        Position = TrackedPlayer.Center + new Vector2(xOffset, HeadOffset + yFloat);
    }

    /// <summary>
    /// Plays the break animation and removes the heart.
    /// </summary>
    public void Break(Action onComplete = null)
    {
        if (_breaking) return;
        _breaking = true;

        // Play break sound
        Audio.Play("event:/game/general/seed_poof", Position);

        // Create particle burst
        if (Scene is Level level)
        {
            var color = Calc.HexToColor("E74C3C"); // Red
            for (int i = 0; i < 6; i++)
            {
                float angle = i * MathHelper.TwoPi / 6f;
                level.Particles.Emit(
                    Strawberry.P_Glow,
                    1,
                    Position,
                    Vector2.One * 3f,
                    color,
                    angle
                );
            }
        }

        // Fade out and remove
        Tween tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeIn, 0.15f, start: true);
        tween.OnUpdate = t =>
        {
            _sprite.Scale = Vector2.One * (1f - t.Eased);
        };
        tween.OnComplete = _ =>
        {
            onComplete?.Invoke();
            RemoveSelf();
        };
        Add(tween);
    }

}

/// <summary>
/// Manages life hearts for a player.
/// </summary>
public static class LifeHeartManager
{
    /// <summary>
    /// Attaches life hearts to a player based on the specified lives count.
    /// </summary>
    public static void AttachToPlayer(Player player, UmcPlayer umcPlayer, int lives)
    {
        var level = Engine.Scene as Level;
        if (level == null) return;

        if (lives <= 0) return;

        // Create hearts for each life
        for (int i = 0; i < lives; i++)
        {
            var heart = new LifeHeart(umcPlayer, player, i, lives);
            level.Add(heart);
        }

        UmcLogger.Info($"Attached {lives} life hearts to {umcPlayer.Name}");
    }

    /// <summary>
    /// Removes one life heart from a player with a break animation.
    /// </summary>
    public static void RemoveOneLife(UmcPlayer umcPlayer, Action onComplete = null)
    {
        var scene = Engine.Scene;
        if (scene == null)
        {
            onComplete?.Invoke();
            return;
        }

        // Find the highest-indexed heart for this player
        LifeHeart toRemove = null;
        foreach (var entity in scene.Tracker.GetEntities<LifeHeart>())
        {
            if (entity is LifeHeart heart && heart.Owner == umcPlayer)
            {
                if (toRemove == null || heart.LifeIndex > toRemove.LifeIndex)
                    toRemove = heart;
            }
        }

        if (toRemove != null)
        {
            toRemove.Break(onComplete);
        }
        else
        {
            onComplete?.Invoke();
        }
    }

    /// <summary>
    /// Removes all life hearts from a player.
    /// </summary>
    public static void RemoveAllLives(UmcPlayer umcPlayer)
    {
        var scene = Engine.Scene;
        if (scene == null) return;

        foreach (var entity in scene.Tracker.GetEntities<LifeHeart>())
        {
            if (entity is LifeHeart heart && heart.Owner == umcPlayer)
            {
                heart.Break();
            }
        }
    }
}

