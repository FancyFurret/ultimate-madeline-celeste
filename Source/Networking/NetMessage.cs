using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;

namespace Celeste.Mod.UltimateMadelineCeleste.Networking;

public interface INetMessage
{
    void Serialize(BinaryWriter writer);
    void Deserialize(BinaryReader reader);
}

public enum SendMode
{
    Reliable,
    Unreliable
}

public readonly struct SendTarget
{
    public static SendTarget Broadcast => new(SendTargetType.Broadcast, 0);
    public static SendTarget Host => new(SendTargetType.Host, 0);
    public static SendTarget ToClient(ulong steamId) => new(SendTargetType.Specific, steamId);

    public SendTargetType Type { get; }
    public ulong SteamId { get; }

    private SendTarget(SendTargetType type, ulong steamId)
    {
        Type = type;
        SteamId = steamId;
    }
}

public enum SendTargetType
{
    Broadcast,
    Host,
    Specific
}

// ============================================================================
// Binary Extensions for common types
// ============================================================================

public static class BinaryExtensions
{
    public static void WriteVector2(this BinaryWriter writer, Vector2 value)
    {
        writer.Write(value.X);
        writer.Write(value.Y);
    }

    public static Vector2 ReadVector2(this BinaryReader reader)
    {
        return new Vector2(reader.ReadSingle(), reader.ReadSingle());
    }
    
    /// <summary>
    /// Writes a Vector2 with position compression (int, 7-bit encoded).
    /// </summary>
    public static void WritePosition(this BinaryWriter writer, Vector2 value)
    {
        Write7BitEncodedInt(writer, (int)value.X);
        Write7BitEncodedInt(writer, (int)value.Y);
    }
    
    public static Vector2 ReadPosition(this BinaryReader reader)
    {
        return new Vector2(Read7BitEncodedInt(reader), Read7BitEncodedInt(reader));
    }
    
    /// <summary>
    /// Writes scale as clamped sbyte * 16.
    /// </summary>
    public static void WriteScale(this BinaryWriter writer, Vector2 scale)
    {
        writer.Write((sbyte)Math.Clamp((int)(scale.X * 16), sbyte.MinValue, sbyte.MaxValue));
        writer.Write((sbyte)Math.Clamp((int)(scale.Y * 16), sbyte.MinValue, sbyte.MaxValue));
    }
    
    public static Vector2 ReadScale(this BinaryReader reader)
    {
        return new Vector2(reader.ReadSByte() / 16f, reader.ReadSByte() / 16f);
    }
    
    /// <summary>
    /// Writes speed as shorts.
    /// </summary>
    public static void WriteSpeed(this BinaryWriter writer, Vector2 speed)
    {
        writer.Write((short)Math.Clamp((int)speed.X, short.MinValue, short.MaxValue));
        writer.Write((short)Math.Clamp((int)speed.Y, short.MinValue, short.MaxValue));
    }
    
    public static Vector2 ReadSpeed(this BinaryReader reader)
    {
        return new Vector2(reader.ReadInt16(), reader.ReadInt16());
    }

    public static void WriteColor(this BinaryWriter writer, Color value)
    {
        writer.Write(value.PackedValue);
    }
    
    public static Color ReadColor(this BinaryReader reader)
    {
        return new Color { PackedValue = reader.ReadUInt32() };
    }

    public static void WriteNullableColor(this BinaryWriter writer, Color? value)
    {
        writer.Write(value?.PackedValue ?? 0);
    }

    public static Color? ReadNullableColor(this BinaryReader reader)
    {
        var packed = reader.ReadUInt32();
        return packed != 0 ? new Color { PackedValue = packed } : null;
    }

    public static void WriteNullableString(this BinaryWriter writer, string value)
    {
        writer.Write(value ?? string.Empty);
    }
    
