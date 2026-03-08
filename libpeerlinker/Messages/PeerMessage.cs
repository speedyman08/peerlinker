using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace libpeerlinker.Messages;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct MessageHeader
{
    public fixed byte lengthPrefix[4];
    public MessageType messageID;
}

public enum MessageType : byte
{
    Choke = 0,
    Unchoke = 1,
    Interested = 2,
    NotInterested = 3,
    Have = 4,
    Bitfield = 5,
    Request = 6,
    Piece = 7,
    Cancel = 8,
    Port = 9,
    KeepAlive = 10, // a special case
}

/// <summary>
/// A class representing every message that can be sent or received between peers
/// Use the <c>MessageFactor</c> class to create this object
/// </summary>
public class Message
{
    public MessageHeader Header { get; init; }
    public byte[]? Payload { get; init; }
    public bool IsKeepAlive { get; init; }

    public MessageType GetMsgType()
    {
        return IsKeepAlive ? MessageType.KeepAlive : Header.messageID;
    }
    
    public byte[] EncodeAsBytes()
    {
        if (IsKeepAlive)
        {
            return new byte[] {0,0,0,0};
        }

        var payloadLen = Payload?.Length ?? 0;
        var headerSize = Marshal.SizeOf(Header);
        var byteBuf = new byte[headerSize + payloadLen];
        MemoryMarshal.Write(byteBuf, Header);

        if (Payload is null)
        {
            return byteBuf;
        } 
        Payload.CopyTo(byteBuf, headerSize);

        return byteBuf;
    }
}

public static class MessageFactory
{
    private static unsafe MessageHeader MakeHeader(int payloadLength, MessageType id)
    {
        MessageHeader ms = new();
        byte* len = (byte*)&ms;
        
        // + 1 for the messageID byte
        BinaryPrimitives.WriteInt32BigEndian(new Span<byte>(len, 4), payloadLength + 1);
        ms.messageID = id;
        
        return ms;
    } 
    public static Message MakeKeepAlive()
    {
        return new Message
        {
            IsKeepAlive = true,
            Payload = null,
        };
    }

    public static Message MakeChoke()
    {
        return new Message
        {
            Header = MakeHeader(0, MessageType.Choke),
            Payload = null
        };
    }

    public static Message MakeUnchoke()
    {
        return new Message
        {
            Header = MakeHeader(0, MessageType.Unchoke),
            Payload = null
        };
    }

    public static Message MakeInterested()
    {
        return new Message
        {
            Header = MakeHeader(0, MessageType.Interested),
            Payload = null
        };
    }

    public static Message MakeNotInterested()
    {
        return new Message
        {
            Header = MakeHeader(0, MessageType.NotInterested),
            Payload = null
        };
    }

    public static Message MakeHave(int pieceIdx)
    {
        var payload = new byte[4];
        BinaryPrimitives.WriteInt32BigEndian(payload, pieceIdx);

        return new Message
        {
            Header = MakeHeader(4, MessageType.Have),
            Payload = payload,
        };
    }
}