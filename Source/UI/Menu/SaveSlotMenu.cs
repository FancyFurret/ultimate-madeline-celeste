using System;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Steam;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Menu;

/// <summary>
/// Adds UMC play options to the save slot menu.
/// </summary>
public class SaveSlotMenu : HookedFeature<SaveSlotMenu>
{
    private enum MenuState { MainMenu, Submenu, Loading }

    private static OuiFileSelectSlot _currentSlot;
    private static MenuState _menuState = MenuState.MainMenu;
    private static string _loadingMessage;
    private static string _errorMessage;
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

    private static void OuiFileSelectSlotCreateButtons(On.Celeste.OuiFileSelectSlot.orig_CreateButtons orig, OuiFileSelectSlot self)
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
        slot.buttons.Clear();
        switch (_menuState)
        {
            case MenuState.Submenu:
                slot.buttons.Add(new OuiFileSelectSlot.Button { Action = () => { Audio.Play("event:/ui/main/button_back"); _menuState = MenuState.MainMenu; _errorMessage = null; slot.CreateButtons(); }, Label = "Back" });
                slot.buttons.Add(new OuiFileSelectSlot.Button { Action = () => StartLocalGame(slot), Label = "Play Local" });
                if (SteamManager.IsInitialized)
                {
                    slot.buttons.Add(new OuiFileSelectSlot.Button { Action = () => StartHostGame(slot), Label = "Create Online Lobby" });
                    slot.buttons.Add(new OuiFileSelectSlot.Button { Action = () => StartJoinGame(slot), Label = "Join Online Lobby" });
                }
                if (!string.IsNullOrEmpty(_errorMessage))
                    slot.buttons.Add(new OuiFileSelectSlot.Button { Action = () => { }, Label = _errorMessage });
                break;
            case MenuState.Loading:
                slot.buttons.Add(new OuiFileSelectSlot.Button { Action = () => { Audio.Play("event:/ui/main/button_back"); CancelConnection(); _menuState = MenuState.Submenu; RebuildButtons(slot); }, Label = "Cancel" });
                slot.buttons.Add(new OuiFileSelectSlot.Button { Action = () => { }, Label = _loadingMessage ?? "Connecting..." });
                break;
            default:
                slot.CreateButtons();
                break;
        }
    }

    private static void StartLocalGame(OuiFileSelectSlot slot)
    {
        Audio.Play("event:/ui/world_map/chapter/checkpoint_start");
        NetworkManager.Instance?.StartLocalSession();
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
        NetworkManager.Instance?.HostOnlineGame();
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
        NetworkManager.Instance?.JoinOnlineGame(clipboard);
    }

    private static void CancelConnection()
    {
        UnsubscribeFromEvents();
        SteamLobby.Instance?.LeaveLobby();
    }

    private static void SubscribeToEvents()
    {
        if (_subscribedToEvents) return;
        var net = NetworkManager.Instance;
        if (net != null) { net.OnConnected += OnConnected; net.OnError += OnError; }
        var lobby = SteamLobby.Instance;
        if (lobby != null) { lobby.OnJoinByCodeFailed += OnJoinByCodeFailed; lobby.OnLobbyJoined += OnLobbyJoined; }
        _subscribedToEvents = true;
    }

    private static void UnsubscribeFromEvents()
    {
        if (!_subscribedToEvents) return;
        var net = NetworkManager.Instance;
        if (net != null) { net.OnConnected -= OnConnected; net.OnError -= OnError; }
        var lobby = SteamLobby.Instance;
        if (lobby != null) { lobby.OnJoinByCodeFailed -= OnJoinByCodeFailed; lobby.OnLobbyJoined -= OnLobbyJoined; }
        _subscribedToEvents = false;
    }

    private static void OnConnected()
    {
        UnsubscribeFromEvents();
        if (_menuState != MenuState.Loading) return;
        if (!GameSession.Started) NetworkManager.Instance?.StartLocalSession();
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
        if (_currentSlot != null) RebuildButtons(_currentSlot);
    }

    private static void OnJoinByCodeFailed(string code) => OnError("Invalid lobby code");
    private static void OnLobbyJoined(CSteamID lobbyId, bool success) { if (!success) OnError("Failed to join lobby"); }

    private static void EnterLevel(Overworld scene)
    {
        if (scene == null) return;
        _menuState = MenuState.MainMenu;
        _errorMessage = null;

        if (_currentSlot != null)
            SaveData.Start(_currentSlot.SaveData, _currentSlot.FileSlot);

        var areaData = AreaData.Get("FancyFurret/UltimateMadelineCeleste/Hub");
        LevelEnter.Go(new global::Celeste.Session(new AreaKey(areaData.ID)), false);
    }
}

