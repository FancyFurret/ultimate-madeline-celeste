
using System;
using Celeste.Mod.SkinModHelper;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Players;

/// <summary>
/// Component attached to local Player entities to handle per-player input routing and skin application.
/// </summary>
public class LocalPlayerController : Component
{
    public UmcPlayer UmcPlayer { get; }
    public Player Player => Entity as Player;
    public PlayerInput PlayerInput { get; private set; }

    public string SkinId
    {
        get => UmcPlayer.SkinId;
        set => UmcPlayer.SkinId = value;
    }

    private VirtualIntegerAxis _origMoveX;
    private VirtualIntegerAxis _origMoveY;
    private VirtualButton _origJump;
    private VirtualButton _origDash;
    private VirtualButton _origGrab;
    private VirtualButton _origTalk;
    private VirtualButton _origPause;
    private VirtualButton _origQuickRestart;
    private VirtualButton _origCrouchDash;
    private VirtualJoystick _origAim;
    private VirtualJoystick _origMountainAim;

    public LocalPlayerController(UmcPlayer umcPlayer) : base(active: true, visible: false)
    {
        UmcPlayer = umcPlayer;
    }

    public override void Added(Entity entity)
    {
        base.Added(entity);

        if (entity is not global::Celeste.Player)
        {
            UmcLogger.Error("LocalPlayerController can only be added to Player entities");
            RemoveSelf();
            return;
        }

        PlayerInput = new PlayerInput(UmcPlayer.Device);
        UmcLogger.Info($"LocalPlayerController attached to player for {UmcPlayer.Name}");
        ApplySkin();
    }

    public void SwapInputsIn()
    {
        _origMoveX = Input.MoveX;
        _origMoveY = Input.MoveY;
        _origJump = Input.Jump;
        _origDash = Input.Dash;
        _origGrab = Input.Grab;
        _origTalk = Input.Talk;
        _origPause = Input.Pause;
        _origQuickRestart = Input.QuickRestart;
        _origCrouchDash = Input.CrouchDash;
        _origAim = Input.Aim;
        _origMountainAim = Input.MountainAim;

        Input.MoveX = PlayerInput.MoveX;
        Input.MoveY = PlayerInput.MoveY;
        Input.GliderMoveY = PlayerInput.GliderMoveY;
        Input.Jump = PlayerInput.Jump;
        Input.Dash = PlayerInput.Dash;
        Input.Grab = PlayerInput.Grab;
        Input.Talk = PlayerInput.Talk;
        Input.Pause = PlayerInput.Pause;
        Input.QuickRestart = PlayerInput.QuickRestart;
        Input.CrouchDash = PlayerInput.CrouchDash;
        Input.Aim = PlayerInput.Aim;
        Input.Feather = PlayerInput.Feather;
        Input.MountainAim = PlayerInput.MountainAim;
    }

    public void SwapInputsOut()
    {
        Input.MoveX = _origMoveX;
        Input.MoveY = _origMoveY;
        Input.Jump = _origJump;
        Input.Dash = _origDash;
        Input.Grab = _origGrab;
        Input.Talk = _origTalk;
        Input.Pause = _origPause;
        Input.QuickRestart = _origQuickRestart;
        Input.CrouchDash = _origCrouchDash;
        Input.Aim = _origAim;
        Input.MountainAim = _origMountainAim;
    }

    public void ApplySkin()
    {
        if (string.IsNullOrEmpty(SkinId) || SkinId == SkinsSystem.DEFAULT) return;
        if (Player == null) return;

        try
        {
            if (SkinsSystem.skinConfigs == null ||
                !SkinsSystem.skinConfigs.TryGetValue(SkinId, out var config) ||
                string.IsNullOrEmpty(config.Character_ID))
            {
                UmcLogger.Warn($"Skin config for '{SkinId}' not found or has no Character_ID");
                return;
            }

            string characterId = config.Character_ID;

            if (!GFX.SpriteBank.Has(characterId))
            {
                UmcLogger.Warn($"Character ID '{characterId}' not found in sprite bank");
                return;
            }

            GFX.SpriteBank.CreateOn(Player.Sprite, characterId);

            if (Player.Sprite.Has("idle"))
            {
                Player.Sprite.Play("idle");
            }

            UmcLogger.Info($"Applied skin '{SkinId}' (character: {characterId}) to {UmcPlayer.Name}");
        }
        catch (Exception ex)
        {
            UmcLogger.Error($"Failed to apply skin '{SkinId}' to {UmcPlayer.Name}: {ex.Message}");
        }
    }

    public override void Removed(Entity entity)
    {
        base.Removed(entity);
        PlayerInput?.Deregister();
        PlayerInput = null;
        UmcLogger.Info($"LocalPlayerController removed from player for {UmcPlayer.Name}");
    }
}
