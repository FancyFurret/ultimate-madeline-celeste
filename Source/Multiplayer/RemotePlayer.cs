using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Core;
using Celeste.Mod.UltimateMadelineCeleste.Networking;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Multiplayer;

/// <summary>
/// Represents a remote player in a networked multiplayer session.
/// </summary>
public class RemotePlayer : Entity
{
    public const int MaxHairLength = 30;
    
    /// <summary>
    /// The UmcPlayer data for this remote player.
    /// </summary>
    public UmcPlayer UmcPlayer { get; }

    /// <summary>
    /// The player sprite for rendering.
    /// </summary>
    public PlayerSprite Sprite { get; private set; }

    /// <summary>
    /// The player hair for rendering.
    /// </summary>
    public PlayerHair Hair { get; private set; }

    /// <summary>
    /// Current facing direction.
    /// </summary>
    public Facings Facing { get; set; } = Facings.Right;

    /// <summary>
    /// Current speed (for particle effects and prediction).
    /// </summary>
    public Vector2 Speed { get; set; }
    
    // ========================================================================
    // Interpolation state
    // ========================================================================
    
    /// <summary>
    /// Target position from network (where we're interpolating towards).
    /// </summary>
    private Vector2 _targetPosition;
    
    /// <summary>
    /// Previous network position (for velocity estimation).
    /// </summary>
    private Vector2 _previousNetworkPosition;
    
    /// <summary>
    /// Time since last network update (for prediction).
    /// </summary>
    private float _timeSinceUpdate;
    
    /// <summary>
    /// Interpolation smoothing factor (higher = snappier, lower = smoother).
    /// </summary>
    public float InterpolationSpeed { get; set; } = 20f;
    
    /// <summary>
    /// Distance threshold to snap position instead of interpolating.
    /// </summary>
    public float SnapDistanceThreshold { get; set; } = 100f;
    
    /// <summary>
    /// Whether to enable interpolation (disable for debugging).
    /// </summary>
    public bool EnableInterpolation { get; set; } = true;
    
    /// <summary>
    /// Whether to use velocity prediction between updates.
    /// </summary>
    public bool EnablePrediction { get; set; } = true;
    
    /// <summary>
    /// Hair colors array for each segment.
    /// </summary>
    public Color[] HairColors { get; set; } = new[] { Color.White };
    
    /// <summary>
    /// Whether the player is dead.
    /// </summary>
    public bool Dead { get; set; }
    
    /// <summary>
    /// Whether the player is currently dashing.
    /// </summary>
    public bool Dashing { get; set; }
    
    /// <summary>
    /// Whether the dash was the "B" type (affects particle color).
    /// </summary>
    public bool DashWasB { get; set; }
    
    /// <summary>
    /// Direction of the dash (for particle emission).
    /// </summary>
    public Vector2? DashDir { get; set; }
    
    /// <summary>
    /// Timer for dash trail spawning.
    /// </summary>
    private float _dashTrailTimer;
    
    /// <summary>
    /// Interval between dash trail spawns.
    /// </summary>
    private const float DashTrailInterval = 0.1f;
    
    /// <summary>
    /// Was dashing last frame (for detecting dash start).
    /// </summary>
    private bool _wasDashing;
    
    /// <summary>
    /// Graphics data for this player (animation mappings, etc).
    /// </summary>
    public PlayerGraphicsData Graphics { get; private set; }
    
    /// <summary>
    /// Alpha for rendering.
    /// </summary>
    public float Alpha { get; set; } = 0.875f;

    public RemotePlayer(UmcPlayer umcPlayer, Vector2 position) : base(position)
    {
        UmcPlayer = umcPlayer;

        // Same depth as regular player
        Depth = 0;

        // Set up collider similar to player (for visual reference only)
        Collider = new Hitbox(8f, 11f, -4f, -11f);

        CreateSprite(PlayerSpriteMode.Madeline);

        // Add a light like the player has
        Add(new VertexLight(new Vector2(0f, -8f), Color.White, 1f, 32, 64));
        
        // Make sure we update even during pause/transitions
        Tag = Tags.Persistent | Tags.PauseUpdate | Tags.TransitionUpdate;
    }

