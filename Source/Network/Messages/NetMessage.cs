using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.UltimateMadelineCeleste.Network.Messages;

public interface INetMessage
{
    void Serialize(BinaryWriter writer);
    void Deserialize(BinaryReader reader);
}

public enum SendMode { Reliable, Unreliable }

public readonly struct SendTarget
{
    public static SendTarget Broadcast => new(SendTargetType.Broadcast, 0);
    public static SendTarget Host => new(SendTargetType.Host, 0);
    public static SendTarget ToClient(ulong steamId) => new(SendTargetType.Specific, steamId);

    public SendTargetType Type { get; }
    public ulong SteamId { get; }

    private SendTarget(SendTargetType type, ulong steamId) { Type = type; SteamId = steamId; }
}

public enum SendTargetType { Broadcast, Host, Specific }

public static class BinaryExtensions
{
    public static void WriteVector2(this BinaryWriter writer, Vector2 value) { writer.Write(value.X); writer.Write(value.Y); }
    public static Vector2 ReadVector2(this BinaryReader reader) => new(reader.ReadSingle(), reader.ReadSingle());

    public static void WritePosition(this BinaryWriter writer, Vector2 value) { Write7BitEncodedInt(writer, (int)value.X); Write7BitEncodedInt(writer, (int)value.Y); }
    public static Vector2 ReadPosition(this BinaryReader reader) => new(Read7BitEncodedInt(reader), Read7BitEncodedInt(reader));

    public static void WriteScale(this BinaryWriter writer, Vector2 scale)
    {
        writer.Write((sbyte)Math.Clamp((int)(scale.X * 16), sbyte.MinValue, sbyte.MaxValue));
        writer.Write((sbyte)Math.Clamp((int)(scale.Y * 16), sbyte.MinValue, sbyte.MaxValue));
    }
    public static Vector2 ReadScale(this BinaryReader reader) => new(reader.ReadSByte() / 16f, reader.ReadSByte() / 16f);

    public static void WriteSpeed(this BinaryWriter writer, Vector2 speed)
    {
        writer.Write((short)Math.Clamp((int)speed.X, short.MinValue, short.MaxValue));
        writer.Write((short)Math.Clamp((int)speed.Y, short.MinValue, short.MaxValue));
    }
    public static Vector2 ReadSpeed(this BinaryReader reader) => new(reader.ReadInt16(), reader.ReadInt16());

    public static void WriteColor(this BinaryWriter writer, Color value) => writer.Write(value.PackedValue);
    public static Color ReadColor(this BinaryReader reader) => new() { PackedValue = reader.ReadUInt32() };
    public static void WriteNullableString(this BinaryWriter writer, string value) => writer.Write(value ?? string.Empty);

    public static void WriteHairColors(this BinaryWriter writer, Color[] colors)
    {
        writer.Write((byte)colors.Length);
        for (int i = 0; i < colors.Length;)
        {
            int origIdx = i;
            ushort packedCol = PackColor(colors[i]);
            writer.Write((byte)((packedCol >> 8) & 0xff));
            writer.Write((byte)(packedCol & 0xff));
            for (i++; i < colors.Length && colors[i] == colors[origIdx]; i++) ;
            if (origIdx + 1 < i)
                writer.Write((byte)((1 << 7) | (i - origIdx - 1)));
        }
    }

    public static Color[] ReadHairColors(this BinaryReader reader)
    {
        int count = reader.ReadByte();
        var colors = new Color[count];
        Color lastColor = Color.White;
        for (int i = 0; i < count;)
        {
            byte firstByte = reader.ReadByte();
            if ((firstByte & (1 << 7)) != 0)
            {
                int repCount = firstByte & ~(1 << 7);
                for (int j = 0; i < count && j < repCount; j++) colors[i++] = lastColor;
            }
            else
            {
                byte secondByte = reader.ReadByte();
                colors[i++] = lastColor = UnpackColor((ushort)((firstByte << 8) | secondByte));
            }
        }
        return colors;
    }

    private static ushort PackColor(Color col) => (ushort)((col.R >> 3) | ((col.G >> 3) << 5) | ((col.B >> 3) << 10));
    private static Color UnpackColor(ushort packed) => new() { R = (byte)(((packed >> 0) & 0b11111) << 3), G = (byte)(((packed >> 5) & 0b11111) << 3), B = (byte)(((packed >> 10) & 0b11111) << 3), A = 255 };

