using System.Security.Cryptography;
using libpeerlinker.Core;
using libpeerlinker.FileHandling;
using libpeerlinker.Tracking;
using libpeerlinker.Utility;

namespace libpeerlinker.DiskHandling;

// A class for writing received blocks to a file, in order, and validation via a digest.
// we also update our own bitfield when we have the necessary blocks for a piece.

public class DiskWriter : IDisposable, IAsyncDisposable
{
    public const string TempFileName = "TorrentData.DAT";
    private readonly FileStream _handle;
    private readonly BitField _completedPieces;
    private readonly int _blocksInPiece;
    private readonly int _pieceLength;
    private readonly byte[] _shaHashes;
    // this is IN ORDER of the pieces we download
    private readonly List<FileEntry> _metaFiles;
    private readonly Dictionary<int, int> _blocksCounterForPiece = new();

    public DiskWriter(TorrentMetadata meta, int blocksInPiece, int totalSize, int pieceLength, BitField ourBitField)
    {
        _handle = new FileStream(TempFileName, FileMode.OpenOrCreate, FileAccess.ReadWrite);
        _blocksInPiece = blocksInPiece;
        _completedPieces = ourBitField;
        _pieceLength = pieceLength;
        _shaHashes = meta.PieceSha1Hashes;
        _metaFiles = meta.AllFiles;
        
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

    // Returns a list of incorrect piece indeces. Run this after downloading the file
    public async Task<List<int>> VerifyFile()
    {
        List<int> badPieceIndices = new();
        
        int curPieceIdx = 0;
        foreach (var existsPiece in _completedPieces)
        {
            if (existsPiece)
            {
                // check from disk, verify sha1 from meta
                var position = curPieceIdx * _pieceLength;
                _handle.Seek(position, 0);

                var metaShaBytes = _shaHashes.AsSpan(curPieceIdx * 20, 20).ToArray();
                var fileBytes = new byte[_pieceLength];
                await _handle.ReadExactlyAsync(fileBytes, 0, _pieceLength);

                using var hasher = SHA1.Create();
                var fileShaBytes = hasher.ComputeHash(fileBytes);
                if (!fileShaBytes.SequenceEqual(metaShaBytes))
                {
                    badPieceIndices.Add(curPieceIdx);
                }
            }
            
            curPieceIdx++;
        }

        return badPieceIndices;
    }
    
    public async Task WriteBlockChunk(List<Block> blocks)
    {
        CountPiecesFromBlocks(blocks);
        foreach (var blk in blocks)
        {
            long filePosition = (long)blk.PieceIdx * _pieceLength + blk.BlockOffset;
            _handle.Seek(filePosition, 0);
            await _handle.WriteAsync(blk.RawData, 0, blk.RawData.Length);
        }
    }

    public async Task SplitFile()
    {
        foreach (var entry in _metaFiles)
        {
            var path = entry.DirectoryHierarchy is null ? entry.SuggestedFilename : Path.Combine(entry.DirectoryHierarchy);
            
            _handle.Seek(0, 0);
            var file = new FileStream(path, FileMode.OpenOrCreate,
                FileAccess.Write);
            await _handle.CopyToAsync(file, (int)entry.Size);
        }
    }

    public void Dispose()
    {
        _handle.Dispose();
        File.Delete(TempFileName);
    }

    public async ValueTask DisposeAsync()
    {
        await _handle.DisposeAsync();
    }
}