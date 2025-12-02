using System;
using Celeste.Mod.UltimateMadelineCeleste.Networking;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Ui;

public class SaveSlotStart : HookedFeature<SaveSlotStart>
{
    private enum MenuState
    {
        MainMenu,
        Submenu,
        Loading
    }

    private static OuiFileSelectSlot _currentSlot;
    private static MenuState _menuState = MenuState.MainMenu;
    private static string _loadingMessage;
    private static string _errorMessage;
    private static OuiFileSelectSlot.Button _loadingButton;
    private static OuiFileSelectSlot.Button _errorButton;

    // Track event subscriptions
    private static bool _subscribedToEvents;

    protected override void Hook()
    {
        base.Hook();
        On.Celeste.OuiFileSelectSlot.CreateButtons += OuiFileSelectSlotCreateButtons;
    }

    protected override void Unhook()
    {
        base.Unhook();
        On.Celeste.OuiFileSelectSlot.CreateButtons -= OuiFileSelectSlotCreateButtons;
        UnsubscribeFromEvents();
    }

    private static void OuiFileSelectSlotCreateButtons(On.Celeste.OuiFileSelectSlot.orig_CreateButtons orig,
        OuiFileSelectSlot self)
    {
        orig(self);

        _currentSlot = self;

        self.buttons.Insert(1, new OuiFileSelectSlot.Button
        {
            Action = () => Instance.OnUMCSelected(self),
            Label = "Play Ultimate Madeline Celeste",
        });
    }

    private void OnUMCSelected(OuiFileSelectSlot slot)
    {
        Audio.Play("event:/ui/main/button_select");
        _menuState = MenuState.Submenu;
        _errorMessage = null;
        RebuildButtons(slot);
    }

    private static void RebuildButtons(OuiFileSelectSlot slot)
    {
        // Clear existing buttons
        slot.buttons.Clear();

        switch (_menuState)
        {
            case MenuState.Submenu:
                BuildSubmenuButtons(slot);
                break;
            case MenuState.Loading:
                BuildLoadingButtons(slot);
                break;
            default:
                // This shouldn't happen, but fallback to recreating original buttons
                slot.CreateButtons();
                break;
        }
    }

    private static void BuildSubmenuButtons(OuiFileSelectSlot slot)
    {
        // Back button
        slot.buttons.Add(new OuiFileSelectSlot.Button
        {
            Action = () =>
            {
                Audio.Play("event:/ui/main/button_back");
                _menuState = MenuState.MainMenu;
                _errorMessage = null;
                slot.CreateButtons();
            },
            Label = "Back",
        });

        // Local play
        slot.buttons.Add(new OuiFileSelectSlot.Button
        {
            Action = () => StartLocalGame(slot),
            Label = "Play Local",
        });

        // Create online lobby (only if Steam available)
        if (SteamManager.IsInitialized)
        {
            slot.buttons.Add(new OuiFileSelectSlot.Button
            {
                Action = () => StartHostGame(slot),
                Label = "Create Online Lobby",
            });

            // Join online lobby
            slot.buttons.Add(new OuiFileSelectSlot.Button
            {
                Action = () => StartJoinGame(slot),
                Label = "Join Online Lobby",
            });
        }

        // Show error if any
        if (!string.IsNullOrEmpty(_errorMessage))
        {
            _errorButton = new OuiFileSelectSlot.Button
            {
                Action = () => { }, // No action
                Label = _errorMessage,
            };
            slot.buttons.Add(_errorButton);
        }
    }

    private static void BuildLoadingButtons(OuiFileSelectSlot slot)
    {
        // Cancel button
        slot.buttons.Add(new OuiFileSelectSlot.Button
        {
            Action = () =>
            {
                Audio.Play("event:/ui/main/button_back");
                CancelConnection();
                _menuState = MenuState.Submenu;
                RebuildButtons(slot);
            },
            Label = "Cancel",
        });

        // Loading indicator
        _loadingButton = new OuiFileSelectSlot.Button
        {
            Action = () => { }, // No action
            Label = _loadingMessage ?? "Connecting...",
        };
        slot.buttons.Add(_loadingButton);
    }

