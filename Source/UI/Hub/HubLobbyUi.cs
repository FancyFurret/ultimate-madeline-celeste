using Celeste.Mod.SkinModHelper;
using Celeste.Mod.UltimateMadelineCeleste.Phases.Hub;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI.Hub;

/// <summary>
/// Renders the player slots HUD at the bottom of the screen.
/// </summary>
public class HubLobbyUi : Entity
{
    public static readonly Color[] PlayerColors =
    [
        Calc.HexToColor("E84646"),
        Calc.HexToColor("F5A623"),
        Calc.HexToColor("4A90D9"),
        Calc.HexToColor("7ED321")
    ];

    public HubLobbyUi()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
        Depth = -10000;
    }

    public override void Render()
    {
        base.Render();

        var session = GameSession.Instance;
        if (session == null) return;

        var hubPhase = HubPhase.Instance;

        const float screenWidth = 1920f, screenHeight = 1080f;
        const float slotWidth = 280f, slotHeight = 75f, edgePadding = 80f;
        float availableWidth = screenWidth - edgePadding * 2;
        float slotSpacing = (availableWidth - slotWidth * PlayersController.MaxPlayers) / (PlayersController.MaxPlayers - 1);
        float slotY = screenHeight - slotHeight - 12f;

        for (var i = 0; i < PlayersController.MaxPlayers; i++)
        {
            var player = session.Players.GetAtSlot(i);
            float x = edgePadding + i * (slotWidth + slotSpacing);
            var playerColor = PlayerColors[i];

            Draw.Rect(x, slotY, slotWidth, slotHeight, Color.Black * 0.7f);

            if (player != null)
            {
                bool isSelecting = hubPhase?.IsPlayerSelecting(player) == true;

                if (isSelecting)
                {
                    float pulse = 0.7f + 0.3f * (float)System.Math.Sin(Scene.TimeActive * 4f);
                    ActiveFont.Draw("Choose Character", new Vector2(x + slotWidth / 2f, slotY + slotHeight / 2f),
                        new Vector2(0.5f), Vector2.One * 0.55f, playerColor * pulse);
                }
                else
                {
                    ActiveFont.Draw(player.Name, new Vector2(x + slotWidth / 2f, slotY + 24f),
                        new Vector2(0.5f), Vector2.One * 0.7f, playerColor);

                    string bottomText = !string.IsNullOrEmpty(player.SkinId)
                        ? (player.SkinId == SkinsSystem.DEFAULT ? "Madeline" : player.SkinId.Replace('_', ' '))
                        : (player.IsLocal
                            ? (player.Device?.Type == InputDeviceType.Keyboard ? "Keyboard" : $"Controller {(player.Device?.ControllerIndex ?? 0) + 1}")
                            : "Online");

                    ActiveFont.Draw(bottomText, new Vector2(x + slotWidth / 2f, slotY + slotHeight - 18f),
                        new Vector2(0.5f), Vector2.One * 0.5f, Color.White * 0.6f);
                }
            }
            else
            {
                ActiveFont.Draw("Press", new Vector2(x + slotWidth / 2f - 30f, slotY + slotHeight / 2f),
                    new Vector2(0.5f), Vector2.One * 0.8f, Color.White * 0.9f);
                Input.GuiButton(Input.Jump, Input.PrefixMode.Attached).DrawCentered(
                    new Vector2(x + slotWidth - 60f, slotY + slotHeight / 2f), Color.White, 0.75f);
            }
        }
    }
}
