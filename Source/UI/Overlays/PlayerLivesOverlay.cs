using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.UI.Hub;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;

/// <summary>
/// Small HUD overlay that displays remaining lives per player during platforming.
/// </summary>
public class PlayerLivesOverlay : Entity
{
    private const float Padding = 14f;
    private const float LineHeight = 22f;
    private const float TextScale = 0.38f;

    public PlayerLivesOverlay()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;
        Depth = -10002;
    }

    public static void Load()
    {
        On.Celeste.Level.LoadLevel += OnLevelLoad;
    }

    public static void Unload()
    {
        On.Celeste.Level.LoadLevel -= OnLevelLoad;
    }

    private static void OnLevelLoad(On.Celeste.Level.orig_LoadLevel orig, Level self, Player.IntroTypes playerIntro, bool isFromLoader)
    {
        orig(self, playerIntro, isFromLoader);
        if (!GameSession.Started) return;

        var existing = self.Entities.FindFirst<PlayerLivesOverlay>();
        if (existing == null)
            self.Add(new PlayerLivesOverlay());
    }

    public override void Render()
    {
        base.Render();

        if (!GameSession.Started) return;
        if (RoundState.Current == null) return;
        if (PlayingPhase.Instance?.SubPhase != PlayingSubPhase.Platforming) return;

        var session = GameSession.Instance;
        if (session?.Players?.All == null || session.Players.All.Count == 0) return;

        var players = session.Players.All.OrderBy(p => p.SlotIndex).ToList();
        if (players.Count == 0) return;

        // Measure for background box.
        float maxWidth = 0f;
        foreach (var p in players)
        {
            int lives = RoundState.Current.GetLivesRemaining(p);
            string livesText = lives <= 0 ? "OUT" : $"♥ {lives}";
            string line = $"{p.Name}  {livesText}";
            float w = ActiveFont.Measure(line).X * TextScale;
            if (w > maxWidth) maxWidth = w;
        }

        const float headerHeight = 22f;
        float boxWidth = maxWidth + Padding * 2f;
        float boxHeight = headerHeight + players.Count * LineHeight + Padding;

        float x = Padding;
        float y = Padding;

        Draw.Rect(x, y, boxWidth, boxHeight, Color.Black * 0.55f);
        ActiveFont.Draw("Lives", new Vector2(x + Padding, y + 4f), new Vector2(0f, 0f), Vector2.One * TextScale, Color.White * 0.9f);

        float lineY = y + headerHeight;
        foreach (var p in players)
        {
            int lives = RoundState.Current.GetLivesRemaining(p);
            string livesText = lives <= 0 ? "OUT" : $"♥ {lives}";
            string line = $"{p.Name}  {livesText}";

            var slotColor = HubLobbyUi.PlayerColors[p.SlotIndex % HubLobbyUi.PlayerColors.Length];
            var color = lives <= 0 ? Color.Gray * 0.85f : Color.White * 0.95f;

            // small color marker
            Draw.Rect(x + 6f, lineY + 6f, 10f, 10f, slotColor * 0.9f);
            ActiveFont.Draw(line, new Vector2(x + Padding + 6f, lineY), new Vector2(0f, 0f), Vector2.One * TextScale, color);
            lineY += LineHeight;
        }
    }
}

