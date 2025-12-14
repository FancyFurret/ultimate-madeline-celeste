using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Celeste.Mod.UltimateMadelineCeleste.Network.Messages;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Monocle;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Network;

/// <summary>
/// Factory delegate for creating networked entities on remote clients.
/// </summary>
/// <param name="networkId">The network ID for the entity</param>
/// <param name="ownerId">The client ID that owns this entity</param>
/// <param name="spawnData">Optional spawn data sent by the owner</param>
/// <returns>The created entity, or null if creation failed</returns>
public delegate Entity NetworkedEntityFactory<T>(uint networkId, ulong ownerId, T spawnData) where T : INetMessage;

/// <summary>
/// Central registry for all networked entities. Routes messages to the correct entity.
/// </summary>
public class NetworkedEntityRegistry
{
    public static NetworkedEntityRegistry Instance { get; private set; }

    private readonly Dictionary<uint, NetworkedEntity> _entities = new();
    private readonly Dictionary<byte, Func<uint, ulong, byte[], Entity>> _factories = new();
    private readonly Dictionary<Type, byte> _entityTypes = new();

    private byte _nextFactoryId;
    private uint _localIdCounter;

    public NetworkedEntityRegistry()
    {
        Instance = this;
    }

    /// <summary>
    /// Ensures all remote entities exist in the current scene.
    /// Call after level loads to recreate any entities that were lost during transition.
    /// </summary>
    public void EnsureEntitiesInScene()
    {
        var scene = Engine.Scene;
        if (scene == null) return;

        int respawnCount = 0;
        foreach (var kvp in _entities.ToList())
        {
            var networkId = kvp.Key;
            var entity = kvp.Value;

            // Skip local entities - they manage themselves
            if (entity.IsOwner) continue;

            // Skip if already in current scene
            if (entity.Entity?.Scene == scene)
                continue;

            // Recreate via factory using entity's own spawn data
            if (_factories.TryGetValue(entity.EntityTypeId, out var factory))
            {
                _entities.Remove(networkId); // Remove stale reference first
                var newEntity = factory(networkId, entity.OwnerId, entity.SpawnData);
                if (newEntity != null)
                {
                    scene.Add(newEntity);
                    respawnCount++;
                    UmcLogger.Info($"[NetRegistry] Recreated remote entity {networkId} in new scene");
                }
            }
        }

        if (respawnCount > 0)
        {
            UmcLogger.Info($"[NetRegistry] Recreated {respawnCount} remote entities after level load");
        }
    }

    /// <summary>
    /// Generates a unique network ID by combining the local client ID (top 16 bits)
    /// with an incrementing counter (bottom 16 bits).
    /// </summary>
    public uint GenerateNetworkId()
    {
        var clientPart = (uint)(NetworkManager.Instance?.LocalClientId ?? 0) & 0xFFFF;
        var localPart = ++_localIdCounter & 0xFFFF;
        return (clientPart << 16) | localPart;
    }

    public void Initialize(MessageRegistry messages)
    {
        messages.Handle<NetworkedEntityMessage>(HandleEntityMessage);
        messages.Handle<SpawnEntityMessage>(HandleSpawnMessage);
        messages.Handle<DespawnEntityMessage>(HandleDespawnMessage);

        // Hook scene changes to clean up local entities that weren't properly removed
        On.Monocle.Scene.End += OnSceneEnd;

        UmcLogger.Info("NetworkedEntityRegistry initialized");
    }

    public void Shutdown()
    {
        On.Monocle.Scene.End -= OnSceneEnd;

        ClearAllEntities();
        _factories.Clear();
        _entityTypes.Clear();
        _nextFactoryId = 0;
        _localIdCounter = 0;
        if (Instance == this) Instance = null;
        UmcLogger.Info("[NetRegistry] Shutdown complete");
    }

