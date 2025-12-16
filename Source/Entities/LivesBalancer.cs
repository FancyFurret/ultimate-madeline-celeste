using System;
using System.Collections.Generic;
using Celeste.Mod.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.UI.Lobby;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// A Lives Balancer billboard where players can adjust their starting lives.
/// Shows per-player lives with color indicators. Bump buttons from below.
/// </summary>
[CustomEntity("UltimateMadelineCeleste/LivesBalancer")]
[Tracked]
public class LivesBalancer : Entity
{
    private const int SignWidth = 74;
    private const int SignHeight = 47; // Just the billboard part
    private const int TotalHeight = 81; // Billboard + posts
    private const int ButtonWidth = 12;
    private const int ButtonHeight = 8;
    private const int ButtonSpacing = 3;
    private const int RowHeight = 10;
    private const int MaxPlayers = 4;
    private const int MaxHeartsDisplay = 8;

    private static readonly Color HeartColor = Calc.HexToColor("E74C3C");

    private MTexture _billboardTexture;
    private MTexture _heartTexture;
    private LifeBumper _minusBtn, _plusBtn, _resetBtn;

    public LivesBalancer(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        Depth = Depths.Below;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);

        _billboardTexture = GFX.Game.GetOrDefault("objects/UMC/livesBalancer/billboard", null);
        _heartTexture = GFX.Game.GetOrDefault("objects/UMC/life/idle00", null);

        // Buttons attached to bottom of billboard (3 buttons, 12px each, 2px spacing)
        float totalWidth = ButtonWidth * 3 + ButtonSpacing * 2; // 40px total
        float startX = Position.X + (SignWidth - totalWidth) / 2f;
        float btnY = Position.Y + SignHeight;

        _minusBtn = new LifeBumper(new Vector2(startX, btnY), ButtonWidth, ButtonHeight, LifeBumper.ButtonType.Minus, OnMinus);
        _plusBtn = new LifeBumper(new Vector2(startX + ButtonWidth + ButtonSpacing, btnY), ButtonWidth, ButtonHeight, LifeBumper.ButtonType.Plus, OnPlus);
        _resetBtn = new LifeBumper(new Vector2(startX + (ButtonWidth + ButtonSpacing) * 2, btnY), ButtonWidth, ButtonHeight, LifeBumper.ButtonType.Reset, OnReset);

        scene.Add(_minusBtn);
        scene.Add(_plusBtn);
        scene.Add(_resetBtn);

        // Register network handler for lives changes
        NetworkManager.Handle<PlayerLivesChangedMessage>(HandlePlayerLivesChanged);
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);
        _minusBtn?.RemoveSelf();
        _plusBtn?.RemoveSelf();
        _resetBtn?.RemoveSelf();
    }

    private void HandlePlayerLivesChanged(CSteamID sender, PlayerLivesChangedMessage message)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var player = session.Players.GetAtSlot(message.PlayerIndex);
        if (player == null) return;

        // Don't apply to our own local players (we already updated locally)
        if (player.IsLocal) return;

        player.MaxLives = message.MaxLives;
        RefreshHearts(player);
        UmcLogger.Info($"Synced lives for player {player.Name}: {message.MaxLives}");
    }

    private void OnMinus(UmcPlayer p)
    {
        if (p == null || p.MaxLives <= 1) { Audio.Play("event:/ui/main/button_invalid"); return; }
        p.MaxLives--;
        Audio.Play("event:/ui/main/button_toggle_off");
        RefreshHearts(p);
        BroadcastLivesChanged(p);
    }

    private void OnPlus(UmcPlayer p)
    {
        if (p == null || p.MaxLives >= MaxHeartsDisplay) { Audio.Play("event:/ui/main/button_invalid"); return; }
        p.MaxLives++;
        Audio.Play("event:/ui/main/button_toggle_on");
        RefreshHearts(p);
        BroadcastLivesChanged(p);
    }

    private void OnReset(UmcPlayer p)
    {
        if (p == null) return;
        p.MaxLives = RoundSettings.Current.DefaultLives;
        Audio.Play("event:/ui/main/whoosh_large_in");
        RefreshHearts(p);
        BroadcastLivesChanged(p);
    }

    private void BroadcastLivesChanged(UmcPlayer p)
    {
        NetworkManager.Broadcast(new PlayerLivesChangedMessage
        {
            PlayerIndex = p.SlotIndex,
            MaxLives = p.MaxLives
        });
    }

    private void RefreshHearts(UmcPlayer p)
    {
        var spawner = PlayerSpawner.Instance;

        // Remove existing hearts
        LifeHeartManager.RemoveAllLives(p);

        UmcLogger.Info($"Refreshing hearts for player {p.Name} with {p.MaxLives} lives");

        // Try local player first
        Player localEntity = spawner?.GetLocalPlayer(p);
        if (localEntity != null)
        {
            UmcLogger.Info($"Attaching hearts to local player {localEntity} with {p.MaxLives} lives");
            LifeHeartManager.AttachToPlayer(localEntity, p, p.MaxLives);
            return;
        }

        // Try remote player
        RemotePlayer remoteEntity = spawner?.GetRemotePlayer(p);
        if (remoteEntity != null)
        {
            UmcLogger.Info($"Attaching hearts to remote player {remoteEntity} with {p.MaxLives} lives");
            LifeHeartManager.AttachToPlayer(remoteEntity, p, p.MaxLives);
        }
    }

    public override void Render()
    {
        base.Render();
        var pos = Position;

        // Billboard texture (includes posts)
        if (_billboardTexture != null)
        {
            _billboardTexture.Draw(pos);
        }

        // Player rows with hearts (on the billboard part)
        var players = GameSession.Instance?.Players.All;
        if (players != null && players.Count > 0)
        {
            float startY = pos.Y + 6;

            for (int i = 0; i < Math.Min(players.Count, MaxPlayers); i++)
            {
                RenderPlayerRow(pos.X + 4, startY + i * RowHeight, players[i]);
            }
        }
    }

    private void RenderPlayerRow(float x, float y, UmcPlayer p)
    {
        // Player color as vertical bar
        Color playerColor = p.SlotIndex < LobbyUi.PlayerColors.Length
            ? LobbyUi.PlayerColors[p.SlotIndex]
            : Color.White;

        Draw.Rect(x, y, 2, 8, playerColor);

        // Hearts for this player's lives
        for (int h = 0; h < p.MaxLives && h < MaxHeartsDisplay; h++)
        {
            float hx = x + 5 + h * 8;
            DrawHeart(hx, y);
        }
    }

    private void DrawHeart(float x, float y)
    {
        if (_heartTexture != null)
        {
            _heartTexture.DrawCentered(new Vector2(x + 3, y + 4), HeartColor);
        }
        else
        {
            Draw.Rect(x, y, 6, 6, HeartColor);
        }
    }

    public MTexture GetHeartTexture() => _heartTexture;
}