    public static void Write7BitEncodedInt(BinaryWriter writer, int value) { uint v = (uint)value; while (v >= 0x80) { writer.Write((byte)(v | 0x80)); v >>= 7; } writer.Write((byte)v); }
    public static int Read7BitEncodedInt(BinaryReader reader) { int result = 0, shift = 0; byte b; do { b = reader.ReadByte(); result |= (b & 0x7F) << shift; shift += 7; } while ((b & 0x80) != 0); return result; }
}

[Flags]
public enum PlayerFrameFlags : byte { None = 0, FacingLeft = 1, HairSimulateMotion = 2, Dead = 4, Dashing = 8, DashB = 16 }

public class PlayerFrameMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
    public Color SpriteColor { get; set; } = Color.White;
    public Facings Facing { get; set; }
    public Vector2 Speed { get; set; }
    public int CurrentAnimationID { get; set; }
    public int CurrentAnimationFrame { get; set; }
    public Color[] HairColors { get; set; } = Array.Empty<Color>();
    public bool HairSimulateMotion { get; set; }
    public bool Dead { get; set; }
    public bool Dashing { get; set; }
    public bool DashWasB { get; set; }
    public Vector2? DashDir { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
        PlayerFrameFlags flags = PlayerFrameFlags.None;
        if (Facing == Facings.Left) flags |= PlayerFrameFlags.FacingLeft;
        if (HairSimulateMotion) flags |= PlayerFrameFlags.HairSimulateMotion;
        if (Dead) flags |= PlayerFrameFlags.Dead;
        if (Dashing && DashDir.HasValue) { flags |= PlayerFrameFlags.Dashing; if (DashWasB) flags |= PlayerFrameFlags.DashB; }
        writer.Write((byte)flags);
        writer.WritePosition(Position);
        writer.WriteScale(Scale);
        writer.WriteColor(SpriteColor);
        writer.WriteSpeed(Speed);
        BinaryExtensions.Write7BitEncodedInt(writer, CurrentAnimationID);
        BinaryExtensions.Write7BitEncodedInt(writer, CurrentAnimationFrame);
        writer.WriteHairColors(HairColors);
        if ((flags & PlayerFrameFlags.Dashing) != 0 && DashDir.HasValue)
            writer.Write((byte)((Math.Atan2(DashDir.Value.Y, DashDir.Value.X) / (2 * Math.PI) * 256 + 256) % 256));
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        var flags = (PlayerFrameFlags)reader.ReadByte();
        Facing = (flags & PlayerFrameFlags.FacingLeft) != 0 ? Facings.Left : Facings.Right;
        HairSimulateMotion = (flags & PlayerFrameFlags.HairSimulateMotion) != 0;
        Dead = (flags & PlayerFrameFlags.Dead) != 0;
        Dashing = (flags & PlayerFrameFlags.Dashing) != 0;
        DashWasB = (flags & PlayerFrameFlags.DashB) != 0;
        Position = reader.ReadPosition();
        Scale = reader.ReadScale();
        SpriteColor = reader.ReadColor();
        Speed = reader.ReadSpeed();
        CurrentAnimationID = BinaryExtensions.Read7BitEncodedInt(reader);
        CurrentAnimationFrame = BinaryExtensions.Read7BitEncodedInt(reader);
        HairColors = reader.ReadHairColors();
        DashDir = Dashing ? new Vector2((float)Math.Cos(reader.ReadByte() / 256.0 * 2 * Math.PI), (float)Math.Sin(reader.ReadByte() / 256.0 * 2 * Math.PI)) : null;
    }
}

public class PlayerGraphicsMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public int Depth { get; set; }
    public PlayerSpriteMode SpriteMode { get; set; }
    public float SpriteRate { get; set; } = 1f;
    public string[] Animations { get; set; } = Array.Empty<string>();
    public byte HairCount { get; set; }
    public Vector2[] HairScales { get; set; } = Array.Empty<Vector2>();
    public string SkinId { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
        writer.Write(Depth);
        BinaryExtensions.Write7BitEncodedInt(writer, (int)SpriteMode);
        writer.Write(SpriteRate);
        BinaryExtensions.Write7BitEncodedInt(writer, Animations.Length);
        foreach (var anim in Animations) writer.Write(anim ?? "");
        writer.Write(HairCount);
        for (int i = 0; i < HairCount && i < HairScales.Length; i++) writer.WriteScale(HairScales[i]);
        writer.WriteNullableString(SkinId);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        Depth = reader.ReadInt32();
        SpriteMode = (PlayerSpriteMode)BinaryExtensions.Read7BitEncodedInt(reader);
        SpriteRate = reader.ReadSingle();
        int animCount = BinaryExtensions.Read7BitEncodedInt(reader);
        Animations = new string[animCount];
        for (int i = 0; i < animCount; i++) Animations[i] = reader.ReadString();
        HairCount = reader.ReadByte();
        HairScales = new Vector2[HairCount];
        for (int i = 0; i < HairCount; i++) HairScales[i] = reader.ReadScale();
        SkinId = reader.ReadString();
    }
}