    /// <summary>
    /// Writes hair colors with RLE compression.
    /// </summary>
    public static void WriteHairColors(this BinaryWriter writer, Color[] colors)
    {
        writer.Write((byte)colors.Length);
        for (int i = 0; i < colors.Length;)
        {
            int origIdx = i;
            ushort packedCol = PackColor(colors[i]);
            writer.Write((byte)((packedCol >> 8) & 0xff));
            writer.Write((byte)(packedCol & 0xff));
            // Count repeats
            for (i++; i < colors.Length && colors[i] == colors[origIdx]; i++) ;
            // Write repeat count if any
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
            // Check if it's a repeat marker (high bit set)
            if ((firstByte & (1 << 7)) != 0)
            {
                int repCount = firstByte & ~(1 << 7);
                for (int j = 0; i < count && j < repCount; j++)
                    colors[i++] = lastColor;
            }
            else
            {
                byte secondByte = reader.ReadByte();
                colors[i++] = lastColor = UnpackColor((ushort)((firstByte << 8) | secondByte));
            }
        }
        return colors;
    }
    
    private static ushort PackColor(Color col) => (ushort)(
        (col.R >> 3) << 0 |
        (col.G >> 3) << 5 |
        (col.B >> 3) << 10
    );
    
    private static Color UnpackColor(ushort packed) => new()
    {
        R = (byte)(((packed >> 0) & 0b11111) << 3),
        G = (byte)(((packed >> 5) & 0b11111) << 3),
        B = (byte)(((packed >> 10) & 0b11111) << 3),
        A = 255
    };
    
    public static void Write7BitEncodedInt(BinaryWriter writer, int value)
    {
        uint v = (uint)value;
        while (v >= 0x80)
        {
            writer.Write((byte)(v | 0x80));
            v >>= 7;
        }
        writer.Write((byte)v);
    }
    
    public static int Read7BitEncodedInt(BinaryReader reader)
    {
        int result = 0;
        int shift = 0;
        byte b;
        do
        {
            b = reader.ReadByte();
            result |= (b & 0x7F) << shift;
            shift += 7;
        } while ((b & 0x80) != 0);
        return result;
    }
}

// ============================================================================
// Message Definitions
// ============================================================================

/// <summary>
/// Player frame flags.
/// </summary>
[Flags]
public enum PlayerFrameFlags : byte
{
    None = 0,
    FacingLeft = 0b00000001,
    HairSimulateMotion = 0b00000010,
    Dead = 0b00000100,
    Dashing = 0b00001000,
    DashB = 0b00010000,
}

/// <summary>
/// Per-frame player state, optimized for bandwidth.
/// Sent every tick via unreliable channel.
/// </summary>
public class PlayerFrameMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    
    // Core transform
    public Vector2 Position { get; set; }
    public Vector2 Scale { get; set; }
    public Color SpriteColor { get; set; } = Color.White;
    public Facings Facing { get; set; }
    public Vector2 Speed { get; set; }
    
    // Animation (use ID for bandwidth, graphics message contains the map)
    public int CurrentAnimationID { get; set; }
    public int CurrentAnimationFrame { get; set; }
    
    // Hair - full array with RLE compression
    public Color[] HairColors { get; set; } = Array.Empty<Color>();
    public bool HairSimulateMotion { get; set; }
    
    // State
    public bool Dead { get; set; }
    public bool Dashing { get; set; }
    public bool DashWasB { get; set; }
    public Vector2? DashDir { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        // Player index (4 players = 2 bits, but we use byte for simplicity)
        writer.Write((byte)PlayerIndex);
        
        // Pack flags into one byte
        PlayerFrameFlags flags = PlayerFrameFlags.None;
        if (Facing == Facings.Left) flags |= PlayerFrameFlags.FacingLeft;
        if (HairSimulateMotion) flags |= PlayerFrameFlags.HairSimulateMotion;
        if (Dead) flags |= PlayerFrameFlags.Dead;
        if (Dashing && DashDir.HasValue)
        {
            flags |= PlayerFrameFlags.Dashing;
            if (DashWasB) flags |= PlayerFrameFlags.DashB;
        }
        writer.Write((byte)flags);
        
        // Position (7-bit encoded ints for compression)
        writer.WritePosition(Position);
        
        // Scale (sbyte * 16, clamped)
        writer.WriteScale(Scale);
        
        // Sprite color
        writer.WriteColor(SpriteColor);
        
        // Speed (shorts)
        writer.WriteSpeed(Speed);
        
        // Animation
        BinaryExtensions.Write7BitEncodedInt(writer, CurrentAnimationID);
        BinaryExtensions.Write7BitEncodedInt(writer, CurrentAnimationFrame);
        
        // Hair colors with RLE
        writer.WriteHairColors(HairColors);
        
        // Dash direction (if dashing)
        if ((flags & PlayerFrameFlags.Dashing) != 0 && DashDir.HasValue)
        {
            // Pack angle into single byte (256 steps for full circle)
            double angle = Math.Atan2(DashDir.Value.Y, DashDir.Value.X);
            writer.Write((byte)((angle / (2 * Math.PI) * 256 + 256) % 256));
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        
        PlayerFrameFlags flags = (PlayerFrameFlags)reader.ReadByte();
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
        
        if (Dashing)
        {
            double angle = reader.ReadByte() / 256.0 * 2 * Math.PI;
            DashDir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
        }
        else
        {
            DashDir = null;
        }
    }
}

