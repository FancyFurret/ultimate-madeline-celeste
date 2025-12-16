using System;
using System.IO;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.UI.Lobby;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// Cursor entity for character selection and picking phases.
/// Works in world space so camera movement doesn't affect cursor position.
/// Supports automatic network synchronization via NetworkedEntity.
/// </summary>
public class PlayerCursor : Entity
{
    public const float CursorScale = 0.6f;
    public const float MoveSpeed = 80f;  // World units per second
    public const float FastMoveSpeed = 160f;
    public const float InterpolationSpeed = 15f;
    public const float SnapDistanceThreshold = 50f;  // World units
    public const float NetworkSyncInterval = 0.05f;

    private static MTexture _cursorTexture;

    public UmcPlayer Player { get; }
    public Color CursorColor { get; set; }
    public bool IsLocal => Player?.IsLocal ?? false;
    public Vector2 WorldPosition => Position;
    public Vector2 ScreenPosition => CoordinateUtils.WorldToScreen(Position);
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional help text to display below the cursor (e.g., "Select Start").
    /// Set to null to hide the help text.
    /// </summary>
    public string HelpText { get; set; }

    private readonly Action<Vector2> _onConfirm;
    private readonly Action _onCancel;

    private float _animTimer;
    private const float BobAmount = 4f;
    private PlayerInput _input;

    // Interpolation for remote cursors
    private Vector2 _targetWorldPosition;
    private bool _hasReceivedPosition;
    private bool _isPaused;

    // Networking
    private NetworkedEntity<CursorPositionPayload> _net;
    private float _networkSyncTimer;

    /// <summary>
    /// Register the cursor factory with the NetworkedEntityRegistry.
    /// Call this once during initialization.
    /// </summary>
    public static void RegisterFactory()
    {
        NetworkedEntityRegistry.Instance.RegisterFactory<CursorPositionPayload>((networkId, ownerId, spawnData) =>
        {
            UmcLogger.Info($"Factory called: networkId={networkId}, ownerId={ownerId}, slotIndex={spawnData?.PlayerSlotIndex}, pos={spawnData?.WorldPosition}");
            var session = GameSession.Instance;
            if (session == null)
                return null;

            // Find player by slot index (more reliable than client ID for multi-player clients)
            var player = spawnData != null
                ? session.Players.GetAtSlot(spawnData.PlayerSlotIndex)
                : session.Players.GetByClientId(ownerId);

            if (player == null)
                return null;

            var cursor = new PlayerCursor(player, isRemote: true);
            if (spawnData != null)
            {
                cursor.Position = spawnData.WorldPosition;
                cursor._targetWorldPosition = spawnData.WorldPosition;
            }

            // Register with active PlayerCursors instance if one exists
            PlayerCursors.ActiveInstance?.RegisterRemoteCursor(player, cursor);

            return cursor;
        });

        UmcLogger.Info("[PlayerCursor] Factory registered");
    }

    public PlayerCursor(UmcPlayer player, bool isRemote = false, Action<Vector2> onConfirm = null, Action onCancel = null)
    {
        Player = player;
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        CursorColor = LobbyUi.PlayerColors[player.SlotIndex % LobbyUi.PlayerColors.Length];
        _cursorTexture ??= GFX.Gui["umc/cursor"];

        if (!isRemote && player.Device != null)
            _input = new PlayerInput(player.Device);

        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
        Depth = -200000;

        // Set up networking
        _net = new NetworkedEntity<CursorPositionPayload>()
            .Handle<CursorPositionPayload>(OnPositionReceived)
            .SetSpawnData(new CursorPositionPayload
            {
                PlayerSlotIndex = player.SlotIndex,
                WorldPosition = WorldPosition
            });
        _net.RemoveOnOwnerDisconnect = true;

        Add(_net);
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);