public class ClientPlayersFrameMessage : INetMessage
{
    public List<PlayerFrameMessage> Frames { get; set; } = new();
    public void Serialize(BinaryWriter writer) { writer.Write((byte)Frames.Count); foreach (var f in Frames) f.Serialize(writer); }
    public void Deserialize(BinaryReader reader) { int c = reader.ReadByte(); Frames = new(c); for (int i = 0; i < c; i++) { var f = new PlayerFrameMessage(); f.Deserialize(reader); Frames.Add(f); } }
}

public class PlayerJoinRequestMessage : INetMessage { public void Serialize(BinaryWriter writer) { } public void Deserialize(BinaryReader reader) { } }

public class PlayerJoinResponseMessage : INetMessage
{
    public bool Success { get; set; }
    public int AssignedPlayerIndex { get; set; }
    public string ErrorMessage { get; set; }
    public void Serialize(BinaryWriter writer) { writer.Write(Success); writer.Write(AssignedPlayerIndex); writer.WriteNullableString(ErrorMessage); }
    public void Deserialize(BinaryReader reader) { Success = reader.ReadBoolean(); AssignedPlayerIndex = reader.ReadInt32(); ErrorMessage = reader.ReadString(); }
}

public class PlayerAddedMessage : INetMessage
{
    public ulong ClientSteamId { get; set; }
    public int PlayerIndex { get; set; }
    public string PlayerName { get; set; }
    public PlayerGraphicsMessage Graphics { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(ClientSteamId);
        writer.Write(PlayerIndex);
        writer.WriteNullableString(PlayerName);
        writer.Write(Graphics != null);
        Graphics?.Serialize(writer);
    }

    public void Deserialize(BinaryReader reader)
    {
        ClientSteamId = reader.ReadUInt64();
        PlayerIndex = reader.ReadInt32();
        PlayerName = reader.ReadString();
        if (reader.ReadBoolean())
        {
            Graphics = new PlayerGraphicsMessage();
            Graphics.Deserialize(reader);
        }
    }
}

public class PlayerRemovedMessage : INetMessage
{
    public ulong ClientSteamId { get; set; }
    public int PlayerIndex { get; set; }
    public void Serialize(BinaryWriter writer) { writer.Write(ClientSteamId); writer.Write(PlayerIndex); }
    public void Deserialize(BinaryReader reader) { ClientSteamId = reader.ReadUInt64(); PlayerIndex = reader.ReadInt32(); }
}

public class LobbyStateMessage : INetMessage
{
    public List<PlayerAddedMessage> Players { get; set; } = new();
    public void Serialize(BinaryWriter writer) { writer.Write((byte)Players.Count); foreach (var p in Players) p.Serialize(writer); }
    public void Deserialize(BinaryReader reader) { int c = reader.ReadByte(); Players = new(c); for (int i = 0; i < c; i++) { var p = new PlayerAddedMessage(); p.Deserialize(reader); Players.Add(p); } }
}

public enum PlayerEventType : byte { Death = 0, Respawn = 1, Dash = 2, Jump = 3, WallJump = 4, Climb = 5, Custom = 255 }

public class PlayerEventMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public PlayerEventType EventType { get; set; }
    public string Data { get; set; }
    public void Serialize(BinaryWriter writer) { writer.Write(PlayerIndex); writer.Write((byte)EventType); writer.WriteNullableString(Data); }
    public void Deserialize(BinaryReader reader) { PlayerIndex = reader.ReadInt32(); EventType = (PlayerEventType)reader.ReadByte(); Data = reader.ReadString(); }
}

public class CursorPositionMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public Vector2 ScreenPosition { get; set; }
    public void Serialize(BinaryWriter writer) { writer.Write((byte)PlayerIndex); writer.Write((ushort)Math.Clamp(ScreenPosition.X, 0, 1920)); writer.Write((ushort)Math.Clamp(ScreenPosition.Y, 0, 1080)); }
    public void Deserialize(BinaryReader reader) { PlayerIndex = reader.ReadByte(); ScreenPosition = new(reader.ReadUInt16(), reader.ReadUInt16()); }
}

