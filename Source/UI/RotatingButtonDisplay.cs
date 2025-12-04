using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI;

/// <summary>
/// Displays a rotating button icon based on a ButtonBinding.
/// Cycles through all bound keys and buttons, filtered by connected devices.
/// </summary>
public static class RotatingButtonDisplay
{
    private const float DefaultRotationInterval = 1f;

    /// <summary>
    /// Gets the current texture to display for a binding, rotating through all options.
    /// </summary>
    public static MTexture GetCurrentTexture(ButtonBinding binding, float time, float rotationInterval = DefaultRotationInterval)
    {
        var textures = GetAllTextures(binding);
        if (textures.Count == 0) return null;

        int index = (int)(time / rotationInterval) % textures.Count;
        return textures[index];
    }

    /// <summary>
    /// Draws a rotating button display at the specified position.
    /// </summary>
    public static void Draw(ButtonBinding binding, Vector2 position, Color color, float scale, float time, float rotationInterval = DefaultRotationInterval)
    {
        var texture = GetCurrentTexture(binding, time, rotationInterval);
        texture?.DrawCentered(position, color, scale);
    }

    /// <summary>
    /// Draws a rotating button display justified to the specified position.
    /// </summary>
    public static void DrawJustified(ButtonBinding binding, Vector2 position, Vector2 justify, Color color, float scale, float time, float rotationInterval = DefaultRotationInterval)
    {
        var texture = GetCurrentTexture(binding, time, rotationInterval);
        texture?.DrawJustified(position, justify, color, scale);
    }

    /// <summary>
    /// Gets all available textures for the binding based on connected devices.
    /// </summary>
    public static List<MTexture> GetAllTextures(ButtonBinding binding)
    {
        var textures = new List<MTexture>();

        // Check if any controller is connected
        bool hasController = false;
        for (int i = 0; i < 4; i++)
        {
            if (MInput.GamePads[i].Attached)
            {
                hasController = true;
                break;
            }
        }

        // Add controller button textures if a controller is connected
        if (hasController)
        {
            foreach (var button in binding.Buttons)
            {
                var texture = Input.GuiSingleButton(button, fallback: null);
                if (texture != null)
                    textures.Add(texture);
            }
        }

        // Always add keyboard textures (keyboard is always "connected")
        foreach (var key in binding.Keys)
        {
            var texture = Input.GuiKey(key);
            if (texture != null)
                textures.Add(texture);
        }

        return textures;
    }

    /// <summary>
    /// Gets the count of available button/key options for the binding.
    /// </summary>
    public static int GetTextureCount(ButtonBinding binding)
    {
        return GetAllTextures(binding).Count;
    }
}

