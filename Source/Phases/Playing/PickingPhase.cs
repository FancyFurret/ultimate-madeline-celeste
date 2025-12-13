using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Entities;
using Celeste.Mod.UltimateMadelineCeleste.Props;
using Celeste.Mod.UltimateMadelineCeleste.Network;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Session;
using Celeste.Mod.UltimateMadelineCeleste.UI.Hub;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Microsoft.Xna.Framework;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Phases.Playing;

public class PickingPhase
{
    public enum PickingState
    {
        WaitingToStart,
        BoxAnimating,
        Picking
    }

    private const float FirstRoundDelay = 2f;
    private const float SubsequentRoundDelay = 0.3f;
    private const int PropCount = 5;

    public PickingState State { get; private set; } = PickingState.WaitingToStart;
    private float _stateTimer;

    private PartyBox _partyBox;
    private Vector2 _boxPosition;

    private List<Prop> _selectedProps = new();
    private readonly Random _random = new();

    private PlayerCursors _cursors;

    private readonly Dictionary<UmcPlayer, Prop> _playerSelections = new();
    private readonly Dictionary<UmcPlayer, Entity> _previewEntities = new();

    public event Action<Dictionary<UmcPlayer, Prop>> OnComplete;

    private Level _level;
    private bool IsHost => NetworkManager.Instance?.IsHost ?? true;

    public PickingPhase(Level level, Vector2 boxPosition)
    {
        _boxPosition = boxPosition;
        _level = level;
        _cursors = new PlayerCursors(level, onConfirm: HandlePickingConfirm, trackWithCamera: false);

        // Register network handlers
        NetworkManager.Handle<PickingStateMessage>(HandlePickingState);
        NetworkManager.Handle<PropsSelectedMessage>(HandlePropsSelected);
        NetworkManager.Handle<PropPickedMessage>(HandlePropPicked);
        NetworkManager.Handle<PickingCompleteMessage>(HandlePickingComplete);
    }

    public void Cleanup()
    {
        _cursors?.RemoveAll();
        UmcLogger.Info("Picking phase cleaned up");
    }

    public void Update()
    {
        if (_level.Paused) return;
        _stateTimer += Engine.DeltaTime;

        if (State == PickingState.WaitingToStart)
            UpdateWaiting();
    }

    private void UpdateWaiting()
    {
        // Only host controls timing
        if (!IsHost) return;

        float delay = RoundState.Current?.RoundNumber == 1 ? FirstRoundDelay : SubsequentRoundDelay;
        if (_stateTimer >= delay)
        {
            StartBoxAnimation();
        }
    }

    private void StartBoxAnimation()
    {
        State = PickingState.BoxAnimating;
        _stateTimer = 0f;

        // Host selects props and broadcasts to clients
        if (IsHost)
        {
            SelectRandomProps();
            BroadcastPropsSelected();
        }

        CreatePartyBox();
        _partyBox.StartAnimation();

        UmcLogger.Info($"Party box animation started with {_selectedProps.Count} props");
    }

    private void CreatePartyBox()
    {
        _partyBox = new PartyBox(_boxPosition, _selectedProps);
        _partyBox.OnOpenComplete += OnBoxOpened;
        _partyBox.OnClosed += OnBoxClosed;
        _level.Add(_partyBox);
    }

    private void SelectRandomProps()
    {
        _selectedProps.Clear();

        var allProps = PropRegistry.All;
        if (allProps.Count == 0)
        {
            UmcLogger.Warn("No props registered!");
            return;
        }

        var available = allProps.ToList();
        for (int i = 0; i < PropCount; i++)
        {
            var index = _random.Next(available.Count);
            _selectedProps.Add(available[index]);
        }

        UmcLogger.Info($"Selected props: {string.Join(", ", _selectedProps.Select(p => p.Name))}");
    }

    private void OnBoxOpened()
    {
        State = PickingState.Picking;
        _stateTimer = 0f;

        _cursors.SpawnForLocalPlayers();

        if (IsHost)
        {
            BroadcastPickingState();
        }

        UmcLogger.Info("Party box opened - picking phase started");
    }

    private void OnBoxClosed()
    {
        OnComplete?.Invoke(_playerSelections);
        UmcLogger.Info("Party box closed - transitioning to placing phase");
    }

    private void HandlePickingConfirm(UmcPlayer player, Vector2 position)
    {
        if (_playerSelections.ContainsKey(player))
        {
            UmcLogger.Info($"Player {player.Name} already picked a prop");
            return;
        }

        var slotResult = _partyBox?.GetSlotAtPosition(position);
        if (slotResult == null)
        {
            UmcLogger.Info($"Player {player.Name} confirmed but no prop under cursor");
            Audio.Play("event:/ui/main/button_invalid");
            return;
        }

        var (slotIndex, prop) = slotResult.Value;

        // If online, send to host for validation; otherwise process locally
        if (NetworkManager.Instance?.IsOnline == true && !IsHost)
        {
            NetworkManager.SendToHost(new PropPickedMessage
            {
                PlayerIndex = player.SlotIndex,
                SlotIndex = slotIndex
            });
        }
        else
        {
            // Host or local - process and broadcast
            ProcessPropPick(player, slotIndex);

            if (NetworkManager.Instance?.IsOnline == true)
            {
                NetworkManager.Broadcast(new PropPickedMessage
                {
                    PlayerIndex = player.SlotIndex,
                    SlotIndex = slotIndex
                });
            }
        }
    }

