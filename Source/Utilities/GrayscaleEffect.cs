using System.IO;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Utilities;

/// <summary>
/// Provides a grayscale shader effect for rendering.
/// </summary>
public static class GrayscaleEffect
{
    private static Effect _effect;

    /// <summary>
    /// Gets the grayscale effect.
    /// </summary>
    public static Effect Effect => _effect;

    /// <summary>
    /// Loads the grayscale shader effect.
    /// </summary>
    public static void Load()
    {
        if (_effect != null) return;
        _effect = new Effect(Engine.Graphics.GraphicsDevice,
            Everest.Content.Get("UltimateMadelineCeleste:/Effects/Grayscale.cso").Data);
    }

    /// <summary>
    /// Unloads the effect.
    /// </summary>
    public static void Unload()
    {
        _effect?.Dispose();
        _effect = null;
    }
}

