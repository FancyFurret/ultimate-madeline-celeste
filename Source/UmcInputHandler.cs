using Celeste.Mod.MotionSmoothing.Utilities;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste;

public class UmcInputHandler : ToggleableFeature<UmcInputHandler>
{
    public override void Load()
    {
        base.Load();
        On.Monocle.Scene.Begin += SceneBeginHook;
    }

    public override void Unload()
    {
        base.Unload();
        On.Monocle.Scene.Begin -= SceneBeginHook;
    }

    private static void SceneBeginHook(On.Monocle.Scene.orig_Begin orig, Scene self)
    {
        orig(self);

        var handler = self.Entities.FindFirst<UMCInputHandlerEntity>();
        if (handler == null)
        {
            handler = new UMCInputHandlerEntity();
            handler.Tag |= Tags.Persistent | Tags.Global;
            self.Add(handler);
        }
        else
        {
            handler.Active = true;
        }
    }

    private class UMCInputHandlerEntity : Entity
    {
        public override void Update()
        {
            base.Update();
            // if (UMCModule.Settings.ButtonToggleSmoothing.Pressed)
            // {
            //     Logger.Log(LogLevel.Info, "MotionSmoothingInputHandler", "Toggling motion smoothing");
            //     UMCModule.Settings.Enabled = !UMCModule.Settings.Enabled;
            // }
        }
    }
}