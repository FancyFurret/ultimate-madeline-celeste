using Celeste.Mod.UltimateMadelineCeleste.Core;
using Celeste.Mod.UltimateMadelineCeleste.Networking;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI;

/// <summary>
/// Global overlay that displays connected Steam players in the top right corner.
/// Shows when connected to an online lobby.
/// </summary>
public class OnlinePlayersOverlay : Entity
{
    public OnlinePlayersOverlay()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate | Tags.TransitionUpdate;
        Depth = -10001;
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

        if (!UmcSession.Started) return;

        var existing = self.Entities.FindFirst<OnlinePlayersOverlay>();
        if (existing == null)
        {
            self.Add(new OnlinePlayersOverlay());
        }
    }

    public override void Render()
    {
        base.Render();

        var controller = MultiplayerController.Instance;
        if (controller?.IsOnline != true) return;

        var lobbyMembers = SteamLobbyManager.Instance?.LobbyMembers;
        if (lobbyMembers == null || lobbyMembers.Count == 0) return;

        const float screenWidth = 1920f;
        const float padding = 15f;
        const float lineHeight = 26f;
        const float boxPadding = 10f;

        var startY = 15f;
        var startX = screenWidth - padding;

        // Calculate box dimensions
        var maxNameWidth = 0f;
        foreach (var member in lobbyMembers)
        {
            var displayName = member.Name;
            if (member.IsHost) displayName += " ★";
            var nameWidth = ActiveFont.Measure(displayName).X * 0.4f;
            if (nameWidth > maxNameWidth) maxNameWidth = nameWidth;
        }

        var boxWidth = maxNameWidth + 30f;
        var boxHeight = (lobbyMembers.Count * lineHeight) + (boxPadding * 2) + 22f;

        // Draw background
        Draw.Rect(startX - boxWidth - boxPadding, startY - boxPadding, boxWidth + (boxPadding * 2), boxHeight, Color.Black * 0.65f);

        // Draw header with connection indicator
        var headerColor = Color.LightGreen;
        var headerText = "● Online";
        ActiveFont.Draw(
            headerText,
            new Vector2(startX - boxWidth / 2f, startY + 2f),
            new Vector2(0.5f, 0f),
            Vector2.One * 0.4f,
            headerColor
        );

        // Draw each connected player
        var y = startY + 24f;
        foreach (var member in lobbyMembers)
        {
            var nameColor = member.IsLocal ? Calc.HexToColor("F5A623") : Color.White * 0.9f;
            var displayName = member.Name;
            if (member.PlayerCount > 1) displayName += $" ({member.PlayerCount})";
            if (member.IsHost) displayName += " ★";

            ActiveFont.Draw(
                displayName,
                new Vector2(startX - boxPadding, y),
                new Vector2(1f, 0f),
                Vector2.One * 0.4f,
                nameColor
            );

            y += lineHeight;
        }
    }
}
