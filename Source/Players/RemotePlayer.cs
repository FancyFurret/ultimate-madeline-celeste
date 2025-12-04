using System;
using System.Collections.Generic;
using Celeste.Mod.SkinModHelper;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Entity representing a remote player in multiplayer. Handles rendering, interpolation, and effects.
/// </summary>
[Tracked]
public class RemotePlayer : Entity
{
    public const int MaxHairLength = 30;

    public UmcPlayer UmcPlayer { get; }
    public PlayerSprite Sprite { get; private set; }
    public PlayerHair Hair { get; private set; }
    public Facings Facing { get; set; } = Facings.Right;
    public Vector2 Speed { get; set; }
    public Color[] HairColors { get; set; } = new[] { Color.White };
    public bool Dead { get; set; }
    public bool Dashing { get; set; }
    public bool DashWasB { get; set; }
    public Vector2? DashDir { get; set; }
    public PlayerGraphicsData Graphics { get; private set; }
    public float Alpha { get; set; } = 0.875f;
    public float InterpolationSpeed { get; set; } = 20f;
    public float SnapDistanceThreshold { get; set; } = 100f;
    public bool EnableInterpolation { get; set; } = true;
    public bool EnablePrediction { get; set; } = true;

    private Vector2 _targetPosition;
    private Vector2 _previousNetworkPosition;
    private float _timeSinceUpdate;
    private float _dashTrailTimer;
    private const float DashTrailInterval = 0.1f;
    private bool _wasDashing;

    public RemotePlayer(UmcPlayer umcPlayer, Vector2 position) : base(position)
    {
        UmcPlayer = umcPlayer;
        Depth = 0;
        Collider = new Hitbox(8f, 11f, -4f, -11f);

        CreateSprite(PlayerSpriteMode.Madeline);
        Add(new VertexLight(new Vector2(0f, -8f), Color.White, 1f, 32, 64));
        Tag = Tags.Persistent | Tags.PauseUpdate | Tags.TransitionUpdate;
    }

    private void CreateSprite(PlayerSpriteMode mode)
    {
        try { Sprite = new PlayerSprite(mode); }
        catch { Sprite = new PlayerSprite(PlayerSpriteMode.Madeline); }

        Add(Hair = new PlayerHair(Sprite));
        Add(Sprite);
        Hair.Color = Player.NormalHairColor;

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

        // Apply current skin if already set
        if (!string.IsNullOrEmpty(UmcPlayer.SkinId))
            ApplySkin(UmcPlayer.SkinId);

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
        _timeSinceUpdate += Engine.DeltaTime;

        if (EnableInterpolation)
        {
            Vector2 targetPos = _targetPosition;

            if (EnablePrediction && Speed != Vector2.Zero)
            {
                float predictionTime = Math.Min(_timeSinceUpdate, 0.1f);
                targetPos += Speed * predictionTime;
            }

            float distanceToTarget = Vector2.Distance(Position, targetPos);

            if (distanceToTarget > SnapDistanceThreshold)
                Position = targetPos;
            else if (distanceToTarget > 0.5f)
            {
                float t = 1f - (float)Math.Exp(-InterpolationSpeed * Engine.DeltaTime);
                Position = Vector2.Lerp(Position, targetPos, t);
            }
        }

        if (Scene is Level level)
        {
            UpdateDashEffects(level);

            if (!(level.GetUpdateHair() ?? true))
                Hair.AfterUpdate();
        }
    }

    private void UpdateDashEffects(Level level)
    {
        bool wasDashing = _wasDashing;
        _wasDashing = Dashing;

        if (!wasDashing && Dashing)
            OnDashStart(level);

        if (!Dashing || DashDir == null || Speed == Vector2.Zero)
        {
            _dashTrailTimer = 0f;
            return;
        }

        if (level.OnRawInterval(0.02f))
        {
            var particleType = DashWasB ? Player.P_DashB : Player.P_DashA;
            var particlePos = Center + Calc.Random.Range(Vector2.One * -2f, Vector2.One * 2f);
            level.ParticlesFG.Emit(particleType, particlePos, DashDir.Value.Angle());
        }

        _dashTrailTimer += Engine.DeltaTime;
        if (_dashTrailTimer >= DashTrailInterval)
        {
            _dashTrailTimer -= DashTrailInterval;
            CreateDashTrail();
        }
    }

    private void OnDashStart(Level level)
    {
        UmcLogger.Info("STARTING DASH");
        level.Displacement.AddBurst(Center, 0.4f, 8f, 64f, 0.5f, Ease.QuadOut, Ease.QuadOut);
        CreateDashTrail();
    }

    private void CreateDashTrail()
    {
        if (Scene is not Level level) return;

        var trailManager = level.Tracker.GetEntity<TrailManager>();
        if (trailManager == null) return;

        Color trailColor = HairColors.Length > 0 ? HairColors[0] : Hair.Color;

        TrailManager.Add(Position, Sprite, Hair, Sprite.Scale, trailColor, Depth + 1, 1f, false, false);
    }

    public override void Render()
    {
        if (!Visible) return;
        base.Render();
    }

