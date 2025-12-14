using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Spawn payload for networked players.
/// </summary>
public class PlayerSpawnPayload : INetMessage
{
    public int SlotIndex { get; set; }
    public string SkinId { get; set; }
    public Vector2 Position { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)SlotIndex);
        writer.Write(SkinId ?? "");
        writer.Write(Position.X);
        writer.Write(Position.Y);
    }

    public void Deserialize(BinaryReader reader)
    {
        SlotIndex = reader.ReadByte();
        SkinId = reader.ReadString();
        Position = new Vector2(reader.ReadSingle(), reader.ReadSingle());
    }
}

/// <summary>
/// Frame update message sent via entity messaging.
/// </summary>
public class PlayerFramePayload : INetMessage
{
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
    public Color SpriteColor { get; set; } = Color.White;
    public Facings Facing { get; set; }
    public Vector2 Speed { get; set; }
    public int CurrentAnimationID { get; set; }
    public int CurrentAnimationFrame { get; set; }
    public Color[] HairColors { get; set; } = Array.Empty<Color>();
    public bool HairSimulateMotion { get; set; }
    public bool Dead { get; set; }
    public bool Dashing { get; set; }
    public bool DashWasB { get; set; }
    public Vector2? DashDir { get; set; }

    // Follower info - list of berry spawn positions this player is carrying
    public List<Vector2> FollowerBerries { get; set; } = new();

    public void Serialize(BinaryWriter writer)
    {
        PlayerFrameFlags flags = PlayerFrameFlags.None;
        if (Facing == Facings.Left) flags |= PlayerFrameFlags.FacingLeft;
        if (HairSimulateMotion) flags |= PlayerFrameFlags.HairSimulateMotion;
        if (Dead) flags |= PlayerFrameFlags.Dead;
        if (Dashing && DashDir.HasValue) { flags |= PlayerFrameFlags.Dashing; if (DashWasB) flags |= PlayerFrameFlags.DashB; }
        writer.Write((byte)flags);
        writer.WritePosition(Position);
        writer.WriteScale(Scale);
        writer.WriteColor(SpriteColor);
        writer.WriteSpeed(Speed);
        BinaryExtensions.Write7BitEncodedInt(writer, CurrentAnimationID);
        BinaryExtensions.Write7BitEncodedInt(writer, CurrentAnimationFrame);
        writer.WriteHairColors(HairColors);
        if ((flags & PlayerFrameFlags.Dashing) != 0 && DashDir.HasValue)
            writer.Write((byte)((Math.Atan2(DashDir.Value.Y, DashDir.Value.X) / (2 * Math.PI) * 256 + 256) % 256));

        // Write follower berries
        writer.Write((byte)FollowerBerries.Count);
        foreach (var pos in FollowerBerries)
        {
            writer.Write(pos.X);
            writer.Write(pos.Y);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        var flags = (PlayerFrameFlags)reader.ReadByte();
        Facing = (flags & PlayerFrameFlags.FacingLeft) != 0 ? Facings.Left : Facings.Right;
        HairSimulateMotion = (flags & PlayerFrameFlags.HairSimulateMotion) != 0;
        Dead = (flags & PlayerFrameFlags.Dead) != 0;
        Dashing = (flags & PlayerFrameFlags.Dashing) != 0;
        DashWasB = (flags & PlayerFrameFlags.DashB) != 0;
        Position = reader.ReadPosition();
        Scale = reader.ReadScale();
        SpriteColor = reader.ReadColor();
        Speed = reader.ReadSpeed();
        CurrentAnimationID = BinaryExtensions.Read7BitEncodedInt(reader);
        CurrentAnimationFrame = BinaryExtensions.Read7BitEncodedInt(reader);
        HairColors = reader.ReadHairColors();
        if (Dashing)
        {
            double angle = reader.ReadByte() / 256.0 * 2 * Math.PI;
            DashDir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }
        else
        {
            DashDir = null;
        }

        // Read follower berries
        int berryCount = reader.ReadByte();
        FollowerBerries = new List<Vector2>(berryCount);
        for (int i = 0; i < berryCount; i++)
        {
            FollowerBerries.Add(new Vector2(reader.ReadSingle(), reader.ReadSingle()));
        }
    }
}

/// <summary>
/// Graphics update message for animation maps etc.
/// </summary>
public class PlayerGraphicsPayload : INetMessage
{
    public int Depth { get; set; }
    public PlayerSpriteMode SpriteMode { get; set; }
    public float SpriteRate { get; set; } = 1f;
    public string[] Animations { get; set; } = Array.Empty<string>();
    public byte HairCount { get; set; }
    public Vector2[] HairScales { get; set; } = Array.Empty<Vector2>();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Depth);
        BinaryExtensions.Write7BitEncodedInt(writer, (int)SpriteMode);
        writer.Write(SpriteRate);
        BinaryExtensions.Write7BitEncodedInt(writer, Animations.Length);
        foreach (var anim in Animations) writer.Write(anim ?? "");
        writer.Write(HairCount);
        for (int i = 0; i < HairCount && i < HairScales.Length; i++) writer.WriteScale(HairScales[i]);
    }

    public void Deserialize(BinaryReader reader)
    {
        Depth = reader.ReadInt32();
        SpriteMode = (PlayerSpriteMode)BinaryExtensions.Read7BitEncodedInt(reader);
        SpriteRate = reader.ReadSingle();
        int animCount = BinaryExtensions.Read7BitEncodedInt(reader);
        Animations = new string[animCount];
        for (int i = 0; i < animCount; i++) Animations[i] = reader.ReadString();
        HairCount = reader.ReadByte();
        HairScales = new Vector2[HairCount];
        for (int i = 0; i < HairCount; i++) HairScales[i] = reader.ReadScale();
    }
}

