using System.Collections.Generic;
using Celeste.Mod.SkinModHelper;
using Microsoft.Xna.Framework.Graphics;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Utilities;

/// <summary>
/// Helper for getting skin portrait textures (first frame of idle animation).
/// </summary>
public static class SkinPortraitHelper
{
    private static readonly Dictionary<string, MTexture> _cache = new();

    /// <summary>
    /// Gets the portrait texture for a skin (first idle frame).
    /// Returns null if unable to get portrait.
    /// </summary>
    public static MTexture GetPortrait(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
            return GetDefaultPortrait();

        // Check cache first
        if (_cache.TryGetValue(skinId, out var cached))
            return cached;

        MTexture portrait = null;

        // Try to get the portrait from skin config
        if (skinId != SkinsSystem.DEFAULT &&
            SkinsSystem.skinConfigs != null &&
            SkinsSystem.skinConfigs.TryGetValue(skinId, out var config) &&
            !string.IsNullOrEmpty(config.Character_ID))
        {
            portrait = GetPortraitFromCharacterId(config.Character_ID);
        }

        // Fall back to default Madeline
        portrait ??= GetDefaultPortrait();

        _cache[skinId] = portrait;
        return portrait;
    }

    private static MTexture GetPortraitFromCharacterId(string characterId)
    {
        if (!GFX.SpriteBank.Has(characterId))
            return null;

        try
        {
            // Create a temporary sprite to get the idle frame
            var sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
            GFX.SpriteBank.CreateOn(sprite, characterId);

            if (sprite.Has("idle") && sprite.Animations.TryGetValue("idle", out var anim) && anim.Frames.Length > 0)
            {
                return anim.Frames[0];
            }
        }
        catch
        {
            // Ignore errors, return null
        }

        return null;
    }

    private static MTexture GetDefaultPortrait()
    {
        const string cacheKey = "__default__";
        if (_cache.TryGetValue(cacheKey, out var cached))
            return cached;

        try
        {
            var sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
            if (sprite.Has("idle") && sprite.Animations.TryGetValue("idle", out var anim) && anim.Frames.Length > 0)
            {
                _cache[cacheKey] = anim.Frames[0];
                return anim.Frames[0];
            }
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    /// <summary>
    /// Clears the portrait cache.
    /// </summary>
    public static void ClearCache()
    {
        _cache.Clear();
    }
}

