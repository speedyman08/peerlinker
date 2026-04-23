using System.Buffers.Binary;
using libpeerlinker.Messages;

namespace libpeerlinker.Core;
/// <summary>
/// A block of data received from a peer, which is a shim over a Piece message.
/// </summary>
public class Block
{
    public required int PieceIdx { get; init; }
    public required int BlockOffset { get; init; }
    public required byte[] RawData { get; init; }

    private Block() {}

    public static Block FromPiece(Message pieceMsg)
    {
        var pieceIdx = BinaryPrimitives.ReadInt32BigEndian(pieceMsg.Payload.AsSpan(0,4));
        var blockOffset = BinaryPrimitives.ReadInt32BigEndian(pieceMsg.Payload.AsSpan(4,4));

        return new Block
        {
            PieceIdx = pieceIdx,
            BlockOffset = blockOffset,
            RawData = pieceMsg.Payload.AsSpan(8).ToArray(),
        };
    }
}