    private void CreateSprite(PlayerSpriteMode mode)
    {
        try
        {
            Sprite = new PlayerSprite(mode);
        }
        catch
        {
            // Fallback if sprite mode fails
            Sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
        }
        
        Add(Hair = new PlayerHair(Sprite));
        Add(Sprite);
        Hair.Color = Player.NormalHairColor;
        
        // Initialize graphics data
        Graphics = new PlayerGraphicsData();
        BuildAnimationMap();
    }
    
    private void BuildAnimationMap()
    {
        Graphics.AnimationIdToName.Clear();
        Graphics.AnimationNameToId.Clear();
        
        int id = 0;
        foreach (var animKey in Sprite.Animations.Keys)
        {
            Graphics.AnimationIdToName[id] = animKey;
            Graphics.AnimationNameToId[animKey] = id;
            id++;
        }
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Hair.Start();
        UmcLogger.Info($"Remote player {UmcPlayer.Name} added to scene");
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        UmcLogger.Info($"Remote player {UmcPlayer.Name} removed from scene");
    }

    public override void Update()
    {
        base.Update();
        
        Visible = !Dead && Alpha > 0;
        
        // Track time since last network update
        _timeSinceUpdate += Engine.DeltaTime;
        
        // Interpolation / prediction
        if (EnableInterpolation)
        {
            Vector2 targetPos = _targetPosition;
            
            // Velocity-based prediction: extrapolate where the player should be
            if (EnablePrediction && Speed != Vector2.Zero)
            {
                // Predict ahead based on velocity and time since last update
                // Clamp prediction time to avoid runaway extrapolation
                float predictionTime = Math.Min(_timeSinceUpdate, 0.1f);
                targetPos += Speed * predictionTime;
            }
            
            float distanceToTarget = Vector2.Distance(Position, targetPos);
            
            if (distanceToTarget > SnapDistanceThreshold)
            {
                // Too far - snap to position
                Position = targetPos;
            }
            else if (distanceToTarget > 0.5f)
            {
                // Smooth interpolation with exponential ease-out
                // This catches up quickly when far, slows down when close
                float t = 1f - (float)Math.Exp(-InterpolationSpeed * Engine.DeltaTime);
                Position = Vector2.Lerp(Position, targetPos, t);
            }
            // else: already close enough, don't jitter
        }
        
        // Dash effects
        if (Scene is Level level)
        {
            UpdateDashEffects(level);
            
            // Update hair during pause
            if (!(level.GetUpdateHair() ?? true))
            {
                Hair.AfterUpdate();
            }
        }
    }
    
    /// <summary>
    /// Updates dash particles and trails.
    /// </summary>
    private void UpdateDashEffects(Level level)
    {
        bool wasDashing = _wasDashing;
        _wasDashing = Dashing;

        // On dash start - trigger distortion and camera effects
        if (!wasDashing && Dashing)
        {
            OnDashStart(level);
        }
        
        UmcLogger.Info($"UspdateDashEffects: {Dashing} {DashDir} {Speed}");
        if (!Dashing || DashDir == null || Speed == Vector2.Zero)
        {
            _dashTrailTimer = 0f;
            return;
        }
        
        // Emit dash particles at intervals
        if (level.OnRawInterval(0.02f))
        {
            var particleType = DashWasB ? Player.P_DashB : Player.P_DashA;
            var particlePos = Center + Calc.Random.Range(Vector2.One * -2f, Vector2.One * 2f);
            level.ParticlesFG.Emit(particleType, particlePos, DashDir.Value.Angle());
        }
        
        // Spawn dash trails (afterimages)
        _dashTrailTimer += Engine.DeltaTime;
        if (_dashTrailTimer >= DashTrailInterval)
        {
            _dashTrailTimer -= DashTrailInterval;
            CreateDashTrail();
        }
    }
    