/// <summary>
/// Component that provides networking for player entities (local and remote).
/// Handles automatic spawn/despawn broadcasting and frame updates.
/// </summary>
public class NetworkedPlayerComponent : Component
{
    private const float NetworkSyncInterval = 0.05f; // 20 Hz

    public UmcPlayer UmcPlayer { get; }
    public bool IsLocal => UmcPlayer?.IsLocal ?? false;

    private NetworkedEntity<PlayerSpawnPayload> _net;
    private float _networkSyncTimer;
    private Dictionary<string, int> _animationMap = new();

    // For remote players - attached berries that follow locally
    private readonly List<UmcBerry> _attachedBerries = new();

    public NetworkedPlayerComponent(UmcPlayer umcPlayer, Vector2 spawnPosition)
        : base(active: true, visible: false)
    {
        UmcPlayer = umcPlayer;

        // Set up networking component
        _net = new NetworkedEntity<PlayerSpawnPayload>()
            .Handle<PlayerFramePayload>(OnFrameReceived)
            .Handle<PlayerGraphicsPayload>(OnGraphicsReceived)
            .SetSpawnData(new PlayerSpawnPayload
            {
                SlotIndex = umcPlayer.SlotIndex,
                SkinId = umcPlayer.SkinId,
                Position = spawnPosition
            });
        _net.RemoveOnOwnerDisconnect = true;
    }

    /// <summary>
    /// Register the factory with NetworkedEntityRegistry. Call once during init.
    /// </summary>
    public static void RegisterFactory()
    {
        NetworkedEntityRegistry.Instance?.RegisterFactory<PlayerSpawnPayload>((networkId, ownerId, spawnData) =>
        {
            if (spawnData == null) return null;

            var session = GameSession.Instance;
            if (session == null) return null;

            var umcPlayer = session.Players.GetAtSlot(spawnData.SlotIndex);
            if (umcPlayer == null)
            {
                UmcLogger.Warn($"[NetworkedPlayer] Factory: player at slot {spawnData.SlotIndex} not found");
                return null;
            }

            // Don't create for local players - they manage their own entities
            if (umcPlayer.IsLocal) return null;

            // Create remote player
            var remotePlayer = new RemotePlayer(umcPlayer, spawnData.Position);
            var netComponent = new NetworkedPlayerComponent(umcPlayer, spawnData.Position);
            remotePlayer.Add(netComponent);

            // Apply skin
            if (!string.IsNullOrEmpty(spawnData.SkinId))
            {
                umcPlayer.SkinId = spawnData.SkinId;
                remotePlayer.ApplySkin(spawnData.SkinId);
            }

            // Register with PlayerSpawner
            PlayerSpawner.Instance?.RegisterRemotePlayer(umcPlayer, remotePlayer);

            // Track with camera
            CameraController.Instance?.TrackEntity(remotePlayer);

            // Attach life hearts
            int lives = RoundState.Current?.GetPlayerStats(umcPlayer)?.LivesRemaining ?? umcPlayer.MaxLives;
            LifeHeartManager.AttachToPlayer(remotePlayer, umcPlayer, lives);

            UmcLogger.Info($"[NetworkedPlayer] Factory created RemotePlayer for {umcPlayer.Name}");
            return remotePlayer;
        });

        UmcLogger.Info("[NetworkedPlayer] Factory registered");
    }

