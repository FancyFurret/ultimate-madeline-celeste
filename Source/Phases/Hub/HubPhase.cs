using System.Collections.Generic;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Players;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.UI.Hub;
using Celeste.Mod.UltimateMadelineCeleste.UI.Overlays;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Hub;

/// <summary>
/// Main hub phase controller. Handles player join/leave and delegates to CharacterSelection.
/// </summary>
public class HubPhase : Entity
{
    public static HubPhase Instance { get; private set; }

    private readonly Dictionary<int, HoldAction> _activeHoldActions = new();
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

    /// <summary>
    /// Gets the active hold action for a player slot, if any.
    /// </summary>
    public HoldAction GetHoldAction(int slotIndex) => _activeHoldActions.GetValueOrDefault(slotIndex);

    public HubPhase()
    {
        Instance = this;
        Tag = Tags.Global | Tags.PauseUpdate;
        Depth = 0;
    }

    public override void Added(Scene scene)
    {
        base.Added(scene);
        Instance = this;

        _characterSelection = new CharacterSelection(this, scene);
        _levelSelection = new LevelSelection(this, GameSession.Instance?.Players);

        scene.Add(new HubLobbyUi());
        scene.Add(new ConnectingOverlay());

        var lobby = LobbyController.Instance;
        var session = GameSession.Instance;

        // Only request lobby state if we're a new client without player data
        // (returning from playing phase means we already have the data)
        bool needsLobbyState = lobby != null && lobby.IsOnline && !lobby.IsHost
            && (session?.Players.All.Count == 0 || session?.Players.All.All(p => string.IsNullOrEmpty(p.SkinId)) == true);

        if (needsLobbyState)
        {
            lobby.RequestLobbyState();
        }

        // Spawn all session players who have skins selected
        // (spawn methods auto-track with camera)
        if (scene is Level level)
        {
            PlayerSpawner.Instance?.SpawnAllSessionPlayers(level);
        }

        // Ensure all networked entities are in the new scene
        NetworkedEntityRegistry.Instance?.EnsureEntitiesInScene();
    }

    public override void Removed(Scene scene)
    {
        base.Removed(scene);

        _characterSelection?.Cleanup();
        _characterSelection = null;
        _levelSelection?.Cleanup();
        _levelSelection = null;
        _activeHoldActions.Clear();

        if (Instance == this) Instance = null;
    }

    public override void Update()
    {
        base.Update();

        var session = GameSession.Instance;
        if (session == null) return;

        var level = Scene as Level;
        if (level == null || level.Paused) return;

        // Don't process anything while waiting for lobby state
        if (LobbyController.Instance?.IsWaitingForLobbyState == true)
            return;

        // Check for pause input from any local player (needed when no Player entities exist)
        CheckPauseInput(level, session);

        UpdateJoinInput(session);
        UpdateHoldActions(session);
        _characterSelection?.Update(level);
        _characterSelection?.CleanupRemovedPlayers(session);

        // Only run level selection when not in character selection
        if (!IsSelecting)
        {
            _levelSelection?.Update(level);
        }
    }

    /// <summary>
    /// Checks for pause input directly. This is needed because when no Player
    /// entities are spawned, the normal pause handling doesn't work.
    /// </summary>
    private void CheckPauseInput(Level level, GameSession session)
    {
        if (level.Tracker.GetEntity<Player>() != null) return;

        if (Input.ESC.Pressed)
        {
            Input.ESC.ConsumePress();
            level.Pause();
        }
    }

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

    private void UpdateHoldActions(GameSession session)
    {
        if (IsLevelTransitioning)
        {
            _activeHoldActions.Clear();
            return;
        }

        // Update existing hold actions
        var completedSlots = new List<int>();
        foreach (var kvp in _activeHoldActions)
        {
            var action = kvp.Value;
            if (!action.IsStillHolding())
            {
                completedSlots.Add(kvp.Key);
                continue;
            }

            if (action.Update())
            {
                completedSlots.Add(kvp.Key);
            }
        }

        foreach (var slot in completedSlots)
            _activeHoldActions.Remove(slot);

        // Check for new hold actions
        foreach (var player in session.Players.All)
        {
            if (!player.IsLocal || player.Device == null) continue;
            if (_activeHoldActions.ContainsKey(player.SlotIndex)) continue;

            // Check if player started holding the leave button
            if (IsStartingHold(player, UmcModule.Settings.ButtonLeave))
            {
                // When selecting a character, allow leaving directly
                // When spawned (has skin), go back to selection first
                var actionType = IsPlayerSelecting(player) || string.IsNullOrEmpty(player.SkinId)
                    ? HoldActionType.Leave
                    : HoldActionType.BackToSelection;

                var capturedPlayer = player;
                _activeHoldActions[player.SlotIndex] = new HoldAction(
                    player,
                    actionType,
                    UmcModule.Settings.ButtonLeave,
                    () => ExecuteHoldAction(session, capturedPlayer, actionType)
                );
            }
        }
    }

    private void ExecuteHoldAction(GameSession session, UmcPlayer player, HoldActionType actionType)
    {
        switch (actionType)
        {
            case HoldActionType.Leave:
                _characterSelection?.CancelSelection(player);
                _characterSelection?.ReleasePedestal(player);
                session.Players.RemoveLocalPlayer(player);
                break;

            case HoldActionType.BackToSelection:
                _characterSelection?.ReturnToSelection(player);
                break;

            case HoldActionType.GiveUp:
                // Future implementation
                break;
        }
    }

    private static bool IsStartingHold(UmcPlayer player, ButtonBinding binding)
    {
        if (player.Device == null) return false;

        if (player.Device.Type == InputDeviceType.Keyboard)
        {
            foreach (var key in binding.Keys)
                if (MInput.Keyboard.Pressed(key)) return true;
            return false;
        }

        var gamepad = MInput.GamePads[player.Device.ControllerIndex];
        foreach (var button in binding.Buttons)
            if (gamepad.Pressed(button)) return true;
        return false;
    }
}

