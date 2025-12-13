using Celeste.Mod.SkinModHelper;

namespace Celeste.Mod.UltimateMadelineCeleste.Utilities;

/// <summary>
/// Helper for skin-related display logic.
/// </summary>
public static class SkinHelper
{
    /// <summary>
    /// Gets a display-friendly name from a skin ID.
    /// Uses the mod's dialog key if available, otherwise extracts the character name from the ID.
    /// </summary>
    public static string GetDisplayName(string skinId)
    {
        if (string.IsNullOrEmpty(skinId))
            return "Player";

        // Default skin shows as "Madeline"
        if (skinId == SkinsSystem.DEFAULT)
            return "Madeline";

        // Try to get the actual name from the skin config
        if (SkinsSystem.skinConfigs != null &&
            SkinsSystem.skinConfigs.TryGetValue(skinId, out var config))
        {
            // Try to get localized name from dialog
            if (!string.IsNullOrEmpty(config.SkinDialogKey) && Dialog.Has(config.SkinDialogKey))
            {
                return Dialog.Clean(config.SkinDialogKey);
            }
        }

        // Fall back to extracting the last segment (often the character name)
        // Skin IDs are often formatted as "ModName_CharacterName" or "Author_Variant_CharacterName"
        int lastUnderscore = skinId.LastIndexOf('_');
        if (lastUnderscore >= 0 && lastUnderscore < skinId.Length - 1)
        {
            return skinId.Substring(lastUnderscore + 1);
        }

        // If no underscore, just use the skin ID as-is
        return skinId;
    }
}

