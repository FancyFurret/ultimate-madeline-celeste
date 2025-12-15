using System;
using System.Collections.Generic;
using Celeste.Mod.Entities;
using Celeste.Mod.SkinModHelper;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Entities;

/// <summary>
/// A pedestal that displays a skin for character selection (like Ultimate Chicken Horse).
/// </summary>
[CustomEntity("UltimateMadelineCeleste/SkinPedestal")]
[Tracked]
public class SkinPedestal : Entity
{
    /// <summary>
    /// The skin name displayed on this pedestal. "Default" for vanilla Madeline.
    /// </summary>
    public string SkinName { get; private set; }
    
    /// <summary>
    /// The display name of the skin (from dialog if available).
    /// </summary>
    public string SkinDisplayName { get; private set; }
    
    /// <summary>
    /// The player sprite used for rendering.
    /// </summary>
    private PlayerSprite _sprite;
    
    /// <summary>
    /// The player hair for rendering.
    /// </summary>
    private PlayerHair _hair;
    
    /// <summary>
    /// The index of this pedestal (used to determine which skin to show).
    /// </summary>
    private readonly int _pedestalIndex;
    
    /// <summary>
    /// Whether this pedestal has been initialized with a skin.
    /// </summary>
    private bool _initialized;
    
    /// <summary>
    /// Cached list of available skins (shared across all pedestals in a room).
    /// </summary>
    private static List<string> _cachedSkins;
    
    /// <summary>
    /// The level ID the cache was created for.
    /// </summary>
    private static string _cachedForLevel;

    public SkinPedestal(EntityData data, Vector2 offset) : base(data.Position + offset)
    {
        _pedestalIndex = data.Int("pedestalIndex", 0);
        
        Depth = Depths.Player;
        
        // Start invisible until we know we have a skin to show
        Visible = false;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        
        // Initialize skin based on pedestal index
        InitializeSkin();
    }
    
    /// <summary>
    /// Initializes the skin for this pedestal based on its index.
    /// </summary>
    private void InitializeSkin()
    {
        if (_initialized) return;
        _initialized = true;
        
        // Get all available skins (cached per level)
        var availableSkins = GetAvailableSkins();
        
        UmcLogger.Info($"SkinPedestal {_pedestalIndex}: {availableSkins.Count} skins available");
        
        // If this pedestal's index is beyond available skins, hide it
        if (_pedestalIndex >= availableSkins.Count)
        {
            UmcLogger.Info($"SkinPedestal {_pedestalIndex}: No skin available at this index, hiding");
            Visible = false;
            Active = false;
            return;
        }
        
        // Create the sprite now that we need it
        CreateSprite(PlayerSpriteMode.Madeline);
        
        // Set the skin for this index
        SetSkin(availableSkins[_pedestalIndex]);
        Visible = true;
    }
    
    /// <summary>
    /// Gets all available skins including Default, randomly shuffled.
    /// Cached per level to ensure consistency.
    /// </summary>
    private static List<string> GetAvailableSkins()
    {
        // Check if we need to rebuild the cache
        string currentLevel = "";
        if (Engine.Scene is Level level)
        {
            currentLevel = level.Session.Area.SID + "_" + level.Session.Level;
        }
        
        if (_cachedSkins != null && _cachedForLevel == currentLevel)
        {
            return _cachedSkins;
        }
        
        var skins = new List<string>();
        
        // Always include default first
        skins.Add(SkinsSystem.DEFAULT);
        
        // Add all registered player skin mods from SkinModHelper Plus
        // skinConfigs is the dictionary of player skins
        if (SkinsSystem.skinConfigs != null)
        {
            UmcLogger.Info($"SkinModHelper Plus has {SkinsSystem.skinConfigs.Count} player skin configs");
            foreach (var kvp in SkinsSystem.skinConfigs)
            {
                string skinName = kvp.Key;
                var config = kvp.Value;
                UmcLogger.Info($"  Found skin: {skinName} (Character_ID: {config.Character_ID})");
                
                // Only include skins that should appear in player list
                if (config.Player_List && skinName != SkinsSystem.DEFAULT)
                {
                    skins.Add(skinName);
                }
            }
        }
        else
        {
            UmcLogger.Warn("SkinsSystem.skinConfigs is null!");
        }
        
        // Shuffle non-default skins using a seeded random for consistency
        // Use level session seed if available, otherwise use a fixed seed
        int seed = Engine.Scene is Level lvl ? lvl.Session.Area.GetHashCode() : 42;
        var random = new Random(seed);
        
        // Fisher-Yates shuffle on everything except the first element (Default)
        for (int i = skins.Count - 1; i > 1; i--)
        {
            int j = random.Next(1, i + 1);
            (skins[i], skins[j]) = (skins[j], skins[i]);
        }
        
        // Cache it
        _cachedSkins = skins;
        _cachedForLevel = currentLevel;
        
        return skins;
    }
    
