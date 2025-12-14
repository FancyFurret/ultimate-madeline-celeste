using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Scoring;

/// <summary>
/// The type of score awarded.
/// </summary>
public enum ScoreType
{
    Finish,
    FirstPlace,
    TrapKill,
    Berry,
    UnderdogBonus,
    Solo
}

/// <summary>
/// Extension methods for ScoreType enum.
/// </summary>
public static class ScoreTypeExtensions
{
    /// <summary>Gets the display label for this score type.</summary>
    public static string GetLabel(this ScoreType type) => type switch
    {
        ScoreType.Finish => "FINISH!",
        ScoreType.FirstPlace => "FIRST!",
        ScoreType.TrapKill => "TRAP!",
        ScoreType.Berry => "BERRY!",
        ScoreType.UnderdogBonus => "UNDERDOG!",
        ScoreType.Solo => "SOLO!",
        _ => ""
    };

    /// <summary>Gets the color for this score type.</summary>
    public static Color GetColor(this ScoreType type) => type switch
    {
        ScoreType.Finish => Calc.HexToColor("162b61"),
        ScoreType.FirstPlace => Calc.HexToColor("10a510"),
        ScoreType.TrapKill => Calc.HexToColor("fc690a"),
        ScoreType.Berry => Calc.HexToColor("d72929"),
        ScoreType.UnderdogBonus => Calc.HexToColor("ad33ab"),
        ScoreType.Solo => Calc.HexToColor("038feb"),
        _ => Color.White
    };

    /// <summary>Gets the base point value for this score type from RoundSettings.</summary>
    public static float GetBasePoints(this ScoreType type) => RoundSettings.Current.GetScorePoints(type);
}

/// <summary>
/// A single score segment with its type and points.
/// </summary>
public class ScoreSegment
{
    public ScoreType Type { get; set; }
    public float Points { get; set; }

    public ScoreSegment(ScoreType type, float points)
    {
        Type = type;
        Points = points;
    }
}

/// <summary>
/// Configuration for scoring system - all values easily configurable.
/// </summary>
public static class ScoringConfig
{
    // ============== WIN CONDITIONS (from RoundSettings) ==============

    /// <summary>Points required to win the game.</summary>
    public static float PointsToWin => RoundSettings.Current.PointsToWin;

    /// <summary>Point difference required to be considered an underdog.</summary>
    public static float UnderdogThreshold => RoundSettings.Current.UnderdogThreshold;

    /// <summary>Minimum players for solo bonus to apply.</summary>
    public static int SoloMinPlayers => RoundSettings.Current.SoloMinPlayers;
    public static int FirstMinPlayers => RoundSettings.Current.FirstMinPlayers;

    /// <summary>Total number of rounds to play (0 = unlimited, play until points reached).</summary>
    public static int MaxRounds => RoundSettings.Current.MaxRounds;

    // ============== UI COLORS ==============

    /// <summary>Color for the grid lines, numbers, and text.</summary>
    public static Color GridColor = Calc.HexToColor("3a3c4d");

    /// <summary>Color for the banner.</summary>
    public static Color BannerColor = Calc.HexToColor("ee6e6b") * 0.85f;

    // ============== PLAYER COLORS ==============

    /// <summary>Colors for each player slot (matches HubLobbyUi).</summary>
    public static readonly Color[] PlayerColors =
    [
        Calc.HexToColor("E84646"), // Red
        Calc.HexToColor("F5A623"), // Orange  
        Calc.HexToColor("4A90D9"), // Blue
        Calc.HexToColor("7ED321")  // Green
    ];

    // ============== ANIMATION DURATIONS (in seconds) ==============

    /// <summary>Duration for the card to slide in from the bottom.</summary>
    public static float CardSlideInDuration = 0.5f;

    /// <summary>Duration for the card to slide out to the bottom.</summary>
    public static float CardSlideOutDuration = 0.4f;

    /// <summary>Duration for each score bar to animate (grow left to right).</summary>
    public static float ScoreBarAnimDuration = 0.6f;

    /// <summary>Delay between animating each score type.</summary>
    public static float ScoreTypeDelay = 0.3f;

    /// <summary>Duration for score type label to fade in.</summary>
    public static float LabelFadeInDuration = 0.2f;

    /// <summary>Duration for score type label to fade out.</summary>
    public static float LabelFadeOutDuration = 0.2f;

    /// <summary>Duration to hold on "Too Easy" or "No Winners" message.</summary>
    public static float SpecialMessageDuration = 2.0f;

    /// <summary>Delay before card starts sliding out after all scores shown.</summary>
    public static float PostScoreDelay = 0.3f;

    /// <summary>Duration for victory sequence focus on flag.</summary>
    public static float VictoryFocusDuration = 0.8f;

    /// <summary>Duration for victory text display.</summary>
    public static float VictoryTextDuration = 3.0f;

    // ============== SCOREBOARD LAYOUT (at 320x180 native resolution) ==============

    /// <summary>Native resolution width.</summary>
    public static float NativeWidth = 320f;

    /// <summary>Native resolution height.</summary>
    public static float NativeHeight = 180f;

    /// <summary>Scale factor for HUD rendering (1920/320 = 6).</summary>
    public static float HudScale = 6f;

    /// <summary>Width of the scoreboard card.</summary>
    public static float CardWidth = 266f;

    /// <summary>Height of the scoreboard card.</summary>
    public static float CardHeight = 132f;

    /// <summary>Height of each player's score row in native pixels.</summary>
    public static float RowHeight = 22f;

    /// <summary>Height of the score bar in native pixels.</summary>
    public static float ScoreBarHeight = 20f;

    /// <summary>Width reserved for player name/avatar.</summary>
    public static float PlayerLabelWidth = 50f;

    /// <summary>Width of the score bar texture.</summary>
    public static float BarWidth = 45f;


    /// <summary>Maximum width of the score bar area (for 5 points).</summary>
    public static float BarAreaWidth = 170f;

    /// <summary>Padding inside the card.</summary>
    public static float CardPadding = 8f;

    /// <summary>Height of the bottom axis area (for numbers).</summary>
    public static float AxisHeight = 14f;

    /// <summary>Width of the banner texture.</summary>
    public static float BannerWidth = 175f;

    /// <summary>Height of the banner texture.</summary>
    public static float BannerHeight = 76f;

    /// <summary>Rotation angle for banner sprite (in radians).</summary>
    public static float BannerRotation = -0.15f;

    /// <summary>Rotation angle for special message text (in radians).</summary>
    public static float SpecialMessageTextRotation = -0.45f;

    /// <summary>Scale for special message text.</summary>
    public static float SpecialMessageTextScale = 1.8f;

    // ============== SPECIAL MESSAGE LABELS ==============

    public static string TooEasyLabel = "Too Easy! No Points!";
    public static string NoWinnersLabel = "No Winners! No Points!";
}
