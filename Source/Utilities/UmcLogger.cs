namespace Celeste.Mod.UltimateMadelineCeleste.Utilities;

public class UmcLogger
{
    public static void Debug(string message)
    {
        Logger.Debug("UMC", message);
    }
    
    public static void Info(string message)
    {
        Logger.Info("UMC", message);
    }
    
    public static void Error(string message)
    {
        Logger.Error("UMC", message);
    }
    
    public static void Warn(string message)
    {
        Logger.Warn("UMC", message);
    }
}