    public override void Added(Entity entity)
    {
        base.Added(entity);
        entity.Add(_net);
    }

    public override void Removed(Entity entity)
    {
        // Clean up attached berries
        foreach (var berry in _attachedBerries)
        {
            berry?.Drop(berry.Position);
        }
        _attachedBerries.Clear();

        base.Removed(entity);
    }

    public override void Update()
    {
        base.Update();

        if (IsLocal)
        {
            UpdateLocalPlayer();
        }
        else
        {
            UpdateRemoteFollowers();
        }
    }

    private void UpdateLocalPlayer()
    {
        _networkSyncTimer += Engine.DeltaTime;
        if (_networkSyncTimer >= NetworkSyncInterval)
        {
            _networkSyncTimer = 0f;
            BroadcastFrame();
        }
    }

    private void BroadcastFrame()
    {
        var player = Entity as Player;
        if (player?.Scene == null || player.Sprite == null || player.Hair == null) return;

        int animId = -1;
        if (_animationMap.TryGetValue(player.Sprite.CurrentAnimationID ?? "idle", out var id))
            animId = id;

        int hairCount = Math.Min(player.Sprite.HairCount, RemotePlayer.MaxHairLength);
        var hairColors = new Color[hairCount];
        for (int i = 0; i < hairCount; i++)
            hairColors[i] = player.Hair.GetHairColor(i);

        // Collect follower berries
        var followerBerries = new List<Vector2>();
        if (player.Leader?.Followers != null)
        {
            foreach (var follower in player.Leader.Followers)
            {
                if (follower.Entity is UmcBerry berry)
                {
                    followerBerries.Add(berry.SpawnPosition);
                }
            }
        }

        var frame = new PlayerFramePayload
        {
            Position = player.Position,
            Scale = player.Sprite.Scale,
            SpriteColor = player.Sprite.Color,
            Facing = player.Facing,
            Speed = player.Speed,
            CurrentAnimationID = animId,
            CurrentAnimationFrame = player.Sprite.CurrentAnimationFrame,
            HairColors = hairColors,
            HairSimulateMotion = player.Hair.SimulateMotion,
            Dead = player.Dead,
            Dashing = player.StateMachine.State == Player.StDash,
            DashWasB = false,
            DashDir = player.StateMachine.State == Player.StDash ? player.DashDir : (Vector2?)null,
            FollowerBerries = followerBerries
        };

        _net.Broadcast(frame, SendMode.Unreliable);
    }

    public void SendGraphics()
    {
        var player = Entity as Player;
        if (player?.Sprite == null || player.Hair == null) return;

        _animationMap.Clear();
        var animations = new string[player.Sprite.Animations.Count];
        int idx = 0;
        foreach (var animKey in player.Sprite.Animations.Keys)
        {
            _animationMap[animKey] = idx;
            animations[idx] = animKey;
            idx++;
        }

        int hairCount = Math.Min(player.Sprite.HairCount, RemotePlayer.MaxHairLength);
        var hairScales = new Vector2[hairCount];
        for (int i = 0; i < hairCount; i++)
        {
            hairScales[i] = player.Hair.GetHairScale(i) *
                new Vector2((i == 0 ? (int)player.Hair.Facing : 1) / Math.Abs(player.Sprite.Scale.X), 1);
        }

        var graphics = new PlayerGraphicsPayload
        {
            Depth = player.Depth,
            SpriteMode = player.Sprite.Mode,
            SpriteRate = player.Sprite.Rate,
            Animations = animations,
            HairCount = (byte)hairCount,
            HairScales = hairScales
        };

        _net.Broadcast(graphics, SendMode.Reliable);
    }

