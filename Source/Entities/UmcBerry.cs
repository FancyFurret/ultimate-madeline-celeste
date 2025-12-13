using System;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// A custom berry entity for UMC that can be picked up, dropped on death, and collected at the goal.
/// Uses Celeste's Follower system and Strawberry visuals.
/// </summary>
[Tracked]
public class UmcBerry : Entity
{
    public static event Action<UmcBerry, UmcPlayer> OnBerryCollected;

    public Vector2 SpawnPosition { get; set; }
    public UmcPlayer Carrier { get; private set; }
    public bool IsCollected { get; private set; }
    public bool IsCarried => Follower.HasLeader;

    // Use Celeste's built-in Follower component
    public Follower Follower { get; private set; }

    private Sprite _sprite;
    private Wiggler _wiggler;
    private BloomPoint _bloom;
    private VertexLight _light;
    private float _wobble;

    public UmcBerry(Vector2 position) : base(position)
    {
        SpawnPosition = position;
        Collider = new Hitbox(14f, 14f, -7f, -7f);
        Depth = Depths.Pickups;

        // Use the same sprite as Strawberry
        Add(_sprite = GFX.SpriteBank.Create("strawberry"));
        _sprite.Play("idle");

        // Use Celeste's Follower component for smooth following
        Add(Follower = new Follower(onLoseLeader: OnLoseLeader));
        Follower.FollowDelay = 0.3f;

        // Standard strawberry visual effects
        Add(_wiggler = Wiggler.Create(0.4f, 4f, v => _sprite.Scale = Vector2.One * (1f + v * 0.35f)));
        Add(_bloom = new BloomPoint(1f, 12f));
        Add(_light = new VertexLight(Color.White, 1f, 16, 24));

        // Player collision for pickup
        Add(new PlayerCollider(OnPlayer));
    }

    public override void Update()
    {
        base.Update();

        if (IsCollected) return;

        // Floating animation when not following
        if (!IsCarried)
        {
            _wobble += Engine.DeltaTime * 4f;
            _sprite.Y = _bloom.Y = _light.Y = (float)Math.Sin(_wobble) * 2f;
        }
    }

    private void OnPlayer(Player player)
    {
        // Don't pick up if already following someone or collected
        if (IsCarried || IsCollected) return;

        var spawner = PlayerSpawner.Instance;
        var umcPlayer = spawner?.GetUmcPlayer(player);
        if (umcPlayer == null || umcPlayer.InDanceMode) return;

        Pickup(player, umcPlayer);
    }

    private void Pickup(Player player, UmcPlayer umcPlayer)
    {
        Carrier = umcPlayer;
        player.Leader.GainFollower(Follower);
        _wiggler.Start();
        Depth = Depths.Top;
        Audio.Play("event:/game/general/strawberry_touch", Position);
        UmcLogger.Info($"Player {umcPlayer.Name} picked up berry");
    }

    private void OnLoseLeader()
    {
        // This is called when the player dies or the follower chain breaks
        // Drop at current position (where berry was last visible, not where player died)
        if (IsCollected) return;

        UmcLogger.Info($"Berry OnLoseLeader: dropping at {Position}, Carrier={Carrier?.Name}");

        // Drop at current position (last visible location)
        Carrier = null;
        Depth = Depths.Pickups;
        Collidable = true;
        _wiggler.Start();
    }

    /// <summary>
    /// Forces the berry to detach from its carrier and drop at the given position.
    /// </summary>
    public void Drop(Vector2 dropPosition)
    {
        if (IsCollected) return;

        UmcLogger.Info($"Berry dropped at {dropPosition} (was carried by {Carrier?.Name})");

        // Remove from follower chain if still attached
        Follower.Leader?.LoseFollower(Follower);
        Carrier = null;

        Position = dropPosition;
        Depth = Depths.Pickups;
        Collidable = true;
        _wiggler.Start();
    }

    /// <summary>
    /// Collects the berry permanently when the carrier reaches the goal.
    /// </summary>
    public void Collect(UmcPlayer collector)
    {
        if (IsCollected) return;

        IsCollected = true;

        // Detach from follower
        Follower.Leader?.LoseFollower(Follower);
        Carrier = null;

        // Play collection effects (same as Strawberry)
        Audio.Play("event:/game/general/strawberry_get", Position, "colour", 0f, "count", 0f);
        _sprite.Play("collect");

        // Spawn particles
        if (Scene is Level level)
        {
            level.ParticlesFG.Emit(Strawberry.P_Glow, 8, Position, Vector2.One * 6f);
        }

        UmcLogger.Info($"Player {collector.Name} collected berry!");
        OnBerryCollected?.Invoke(this, collector);

        // Remove after collect animation
        Add(new Coroutine(CollectRoutine()));
    }

    private System.Collections.IEnumerator CollectRoutine()
    {
        // Wait for collect animation to finish
        while (_sprite.Animating)
            yield return null;

        RemoveSelf();
    }

    public static void ClearHandlers()
    {
        OnBerryCollected = null;
    }
}