    /// <summary>
    /// Called when a scene ends. Cleans up local entities that weren't properly removed
    /// due to scene transition bypassing the normal entity removal queue.
    /// </summary>
    private void OnSceneEnd(On.Monocle.Scene.orig_End orig, Scene self)
    {
        orig(self);

        // Find local entities whose parent entity was in the ending scene
        var localEntitiesToRemove = _entities
            .Where(kvp => kvp.Value.IsOwner && kvp.Value.Entity?.Scene == self)
            .ToList();

        if (localEntitiesToRemove.Count > 0)
        {
            UmcLogger.Info($"[NetRegistry] Scene ending - cleaning up {localEntitiesToRemove.Count} local entities");

            foreach (var kvp in localEntitiesToRemove)
            {
                var entity = kvp.Value;
                var networkId = kvp.Key;

                // Broadcast despawn if auto-spawn was enabled
                if (entity.AutoSpawn)
                {
                    BroadcastDespawn(networkId);
                }

                _entities.Remove(networkId);
                UmcLogger.Info($"[NetRegistry] Cleaned up local entity: networkId={networkId}");
            }
        }
    }

    /// <summary>
    /// Removes all networked entities from the scene and clears the registry.
    /// </summary>
    public void ClearAllEntities()
    {
        UmcLogger.Info($"[NetRegistry] ClearAllEntities: clearing {_entities.Count} entities");

        foreach (var kvp in _entities.ToList())
        {
            var entity = kvp.Value;
            entity.Entity?.RemoveSelf();
        }

        _entities.Clear();
    }

    public byte GetEntityTypeId<T>() where T : INetMessage, new()
    {
        if (!_entityTypes.TryGetValue(typeof(T), out var typeId))
        {
            throw new Exception($"Entity type {typeof(T).Name} has not been registered");
        }
        return typeId;
    }

    public void RegisterFactory<T>(NetworkedEntityFactory<T> factory) where T : INetMessage, new()
    {
        var factoryId = _nextFactoryId++;
        _factories[factoryId] = (networkId, ownerId, spawnData) =>
        {
            var message = new T();
            if (spawnData != null && spawnData.Length > 0)
            {
                message.Deserialize(new BinaryReader(new MemoryStream(spawnData)));
            }
            var entity = factory(networkId, ownerId, message);
            if (entity != null)
            {
                NetworkedEntity<T>.SetUp(entity, networkId, ownerId, spawnData);
            }
            return entity;
        };
        _entityTypes[typeof(T)] = factoryId;
        UmcLogger.Info($"[NetRegistry] RegisterFactory: type={typeof(T).Name}, factoryId={factoryId}");
    }

    public void Register<T>(NetworkedEntity<T> entity) where T : INetMessage, new()
    {
        if (_entities.ContainsKey(entity.NetworkId))
        {
            UmcLogger.Warn($"NetworkedEntity with ID {entity.NetworkId} already registered, replacing");
        }
        _entities[entity.NetworkId] = entity;
        UmcLogger.Info($"[NetRegistry] Registered entity: networkId={entity.NetworkId}, totalEntities={_entities.Count}");
    }

    public void Unregister<T>(NetworkedEntity<T> entity) where T : INetMessage, new()
    {
        if (_entities.TryGetValue(entity.NetworkId, out var existing) && existing == entity)
        {
            _entities.Remove(entity.NetworkId);
        }
    }

    public bool HasEntity(uint networkId) => _entities.ContainsKey(networkId);

    /// <summary>
    /// Removes entities owned by a specific client that are marked for removal on disconnect.
    /// Called when a peer disconnects to clean up temporary entities like cursors.
    /// </summary>
    public void RemoveEntitiesOwnedBy(ulong clientId)
    {
        UmcLogger.Info($"Removing entities owned by client: {clientId}");
        var entitiesToRemove = _entities
            .Where(kvp => kvp.Value.OwnerId == clientId && kvp.Value.RemoveOnOwnerDisconnect)
            .Select(kvp => kvp.Key)
            .ToList();

        UmcLogger.Info($"[NetRegistry] RemoveEntitiesOwnedBy: clientId={clientId}, count={entitiesToRemove.Count}");

        foreach (var networkId in entitiesToRemove)
        {
            if (_entities.TryGetValue(networkId, out var entity))
            {
                entity.Entity?.RemoveSelf();
                _entities.Remove(networkId);
                UmcLogger.Info($"[NetRegistry] Removed entity owned by disconnected client: networkId={networkId}");
            }
        }
    }