    private void OnFrameReceived(PlayerFramePayload frame)
    {
        var remotePlayer = Entity as RemotePlayer;
        if (remotePlayer == null) return;

        // Apply frame to remote player
        remotePlayer.UpdateGeneric(frame.Position, frame.Scale, frame.SpriteColor, frame.Facing, frame.Speed);
        remotePlayer.UpdateAnimation(frame.CurrentAnimationID, frame.CurrentAnimationFrame);
        remotePlayer.UpdateHair(frame.Facing, frame.HairColors, frame.HairSimulateMotion);
        remotePlayer.Dead = frame.Dead;
        remotePlayer.Dashing = frame.Dashing;
        remotePlayer.DashWasB = frame.DashWasB;
        remotePlayer.DashDir = frame.DashDir;

        // Update last network state
        UmcPlayer.LastNetworkState = new RemotePlayerNetworkState
        {
            Position = frame.Position,
            Facing = frame.Facing,
            Scale = frame.Scale,
            Speed = frame.Speed,
            AnimationFrame = frame.CurrentAnimationFrame,
            Dead = frame.Dead
        };

        // Sync followers
        SyncFollowerBerries(frame.FollowerBerries);
    }

    private void OnGraphicsReceived(PlayerGraphicsPayload graphics)
    {
        var remotePlayer = Entity as RemotePlayer;
        if (remotePlayer == null) return;

        // Convert to the format RemotePlayer expects
        var msg = new PlayerGraphicsMessage
        {
            PlayerIndex = UmcPlayer.SlotIndex,
            Depth = graphics.Depth,
            SpriteMode = graphics.SpriteMode,
            SpriteRate = graphics.SpriteRate,
            Animations = graphics.Animations,
            HairCount = graphics.HairCount,
            HairScales = graphics.HairScales
        };
        remotePlayer.UpdateGraphics(msg);
    }

    /// <summary>
    /// Syncs followers by attaching/detaching berries based on what the remote player is carrying.
    /// Positions are computed locally using the remote player's position.
    /// </summary>
    private void SyncFollowerBerries(List<Vector2> berrySpawnPositions)
    {
        var remotePlayer = Entity as RemotePlayer;
        if (remotePlayer == null) return;

        var berryManager = BerryManager.Instance;
        var level = Entity.Scene as Level;
        if (berryManager == null || level == null) return;

        // Find berries that should be attached
        var shouldBeAttached = new HashSet<UmcBerry>();
        foreach (var spawnPos in berrySpawnPositions)
        {
            var berry = FindBerryBySpawnPosition(level, spawnPos);
            if (berry != null && !berry.IsCollected)
            {
                shouldBeAttached.Add(berry);
            }
        }

        // Detach berries that are no longer being carried
        for (int i = _attachedBerries.Count - 1; i >= 0; i--)
        {
            var berry = _attachedBerries[i];
            if (!shouldBeAttached.Contains(berry))
            {
                // Berry was dropped - let it fall at its current position
                berry.Drop(berry.Position);
                _attachedBerries.RemoveAt(i);
            }
        }

        // Attach new berries
        foreach (var berry in shouldBeAttached)
        {
            if (!_attachedBerries.Contains(berry))
            {
                berry.PickupByRemote(UmcPlayer);
                _attachedBerries.Add(berry);
            }
        }
    }

    /// <summary>
    /// Updates follower positions to trail behind the remote player.
    /// </summary>
    private void UpdateRemoteFollowers()
    {
        var remotePlayer = Entity as RemotePlayer;
        if (remotePlayer == null || _attachedBerries.Count == 0) return;

        // Simple trailing behavior - berries follow with delay
        Vector2 followPos = remotePlayer.Position;
        const float followDistance = 12f;

        for (int i = 0; i < _attachedBerries.Count; i++)
        {
            var berry = _attachedBerries[i];
            if (berry == null || berry.Scene == null) continue;

            // Lerp toward follow position
            Vector2 targetPos = followPos - new Vector2(0, (i + 1) * followDistance);
            float t = 1f - (float)Math.Exp(-8f * Engine.DeltaTime);
            berry.Position = Vector2.Lerp(berry.Position, targetPos, t);

            followPos = berry.Position;
        }
    }

    private UmcBerry FindBerryBySpawnPosition(Level level, Vector2 spawnPos)
    {
        const float tolerance = 1f;
        foreach (var entity in level.Tracker.GetEntities<UmcBerry>())
        {
            if (entity is UmcBerry berry &&
                Math.Abs(berry.SpawnPosition.X - spawnPos.X) < tolerance &&
                Math.Abs(berry.SpawnPosition.Y - spawnPos.Y) < tolerance)
            {
                return berry;
            }
        }
        return null;
    }
}