    private static void StartLocalGame(OuiFileSelectSlot slot)
    {
        Audio.Play("event:/ui/world_map/chapter/checkpoint_start");
        MultiplayerController.Instance?.StartLocalSession();
        EnterLevel(slot.Scene as Overworld);
    }

    private static void StartHostGame(OuiFileSelectSlot slot)
    {
        Audio.Play("event:/ui/main/button_select");
        _menuState = MenuState.Loading;
        _loadingMessage = "Creating lobby...";
        _errorMessage = null;
        RebuildButtons(slot);

        SubscribeToEvents();
        MultiplayerController.Instance?.HostOnlineGame();
    }

    private static void StartJoinGame(OuiFileSelectSlot slot)
    {
        var clipboard = TextInput.GetClipboardText()?.Trim();
        if (string.IsNullOrEmpty(clipboard) || clipboard.Length != 11)
        {
            Audio.Play("event:/ui/main/button_invalid");
            _errorMessage = "Copy a lobby code first!";
            RebuildButtons(slot);
            return;
        }

        Audio.Play("event:/ui/main/button_select");
        _menuState = MenuState.Loading;
        _loadingMessage = "Joining lobby...";
        _errorMessage = null;
        RebuildButtons(slot);

        SubscribeToEvents();
        MultiplayerController.Instance?.JoinOnlineGame(clipboard);
    }

    private static void CancelConnection()
    {
        UnsubscribeFromEvents();
        SteamLobbyManager.Instance?.LeaveLobby();
    }

    private static void SubscribeToEvents()
    {
        if (_subscribedToEvents) return;

        var controller = MultiplayerController.Instance;
        if (controller != null)
        {
            controller.OnConnected += OnConnected;
            controller.OnError += OnError;
        }

        var lobbyManager = SteamLobbyManager.Instance;
        if (lobbyManager != null)
        {
            lobbyManager.OnJoinByCodeFailed += OnJoinByCodeFailed;
            lobbyManager.OnLobbyJoined += OnLobbyJoined;
        }

        _subscribedToEvents = true;
    }

    private static void UnsubscribeFromEvents()
    {
        if (!_subscribedToEvents) return;

        var controller = MultiplayerController.Instance;
        if (controller != null)
        {
            controller.OnConnected -= OnConnected;
            controller.OnError -= OnError;
        }

        var lobbyManager = SteamLobbyManager.Instance;
        if (lobbyManager != null)
        {
            lobbyManager.OnJoinByCodeFailed -= OnJoinByCodeFailed;
            lobbyManager.OnLobbyJoined -= OnLobbyJoined;
        }

        _subscribedToEvents = false;
    }

    private static void OnConnected()
    {
        UnsubscribeFromEvents();

        if (_menuState != MenuState.Loading) return;

        // Start local session for online play
        if (!Core.UmcSession.Started)
        {
            MultiplayerController.Instance?.StartLocalSession();
        }

        Audio.Play("event:/ui/world_map/chapter/checkpoint_start");
        EnterLevel(_currentSlot?.Scene as Overworld);
    }

    private static void OnError(string error)
    {
        UnsubscribeFromEvents();

        if (_menuState != MenuState.Loading) return;

        Audio.Play("event:/ui/main/button_invalid");
        _menuState = MenuState.Submenu;
        _errorMessage = error ?? "Connection failed";

        if (_currentSlot != null)
        {
            RebuildButtons(_currentSlot);
        }
    }

    private static void OnJoinByCodeFailed(string code)
    {
        OnError("Invalid lobby code");
    }

    private static void OnLobbyJoined(CSteamID lobbyId, bool success)
    {
        if (!success)
        {
            OnError("Failed to join lobby");
        }
        // Success is handled by OnConnected
    }

    private static void EnterLevel(Overworld scene)
    {
        if (scene == null) return;

        _menuState = MenuState.MainMenu;
        _errorMessage = null;

        LevelEnter.Go(new Session(new AreaKey(AreaData.Get("FancyFurret/UltimateMadelineCeleste/Hub").ID)), false);
    }
}
