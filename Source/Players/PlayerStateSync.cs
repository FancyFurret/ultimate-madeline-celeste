using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Handles real-time player state synchronization: frame data, graphics, and animations.
/// </summary>
public class PlayerStateSync
{
    public static PlayerStateSync Instance { get; private set; }

    private readonly Dictionary<int, Dictionary<string, int>> _localAnimationMaps = new();
    private readonly HashSet<int> _sentGraphicsFor = new();

    public PlayerStateSync()
    {
        Instance = this;
    }

    public void RegisterMessages(MessageRegistry messages)
    {
        messages.Register<ClientPlayersFrameMessage>(0, HandleClientPlayersFrame, checkTimestamp: true);
        messages.Register<PlayerGraphicsMessage>(7, HandlePlayerGraphics);
        messages.Register<PlayerEventMessage>(6, HandlePlayerEventMessage);
    }

    public void InvalidateGraphics(int slotIndex)
    {
        _sentGraphicsFor.Remove(slotIndex);
    }

    public void Clear()
    {
        _localAnimationMaps.Clear();
        _sentGraphicsFor.Clear();
    }

    public void Update()
    {
        if (NetworkManager.Instance?.ShouldSendStateUpdate() == true)
        {
            BroadcastLocalPlayerStates();
        }
    }

    private void BroadcastLocalPlayerStates()
    {
        var players = GameSession.Instance?.Players;
        var spawner = PlayerSpawner.Instance;
        var net = NetworkManager.Instance;
        if (players == null || spawner == null || net == null) return;

        var frames = new List<PlayerFrameMessage>();

        foreach (var player in players.Local)
        {
            var playerEntity = spawner.GetLocalPlayer(player);
            if (playerEntity?.Scene == null || playerEntity.Sprite == null || playerEntity.Hair == null)
                continue;

            if (!_sentGraphicsFor.Contains(player.SlotIndex))
            {
                SendPlayerGraphics(player, playerEntity);
                _sentGraphicsFor.Add(player.SlotIndex);
            }

            int animId = -1;
            if (_localAnimationMaps.TryGetValue(player.SlotIndex, out var animMap))
            {
                animMap.TryGetValue(playerEntity.Sprite.CurrentAnimationID ?? "idle", out animId);
            }

            int hairCount = Math.Min(playerEntity.Sprite.HairCount, RemotePlayer.MaxHairLength);
            var hairColors = new Color[hairCount];
            for (int i = 0; i < hairCount; i++)
            {
                hairColors[i] = playerEntity.Hair.GetHairColor(i);
            }

            frames.Add(new PlayerFrameMessage
            {
                PlayerIndex = player.SlotIndex,
                Position = playerEntity.Position,
                Scale = playerEntity.Sprite.Scale,
                SpriteColor = playerEntity.Sprite.Color,
                Facing = playerEntity.Facing,
                Speed = playerEntity.Speed,
                CurrentAnimationID = animId,
                CurrentAnimationFrame = playerEntity.Sprite.CurrentAnimationFrame,
                HairColors = hairColors,
                HairSimulateMotion = playerEntity.Hair.SimulateMotion,
                Dead = playerEntity.Dead,
                Dashing = playerEntity.StateMachine.State == Player.StDash,
                DashWasB = false,
                DashDir = playerEntity.StateMachine.State == Player.StDash ? playerEntity.DashDir : null
            });
        }

        if (frames.Count > 0)
        {
            net.Messages.Broadcast(new ClientPlayersFrameMessage { Frames = frames }, SendMode.Unreliable);
        }
    }

    public void SendPlayerGraphics(UmcPlayer umcPlayer, Player playerEntity)
    {
        var graphics = BuildPlayerGraphics(umcPlayer, playerEntity);
        if (graphics == null) return;

        NetworkManager.Instance?.Messages.Broadcast(graphics, SendMode.Reliable);
    }

    /// <summary>
    /// Builds a PlayerGraphicsMessage for a spawned local player. Returns null if player isn't ready.
    /// </summary>
    public PlayerGraphicsMessage BuildPlayerGraphics(UmcPlayer umcPlayer, Player playerEntity)
    {
        if (playerEntity?.Sprite == null || playerEntity.Hair == null) return null;

        var animMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var animations = new string[playerEntity.Sprite.Animations.Count];
        int idx = 0;
        foreach (var animKey in playerEntity.Sprite.Animations.Keys)
        {
            animMap[animKey] = idx;
            animations[idx] = animKey;
            idx++;
        }
        _localAnimationMaps[umcPlayer.SlotIndex] = animMap;

        int hairCount = Math.Min(playerEntity.Sprite.HairCount, RemotePlayer.MaxHairLength);
        var hairScales = new Vector2[hairCount];
        for (int i = 0; i < hairCount; i++)
        {
            hairScales[i] = playerEntity.Hair.GetHairScale(i) *
                new Vector2((i == 0 ? (int)playerEntity.Hair.Facing : 1) / Math.Abs(playerEntity.Sprite.Scale.X), 1);
        }

        return new PlayerGraphicsMessage
        {
            PlayerIndex = umcPlayer.SlotIndex,
            Depth = playerEntity.Depth,
            SpriteMode = playerEntity.Sprite.Mode,
            SpriteRate = playerEntity.Sprite.Rate,
            Animations = animations,
            HairCount = (byte)hairCount,
            HairScales = hairScales,
            SkinId = umcPlayer.SkinId
        };
    }

    private void HandleClientPlayersFrame(CSteamID senderId, ClientPlayersFrameMessage message)
    {
        var players = GameSession.Instance?.Players;
        var spawner = PlayerSpawner.Instance;
        if (players == null) return;

        foreach (var frame in message.Frames)
        {
            var player = players.Get(senderId.m_SteamID, frame.PlayerIndex);
            if (player == null) continue;

            player.LastNetworkState = new RemotePlayerNetworkState
            {
                Position = frame.Position,
                Facing = frame.Facing,
                Scale = frame.Scale,
                Speed = frame.Speed,
                AnimationFrame = frame.CurrentAnimationFrame,
                Dead = frame.Dead
            };

            var remotePlayer = spawner?.GetRemotePlayer(player);
            remotePlayer?.ApplyFrame(frame);
        }
    }

    private void HandlePlayerGraphics(CSteamID senderId, PlayerGraphicsMessage message)
    {
        var players = GameSession.Instance?.Players;
        var spawner = PlayerSpawner.Instance;
        if (players == null || spawner == null) return;

        var player = players.Get(senderId.m_SteamID, message.PlayerIndex);
        if (player == null || player.IsLocal) return;

        players.SetPlayerSkin(player, message.SkinId);

        var remotePlayer = spawner.GetRemotePlayer(player);
        remotePlayer?.UpdateGraphics(message);
    }

    private void HandlePlayerEventMessage(CSteamID senderId, PlayerEventMessage message)
    {
        UmcLogger.Debug($"Player event: {message.EventType} for slot {message.PlayerIndex}");
    }
}

