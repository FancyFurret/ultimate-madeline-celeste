using System;
using System.Collections;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// Custom dead body for UMC that plays the death animation without triggering level reload/screen wipe.
/// Can be tracked for postmortem features.
/// </summary>
[Tracked]
public class UmcPlayerDeadBody : Entity
{
    /// <summary>
    /// The UmcPlayer this dead body belongs to.
    /// </summary>
    public UmcPlayer UmcPlayer { get; }

    /// <summary>
    /// Called when the death animation completes.
    /// </summary>
    public Action OnDeathComplete;

    private readonly Player _player;
    private readonly PlayerHair _hair;
    private readonly PlayerSprite _sprite;
    private readonly VertexLight _light;
    private readonly Color _initialHairColor;
    private readonly Vector2 _bounce;
    private Facings _facing;
    private float _scale;
    private DeathEffect _deathEffect;
    private bool _finished;

    public UmcPlayerDeadBody(Player player, Vector2 direction, UmcPlayer umcPlayer) : base(player.Position)
    {
        _player = player;
        UmcPlayer = umcPlayer;
        Depth = Depths.Top;

        _facing = player.Facing;
        _bounce = direction;
        _scale = 1f;

        // Take over the player's visual components
        Add(_hair = player.Hair);
        Add(_sprite = player.Sprite);
        Add(_light = player.Light);

        _sprite.Color = Color.White;
        _initialHairColor = _hair.Color;

        // Start the death animation
        Add(new Coroutine(DeathRoutine()));
    }

    public override void Awake(Scene scene)
    {
        base.Awake(scene);

        if (_bounce != Vector2.Zero)
        {
            if (Math.Abs(_bounce.X) > Math.Abs(_bounce.Y))
            {
                _sprite.Play("deadside");
                _facing = (Facings)(-(int)Math.Sign(_bounce.X));
            }
            else
            {
                var adjustedBounce = Calc.AngleToVector(
                    Calc.AngleApproach(_bounce.Angle(), new Vector2(-(float)_player.Facing, 0f).Angle(), 0.5f), 1f);

                if (adjustedBounce.Y < 0f)
                    _sprite.Play("deadup");
                else
                    _sprite.Play("deaddown");
            }
        }
    }

    private IEnumerator DeathRoutine()
    {
        if (_bounce != Vector2.Zero)
        {
            Audio.Play("event:/char/madeline/predeath", Position);
            _scale = 1.5f;
            Celeste.Freeze(0.05f);
            yield return null;

            var from = Position;
            var to = from + _bounce * 24f;

            var tween = Tween.Create(Tween.TweenMode.Oneshot, Ease.CubeOut, 0.5f, start: true);
            tween.OnUpdate = t =>
            {
                Position = from + (to - from) * t.Eased;
                _scale = 1.5f - t.Eased * 0.5f;
                _sprite.Rotation = (float)(Math.Floor(t.Eased * 4f) * Math.PI * 2.0);
            };
            Add(tween);

            yield return tween.Duration * 0.75f;
            tween.Stop();
        }

        Position += Vector2.UnitY * -5f;

        if (Scene is Level level)
        {
            level.Displacement.AddBurst(Position, 0.3f, 0f, 80f, 1f);
            level.Shake(0.3f);
        }

        Input.Rumble(RumbleStrength.Strong, RumbleLength.Long);
        Audio.Play("event:/char/madeline/death", Position);

        _deathEffect = new DeathEffect(_initialHairColor, Center - Position);
        _deathEffect.OnUpdate = f => _light.Alpha = 1f - f;
        Add(_deathEffect);

        yield return _deathEffect.Duration * 0.65f;

        // Animation complete - don't reload, just notify
        End();
    }

    private void End()
    {
        if (_finished) return;
        _finished = true;

        UmcLogger.Info($"Death animation complete for {UmcPlayer?.Name}");
        OnDeathComplete?.Invoke();

        // Don't remove self immediately - keep the body visible for postmortem features
        // RemoveSelf();
    }

    public override void Update()
    {
        base.Update();
        _hair.Color = _sprite.CurrentAnimationFrame == 0 ? Color.White : _initialHairColor;
    }

    public override void Render()
    {
        if (_deathEffect == null)
        {
            _sprite.Scale.X = (float)_facing * _scale;
            _sprite.Scale.Y = _scale;
            _hair.Facing = _facing;
            base.Render();
        }
        else
        {
            _deathEffect.Render();
        }
    }

    /// <summary>
    /// Removes this dead body from the scene.
    /// </summary>
    public void Cleanup()
    {
        RemoveSelf();
    }
}

