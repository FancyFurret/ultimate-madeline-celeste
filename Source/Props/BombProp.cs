using System;
using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Props;

/// <summary>
/// Bomb prop that destroys other props in its explosion radius when placed.
/// Variants: 1x1 (8px), 3x3 (24px), 5x5 (40px).
/// </summary>
public class BombProp : Prop
{
    private readonly string _id;
    private readonly string _name;
    private readonly int _explosionSize;
    private readonly string _texturePath;

    public override string Id => _id;
    public override string Name => _name;
    public override PropCategory Category => PropCategory.Bomb;

    /// <summary>
    /// The explosion size in pixels.
    /// </summary>
    public int ExplosionSize => _explosionSize;

    /// <summary>
    /// The texture path for this bomb variant.
    /// </summary>
    public string TexturePath => _texturePath;

    public BombProp(string id, string name, int explosionSize, string texturePath)
    {
        _id = id;
        _name = name;
        _explosionSize = explosionSize;
        _texturePath = texturePath;
    }

    public override SpriteInfo GetSprite(float rotation = 0f)
    {
        // The visual size matches the explosion area
        return SpriteInfo.Custom(_explosionSize, _explosionSize, Vector2.Zero);
    }

    /// <summary>
    /// Bombs should not be registered as placed props since they destroy themselves.
    /// </summary>
    public override bool SkipRegistration => true;

    protected override Entity BuildEntity(Vector2 position, float rotation, bool mirrorX, bool mirrorY)
    {
        return new PlacedBomb(position, _explosionSize, _texturePath);
    }

    public override void OnPlaced(Entity entity)
    {
        // Start the visual explosion effect (flash + removal)
        // The actual prop destruction is handled by PlacingPhase via network
        if (entity is PlacedBomb bomb)
        {
            bomb.StartExplosionEffect();
        }
    }

    /// <summary>
    /// Calculates which prop indices should be destroyed by a bomb at the given position.
    /// Called by PlacingPhase on the host to determine destruction.
    /// </summary>
    public static List<int> CalculateDestroyedPropIndices(Vector2 bombPosition, int explosionSize)
    {
        var indices = new List<int>();
        var roundState = RoundState.Current;
        if (roundState == null) return indices;

        var explosionBounds = new Rectangle(
            (int)bombPosition.X,
            (int)bombPosition.Y,
            explosionSize,
            explosionSize
        );

        for (int i = 0; i < roundState.PlacedProps.Count; i++)
        {
            var placedProp = roundState.PlacedProps[i];
            if (placedProp.Entity == null) continue;
            if (placedProp.Entity.Scene == null) continue;

            var propBounds = GetPropBounds(placedProp);

            if (explosionBounds.Intersects(propBounds))
            {
                indices.Add(i);
            }
        }

        return indices;
    }

