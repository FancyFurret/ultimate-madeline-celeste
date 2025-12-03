using System.Collections.Generic;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.UI.Hub;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Hub;

/// <summary>
/// Main hub phase controller. Handles player join/leave and delegates to CharacterSelection.
/// </summary>
public class HubPhase : Entity
{
    public static HubPhase Instance { get; private set; }

    private const string HubStageId = "FancyFurret/UltimateMadelineCeleste/Hub";
    private const float LeaveHoldDuration = 0.5f;

    private readonly Dictionary<int, float> _leaveHoldTimes = new();
    private CharacterSelection _characterSelection;
    private LevelSelection _levelSelection;

    public bool IsSelecting => _characterSelection?.IsSelecting ?? false;
    public bool IsCountdownActive => _levelSelection?.IsCountdownActive ?? false;

    /// <summary>
    /// When true, players cannot join and movement is frozen.
    /// </summary>
    public bool IsLevelTransitioning { get; set; }
    public bool IsPlayerSelecting(UmcPlayer player) => _characterSelection?.IsPlayerSelecting(player) ?? false;
    public Vector2? GetSpawnPosition(UmcPlayer player) => _characterSelection?.GetSpawnPosition(player);

    public HubPhase()
    {
        Instance = this;
        Tag = Tags.Global | Tags.PauseUpdate;
        Depth = 0;
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
            if (!GameSession.Started)
                NetworkManager.Instance?.StartLocalSession();

            if (self.Entities.FindFirst<HubPhase>() == null)
                self.Add(new HubPhase());
        }
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Instance = this;

        var players = GameSession.Instance?.Players;
        _characterSelection = new CharacterSelection(scene, players);
        _levelSelection = new LevelSelection(scene, players);

        scene.Add(new HubLobbyUi());

        var net = NetworkManager.Instance;
        if (net != null)
        {
            _characterSelection.RegisterMessages(net.Messages);
            _levelSelection.RegisterMessages(net.Messages);
        }
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);

        _characterSelection?.Cleanup();
        _characterSelection = null;
        _levelSelection?.Cleanup();
        _levelSelection = null;
        _leaveHoldTimes.Clear();

        if (Instance == this) Instance = null;
    }

    public override void Update()
    {
        base.Update();

        var session = GameSession.Instance;
        if (session == null) return;

        var level = Scene as Level;
        if (level == null || level.Paused) return;

        UpdateJoinInput(session);
        UpdateLeaveInput(session);
        UpdateBackToSelection(session);
        _characterSelection?.Update(level);
        _characterSelection?.CleanupRemovedPlayers(session);

        // Only run level selection when not in character selection
        if (!IsSelecting)
        {
            _levelSelection?.Update(level);
        }
    }

    #region Player Join/Leave

    private void UpdateJoinInput(GameSession session)
    {
        if (IsLevelTransitioning) return;

        var joinDevice = session.Players.DetectJoinInput();
        if (joinDevice != null)
        {
            session.Players.RequestAddPlayer(joinDevice, autoSpawn: false, (success, slotIndex) =>
            {
                if (success)
                {
                    var player = session.Players.GetAtSlot(slotIndex);
                    if (player != null) _characterSelection?.StartSelection(player);
                }
            });
        }
    }

    private void UpdateLeaveInput(GameSession session)
    {
        if (IsLevelTransitioning) return;

        foreach (var player in session.Players.All)
        {
            if (!player.IsLocal || player.Device == null) continue;
            if (IsPlayerSelecting(player)) continue;

            if (IsHoldingLeaveButton(player))
            {
                _leaveHoldTimes.TryAdd(player.SlotIndex, 0f);
                _leaveHoldTimes[player.SlotIndex] += Engine.DeltaTime;

                if (_leaveHoldTimes[player.SlotIndex] >= LeaveHoldDuration)
                {
                    _leaveHoldTimes.Remove(player.SlotIndex);
                    _characterSelection?.ReleasePedestal(player);
                    session.Players.RemoveLocalPlayer(player);
                    break;
                }
            }
            else
            {
                _leaveHoldTimes.Remove(player.SlotIndex);
            }
        }
    }

    private void UpdateBackToSelection(GameSession session)
    {
        if (IsLevelTransitioning) return;

        foreach (var player in session.Players.All)
        {
            if (!player.IsLocal || player.Device == null) continue;
            if (IsPlayerSelecting(player)) continue;
            if (string.IsNullOrEmpty(player.SkinId)) continue;

            if (IsPressingBackButton(player))
            {
                _characterSelection?.ReturnToSelection(player);
                break;
            }
        }
    }

    private static bool IsHoldingLeaveButton(UmcPlayer player)
    {
        if (player.Device == null) return false;
        if (player.Device.Type == InputDeviceType.Keyboard)
            return MInput.Keyboard.Check(Keys.K);
        return MInput.GamePads[player.Device.ControllerIndex].Check(Buttons.Back);
    }

    private static bool IsPressingBackButton(UmcPlayer player)
    {
        if (player.Device == null) return false;
        var playerEntity = PlayerSpawner.Instance?.GetLocalPlayer(player);
        var controller = playerEntity?.Get<LocalPlayerController>();
        return controller?.PlayerInput?.MenuCancel.Pressed ?? false;
    }

    #endregion
}

