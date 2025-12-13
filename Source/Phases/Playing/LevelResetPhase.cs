using System;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

/// <summary>
/// Resets level entities (berries, props with NeedsReset) to their original positions.
/// Runs after scoring, before the next picking phase.
/// </summary>
public class LevelResetPhase
{
    private const float ResetDuration = 0.5f;
    private const float DelayBetweenResets = 0.08f;
    private const float PostResetDelay = 0.3f;

    private readonly Level _level;
    private float _timer;
    private bool _resetsStarted;
    private bool _completed;
    private int _pendingResets;

    public event Action OnComplete;

    public LevelResetPhase(Level level)
    {
        _level = level;
        _timer = 0f;
        _resetsStarted = false;
        _pendingResets = 0;
    }

    public void Update()
    {
        if (_completed) return;

        if (!_resetsStarted)
        {
            StartResets();
            _resetsStarted = true;
            if (_completed) return;
        }

        _timer += Engine.DeltaTime;

        if (_pendingResets <= 0 && _timer >= PostResetDelay)
        {
            Complete();
        }
    }

    private void StartResets()
    {
        UmcLogger.Info("Starting level reset animations");

        float delay = 0f;

        // Reset berries
        foreach (var entity in _level.Tracker.GetEntities<UmcBerry>())
        {
            if (entity is UmcBerry berry && !berry.IsCollected)
            {
                if (AnimateEntityReset(berry, berry.SpawnPosition, delay))
                    delay += DelayBetweenResets;
            }
        }

        // Reset placed props that have NeedsReset = true
        var round = RoundState.Current;
        if (round != null)
        {
            foreach (var placed in round.PlacedProps)
            {
                if (placed.Entity == null || placed.Entity.Scene == null) continue;
                if (!placed.Prop.Prop.NeedsReset) continue;

                // Use PropInstance.SetPosition which handles OnPositionChanged
                if (AnimatePropReset(placed, delay))
                    delay += DelayBetweenResets;
            }
        }

        if (_pendingResets == 0)
        {
            // Nothing to reset, complete immediately
            Complete();
        }
    }

    /// <summary>
    /// Animates a regular entity back to target position.
    /// </summary>
    private bool AnimateEntityReset(Entity entity, Vector2 targetPos, float delay)
    {
        var startPos = entity.Position;

        if (Vector2.Distance(startPos, targetPos) < 1f)
            return false;

        _pendingResets++;

        var tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.SineInOut, ResetDuration, start: false);
        tween.OnUpdate = t => entity.Position = Vector2.Lerp(startPos, targetPos, t.Eased);
        tween.OnComplete = _ =>
        {
            entity.Position = targetPos;
            _pendingResets--;
        };

        Alarm.Set(entity, delay, () => tween.Start());
        entity.Add(tween);

        return true;
    }

    /// <summary>
    /// Animates a PropInstance back to its spawn position using SetPosition.
    /// </summary>
    private bool AnimatePropReset(PlacedProp placed, float delay)
    {
        var entity = placed.Entity;
        var propInstance = placed.Prop;

        // Get current entity position, convert to top-left for PropInstance
        var spriteOffset = propInstance.Prop.GetSprite(propInstance.Rotation).Offset;
        var currentTopLeft = entity.Position - spriteOffset;
        var targetTopLeft = propInstance.Position; // Original placement position

        if (Vector2.Distance(currentTopLeft, targetTopLeft) < 1f)
            return false;

        _pendingResets++;

        var tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.SineInOut, ResetDuration, start: false);
        tween.OnUpdate = t => propInstance.SetPosition(Vector2.Lerp(currentTopLeft, targetTopLeft, t.Eased));
        tween.OnComplete = _ =>
        {
            propInstance.SetPosition(targetTopLeft);
            _pendingResets--;
        };

        Alarm.Set(entity, delay, () => tween.Start());
        entity.Add(tween);

        return true;
    }

    private void Complete()
    {
        _completed = true;
        UmcLogger.Info("Level reset complete");
        OnComplete?.Invoke();
    }

    public void Cleanup()
    {
    }
}
