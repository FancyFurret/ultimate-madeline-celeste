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

    private const float ScreenWidth = 1920f;
    private const float ScreenHeight = 1080f;
    private const float SlotWidth = 280f;
    private const float SlotHeight = 75f;
    private const float EdgePadding = 80f;

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

        float availableWidth = ScreenWidth - EdgePadding * 2;
        float slotSpacing = (availableWidth - SlotWidth * PlayersController.MaxPlayers) / (PlayersController.MaxPlayers - 1);
        float slotY = ScreenHeight - SlotHeight - 12f;

        for (var i = 0; i < PlayersController.MaxPlayers; i++)
        {
            var player = session.Players.GetAtSlot(i);
            float x = EdgePadding + i * (SlotWidth + slotSpacing);
            var playerColor = PlayerColors[i];

            Draw.Rect(x, slotY, SlotWidth, SlotHeight, Color.Black * 0.7f);

            if (player != null)
            {
                bool isSelecting = hubPhase?.IsPlayerSelecting(player) == true;

                var holdAction = hubPhase?.GetHoldAction(i);
                if (holdAction?.ShowVisual == true)
                {
                    RenderHoldIndicator(x, slotY, holdAction);
                }

                if (isSelecting)
                {
                    float pulse = 0.7f + 0.3f * (float)System.Math.Sin(Scene.TimeActive * 4f);
                    ActiveFont.Draw("Choose Character", new Vector2(x + SlotWidth / 2f, slotY + SlotHeight / 2f),
                        new Vector2(0.5f), Vector2.One * 0.55f, playerColor * pulse);
                }
                else
                {
                    ActiveFont.Draw(player.Name, new Vector2(x + SlotWidth / 2f, slotY + 24f),
                        new Vector2(0.5f), Vector2.One * 0.7f, playerColor);

                    string bottomText = !string.IsNullOrEmpty(player.SkinId)
                        ? (player.SkinId == SkinsSystem.DEFAULT ? "Madeline" : player.SkinId.Replace('_', ' '))
                        : (player.IsLocal
                            ? (player.Device?.Type == InputDeviceType.Keyboard ? "Keyboard" : $"Controller {(player.Device?.ControllerIndex ?? 0) + 1}")
                            : "Online");

                    ActiveFont.Draw(bottomText, new Vector2(x + SlotWidth / 2f, slotY + SlotHeight - 18f),
                        new Vector2(0.5f), Vector2.One * 0.5f, Color.White * 0.6f);
                }
            }
            else
            {
                ActiveFont.Draw("Press", new Vector2(x + SlotWidth / 2f - 30f, slotY + SlotHeight / 2f),
                    new Vector2(0.5f), Vector2.One * 0.8f, Color.White * 0.9f);
                RotatingButtonDisplay.Draw(
                    UmcModule.Settings.ButtonJoin,
                    new Vector2(x + SlotWidth - 60f, slotY + SlotHeight / 2f),
                    Color.White,
                    0.75f,
                    Scene.TimeActive
                );
            }
        }
    }

    private void RenderHoldIndicator(float slotX, float slotY, HoldAction action)
    {
        const float iconScale = 0.5f;
        const float bgPadding = 6f;
        const float slotPadding = 8f;

        var buttonTexture = GetButtonTexture(action);
        if (buttonTexture == null) return;

        // Calculate sizes based on icon dimensions
        float iconWidth = buttonTexture.Width * iconScale;
        float iconHeight = buttonTexture.Height * iconScale;
        float bgWidth = iconWidth + bgPadding * 2;
        float bgHeight = iconHeight + bgPadding * 2;

        // Position above the slot, centered
        float centerX = slotX + SlotWidth / 2f;
        float indicatorY = slotY - bgHeight - slotPadding;

        // Background (black transparent, grows from bottom)
        float progressHeight = bgHeight * action.VisualProgress;
        float bgY = indicatorY + bgHeight - progressHeight;

        Draw.Rect(
            centerX - bgWidth / 2f,
            bgY,
            bgWidth,
            progressHeight,
            Color.Black * 0.5f
        );

        // Button icon centered
        buttonTexture.DrawCentered(
            new Vector2(centerX, indicatorY + bgHeight / 2f),
            Color.White,
            iconScale
        );
    }

    private static MTexture GetButtonTexture(HoldAction action)
    {
        var binding = action.Binding;
        var device = action.Player.Device;

        if (device?.Type == InputDeviceType.Keyboard)
            return Input.GuiKey(binding.Keys.Count > 0 ? binding.Keys[0] : Microsoft.Xna.Framework.Input.Keys.None);

        return binding.Buttons.Count > 0 ? Input.GuiSingleButton(binding.Buttons[0], fallback: null) : null;
    }

}
