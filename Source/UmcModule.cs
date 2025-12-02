using System;
using System.Collections.Generic;
using System.Reflection;
using Celeste.Mod.MotionSmoothing;
using Celeste.Mod.UltimateMadelineCeleste.Multiplayer;
using Celeste.Mod.UltimateMadelineCeleste.Networking;
using Celeste.Mod.UltimateMadelineCeleste.Ui;
using Celeste.Mod.UltimateMadelineCeleste.UI;
using Celeste.Pico8;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste;

public class UmcModule : EverestModule
{
    public const BindingFlags AllFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static UmcModule Instance { get; private set; }

    public override Type SettingsType => typeof(UmcSettings);
    public static UmcSettings Settings => (UmcSettings)Instance._Settings;

    public bool InLevel => Engine.Scene is Level || Engine.Scene is LevelLoader ||
                           Engine.Scene is LevelExit || Engine.Scene is Emulator;

    public UmcModule()
    {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(UmcModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(MotionSmoothingModule), LogLevel.Info);
#endif
    }

    public List<Action<bool>> EnabledActions { get; } = new();

    private SaveSlotStart SaveSlotStart { get; } = new();
    private UmcInputHandler InputHandler { get; } = new();

    // Multiplayer systems
    private MultiMadelineManager MultiMadelineManager { get; } = new();
    private MultiMadelineCameraController CameraController { get; } = new();

    // Multiplayer (handles both local and online)
    private MultiplayerController MultiplayerController { get; } = new();

    public override void Load()
    {
        SaveSlotStart.Load();
        InputHandler.Load();
        InputHandler.Enable();
        PauseMenuExtensions.Load();
        HubLobbyUi.Load();
        OnlinePlayersOverlay.Load();

        // Load multiplayer systems
        MultiMadelineManager.Load();
        CameraController.Load();
        MultiplayerController.Load();

        On.Monocle.Scene.Begin += SceneBeginHook;
        On.Monocle.Engine.Update += EngineUpdateHook;

        ApplySettings();
    }

    public override void Unload()
    {
        SaveSlotStart.Unload();
        InputHandler.Unload();
        PauseMenuExtensions.Unload();
        HubLobbyUi.Unload();
        OnlinePlayersOverlay.Unload();

        // Unload multiplayer systems
        MultiMadelineManager.Unload();
        CameraController.Unload();
        MultiplayerController.Unload();

        On.Monocle.Scene.Begin -= SceneBeginHook;
        On.Monocle.Engine.Update -= EngineUpdateHook;
    }

    public override void LoadContent(bool firstLoad)
    {
        base.LoadContent(firstLoad);
    }

    public void ApplySettings()
    {
        // if (MotionSmoothing == null) return;
    }

    private static void SceneBeginHook(On.Monocle.Scene.orig_Begin orig, Scene self)
    {
        orig(self);
        Instance.ApplySettings();
    }

    private void EngineUpdateHook(On.Monocle.Engine.orig_Update orig, Engine self, Microsoft.Xna.Framework.GameTime gameTime)
    {
        orig(self, gameTime);

        // Update multiplayer networking
        MultiplayerController.Update();
    }
}