/// <summary>
/// Player graphics data that changes rarely (sent once when player joins or sprite changes).
/// Contains animation string mappings and hair setup.
/// </summary>
public class PlayerGraphicsMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    
    public int Depth { get; set; }
    public PlayerSpriteMode SpriteMode { get; set; }
    public float SpriteRate { get; set; } = 1f;
    
    /// <summary>
    /// Animation name array - index corresponds to animation ID sent in frame messages.
    /// </summary>
    public string[] Animations { get; set; } = Array.Empty<string>();
    
    // Hair setup
    public byte HairCount { get; set; }
    public Vector2[] HairScales { get; set; } = Array.Empty<Vector2>();
    
    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerIndex);
        writer.Write(Depth);
        BinaryExtensions.Write7BitEncodedInt(writer, (int)SpriteMode);
        writer.Write(SpriteRate);
        
        // Animation names
        BinaryExtensions.Write7BitEncodedInt(writer, Animations.Length);
        foreach (var anim in Animations)
            writer.Write(anim ?? "");
        
        // Hair
        writer.Write(HairCount);
        for (int i = 0; i < HairCount && i < HairScales.Length; i++)
        {
            writer.WriteScale(HairScales[i]);
        }
    }
    
    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadByte();
        Depth = reader.ReadInt32();
        SpriteMode = (PlayerSpriteMode)BinaryExtensions.Read7BitEncodedInt(reader);
        SpriteRate = reader.ReadSingle();
        
        int animCount = BinaryExtensions.Read7BitEncodedInt(reader);
        Animations = new string[animCount];
        for (int i = 0; i < animCount; i++)
            Animations[i] = reader.ReadString();
        
        HairCount = reader.ReadByte();
        HairScales = new Vector2[HairCount];
        for (int i = 0; i < HairCount; i++)
            HairScales[i] = reader.ReadScale();
    }
}

/// <summary>
/// A client broadcasting all their players' frame states.
/// </summary>
public class ClientPlayersFrameMessage : INetMessage
{
    public List<PlayerFrameMessage> Frames { get; set; } = new();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Frames.Count);
        foreach (var frame in Frames)
        {
            frame.Serialize(writer);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        var count = reader.ReadByte();
        Frames = new List<PlayerFrameMessage>(count);
        for (var i = 0; i < count; i++)
        {
            var frame = new PlayerFrameMessage();
            frame.Deserialize(reader);
            Frames.Add(frame);
        }
    }
}