    public void UpdateGeneric(Vector2 pos, Vector2 scale, Color color, Facings facing, Vector2 speed)
    {
        _previousNetworkPosition = _targetPosition;
        _targetPosition = pos;
        _timeSinceUpdate = 0f;

        if (!EnableInterpolation || _previousNetworkPosition == Vector2.Zero)
        {
            Position = pos;
            _previousNetworkPosition = pos;
        }

        if (Math.Abs(scale.X) < 0.01f) scale.X = 1f;
        if (Math.Abs(scale.Y) < 0.01f) scale.Y = 1f;

        scale.X = scale.X > 0 ? Calc.Clamp(scale.X, 0.5f, 1.5f) : Calc.Clamp(scale.X, -1.5f, -0.5f);
        scale.Y = scale.Y > 0 ? Calc.Clamp(scale.Y, 0.5f, 1.5f) : Calc.Clamp(scale.Y, -1.5f, -0.5f);

        Sprite.Scale = scale;
        Sprite.Scale.X *= (float)facing;
        Sprite.Color = color * Alpha;

        Facing = facing;
        Hair.Facing = facing;
        Speed = speed;
    }

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

    public void ApplyFrame(PlayerFrameMessage frame)
    {
        UpdateGeneric(frame.Position, frame.Scale, frame.SpriteColor, frame.Facing, frame.Speed);
        UpdateAnimation(frame.CurrentAnimationID, frame.CurrentAnimationFrame);
        UpdateHair(frame.Facing, frame.HairColors, frame.HairSimulateMotion);
        Dead = frame.Dead;
        Dashing = frame.Dashing;
        DashWasB = frame.DashWasB;
        DashDir = frame.DashDir;
    }

    public void UpdateGraphics(PlayerGraphicsMessage graphics)
    {
        Depth = graphics.Depth + 1;
        Sprite.Rate = graphics.SpriteRate;

        if (Sprite.Mode != graphics.SpriteMode)
            SetSpriteMode(graphics.SpriteMode);

        Graphics.AnimationIdToName.Clear();
        Graphics.AnimationNameToId.Clear();
        for (int i = 0; i < graphics.Animations.Length; i++)
        {
            Graphics.AnimationIdToName[i] = graphics.Animations[i];
            Graphics.AnimationNameToId[graphics.Animations[i]] = i;
        }

        Sprite.HairCount = graphics.HairCount;

        while (Hair.Nodes.Count < graphics.HairCount)
            Hair.Nodes.Add(Hair.Nodes.Count > 0 ? Hair.Nodes[^1] : Vector2.Zero);
        while (Hair.Nodes.Count > graphics.HairCount)
            Hair.Nodes.RemoveAt(Hair.Nodes.Count - 1);
    }

    public void SetSpriteMode(PlayerSpriteMode mode)
    {
        string currentAnim = Sprite?.CurrentAnimationID ?? "idle";

        Remove(Sprite);
        Remove(Hair);

        try { Sprite = new PlayerSprite(mode); }
        catch { Sprite = new PlayerSprite(PlayerSpriteMode.Madeline); }

        if (Sprite.Has(currentAnim)) Sprite.Play(currentAnim);
        else Sprite.Play("idle");

        Add(Sprite);
        Hair = new PlayerHair(Sprite);
        Add(Hair);

        BuildAnimationMap();
    }

    public bool ApplySkin(string skinId)
    {
        if (string.IsNullOrEmpty(skinId) || skinId == SkinsSystem.DEFAULT) return false;

        try
        {
            // Get the character ID from SkinModHelper's skin config
            if (SkinsSystem.skinConfigs == null ||
                !SkinsSystem.skinConfigs.TryGetValue(skinId, out var config) ||
                string.IsNullOrEmpty(config.Character_ID))
            {
                UmcLogger.Warn($"Skin config for '{skinId}' not found or has no Character_ID");
                return false;
            }

            string characterId = config.Character_ID;
            if (!GFX.SpriteBank.Has(characterId))
            {
                UmcLogger.Warn($"Character ID '{characterId}' not found in sprite bank");
                return false;
            }

            string currentAnim = Sprite?.CurrentAnimationID ?? "idle";

            Remove(Sprite);
            Remove(Hair);

            Sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
            GFX.SpriteBank.CreateOn(Sprite, characterId);

            if (Sprite.Has(currentAnim))
                Sprite.Play(currentAnim);

            Add(Sprite);
            Hair = new PlayerHair(Sprite);
            Add(Hair);

            BuildAnimationMap();

            UmcLogger.Info($"Applied skin '{skinId}' (character: {characterId}) to remote player {UmcPlayer.Name}");
            return true;
        }
        catch (Exception ex)
        {
            UmcLogger.Error($"Failed to apply skin '{skinId}' to remote player: {ex.Message}");
            return false;
        }
    }
}

public class PlayerGraphicsData
{
    public Dictionary<int, string> AnimationIdToName { get; } = new();
    public Dictionary<string, int> AnimationNameToId { get; } = new(StringComparer.OrdinalIgnoreCase);
}

internal static class LevelExtensions
{
    public static bool? GetUpdateHair(this Level level) => level.FrozenOrPaused ? false : null;
}
