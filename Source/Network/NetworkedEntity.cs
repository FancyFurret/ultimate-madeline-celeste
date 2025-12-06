using System;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Network;

/// <summary>
/// Component that gives entities networking capabilities.
/// Add to an entity to enable networked message sending/receiving.
/// </summary>
public abstract class NetworkedEntity : Component
{
    public uint NetworkId { get; protected set; }
    public ulong OwnerId { get; protected set; }
    public bool IsOwner => OwnerId == NetworkManager.Instance?.LocalClientId;
    public bool AutoSpawn { get; set; } = true;
    public bool RemoveOnOwnerDisconnect { get; set; } = false;

    public abstract byte EntityTypeId { get; }
    public abstract byte[] SpawnData { get; }

    public NetworkedEntity(bool active = false, bool visible = false)
        : base(active, visible)
    {
    }

    public abstract void InvokeHandler(byte messageType, BinaryReader reader);
}

public class NetworkedEntity<T> : NetworkedEntity where T : INetMessage, new()
{
    private readonly Dictionary<byte, Action<BinaryReader>> _handlers = new();
    private byte[] _spawnData;
    private bool _hasSpawned;
    private readonly NetworkedEntityRegistry _registry;
    private readonly byte _entityTypeId;

    public override byte EntityTypeId => _entityTypeId;
    public override byte[] SpawnData => _spawnData;

    private readonly Dictionary<Type, byte> _messageTypes = new();
    private byte _nextMessageTypeId;

    public NetworkedEntity()
        : base(active: false, visible: false)
    {
        _registry = NetworkedEntityRegistry.Instance;
        NetworkId = _registry?.GenerateNetworkId() ?? GenerateFallbackId();
        OwnerId = NetworkManager.Instance?.LocalClientId ?? 1;
        _entityTypeId = _registry?.GetEntityTypeId<T>() ?? 0;
    }

    private static uint _fallbackCounter;
    private static uint GenerateFallbackId() => ++_fallbackCounter;

    public static NetworkedEntity<T> SetUp(Entity entity, uint networkId, ulong ownerId, byte[] spawnData)
    {
        var net = entity.Get<NetworkedEntity<T>>();
        if (net == null)
        {
            throw new Exception("NetworkedEntity not found");
        }

        // Unregister with old ID, update, then re-register with new ID
        NetworkedEntityRegistry.Instance?.Unregister(net);
        net.NetworkId = networkId;
        net.OwnerId = ownerId;
        net._hasSpawned = true; // Mark as spawned so it doesn't try to broadcast spawn again
        NetworkedEntityRegistry.Instance?.Register(net);

        return net;
    }

    public NetworkedEntity<T> SetSpawnData(T message)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        message.Serialize(writer);
        _spawnData = stream.ToArray();
        return this;
    }

    public override void Added(Entity entity)
    {
        base.Added(entity);
        NetworkedEntityRegistry.Instance?.Register(this);
    }

    public override void EntityAdded(Scene scene)
    {
        base.EntityAdded(scene);

        // Send spawn message if we own this entity and auto-spawn is enabled
        if (IsOwner && AutoSpawn && !_hasSpawned)
        {
            NetworkedEntityRegistry.Instance?.BroadcastSpawn(_entityTypeId, NetworkId, OwnerId, _spawnData);
            _hasSpawned = true;
        }
    }

    public override void Removed(Entity entity)
    {
        base.Removed(entity);
        HandleRemoval();
    }

    public override void EntityRemoved(Scene scene)
    {
        base.EntityRemoved(scene);
        HandleRemoval();
    }

    private void HandleRemoval()
    {
        // Send despawn message if we own this entity
        if (IsOwner && AutoSpawn && _hasSpawned)
        {
            NetworkedEntityRegistry.Instance?.BroadcastDespawn(NetworkId);
        }
        NetworkedEntityRegistry.Instance?.Unregister(this);
    }

    /// <summary>
    /// Register a handler for a specific message type.
    /// </summary>
    public NetworkedEntity<T> Handle<TMessage>(Action<TMessage> handler) where TMessage : INetMessage, new()
    {
        var messageTypeId = _nextMessageTypeId++;
        _handlers[messageTypeId] = reader =>
        {
            var msg = new TMessage();
            msg.Deserialize(reader);
            handler(msg);
        };
        _messageTypes[typeof(TMessage)] = messageTypeId;
        return this;
    }

    /// <summary>
    /// Broadcast a message to all clients (including self if BroadcastWithSelf).
    /// </summary>
    public void Broadcast<TMessage>(TMessage message, SendMode mode = SendMode.Reliable) where TMessage : INetMessage, new()
    {
        if (!_messageTypes.TryGetValue(typeof(TMessage), out var typeId))
        {
            UmcLogger.Warn($"[NetEntity] Broadcast failed - unknown message type: {typeof(TMessage).Name}");
            return;
        }
        NetworkedEntityRegistry.Instance?.Broadcast(NetworkId, typeId, message, mode);
    }

    /// <summary>
    /// Broadcast a message to all clients AND handle locally.
    /// </summary>
    public void BroadcastWithSelf<TMessage>(TMessage message, SendMode mode = SendMode.Reliable) where TMessage : INetMessage, new()
    {
        if (!_messageTypes.TryGetValue(typeof(TMessage), out var typeId))
        {
            UmcLogger.Warn($"[NetEntity] BroadcastWithSelf failed - unknown message type: {typeof(TMessage).Name}");
            return;
        }
        NetworkedEntityRegistry.Instance?.BroadcastWithSelf(NetworkId, typeId, message, mode);
    }

    /// <summary>
    /// Send a message to a specific client.
    /// </summary>
    public void SendTo<TMessage>(TMessage message, CSteamID target, SendMode mode = SendMode.Reliable) where TMessage : INetMessage, new()
    {
        if (!_messageTypes.TryGetValue(typeof(TMessage), out var typeId))
        {
            UmcLogger.Warn($"[NetEntity] SendTo failed - unknown message type: {typeof(TMessage).Name}");
            return;
        }
        NetworkedEntityRegistry.Instance?.SendTo(NetworkId, typeId, message, target, mode);
    }

    internal void HandleMessage(byte messageType, BinaryReader reader)
    {
        if (_handlers.TryGetValue(messageType, out var handler))
        {
            handler(reader);
        }
        else
        {
            UmcLogger.Warn($"[NetEntity] No handler for message: networkId={NetworkId}, messageType={messageType}");
        }
    }

    public override void InvokeHandler(byte messageType, BinaryReader reader)
    {
        if (_handlers.TryGetValue(messageType, out var handler))
        {
            handler?.Invoke(reader);
        }
        else
        {
            UmcLogger.Warn($"[NetEntity] InvokeHandler - no handler: networkId={NetworkId}, messageType={messageType}");
        }
    }
}