    /// <summary>
    /// Clears the skin cache (call when skins change).
    /// </summary>
    public static void ClearSkinCache()
    {
        _cachedSkins = null;
        _cachedForLevel = null;
    }
    
    /// <summary>
    /// Sets the skin displayed on this pedestal.
    /// </summary>
    public void SetSkin(string skinName)
    {
        SkinName = skinName;
        
        // Get display name from config or use skin name
        if (skinName == SkinsSystem.DEFAULT)
        {
            SkinDisplayName = "Madeline";
        }
        else if (SkinsSystem.skinConfigs != null && 
                 SkinsSystem.skinConfigs.TryGetValue(skinName, out var config))
        {
            // Try to get localized name from dialog
            if (!string.IsNullOrEmpty(config.SkinDialogKey) && Dialog.Has(config.SkinDialogKey))
            {
                SkinDisplayName = Dialog.Clean(config.SkinDialogKey);
            }
            else
            {
                // Use skin name as fallback, make it more readable
                SkinDisplayName = skinName.Replace('_', ' ');
            }
        }
        else
        {
            SkinDisplayName = skinName;
        }
        
        // Apply the skin to the sprite
        ApplySkinToSprite();
        
        UmcLogger.Info($"SkinPedestal {_pedestalIndex}: Set skin to '{skinName}' ({SkinDisplayName})");
    }
    
    /// <summary>
    /// Creates the player sprite.
    /// </summary>
    private void CreateSprite(PlayerSpriteMode mode)
    {
        if (_sprite != null)
        {
            Remove(_sprite);
        }
        if (_hair != null)
        {
            Remove(_hair);
        }
        
        try
        {
            _sprite = new PlayerSprite(mode);
        }
        catch
        {
            _sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
        }
        
        _hair = new PlayerHair(_sprite);
        _hair.Color = Player.NormalHairColor;
        
        // Center the sprite
        _sprite.Justify = new Vector2(0.5f, 1f);
        
        Add(_hair);
        Add(_sprite);
        
        // Start with idle animation
        if (_sprite.Has("idle"))
        {
            _sprite.Play("idle");
        }
    }
    
    /// <summary>
    /// Applies the current skin to the sprite.
    /// </summary>
    private void ApplySkinToSprite()
    {
        if (string.IsNullOrEmpty(SkinName) || SkinName == SkinsSystem.DEFAULT)
        {
            // Default skin - just recreate the standard sprite
            CreateSprite(PlayerSpriteMode.Madeline);
            return;
        }
        
        // Try to apply custom skin from SkinModHelper Plus
        try
        {
            if (SkinsSystem.skinConfigs.TryGetValue(SkinName, out var config) && 
                !string.IsNullOrEmpty(config.Character_ID))
            {
                string characterId = config.Character_ID;
                
                if (GFX.SpriteBank.Has(characterId))
                {
                    string currentAnim = _sprite?.CurrentAnimationID ?? "idle";
                    
                    // Recreate sprite and apply custom skin
                    if (_sprite != null) Remove(_sprite);
                    if (_hair != null) Remove(_hair);
                    
                    _sprite = new PlayerSprite(PlayerSpriteMode.Madeline);
                    GFX.SpriteBank.CreateOn(_sprite, characterId);
                    
                    _hair = new PlayerHair(_sprite);
                    _hair.Color = Player.NormalHairColor;
                    
                    _sprite.Justify = new Vector2(0.5f, 1f);
                    
                    Add(_hair);
                    Add(_sprite);
                    
                    if (_sprite.Has(currentAnim))
                        _sprite.Play(currentAnim);
                    else if (_sprite.Has("idle"))
                        _sprite.Play("idle");
                        
                    UmcLogger.Info($"Applied custom skin '{characterId}' to pedestal");
                }
                else
                {
                    UmcLogger.Warn($"Character ID '{characterId}' not found in sprite bank, using default");
                    CreateSprite(PlayerSpriteMode.Madeline);
                }
            }
            else
            {
                UmcLogger.Warn($"Skin config for '{SkinName}' not found or has no Character_ID, using default");
                CreateSprite(PlayerSpriteMode.Madeline);
            }
        }
        catch (Exception ex)
        {
            UmcLogger.Error($"Failed to apply skin '{SkinName}' to pedestal: {ex.Message}");
            CreateSprite(PlayerSpriteMode.Madeline);
        }
    }

    public override void Update()
    {
        if (!Active) return;
        
        base.Update();
        
        // Update hair facing
        if (_hair != null)
        {
            _hair.Facing = Facings.Right;
        }
    }
}