    /// <summary>
    /// Called when the remote player starts a dash.
    /// </summary>
    private void OnDashStart(Level level)
    {
        // Displacement burst (the ripple/distortion effect)
        UmcLogger.Info($"OnDashStart: {DashDir}");
        level.Displacement.AddBurst(Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        CreateDashTrail();
    }
    
    /// <summary>
    /// Creates a dash trail (afterimage) at the current position.
    /// </summary>
    private void CreateDashTrail()
    {
        if (Scene is not Level level) return;
        
        var trailManager = level.Tracker.GetEntity<TrailManager>();
        if (trailManager == null) return;
        
        // Get hair color for trail
        Color trailColor = Hair.Color;
        if (HairColors.Length > 0)
            trailColor = HairColors[0];
        
        TrailManager.Add(
            Position,
            Sprite,
            Hair,
            Sprite.Scale,
            trailColor,
            Depth + 1,
            1f,       // duration
            false,    // frozen update
            false     // use raw delta time
        );
    }

    public override void Render()
    {
        if (!Visible) return;
        base.Render();
    }

    /// <summary>
    /// Updates position, scale, color, facing, and speed from frame data.
    /// </summary>
    public void UpdateGeneric(Vector2 pos, Vector2 scale, Color color, Facings facing, Vector2 speed)
    {
        // Store previous position for interpolation reference
        _previousNetworkPosition = _targetPosition;
        
        // Set target position for interpolation
        _targetPosition = pos;
        
        // Reset time since update (for prediction)
        _timeSinceUpdate = 0f;
        
        // If this is the first update or interpolation is disabled, snap directly
        if (!EnableInterpolation || _previousNetworkPosition == Vector2.Zero)
        {
            Position = pos;
            _previousNetworkPosition = pos;
        }
        
        // Clamp scale and handle zero/near-zero values
        if (Math.Abs(scale.X) < 0.01f) scale.X = 1f;
        if (Math.Abs(scale.Y) < 0.01f) scale.Y = 1f;
        
        if (scale.X > 0.0f)
            scale.X = Calc.Clamp(scale.X, 0.5f, 1.5f);
        else
            scale.X = Calc.Clamp(scale.X, -1.5f, -0.5f);
            
        if (scale.Y > 0.0f)
            scale.Y = Calc.Clamp(scale.Y, 0.5f, 1.5f);
        else
            scale.Y = Calc.Clamp(scale.Y, -1.5f, -0.5f);
        
        Sprite.Scale = scale;
        Sprite.Scale.X *= (float)facing;
        Sprite.Color = color * Alpha;
        
        Facing = facing;
        Hair.Facing = facing;
        Speed = speed;
    }

    /// <summary>
    /// Updates animation from ID and frame.
    /// </summary>
    public void UpdateAnimation(int animationID, int animationFrame)
    {
        if (!Graphics.AnimationIdToName.TryGetValue(animationID, out string animName))
            return;
            
        if (animName != null && Sprite.Animations.ContainsKey(animName))
        {
            if (Sprite.CurrentAnimationID != animName)
                Sprite.Play(animName);
            Sprite.SetAnimationFrame(animationFrame);
        }
    }
    
    /// <summary>
    /// Updates animation using string ID (for legacy/fallback).
    /// </summary>
    public void UpdateAnimation(string animationName, int animationFrame)
    {
        if (string.IsNullOrEmpty(animationName)) return;
        
        if (Sprite.Animations.ContainsKey(animationName))
        {
            if (Sprite.CurrentAnimationID != animationName)
                Sprite.Play(animationName);
            if (animationFrame >= 0 && animationFrame < Sprite.CurrentAnimationTotalFrames)
                Sprite.SetAnimationFrame(animationFrame);
        }
    }

    /// <summary>
    /// Updates hair colors and simulation.
    /// </summary>
    public void UpdateHair(Facings facing, Color[] colors, bool simulateMotion)
    {
        if (colors.Length <= 0)
            colors = new[] { Player.NormalHairColor };
        else
            Hair.Color = colors[0];
        
        Hair.Facing = facing;
        HairColors = colors;
        Hair.Alpha = Alpha;
        Hair.SimulateMotion = simulateMotion;
    }

    /// <summary>
    /// Updates dead state.
    /// </summary>
    public void UpdateDead(bool dead)
    {
        Dead = dead;
    }

    /// <summary>
    /// Applies a full frame message from network.
    /// </summary>
    public void ApplyFrame(PlayerFrameMessage frame)
    {
        UpdateGeneric(frame.Position, frame.Scale, frame.SpriteColor, frame.Facing, frame.Speed);
        UpdateAnimation(frame.CurrentAnimationID, frame.CurrentAnimationFrame);
        UpdateHair(frame.Facing, frame.HairColors, frame.HairSimulateMotion);
        UpdateDead(frame.Dead);
        UpdateDash(frame.Dashing, frame.DashWasB, frame.DashDir);
    }
    
    /// <summary>
    /// Updates dash state.
    /// </summary>
    public void UpdateDash(bool dashing, bool dashWasB, Vector2? dashDir)
    {
        Dashing = dashing;
        DashWasB = dashWasB;
        DashDir = dashDir;
    }
    
    /// <summary>
    /// Updates graphics from a graphics message (sprite mode, animations, etc).
    /// </summary>
    public void UpdateGraphics(PlayerGraphicsMessage graphics)
    {
        // Update depth
        Depth = graphics.Depth + 1;
        
        // Update sprite rate
        Sprite.Rate = graphics.SpriteRate;
        
        // Update sprite mode if changed
        if (Sprite.Mode != graphics.SpriteMode)
        {
            SetSpriteMode(graphics.SpriteMode);
        }
        
        // Update animation map from server
        Graphics.AnimationIdToName.Clear();
        Graphics.AnimationNameToId.Clear();
        for (int i = 0; i < graphics.Animations.Length; i++)
        {
            Graphics.AnimationIdToName[i] = graphics.Animations[i];
            Graphics.AnimationNameToId[graphics.Animations[i]] = i;
        }
        
        // Update hair count
        Sprite.HairCount = graphics.HairCount;
        
        // Ensure hair nodes match count
        while (Hair.Nodes.Count < graphics.HairCount)
            Hair.Nodes.Add(Hair.Nodes.Count > 0 ? Hair.Nodes[^1] : Vector2.Zero);
        while (Hair.Nodes.Count > graphics.HairCount)
            Hair.Nodes.RemoveAt(Hair.Nodes.Count - 1);
    }

    /// <summary>
    /// Changes the sprite mode (skin) for this remote player.
    /// </summary>
    public void SetSpriteMode(PlayerSpriteMode mode)
    {
        string currentAnim = Sprite?.CurrentAnimationID ?? "idle";
        
        Remove(Sprite);
        Remove(Hair);

        try
        {
            Sprite = new PlayerSprite(mode);
        }
        catch
        {
            Sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
        }
        
        if (Sprite.Has(currentAnim))
            Sprite.Play(currentAnim);
        else
            Sprite.Play("idle");
            
        Add(Sprite);

        Hair = new PlayerHair(Sprite);
        Add(Hair);
        
        BuildAnimationMap();
    }

    /// <summary>
    /// Sets a custom skin by sprite name (for SkinModHelper integration).
    /// </summary>
    public void SetCustomSkin(string spriteName)
    {
        if (string.IsNullOrEmpty(spriteName)) return;

        try
        {
            if (GFX.SpriteBank.Has(spriteName))
            {
                string currentAnim = Sprite?.CurrentAnimationID ?? "idle";
                
                Remove(Sprite);
                Remove(Hair);

                Sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
                GFX.SpriteBank.CreateOn(Sprite, spriteName);
                
                if (Sprite.Has(currentAnim))
                    Sprite.Play(currentAnim);
                    
                Add(Sprite);

                Hair = new PlayerHair(Sprite);
                Add(Hair);
                
                BuildAnimationMap();

                UmcLogger.Info($"Applied custom skin '{spriteName}' to remote player {UmcPlayer.Name}");
            }
        }
        catch (Exception ex)
        {
            UmcLogger.Error($"Failed to apply skin '{spriteName}' to remote player: {ex.Message}");
        }
    }
}

/// <summary>
/// Cached graphics data for a remote player.
/// </summary>
public class PlayerGraphicsData
{
    public Dictionary<int, string> AnimationIdToName { get; } = new();
    public Dictionary<string, int> AnimationNameToId { get; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// Helper extension for Level to check if hair should update.
/// </summary>
internal static class LevelExtensions
{
    public static bool? GetUpdateHair(this Level level)
    {
        // During certain states (pause, overlay), hair shouldn't auto-update
        return level.FrozenOrPaused ? false : null;
    }
}