    /// <summary>
    /// Sends spawn messages for all existing entities to a specific client.
    /// Called when a new client joins to sync them with existing state.
    /// </summary>
    public void SendAllEntitiesTo(CSteamID target)
    {
        UmcLogger.Info($"[NetRegistry] SendAllEntitiesTo: target={target.m_SteamID}, entityCount={_entities.Count}");

        foreach (var kvp in _entities)
        {
            var entity = kvp.Value;

            UmcLogger.Info($"[NetRegistry] Sending entity to new client: networkId={entity.NetworkId}, ownerId={entity.OwnerId}, typeId={entity.EntityTypeId}");

            NetworkManager.SendTo(new SpawnEntityMessage
            {
                EntityType = entity.EntityTypeId,
                NetworkId = entity.NetworkId,
                OwnerId = entity.OwnerId,
                SpawnData = entity.SpawnData ?? Array.Empty<byte>()
            }, target);
        }
    }

    /// <summary>
    /// Broadcast a spawn message for an entity. Called automatically when NetworkedEntity is added.
    /// </summary>
    public void BroadcastSpawn(byte entityType, uint networkId, ulong ownerId, byte[] spawnData = null)
    {
        var isOnline = NetworkManager.Instance?.IsOnline ?? false;
        UmcLogger.Info($"[NetRegistry] BroadcastSpawn: entityType={entityType}, networkId={networkId}, ownerId={ownerId}, dataSize={spawnData?.Length ?? 0}, isOnline={isOnline}");

        if (!isOnline)
        {
            UmcLogger.Info("[NetRegistry] BroadcastSpawn skipped - not online");
            return;
        }

        NetworkManager.Broadcast(new SpawnEntityMessage
        {
            EntityType = entityType,
            NetworkId = networkId,
            OwnerId = ownerId,
            SpawnData = spawnData ?? Array.Empty<byte>()
        });
    }

    /// <summary>
    /// Broadcast a despawn message for an entity.
    /// </summary>
    public void BroadcastDespawn(uint networkId)
    {
        var isOnline = NetworkManager.Instance?.IsOnline ?? false;
        UmcLogger.Info($"[NetRegistry] BroadcastDespawn: networkId={networkId}, isOnline={isOnline}");

        if (!isOnline)
        {
            UmcLogger.Info("[NetRegistry] BroadcastDespawn skipped - not online");
            return;
        }

        NetworkManager.Broadcast(new DespawnEntityMessage { NetworkId = networkId });
    }

    public void Broadcast<T>(uint entityId, byte messageType, T message, SendMode mode) where T : INetMessage, new()
    {
        var isOnline = NetworkManager.Instance?.IsOnline ?? false;
        if (!isOnline) return;

        var wrapper = CreateWrapper(entityId, messageType, message);
        NetworkManager.Broadcast(wrapper, mode);
    }

    public void BroadcastWithSelf<T>(uint entityId, byte messageType, T message, SendMode mode) where T : INetMessage, new()
    {
        var isOnline = NetworkManager.Instance?.IsOnline ?? false;

        if (isOnline)
        {
            var wrapper = CreateWrapper(entityId, messageType, message);
            NetworkManager.Broadcast(wrapper, mode);
        }

        // Also invoke locally
        if (_entities.TryGetValue(entityId, out var entity))
        {
            using var stream = new MemoryStream();
            using var writer = new BinaryWriter(stream);
            message.Serialize(writer);
            stream.Position = 0;
            using var reader = new BinaryReader(stream);
            entity.InvokeHandler(messageType, reader);
        }
    }

    public void SendTo<T>(uint entityId, byte messageType, T message, CSteamID target, SendMode mode) where T : INetMessage, new()
    {
        var wrapper = CreateWrapper(entityId, messageType, message);
        NetworkManager.SendTo(wrapper, target, mode);
    }