    private void ProcessPropPick(UmcPlayer player, int slotIndex)
    {
        if (_playerSelections.ContainsKey(player)) return;

        var prop = _partyBox?.GetPropAtSlot(slotIndex);
        if (prop == null) return;

        _playerSelections[player] = prop;
        _partyBox?.RemoveSlot(slotIndex);
        _cursors.Remove(player);

        Audio.Play("event:/ui/main/button_select");
        UmcLogger.Info($"Player {player.Name} selected slot {slotIndex}: {prop.Name}");

        CheckAllPlayersPicked();
    }

    private void CheckAllPlayersPicked()
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var allPlayers = session.Players.All;
        var totalPlayers = allPlayers.Count;
        var pickedCount = _playerSelections.Count;

        UmcLogger.Info($"Picked: {pickedCount}/{totalPlayers}");

        if (pickedCount >= totalPlayers)
        {
            // Host triggers completion
            if (IsHost)
            {
                NetworkManager.BroadcastWithSelf(new PickingCompleteMessage());
            }
        }
    }

    public void CompletePicking()
    {
        _cursors.RemoveAll();

        foreach (var entity in _previewEntities.Values)
            entity?.RemoveSelf();

        _previewEntities.Clear();

        _partyBox?.StartCloseAnimation();

        UmcLogger.Info("Picking complete - waiting for box to close");
    }

    #region Network Broadcasting

    private void BroadcastPropsSelected()
    {
        if (!IsHost) return;

        var net = NetworkManager.Instance;
        if (net?.IsOnline != true) return;

        net.Messages.Broadcast(new PropsSelectedMessage
        {
            PropIds = _selectedProps.Select(p => p.Id).ToArray(),
            BoxPositionX = _boxPosition.X,
            BoxPositionY = _boxPosition.Y
        });
    }

    private void BroadcastPickingState()
    {
        var net = NetworkManager.Instance;
        if (net?.IsOnline != true || !net.IsHost) return;

        net.Messages.Broadcast(new PickingStateMessage
        {
            State = (byte)State
        });
    }

    #endregion

    #region Network Handlers

    private void HandlePickingState(PickingStateMessage message)
    {
        if (IsHost) return;

        var newState = (PickingState)message.State;
        if (newState == PickingState.Picking && State != PickingState.Picking)
        {
            State = PickingState.Picking;
            _stateTimer = 0f;

            // Ensure party box exists
            if (_partyBox == null && _selectedProps.Count > 0)
            {
                CreatePartyBox();
            }

            _cursors.SpawnForLocalPlayers();
            UmcLogger.Info("Synced to picking state from host");
        }
    }

    private void HandlePropsSelected(PropsSelectedMessage message)
    {
        if (IsHost) return;

        _selectedProps.Clear();
        foreach (var propId in message.PropIds)
        {
            var prop = PropRegistry.Get(propId);
            if (prop != null)
                _selectedProps.Add(prop);
        }

        _boxPosition = new Vector2(message.BoxPositionX, message.BoxPositionY);

        // Start animation to sync with host
        State = PickingState.BoxAnimating;
        _stateTimer = 0f;
        CreatePartyBox();
        _partyBox.StartAnimation();

        UmcLogger.Info($"Received props from host: {string.Join(", ", _selectedProps.Select(p => p.Name))}");
    }

    private void HandlePropPicked(CSteamID sender, PropPickedMessage message)
    {
        var session = GameSession.Instance;
        if (session == null) return;

        var player = session.Players.GetAtSlot(message.PlayerIndex);
        if (player == null) return;

        // If we're host and received from client, validate and rebroadcast
        if (IsHost && sender.m_SteamID != NetworkManager.Instance.LocalClientId)
        {
            ProcessPropPick(player, message.SlotIndex);
            NetworkManager.Broadcast(new PropPickedMessage
            {
                PlayerIndex = message.PlayerIndex,
                SlotIndex = message.SlotIndex
            });
        }
        else if (!IsHost)
        {
            // Client receiving from host
            ProcessPropPick(player, message.SlotIndex);
        }
    }

    private void HandlePickingComplete(PickingCompleteMessage message)
    {
        CompletePicking();
    }

    #endregion
}

#region Messages

public class PickingStateMessage : INetMessage
{
    public byte State { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(State);
    }

    public void Deserialize(BinaryReader reader)
    {
        State = reader.ReadByte();
    }
}

public class PropsSelectedMessage : INetMessage
{
    public string[] PropIds { get; set; } = Array.Empty<string>();
    public float BoxPositionX { get; set; }
    public float BoxPositionY { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PropIds.Length);
        foreach (var id in PropIds)
            writer.Write(id ?? "");
        writer.Write(BoxPositionX);
        writer.Write(BoxPositionY);
    }

    public void Deserialize(BinaryReader reader)
    {
        int count = reader.ReadByte();
        PropIds = new string[count];
        for (int i = 0; i < count; i++)
            PropIds[i] = reader.ReadString();
        BoxPositionX = reader.ReadSingle();
        BoxPositionY = reader.ReadSingle();
    }
}

public class PropPickedMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public int SlotIndex { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
        writer.Write((byte)SlotIndex);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        SlotIndex = reader.ReadByte();
    }
}

public class PickingCompleteMessage : INetMessage
{
    public void Serialize(BinaryWriter writer) { }
    public void Deserialize(BinaryReader reader) { }
}

#endregion
