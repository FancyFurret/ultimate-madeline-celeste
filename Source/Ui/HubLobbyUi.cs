using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Core;
using Celeste.Mod.UltimateMadelineCeleste.Networking;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.UI;

public class HubLobbyUi : Entity
{
    private const string HubStageId = "FancyFurret/UltimateMadelineCeleste/Hub";

    public static readonly Color[] PlayerColors =
    [
        Calc.HexToColor("E84646"),
        Calc.HexToColor("F5A623"),
        Calc.HexToColor("4A90D9"),
        Calc.HexToColor("7ED321")
    ];

    private const float LeaveHoldDuration = 0.5f;
    private readonly Dictionary<int, float> _leaveHoldTimes = new();

    public HubLobbyUi()
    {
        Tag = Tags.HUD | Tags.Global | Tags.PauseUpdate;
        Depth = -10000;
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

        if (self.Session.Area.SID == HubStageId)
        {
            // Start a local session if not already in one
            if (!UmcSession.Started)
            {
                MultiplayerController.Instance?.StartLocalSession();
            }

            var existing = self.Entities.FindFirst<HubLobbyUi>();
            if (existing == null)
                self.Add(new HubLobbyUi());
        }
    }

    public override void Update()
    {
        base.Update();

        var session = UmcSession.Instance;
        if (session == null) return;

        var level = Scene as Level;
        if (level == null) return;
        if (level.Paused) return;

        var controller = MultiplayerController.Instance;
        if (controller == null) return;

        // Check for join input from any unclaimed device
        var joinDevice = session.DetectJoinInput();
        if (joinDevice != null)
            UmcSession.Instance.Players.RequestAddPlayer(joinDevice);

        // Check for leave input (hold to leave)
        foreach (var player in session.Players.All)
        {
            if (!player.IsLocal || player.Device == null) continue;
            
            if (IsHoldingLeaveButton(player))
            {
                _leaveHoldTimes.TryAdd(player.SlotIndex, 0f);
                _leaveHoldTimes[player.SlotIndex] += Engine.DeltaTime;

                if (_leaveHoldTimes[player.SlotIndex] >= LeaveHoldDuration)
                {
                    _leaveHoldTimes.Remove(player.SlotIndex);
                    UmcSession.Instance.Players.RemoveLocalPlayer(player);
                    break;
                }
            }
            else
            {
                _leaveHoldTimes.Remove(player.SlotIndex);
            }
        }
    }

    private static bool IsHoldingLeaveButton(UmcPlayer player)
    {
        if (player.Device == null) return false;
        if (player.Device.Type == InputDeviceType.Keyboard)
            return MInput.Keyboard.Check(Keys.K);

        var gamepad = MInput.GamePads[player.Device.ControllerIndex];
        return gamepad.Check(Buttons.Back);
    }

    public override void Render()
    {
        base.Render();

        var level = Scene as Level;
        if (level == null) return;

        var session = UmcSession.Instance;
        if (session == null) return;

        var screenWidth = 1920f;
        var screenHeight = 1080f;

        var slotWidth = 280f;
        var slotHeight = 75f;
        var edgePadding = 80f;
        var availableWidth = screenWidth - (edgePadding * 2);
        var slotSpacing = (availableWidth - (slotWidth * PlayersController.MaxPlayers)) / (PlayersController.MaxPlayers - 1);
        var slotY = screenHeight - slotHeight - 12f;

        var bgColor = Color.Black * 0.7f;

        for (var i = 0; i < PlayersController.MaxPlayers; i++)
        {
            var player = session.Players.GetAtSlot(i);

            var x = edgePadding + (i * (slotWidth + slotSpacing));
            var playerColor = PlayerColors[i];

            // Draw dark background
            Draw.Rect(x, slotY, slotWidth, slotHeight, bgColor);

            if (player != null)
            {
                // Draw player name
                var namePos = new Vector2(x + slotWidth / 2f, slotY + 24f);
                ActiveFont.Draw(
                    player.Name,
                    namePos,
                    new Vector2(0.5f, 0.5f),
                    Vector2.One * 0.7f,
                    playerColor
                );

                // Draw input device indicator
                var deviceText = player.IsLocal == false ? "Online" 
                    : player.Device?.Type == InputDeviceType.Keyboard ? "Keyboard"
                    : $"Controller {(player.Device?.ControllerIndex ?? 0) + 1}";
                var devicePos = new Vector2(x + slotWidth / 2f, slotY + slotHeight - 18f);
                ActiveFont.Draw(
                    deviceText,
                    devicePos,
                    new Vector2(0.5f, 0.5f),
                    Vector2.One * 0.5f,
                    Color.White * 0.6f
                );
            }
            else
            {
                // Draw "Press to join" prompt
                var statusText = "Press";
                var statusPos = new Vector2(x + slotWidth / 2f - 30f, slotY + slotHeight / 2f);

                ActiveFont.Draw(
                    statusText,
                    statusPos,
                    new Vector2(0.5f, 0.5f),
                    Vector2.One * 0.8f,
                    Color.White * 0.9f
                );

                // Draw button prompt
                var buttonIcon = Input.GuiButton(Input.Jump, Input.PrefixMode.Attached);
                var buttonX = x + slotWidth - 60f;
                var buttonY = slotY + slotHeight / 2f;
                var scale = 0.75f;

                buttonIcon.DrawCentered(new Vector2(buttonX, buttonY), Color.White, scale);
            }
        }
    }
}
