using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;

/// <summary>
/// Overlay shown while waiting for lobby state from host.
/// Grays out the screen and shows "Connecting..." text.
/// </summary>
public class ConnectingOverlay : Entity
{
    public static ConnectingOverlay Instance { get; private set; }

    private float _dotTimer;
    private int _dotCount;
    private bool _isVisible;

    public ConnectingOverlay()
    {
        Instance = this;
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.FrozenUpdate;
        Depth = -1000000; // In front of everything
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Instance = this;

        // Check if we should be visible
        UpdateVisibility();

        // Subscribe to lobby state received
        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.OnLobbyStateReceived += OnLobbyStateReceived;
        }
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);

        if (LobbyController.Instance != null)
        {
            LobbyController.Instance.OnLobbyStateReceived -= OnLobbyStateReceived;
        }

        if (Instance == this)
            Instance = null;
    }

    private void OnLobbyStateReceived()
    {
        _isVisible = false;
        UmcLogger.Info("ConnectingOverlay: Lobby state received, hiding overlay");
    }

    private void UpdateVisibility()
    {
        var lobby = LobbyController.Instance;
        _isVisible = lobby != null && lobby.IsWaitingForLobbyState && !lobby.IsHost;
    }

    public override void Update()
    {
        base.Update();

        UpdateVisibility();

        if (_isVisible)
        {
            _dotTimer += Engine.DeltaTime;
            if (_dotTimer >= 0.5f)
            {
                _dotTimer = 0f;
                _dotCount = (_dotCount + 1) % 4;
            }
        }
    }

    public override void Render()
    {
        if (!_isVisible) return;

        // Dark overlay covering entire screen
        Draw.Rect(0, 0, 1920, 1080, Color.Black * 0.7f);

        // "Connecting" text with animated dots
        string dots = new string('.', _dotCount);
        string text = $"Connecting{dots}";

        // Center the text
        Vector2 textSize = ActiveFont.Measure(text);
        Vector2 position = new Vector2(960f, 540f);

        // Draw text with outline
        ActiveFont.DrawOutline(
            text,
            position,
            new Vector2(0.5f, 0.5f),
            Vector2.One * 1.5f,
            Color.White,
            2f,
            Color.Black
        );

        // Subtitle
        string subtitle = "Waiting for host...";
        ActiveFont.DrawOutline(
            subtitle,
            position + new Vector2(0, 60),
            new Vector2(0.5f, 0.5f),
            Vector2.One * 0.7f,
            Color.White * 0.7f,
            1f,
            Color.Black
        );
    }
}