        // Start cursor at center of current view (in world space)
        var level = scene as Level;
        if (level?.Camera != null)
        {
            float visibleWidth = 320f / level.Zoom;
            float visibleHeight = 180f / level.Zoom;
            Position = level.Camera.Position + new Vector2(visibleWidth / 2f, visibleHeight / 2f);
        }
        _targetWorldPosition = Position;
    }

    private void OnPositionReceived(CursorPositionPayload payload)
    {
        if (!_hasReceivedPosition)
        {
            // Snap to first received position
            Position = payload.WorldPosition;
            _hasReceivedPosition = true;
        }

        _targetWorldPosition = payload.WorldPosition;
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        _input?.Deregister();
        _input = null;
    }

    public override void Update()
    {
        base.Update();
        if (!IsActive) return;

        var level = SceneAs<Level>();
        _isPaused = level != null && level.Paused;

        _animTimer += Engine.DeltaTime * 3f;

        if (IsLocal)
        {
            if (!_isPaused && _input != null) UpdateInput(level);
            UpdateNetworkSync();
        }
        else
        {
            UpdateInterpolation();
        }
    }

    private void UpdateNetworkSync()
    {
        _networkSyncTimer += Engine.DeltaTime;
        if (_networkSyncTimer >= NetworkSyncInterval)
        {
            _networkSyncTimer = 0f;

            var isOnline = NetworkManager.Instance?.IsOnline ?? false;
            if (isOnline)
            {
                _net.Broadcast(new CursorPositionPayload
                {
                    WorldPosition = WorldPosition
                }, SendMode.Unreliable);
            }
        }
    }

    private void UpdateInterpolation()
    {
        float distanceToTarget = Vector2.Distance(Position, _targetWorldPosition);

        if (distanceToTarget > SnapDistanceThreshold)
        {
            // Snap if too far away
            Position = _targetWorldPosition;
        }
        else if (distanceToTarget > 0.5f)
        {
            // Smooth exponential interpolation
            float t = 1f - (float)Math.Exp(-InterpolationSpeed * Engine.DeltaTime);
            Position = Vector2.Lerp(Position, _targetWorldPosition, t);
        }
    }

    private void UpdateInput(Level level)
    {
        Vector2 moveDir = _input.Feather.Value;

        if (moveDir != Vector2.Zero)
        {
            if (moveDir.Length() > 1f) moveDir.Normalize();

            // Use fast speed when holding cursor sprint
            float speed = _input.CursorSprint.Check ? FastMoveSpeed : MoveSpeed;

            Position += moveDir * speed * Engine.DeltaTime;
            Position = CoordinateUtils.ClampToLevel(Position, level);
        }

        if (_input.MenuConfirm.Pressed) _onConfirm?.Invoke(Position);
        if (_input.MenuCancel.Pressed) _onCancel?.Invoke();
    }

    public override void Render()
    {
        if (!IsActive || _cursorTexture == null) return;
        if (_isPaused) return;

        base.Render();

        var bob = (float)Math.Sin(_animTimer) * BobAmount;
        var screenPos = ScreenPosition;
        var renderPos = screenPos + new Vector2(0, bob);
        var textureSize = _cursorTexture.Width * CursorScale;

        _cursorTexture.Draw(renderPos + new Vector2(-textureSize + 8, 8), Vector2.Zero, Color.Black * 0.5f, CursorScale);
        _cursorTexture.Draw(renderPos + new Vector2(-textureSize, 0), Vector2.Zero, CursorColor, CursorScale);

        // Draw help text below cursor (cursor hangs above renderPos, so offset down from the texture bottom)
        if (!string.IsNullOrEmpty(HelpText))
        {
            const float textScale = 0.5f;
            var cursorHeight = _cursorTexture.Height * CursorScale;
            var textPos = renderPos + new Vector2(-textureSize / 2f, cursorHeight + 4);
            ActiveFont.DrawOutline(HelpText, textPos, new Vector2(0.5f, 0f), Vector2.One * textScale, Color.White, 2f, Color.Black);
        }
    }
}

public class CursorPositionPayload : INetMessage
{
    public int PlayerSlotIndex { get; set; }
    public Vector2 WorldPosition { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerSlotIndex);
        writer.Write(WorldPosition.X);
        writer.Write(WorldPosition.Y);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerSlotIndex = reader.ReadByte();
        WorldPosition = new Vector2(reader.ReadSingle(), reader.ReadSingle());
    }
}