/// <summary>
/// Legacy message - use ClientPlayersFrameMessage instead.
/// </summary>
[Obsolete("Use ClientPlayersFrameMessage")]
public class ClientPlayersStateMessage : INetMessage
{
    public List<PlayerFrameMessage> PlayerStates { get; set; } = new();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)PlayerStates.Count);
        foreach (var state in PlayerStates)
        {
            state.Serialize(writer);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        var count = reader.ReadByte();
        PlayerStates = new List<PlayerFrameMessage>(count);
        for (var i = 0; i < count; i++)
        {
            var state = new PlayerFrameMessage();
            state.Deserialize(reader);
            PlayerStates.Add(state);
        }
    }
}

/// <summary>
/// Client requesting to add a player (sent to host).
/// </summary>
public class PlayerJoinRequestMessage : INetMessage
{
    public void Serialize(BinaryWriter writer) { }
    public void Deserialize(BinaryReader reader) { }
}

/// <summary>
/// Host responding to a player join request.
/// </summary>
public class PlayerJoinResponseMessage : INetMessage
{
    public bool Success { get; set; }
    public int AssignedPlayerIndex { get; set; }
    public string ErrorMessage { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(Success);
        writer.Write(AssignedPlayerIndex);
        writer.WriteNullableString(ErrorMessage);
    }

    public void Deserialize(BinaryReader reader)
    {
        Success = reader.ReadBoolean();
        AssignedPlayerIndex = reader.ReadInt32();
        ErrorMessage = reader.ReadString();
    }
}

/// <summary>
/// Host broadcasting that a new player was added.
/// </summary>
public class PlayerAddedMessage : INetMessage
{
    public ulong ClientSteamId { get; set; }
    
    /// <summary>
    /// The player's slot index (0-3). Assigned by host.
    /// </summary>
    public int PlayerIndex { get; set; }
    
    public string PlayerName { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(ClientSteamId);
        writer.Write(PlayerIndex);
        writer.WriteNullableString(PlayerName);
    }

    public void Deserialize(BinaryReader reader)
    {
        ClientSteamId = reader.ReadUInt64();
        PlayerIndex = reader.ReadInt32();
        PlayerName = reader.ReadString();
    }
}

/// <summary>
/// Host broadcasting that a player was removed.
/// </summary>
public class PlayerRemovedMessage : INetMessage
{
    public ulong ClientSteamId { get; set; }
    public int PlayerIndex { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(ClientSteamId);
        writer.Write(PlayerIndex);
    }

    public void Deserialize(BinaryReader reader)
    {
        ClientSteamId = reader.ReadUInt64();
        PlayerIndex = reader.ReadInt32();
    }
}

/// <summary>
/// Host sending current lobby state to a newly joined client.
/// </summary>
public class LobbyStateMessage : INetMessage
{
    public List<PlayerAddedMessage> Players { get; set; } = new();

    public void Serialize(BinaryWriter writer)
    {
        writer.Write((byte)Players.Count);
        foreach (var player in Players)
        {
            player.Serialize(writer);
        }
    }

    public void Deserialize(BinaryReader reader)
    {
        var count = reader.ReadByte();
        Players = new List<PlayerAddedMessage>(count);
        for (var i = 0; i < count; i++)
        {
            var player = new PlayerAddedMessage();
            player.Deserialize(reader);
            Players.Add(player);
        }
    }
}

/// <summary>
/// Player event types.
/// </summary>
public enum PlayerEventType : byte
{
    Death = 0,
    Respawn = 1,
    Dash = 2,
    Jump = 3,
    WallJump = 4,
    Climb = 5,
    Custom = 255
}

/// <summary>
/// A player event (death, dash, etc.).
/// </summary>
public class PlayerEventMessage : INetMessage
{
    public int PlayerIndex { get; set; }
    public PlayerEventType EventType { get; set; }
    public string Data { get; set; }

    public void Serialize(BinaryWriter writer)
    {
        writer.Write(PlayerIndex);
        writer.Write((byte)EventType);
        writer.WriteNullableString(Data);
    }

    public void Deserialize(BinaryReader reader)
    {
        PlayerIndex = reader.ReadInt32();
        EventType = (PlayerEventType)reader.ReadByte();
        Data = reader.ReadString();
    }
}

