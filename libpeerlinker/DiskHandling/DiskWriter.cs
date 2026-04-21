using libpeerlinker.Core;
using libpeerlinker.Utility;

namespace libpeerlinker.DiskHandling;

// A class for writing received blocks to a file, in order, and validation via a digest.
// we also update our own bitfield when we have the necessary blocks for a piece.

public class DiskWriter
{
    private readonly FileStream _handle;
    private readonly BitField _completedPieces;
    private readonly int _blocksInPiece;
    private readonly int _pieceLength;
    private readonly Dictionary<int, int> _blocksCounterForPiece = new();

    public DiskWriter(string filename, int blocksInPiece, int totalSize, int pieceLength, BitField ourBitField)
    {
        _handle = new FileStream(filename, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        _blocksInPiece = blocksInPiece;
        _completedPieces = ourBitField;
        _pieceLength = pieceLength;
        
        _handle.SetLength(totalSize);
    }

    private void CountPiecesFromBlocks(List<Block> blocks)
    {
        var groupedBlocks = blocks.GroupBy(blk => blk.PieceIdx)
            .OrderBy(grp => grp.Key).ToList();
        
        groupedBlocks.ForEach(grouping =>
        {
            _blocksCounterForPiece[grouping.Key] = grouping.Count();
        });
        
        // then update the bitfield
        var completedPieceIndices = _blocksCounterForPiece.Where((_, v) => v >= _blocksInPiece);
        foreach (var completedPieceIndex in completedPieceIndices)
        {
            _completedPieces.SetPiece(completedPieceIndex.Key);
        }
    }

    public async Task WriteBlockChunk(List<Block> blocks)
    {
        CountPiecesFromBlocks(blocks);
        foreach (var blk in blocks)
        {
            long filePosition = (long)blk.PieceIdx * _pieceLength + blk.BlockOffset;
            _handle.Seek(filePosition, SeekOrigin.Begin);
            await _handle.WriteAsync(blk.RawData, 0, blk.RawData.Length);
        }
    }
}