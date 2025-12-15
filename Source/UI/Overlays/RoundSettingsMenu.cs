using System;
using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;

/// <summary>
/// A submenu for configuring round settings during pause.
/// Only accessible by the host in the lobby.
/// </summary>
public static class RoundSettingsMenu
{
    private static TextMenu _settingsMenu;
    private static TextMenu _parentMenu;
    private static Action _onClose;

    /// <summary>
    /// Opens the round settings menu.
    /// </summary>
    public static void Open(TextMenu parentMenu, Action onClose)
    {
        _parentMenu = parentMenu;
        _onClose = onClose;

        // Hide the parent menu
        parentMenu.Visible = false;
        parentMenu.Focused = false;

        // Create the settings menu
        _settingsMenu = new TextMenu();

        // Handle cancel (B button / ESC) to close properly
        _settingsMenu.OnCancel = () =>
        {
            Audio.Play("event:/ui/main/button_back");
            Close();
        };
        _settingsMenu.OnESC = () =>
        {
            Audio.Play("event:/ui/main/button_back");
            Close();
        };
        _settingsMenu.OnClose = () =>
        {
            // Ensure parent menu is restored if closed externally
            if (_parentMenu != null)
            {
                _parentMenu.Visible = true;
                _parentMenu.Focused = true;
                _parentMenu = null;
            }
            _settingsMenu = null;
            _onClose?.Invoke();
            _onClose = null;
        };

        BuildMenu();

        // Add to current scene
        var level = Engine.Scene as Level;
        level?.Add(_settingsMenu);
    }

    private static void BuildMenu()
    {
        // Clear existing items except header
        var itemsToRemove = new List<TextMenu.Item>(_settingsMenu.Items);
        foreach (var item in itemsToRemove)
        {
            _settingsMenu.Remove(item);
        }

        _settingsMenu.Add(new TextMenu.Header("Round Settings"));

        var settings = RoundSettings.Current;

        // === WIN CONDITIONS ===
        _settingsMenu.Add(new TextMenu.SubHeader("Win Conditions"));

        // Points to Win (1-20)
        _settingsMenu.Add(new TextMenuExt.IntSlider("Points to Win", 1, 20, (int)settings.PointsToWin)
            .Change(value => settings.PointsToWin = value));

        // === POINT VALUES ===
        _settingsMenu.Add(new TextMenu.SubHeader("Points Per Score Type"));

        // Use slider with 0.1 increments (multiply by 10 for display)
        AddFloatSlider("Finish", 0, 50, settings.ScoreValues.Finish,
            v => settings.ScoreValues.Finish = v);

        AddFloatSlider("First Place", 0, 20, settings.ScoreValues.FirstPlace,
            v => settings.ScoreValues.FirstPlace = v);

        AddFloatSlider("Trap Kill", 0, 20, settings.ScoreValues.TrapKill,
            v => settings.ScoreValues.TrapKill = v);

        AddFloatSlider("Berry", 0, 20, settings.ScoreValues.Berry,
            v => settings.ScoreValues.Berry = v);

        AddFloatSlider("Underdog Bonus", 0, 20, settings.ScoreValues.UnderdogBonus,
            v => settings.ScoreValues.UnderdogBonus = v);

        AddFloatSlider("Solo Finish", 0, 20, settings.ScoreValues.Solo,
            v => settings.ScoreValues.Solo = v);

        // === MINIMUM PER CATEGORY ===
        _settingsMenu.Add(new TextMenu.SubHeader("Minimum Props Per Category"));

        // Show ALL categories, even if not in the dictionary (default to 0)
        AddCategorySlider("Deadly", PropCategory.Deadly, settings);
        AddCategorySlider("Block", PropCategory.Block, settings);
        AddCategorySlider("Movement", PropCategory.Movement, settings);
        AddCategorySlider("Platform", PropCategory.Platform, settings);
        AddCategorySlider("Special", PropCategory.Special, settings);
        AddCategorySlider("Collectible", PropCategory.Collectible, settings);
        AddCategorySlider("Bomb", PropCategory.Bomb, settings);

        // === ACTIONS ===
        _settingsMenu.Add(new TextMenu.SubHeader(""));

        // Reset to Default button
        var resetButton = new TextMenu.Button("Reset to Default");
        resetButton.Pressed(() =>
        {
            Audio.Play("event:/ui/main/button_select");
            RoundSettings.ResetToDefault();
            // Rebuild the menu to reflect new values
            BuildMenu();
        });
        _settingsMenu.Add(resetButton);

        // Back button
        var backButton = new TextMenu.Button("Back");
        backButton.Pressed(() =>
        {
            Audio.Play("event:/ui/main/button_back");
            Close();
        });
        _settingsMenu.Add(backButton);
    }

    private static void AddFloatSlider(string label, int min, int max, float currentValue, Action<float> onChange)
    {
        // Convert float to int (0.1 increments stored as integers 0-50 etc)
        int intValue = (int)(currentValue * 10f);
        intValue = Math.Clamp(intValue, min, max);

        _settingsMenu.Add(new TextMenu.Slider(
            label,
            i => (i / 10f).ToString("0.0"),
            min,
            max,
            intValue
        ).Change(value => onChange(value / 10f)));
    }

    private static void AddCategorySlider(string label, PropCategory category, RoundSettings settings)
    {
        int currentMin = settings.MinimumPerCategory.GetValueOrDefault(category, 0);

        _settingsMenu.Add(new TextMenuExt.IntSlider($"Min {label}", 0, 5, currentMin)
            .Change(value =>
            {
                if (value == 0)
                    settings.MinimumPerCategory.Remove(category);
                else
                    settings.MinimumPerCategory[category] = value;
            }));
    }

    /// <summary>
    /// Closes the round settings menu and returns to parent.
    /// </summary>
    public static void Close()
    {
        if (_settingsMenu != null)
        {
            _settingsMenu.Close();
            // OnClose callback handles the rest
        }
    }

    /// <summary>
    /// Returns true if the settings menu is currently open.
    /// </summary>
    public static bool IsOpen => _settingsMenu != null;
}