/// <summary>
/// A bumper button hit from below with texture.
/// </summary>
[Tracked]
public class LifeBumper : Solid
{
    public enum ButtonType { Minus, Plus, Reset }

    private readonly ButtonType _type;
    private readonly Action<UmcPlayer> _onHit;
    private readonly int _width;
    private readonly int _height;
    private readonly Dictionary<int, float> _cooldowns = new();
    private float _bounceOffset;
    private MTexture _texture;
    private bool _hitThisFrame;

    public LifeBumper(Vector2 pos, int width, int height, ButtonType type, Action<UmcPlayer> onHit)
        : base(pos, width, height, safe: false)
    {
        _width = width;
        _height = height;
        _type = type;
        _onHit = onHit;
        Depth = Depths.Below - 1;
        OnDashCollide = OnDashed;
        // Make collider taller to catch Madeline's small hitbox, extend downward
        Collider = new Hitbox(width, height + 6, 0, 0);
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);

        string textureName = _type switch
        {
            ButtonType.Minus => "buttonSub",
            ButtonType.Plus => "buttonAdd",
            ButtonType.Reset => "buttonReset",
            _ => "buttonAdd"
        };
        _texture = GFX.Game.GetOrDefault($"objects/UMC/livesBalancer/{textureName}", null);
    }

    private DashCollisionResults OnDashed(Player player, Vector2 dir)
    {
        if (dir.Y > 0) // Dashing upward into the button
        {
            TriggerHit(player);
        }
        return DashCollisionResults.NormalCollision;
    }

    public override void Update()
    {
        base.Update();
        _hitThisFrame = false;

        var expired = new List<int>();
        foreach (var k in _cooldowns.Keys)
        {
            _cooldowns[k] -= Engine.DeltaTime;
            if (_cooldowns[k] <= 0) expired.Add(k);
        }
        foreach (var k in expired) _cooldowns.Remove(k);

        // Check for players hitting from below - use visual button position
        if (Scene is Level level)
        {
            float visualTop = Position.Y;
            float visualBottom = Position.Y + _height;

            foreach (var entity in level.Tracker.GetEntities<Player>())
            {
                if (entity is Player player && player.Speed.Y < 0) // Moving up
                {
                    float playerTop = player.Top;

                    // Player's head is within range of button bottom
                    bool verticalHit = playerTop >= visualTop - 4 && playerTop <= visualBottom + 8;
                    bool horizontalHit = player.CenterX >= Position.X && player.CenterX <= Position.X + _width;

                    if (verticalHit && horizontalHit)
                    {
                        TriggerHit(player);
                    }
                }
            }
        }

        _bounceOffset = Calc.Approach(_bounceOffset, 0f, 32f * Engine.DeltaTime);
    }

    public void TriggerHit(Player player)
    {
        if (_hitThisFrame) return;
        _hitThisFrame = true;

        // Always animate the bounce
        _bounceOffset = -3f;

        var spawner = PlayerSpawner.Instance;
        var umcPlayer = spawner?.GetUmcPlayer(player);
        if (umcPlayer == null) return;

        if (_cooldowns.ContainsKey(umcPlayer.SlotIndex)) return;

        _onHit?.Invoke(umcPlayer);
        _cooldowns[umcPlayer.SlotIndex] = 0.3f;
    }

    public override void Render()
    {
        float y = Position.Y + _bounceOffset;

        if (_texture != null)
        {
            _texture.Draw(new Vector2(Position.X, y));
        }
        else
        {
            Draw.Rect(Position.X, y, _width, _height, Color.Gray);
        }

    }
}
