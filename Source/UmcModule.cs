using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Phases;
using Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.UI.Hub;
using Celeste.Mod.UltimateMadelineCeleste.UI.Menu;
using Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;
using Celeste.Pico8;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste;

public class UmcModule : EverestModule
{
    public const BindingFlags AllFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static UmcModule Instance { get; private set; }
    public override Type SettingsType => typeof(UmcSettings);
    public static UmcSettings Settings => (UmcSettings)Instance._Settings;

    public bool InLevel => Engine.Scene is Level || Engine.Scene is LevelLoader || Engine.Scene is LevelExit || Engine.Scene is Emulator;

    public List<Action<bool>> EnabledActions { get; } = new();

    private SaveSlotMenu SaveSlotMenu { get; } = new();
    private UmcInputHandler InputHandler { get; } = new();
    private PhaseManager PhaseManager { get; } = new();
    private PlayerSpawner PlayerSpawner { get; } = new();
    private CameraController CameraController { get; } = new();
    private NetworkManager NetworkManager { get; } = new();

    public UmcModule()
    {
        Instance = this;
#if DEBUG
        Logger.SetLogLevel(nameof(UmcModule), LogLevel.Verbose);
#else
        Logger.SetLogLevel(nameof(UmcModule), LogLevel.Info);
#endif
    }

    public override void Load()
    {
        PropRegistry.RegisterBuiltIn();
        RegisterMessages();

        SaveSlotMenu.Load();
        InputHandler.Load();
        InputHandler.Enable();
        PauseMenuExtensions.Load();
        OnlinePlayersOverlay.Load();

        PlayerSpawner.Load();
        CameraController.Load();
        NetworkManager.Load();
        PhaseManager.Load();

        On.Monocle.Scene.Begin += SceneBeginHook;
        On.Monocle.Engine.Update += EngineUpdateHook;

        ApplySettings();
    }

    public override void Unload()
    {
        SaveSlotMenu.Unload();
        InputHandler.Unload();
        PauseMenuExtensions.Unload();
        PhaseManager.Unload();
        OnlinePlayersOverlay.Unload();

        PlayerSpawner.Unload();
        CameraController.Unload();
        NetworkManager.Unload();

        On.Monocle.Scene.Begin -= SceneBeginHook;
        On.Monocle.Engine.Update -= EngineUpdateHook;
    }

    public override void LoadContent(bool firstLoad) => base.LoadContent(firstLoad);

    public void ApplySettings() { }

    private void RegisterMessages()
    {
        var messages = NetworkManager.Messages;

        // Core networking messages
        messages.Register<NetworkedEntityMessage>();
        messages.Register<SpawnEntityMessage>();
        messages.Register<DespawnEntityMessage>();

        // Player state messages
        messages.Register<PlayerFrameMessage>();
        messages.Register<PlayerGraphicsMessage>();
        messages.Register<ClientPlayersFrameMessage>();

        // Lobby/session messages
        messages.Register<PlayerJoinRequestMessage>();
        messages.Register<PlayerJoinResponseMessage>();
        messages.Register<PlayerAddedMessage>();
        messages.Register<PlayerRemovedMessage>();
        messages.Register<LobbyStateMessage>();
        messages.Register<RequestLobbyStateMessage>();
        messages.Register<PlayerEventMessage>();

        // Hub phase messages
        messages.Register<UpdateSkinMessage>();
        messages.Register<LevelVoteStateMessage>();
        messages.Register<LevelVoteEliminateMessage>();

        // Playing phase messages
        messages.Register<LoadLevelMessage>();
        messages.Register<ReturnToLobbyMessage>();

        // Picking phase messages
        messages.Register<PickingStateMessage>();
        messages.Register<PropsSelectedMessage>();
        messages.Register<PropPickedMessage>();
        messages.Register<PickingCompleteMessage>();

        // Placing phase messages
        messages.Register<PropPlacedMessage>();
        messages.Register<PlacingCompleteMessage>();

        // Platforming phase messages
        messages.Register<PlayerDeathSyncMessage>();
        messages.Register<PlatformingCompleteMessage>();

        // Register entity factories
        PlayerCursor.RegisterFactory();
    }

    private static void SceneBeginHook(On.Monocle.Scene.orig_Begin orig, Scene self)
    {
        orig(self);
        Instance.ApplySettings();
    }

    private void EngineUpdateHook(On.Monocle.Engine.orig_Update orig, Engine self, Microsoft.Xna.Framework.GameTime gameTime)
    {
        orig(self, gameTime);
        NetworkManager.Update();
        PlayerStateSync.Instance?.Update();
    }
}
