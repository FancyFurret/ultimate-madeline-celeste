using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Steam;
using Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;

/// <summary>
/// Extends the pause menu with Steam multiplayer options and simplifies menu for multiplayer.
/// </summary>
public static class PauseMenuExtensions
{
    private static bool _subscribedToEvents;
    private static TextMenu _currentMenu;
    private static TextMenu.Button _resumeButton;
    private static TextMenu.Button _optionsButton;
    private static TextMenu.Button _modOptionsButton;
    private static TextMenu.Button _quitButton;

    public static void Load()
    {
        Everest.Events.Level.OnCreatePauseMenuButtons += OnCreatePauseMenuButtons;
    }

    public static void Unload()
    {
        Everest.Events.Level.OnCreatePauseMenuButtons -= OnCreatePauseMenuButtons;
        UnsubscribeFromConnectionEvents();
    }

    private static void SubscribeToConnectionEvents()
    {
        if (_subscribedToEvents) return;

        var controller = NetworkManager.Instance;
        if (controller != null)
        {
            controller.OnConnected += OnConnectionStateChanged;
            controller.OnDisconnected += OnConnectionStateChanged;
            _subscribedToEvents = true;
        }
    }

    private static void UnsubscribeFromConnectionEvents()
    {
        if (!_subscribedToEvents) return;

        var controller = NetworkManager.Instance;
        if (controller != null)
        {
            controller.OnConnected -= OnConnectionStateChanged;
            controller.OnDisconnected -= OnConnectionStateChanged;
        }

        _subscribedToEvents = false;
    }

    private static void OnConnectionStateChanged()
    {
        UmcLogger.Debug("OnConnectionStateChanged");
        RebuildMenu();
    }

    private static void OnCreatePauseMenuButtons(Level level, TextMenu menu, bool minimal)
    {
        if (!GameSession.Started) return;

        _currentMenu = menu;

        SubscribeToConnectionEvents();
        CaptureEssentialButtons(level, menu);
        RebuildMenu();
    }

    private static void CaptureEssentialButtons(Level level, TextMenu menu)
    {
        _resumeButton = null;
        _optionsButton = null;
        _modOptionsButton = null;

        var resumeLabel = Dialog.Clean("menu_pause_resume");
        var optionsLabel = Dialog.Clean("menu_pause_options");

        var items = new List<TextMenu.Item>(menu.Items);
        foreach (var item in items)
        {
            if (item is TextMenu.Header)
            {
                continue;
            }

            if (item is TextMenu.Button button)
            {
                if (button.Label == resumeLabel)
                {
                    _resumeButton = button;
                }
                else if (button.Label == optionsLabel)
                {
                    _optionsButton = button;
                }
                else if (button.Label == "Mod Options")
                {
                    _modOptionsButton = button;
                }
            }

            menu.Remove(item);
        }

        // Create custom quit button
        _quitButton = new TextMenu.Button("Quit");
        _quitButton.Pressed(() =>
        {
            level.PauseMainMenuOpen = false;
            Audio.Play("event:/ui/main/button_select");

            _currentMenu = null;
            UnsubscribeFromConnectionEvents();

            PlayingPhase.Instance?.CleanupAllPhases();
            NetworkManager.Instance?.LeaveSession();
            level.DoScreenWipe(false, () =>
            {
                Engine.Scene = new OverworldLoader(Overworld.StartMode.MainMenu);
            });
        });
    }

    private static void RebuildMenu()
    {
        if (_currentMenu == null)
        {
            return;
        }

        ClearMenuItems(_currentMenu);

        if (_resumeButton != null)
        {
            _currentMenu.Add(_resumeButton);
        }

        // Add Back to Lobby button if we're in a playing phase and are the host
        AddBackToLobbyButton(_currentMenu);

        // Only show online options if we're in an online session
        if (NetworkManager.Instance?.IsOnline == true)
        {
            AddOnlineSessionButtons(_currentMenu);
        }

        if (_optionsButton != null)
        {
            _currentMenu.Add(_optionsButton);
        }

        if (_modOptionsButton != null)
        {
            _currentMenu.Add(_modOptionsButton);
        }

        if (_quitButton != null)
        {
            _currentMenu.Add(_quitButton);
        }
    }

    private static void AddBackToLobbyButton(TextMenu menu)
    {
        // Only show if we're in the Playing phase and are the host
        var session = GameSession.Instance;
        var net = NetworkManager.Instance;
        if (session == null || session.Phase != GamePhase.Playing) return;
        if (net != null && !net.IsHost) return;

        var backToLobbyButton = new TextMenu.Button("Back to Lobby");
        backToLobbyButton.Pressed(() =>
        {
            Audio.Play("event:/ui/main/button_select");
            PlayingPhase.Instance?.ReturnToLobby();
        });
        menu.Add(backToLobbyButton);
    }

    private static void ClearMenuItems(TextMenu menu)
    {
        var items = new List<TextMenu.Item>(menu.Items);
        foreach (var item in items)
        {
            if (item is TextMenu.Header)
            {
                continue;
            }

            menu.Remove(item);
        }
    }

    private static void AddOnlineSessionButtons(TextMenu menu)
    {
        var controller = NetworkManager.Instance;

        // Copy lobby code button
        var copyCodeButton = new TextMenu.Button("Copy Lobby Code");
        copyCodeButton.Pressed(() =>
        {
            var code = LobbyController.Instance?.LobbyCode;
            if (!string.IsNullOrEmpty(code))
            {
                TextInput.SetClipboardText(code);
                copyCodeButton.Label = "Copied!";
                Audio.Play("event:/ui/main/button_select");
            }
        });
        menu.Add(copyCodeButton);

        // Invite friends button
        var inviteButton = new TextMenu.Button("Invite Friends");
        inviteButton.Pressed(() =>
        {
            controller?.InviteFriends();
            Audio.Play("event:/ui/main/button_select");
        });
        menu.Add(inviteButton);
    }
}