    private NetworkedEntityMessage CreateWrapper<T>(uint entityId, byte messageType, T message) where T : INetMessage, new()
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        message.Serialize(writer);

        return new NetworkedEntityMessage
        {
            EntityId = entityId,
            MessageType = messageType,
            Payload = stream.ToArray()
        };
    }

    private void HandleEntityMessage(CSteamID sender, NetworkedEntityMessage message)
    {
        if (!_entities.TryGetValue(message.EntityId, out var entity))
        {
            // Entity not found - might not be spawned yet, drop the message
            return;
        }

        using var stream = new MemoryStream(message.Payload);
        using var reader = new BinaryReader(stream);
        entity.InvokeHandler(message.MessageType, reader);
    }

    private void HandleSpawnMessage(CSteamID sender, SpawnEntityMessage message)
    {
        // Don't process our own entities
        if (message.OwnerId == NetworkManager.Instance?.LocalClientId)
        {
            return;
        }

        ProcessSpawn(message);
    }

    private void ProcessSpawn(SpawnEntityMessage message)
    {
        // Don't spawn if we already have this entity in a scene
        if (_entities.TryGetValue(message.NetworkId, out var existing) && existing.Entity?.Scene != null)
        {
            return;
        }

        if (!_factories.TryGetValue(message.EntityType, out var factory))
        {
            UmcLogger.Warn($"No factory registered for entity type: {message.EntityType}");
            return;
        }

        var entity = factory(message.NetworkId, message.OwnerId, message.SpawnData);
        if (entity != null)
        {
            var scene = Engine.Scene;
            scene?.Add(entity);
            UmcLogger.Info($"Spawned remote entity: {message.EntityType} (ID: {message.NetworkId})");
        }
    }

    private void HandleDespawnMessage(CSteamID sender, DespawnEntityMessage message)
    {
        if (_entities.TryGetValue(message.NetworkId, out var entity))
        {
            // Don't despawn our own entities
            if (entity.IsOwner) return;

            entity.Entity?.RemoveSelf();
            _entities.Remove(message.NetworkId);
            UmcLogger.Info($"Despawned remote entity: {message.NetworkId}");
        }
    }
}

/// <summary>
/// Wrapper message for all networked entity communications.
/// </summary>
public class NetworkedEntityMessage : INetMessage
{
    public uint EntityId { get; set; }
    public byte MessageType { get; set; }
    public byte[] Payload { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(EntityId);
        writer.Write(MessageType);
        writer.Write((ushort)Payload.Length);
        writer.Write(Payload);
    }

    public void Deserialize(BinaryReader reader)
    {
        EntityId = reader.ReadUInt32();
        MessageType = reader.ReadByte();
        var length = reader.ReadUInt16();
        Payload = reader.ReadBytes(length);
    }
}

/// <summary>
/// Message to spawn an entity on remote clients.
/// </summary>
public class SpawnEntityMessage : INetMessage
{
    public byte EntityType { get; set; }
    public uint NetworkId { get; set; }
    public ulong OwnerId { get; set; }
    public byte[] SpawnData { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(EntityType);
        writer.Write(NetworkId);
        writer.Write(OwnerId);
        writer.Write((ushort)(SpawnData?.Length ?? 0));
        if (SpawnData != null && SpawnData.Length > 0)
            writer.Write(SpawnData);
    }

    public void Deserialize(BinaryReader reader)
    {
        EntityType = reader.ReadByte();
        NetworkId = reader.ReadUInt32();
        OwnerId = reader.ReadUInt64();
        var length = reader.ReadUInt16();
        SpawnData = length > 0 ? reader.ReadBytes(length) : Array.Empty<byte>();
    }
}

/// <summary>
/// Message to despawn an entity on remote clients.
/// </summary>
public class DespawnEntityMessage : INetMessage
{
    public uint NetworkId { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(NetworkId);
    }

    public void Deserialize(BinaryReader reader)
    {
        NetworkId = reader.ReadUInt32();
    }
}
