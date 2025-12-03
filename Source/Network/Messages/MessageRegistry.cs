using System;
using System.Collections.Generic;
using System.IO;
using Celeste.Mod.UltimateMadelineCeleste.Utilities;
using Steamworks;

namespace Celeste.Mod.UltimateMadelineCeleste.Network.Messages;

/// <summary>
/// Handles message registration, serialization, and routing.
/// </summary>
public class MessageRegistry
{
    private readonly Dictionary<byte, MessageRegistration> _idToRegistration = new();
    private readonly Dictionary<Type, byte> _typeToId = new();
    private readonly Dictionary<CSteamID, double> _lastTimestamps = new();

    private Action<CSteamID, byte[], SendMode> _sendToClient;
    private Action<byte[], SendMode> _broadcast;
    private Func<CSteamID?> _getHostId;

    public void Configure(Action<CSteamID, byte[], SendMode> sendToClient, Action<byte[], SendMode> broadcast, Func<CSteamID?> getHostId)
    {
        _sendToClient = sendToClient;
        _broadcast = broadcast;
        _getHostId = getHostId;
    }

    public void Register<T>(byte typeId, Action<CSteamID, T> handler, bool checkTimestamp = false) where T : INetMessage, new()
    {
        if (_idToRegistration.ContainsKey(typeId))
        {
            // throw new InvalidOperationException($"Message type ID {typeId} already registered");
        }

        _idToRegistration[typeId] = new MessageRegistration
        {
            TypeId = typeId,
            MessageType = typeof(T),
            Factory = () => new T(),
            Handler = (sender, msg) => handler(sender, (T)msg),
            CheckTimestamp = checkTimestamp
        };
        _typeToId[typeof(T)] = typeId;
    }

    public void Send<T>(T message, SendMode mode, SendTarget target) where T : INetMessage
    {
        if (!_typeToId.TryGetValue(typeof(T), out var typeId))
        {
            UmcLogger.Error($"Cannot send unregistered message: {typeof(T).Name}");
            return;
        }

        var data = Serialize(typeId, message);

        switch (target.Type)
        {
            case SendTargetType.Broadcast:
                _broadcast?.Invoke(data, mode);
                break;
            case SendTargetType.Host:
                var hostId = _getHostId?.Invoke();
                if (hostId.HasValue) _sendToClient?.Invoke(hostId.Value, data, mode);
                else UmcLogger.Warn("Cannot send to host - host ID not available");
                break;
            case SendTargetType.Specific:
                _sendToClient?.Invoke(new CSteamID(target.SteamId), data, mode);
                break;
        }
    }

    public void SendTo<T>(T message, CSteamID target, SendMode mode = SendMode.Reliable) where T : INetMessage => Send(message, mode, SendTarget.ToClient(target.m_SteamID));
    public void Broadcast<T>(T message, SendMode mode = SendMode.Reliable) where T : INetMessage => Send(message, mode, SendTarget.Broadcast);
    public void SendToHost<T>(T message, SendMode mode = SendMode.Reliable) where T : INetMessage => Send(message, mode, SendTarget.Host);

    public bool HandleRawMessage(CSteamID sender, byte[] data)
    {
        if (data == null || data.Length == 0) return false;

        try
        {
            using var stream = new MemoryStream(data);
            using var reader = new BinaryReader(stream);

            var typeId = reader.ReadByte();
            var timestamp = reader.ReadDouble();

            if (!_idToRegistration.TryGetValue(typeId, out var registration))
            {
                UmcLogger.Warn($"Unknown message type ID: {typeId}");
                return false;
            }

            if (registration.CheckTimestamp)
            {
                if (_lastTimestamps.TryGetValue(sender, out var lastTimestamp) && timestamp < lastTimestamp)
                    return false;
                _lastTimestamps[sender] = timestamp;
            }

            var message = registration.Factory() as INetMessage;
            message?.Deserialize(reader);
            registration.Handler?.Invoke(sender, message);
            return true;
        }
        catch (Exception ex)
        {
            UmcLogger.Error($"Failed to handle message: {ex.Message}");
            return false;
        }
    }

    public void ClearPeerState(CSteamID peer) => _lastTimestamps.Remove(peer);
    public void Clear() => _lastTimestamps.Clear();

    private byte[] Serialize(byte typeId, INetMessage message)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write(typeId);
        writer.Write(DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds);
        message.Serialize(writer);
        return stream.ToArray();
    }

    private class MessageRegistration
    {
        public byte TypeId { get; set; }
        public Type MessageType { get; set; }
        public Func<object> Factory { get; set; }
        public Action<CSteamID, object> Handler { get; set; }
        public bool CheckTimestamp { get; set; }
    }
}