    /// <summary>
    /// Gets the placed props that would be affected by a bomb at the given position.
    /// Used for preview highlighting during placement.
    /// </summary>
    public static List<(PlacedProp prop, Rectangle bounds)> GetAffectedProps(Vector2 bombPosition, int explosionSize)
    {
        var result = new List<(PlacedProp, Rectangle)>();
        var roundState = RoundState.Current;
        if (roundState == null) return result;

        var explosionBounds = new Rectangle(
            (int)bombPosition.X,
            (int)bombPosition.Y,
            explosionSize,
            explosionSize
        );

        foreach (var placedProp in roundState.PlacedProps)
        {
            if (placedProp.Entity == null) continue;
            if (placedProp.Entity.Scene == null) continue;

            var propBounds = GetPropBounds(placedProp);

            if (explosionBounds.Intersects(propBounds))
            {
                result.Add((placedProp, propBounds));
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the bounding rectangle of a placed prop using its sprite info.
    /// The prop's GetSprite() provides accurate size and offset information.
    /// </summary>
    public static Rectangle GetPropBounds(PlacedProp placedProp)
    {
        var propInstance = placedProp.Prop;
        var prop = propInstance.Prop;
        var entity = placedProp.Entity;

        // Use the prop's sprite info for accurate bounds
        var spriteInfo = prop.GetSprite(propInstance.Rotation);
        var topLeft = entity.Position - spriteInfo.Offset;

        return new Rectangle(
            (int)topLeft.X,
            (int)topLeft.Y,
            (int)spriteInfo.Width,
            (int)spriteInfo.Height
        );
    }
}

/// <summary>
/// A bomb entity that shows explosion radius preview and plays effects when placed.
/// Actual prop destruction is handled by the network layer.
/// </summary>
public class PlacedBomb : Entity
{
    private readonly int _size;
    private readonly string _texturePath;
    private MTexture _bombTexture;

    // Explosion state machine
    private enum ExplosionState { Preview, Flash, Expanding, Confetti, Done }
    private ExplosionState _state = ExplosionState.Preview;
    private float _stateTimer;

    // Timing
    private const float FlashDuration = 0.1f;
    private const float ExpandDuration = 0.15f;
    private const float ConfettiDuration = 0.35f;

    // Visual effects
    private float _expandScale;
    private readonly List<ConfettiParticle> _confetti = new();
    private static readonly Color[] ConfettiColors = {
        Calc.HexToColor("ff2200"), // Deep red
        Calc.HexToColor("ff4400"), // Red-orange
        Calc.HexToColor("ff6600"), // Orange
        Calc.HexToColor("ff8800"), // Orange-yellow
        Calc.HexToColor("ffaa00"), // Golden orange
        Calc.HexToColor("ffcc00"), // Yellow-orange
        Calc.HexToColor("ffdd44"), // Bright yellow
        Calc.HexToColor("ffffff"), // White hot
    };

    /// <summary>
    /// The explosion size in pixels.
    /// </summary>
    public int Size => _size;

    public PlacedBomb(Vector2 position, int size, string texturePath) : base(position)
    {
        _size = size;
        _texturePath = texturePath;
        Collider = new Hitbox(size, size);
        Depth = -9999; // Render on top during preview

        _bombTexture = GFX.Game[texturePath];
    }

    public override void Update()
    {
        base.Update();

        switch (_state)
        {
            case ExplosionState.Flash:
                _stateTimer -= Engine.DeltaTime;
                if (_stateTimer <= 0f)
                {
                    _state = ExplosionState.Expanding;
                    _stateTimer = ExpandDuration;
                    _expandScale = 0f;
                }
                break;

            case ExplosionState.Expanding:
                _stateTimer -= Engine.DeltaTime;
                _expandScale = 1f - (_stateTimer / ExpandDuration);
                _expandScale = Ease.ExpoOut(_expandScale);

                if (_stateTimer <= 0f)
                {
                    _state = ExplosionState.Confetti;
                    _stateTimer = ConfettiDuration;
                    SpawnConfetti();
                }
                break;

            case ExplosionState.Confetti:
                _stateTimer -= Engine.DeltaTime;
                UpdateConfetti();

                if (_stateTimer <= 0f)
                {
                    _state = ExplosionState.Done;
                    RemoveSelf();
                }
                break;
        }
    }

    private void SpawnConfetti()
    {
        var center = Position + new Vector2(_size / 2f, _size / 2f);

        // Scale everything with bomb size
        float sizeMultiplier = _size / 8f; // 1x for small, 3x for medium, 5x for large
        int particleCount = (int)(15 * sizeMultiplier);
        float baseSpeed = 150f + sizeMultiplier * 50f;
        float speedVariance = 200f + sizeMultiplier * 100f;
        float baseLife = 0.12f + sizeMultiplier * 0.04f;

        for (int i = 0; i < particleCount; i++)
        {
            float angle = Calc.Random.NextFloat() * MathF.PI * 2f;
            float speed = baseSpeed + Calc.Random.NextFloat() * speedVariance;
            float size = 1f + Calc.Random.NextFloat() * (1f + sizeMultiplier * 0.5f);

            _confetti.Add(new ConfettiParticle
            {
                Position = center,
                Velocity = new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * speed,
                Color = Calc.Random.Choose(ConfettiColors),
                Size = size,
                Rotation = Calc.Random.NextFloat() * MathF.PI * 2f,
                RotationSpeed = Calc.Random.Range(-15f, 15f),
                Life = baseLife + Calc.Random.NextFloat() * 0.15f
            });
        }
    }

    private void UpdateConfetti()
    {
        float friction = 0.85f; // Heavy friction for quick stop

        foreach (var p in _confetti)
        {
            p.Velocity *= friction;
            p.Position += p.Velocity * Engine.DeltaTime;
            p.Rotation += p.RotationSpeed * Engine.DeltaTime;
            p.Life -= Engine.DeltaTime;
            p.Size *= 0.95f; // Shrink over time
        }

        _confetti.RemoveAll(p => p.Life <= 0f || p.Size < 0.5f);
    }

    /// <summary>
    /// Starts the visual explosion effect (sound, flash, then removal).
    /// Called after network confirms placement.
    /// </summary>
    public void StartExplosionEffect()
    {
        if (_state != ExplosionState.Preview) return;

        // Play explosion sound
        Audio.Play("event:/game/general/wall_break_stone", Position);

        // Screen shake based on bomb size
        float shakeAmount = _size / 16f;
        if (Scene is Level level)
        {
            level.Shake(shakeAmount * 0.1f);
        }

        _state = ExplosionState.Flash;
        _stateTimer = FlashDuration;
    }

    public override void Render()
    {
        base.Render();
        var center = Position + new Vector2(_size / 2f, _size / 2f);

        switch (_state)
        {
            case ExplosionState.Preview:
                RenderPreview();
                break;

            case ExplosionState.Flash:
                // Bright white flash
                float flashAlpha = _stateTimer / FlashDuration;
                Draw.Rect(Position.X - 4, Position.Y - 4, _size + 8, _size + 8, Color.White * flashAlpha);
                break;

            case ExplosionState.Expanding:
                // Expanding shockwave ring
                float ringRadius = (_size / 2f) * _expandScale * 1.5f;
                float ringThickness = 4f * (1f - _expandScale);
                float alpha = 1f - _expandScale;

                // Orange/yellow explosion colors
                var innerColor = Color.Lerp(Color.Yellow, Color.Orange, _expandScale) * alpha;
                var outerColor = Color.Lerp(Color.Orange, Color.Red, _expandScale) * alpha * 0.5f;

                // Draw expanding circles
                DrawCircle(center, ringRadius, outerColor, 32);
                DrawCircle(center, ringRadius * 0.7f, innerColor, 24);
                DrawCircle(center, ringRadius * 0.4f, Color.White * alpha * 0.8f, 16);
                break;

            case ExplosionState.Confetti:
                // Render confetti particles
                foreach (var p in _confetti)
                {
                    float alpha2 = MathHelper.Clamp(p.Life * 2f, 0f, 1f);
                    var color = p.Color * alpha2;

                    // Draw rotated rectangle
                    DrawRotatedRect(p.Position, p.Size, p.Size * 0.6f, p.Rotation, color);
                }
                break;
        }
    }

    private void RenderPreview()
    {
        // Pulsing red glow
        float pulse = (float)Math.Sin(Scene?.TimeActive * 6f ?? 0f) * 0.3f + 0.7f;

        // Red explosion area preview
        var redColor = new Color(255, 50, 50, (int)(100 * pulse));
        Draw.Rect(Position.X, Position.Y, _size, _size, redColor);

        // Red border
        var borderColor = new Color(255, 0, 0, (int)(200 * pulse));
        float borderWidth = 1f;
        Draw.Rect(Position.X, Position.Y, _size, borderWidth, borderColor);
        Draw.Rect(Position.X, Position.Y + _size - borderWidth, _size, borderWidth, borderColor);
        Draw.Rect(Position.X, Position.Y, borderWidth, _size, borderColor);
        Draw.Rect(Position.X + _size - borderWidth, Position.Y, borderWidth, _size, borderColor);

        // Render affected props with red highlight boxes
        RenderAffectedProps(pulse);

        // Bomb texture centered (bobbing slightly)
        float bob = (float)Math.Sin(Scene?.TimeActive * 4f ?? 0f) * 2f;
        var center = Position + new Vector2(_size / 2f, _size / 2f + bob);
        _bombTexture.DrawCentered(center);
    }

    private void RenderAffectedProps(float pulse)
    {
        var affected = BombProp.GetAffectedProps(Position, _size);

        foreach (var (prop, bounds) in affected)
        {
            // Semi-transparent red fill
            var fillColor = new Color(255, 50, 50, (int)(80 * pulse));
            Draw.Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height, fillColor);

            // Red border (same style as bomb preview)
            var borderColor = new Color(255, 0, 0, (int)(200 * pulse));
            Draw.HollowRect(bounds.X, bounds.Y, bounds.Width, bounds.Height, borderColor);
        }
    }

    private static void DrawCircle(Vector2 center, float radius, Color color, int segments)
    {
        if (radius <= 0) return;
        for (int i = 0; i < segments; i++)
        {
            float a1 = i / (float)segments * MathF.PI * 2f;
            float a2 = (i + 1) / (float)segments * MathF.PI * 2f;
            var p1 = center + new Vector2(MathF.Cos(a1), MathF.Sin(a1)) * radius;
            var p2 = center + new Vector2(MathF.Cos(a2), MathF.Sin(a2)) * radius;
            Draw.Line(p1, p2, color, 2f);
        }
    }

    private static void DrawRotatedRect(Vector2 pos, float width, float height, float rotation, Color color)
    {
        var cos = MathF.Cos(rotation);
        var sin = MathF.Sin(rotation);
        var hw = width / 2f;
        var hh = height / 2f;

        Vector2 Rotate(float x, float y) => new(
            pos.X + x * cos - y * sin,
            pos.Y + x * sin + y * cos
        );

        var tl = Rotate(-hw, -hh);
        var tr = Rotate(hw, -hh);
        var br = Rotate(hw, hh);
        var bl = Rotate(-hw, hh);

        Draw.Line(tl, tr, color);
        Draw.Line(tr, br, color);
        Draw.Line(br, bl, color);
        Draw.Line(bl, tl, color);
    }

    private class ConfettiParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public Color Color;
        public float Size;
        public float Rotation;
        public float RotationSpeed;
        public float Life;
    }
}
