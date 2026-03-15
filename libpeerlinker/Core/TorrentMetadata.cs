using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using BencodeNET.Exceptions;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using libpeerlinker.Utility;
using libpeerlinker.FileHandling;

namespace libpeerlinker.Tracking;

/// <summary>
/// An object representing the metadata found in .torrent files.
/// Use the static <c>FromFile()</c> factory method to create a <c>TorrentMetadata</c> object.
/// </summary>
public class TorrentMetadata
{
    /// The list of files defined in the .torrent
    public List<FileEntry> AllFiles { get; private set; } = [];

    /// The tracker's URL
    public string TrackerUrl { get; private init; } = "";

    /// Array representing the SHA-1 of every single piece concatenated.
    public byte[] PieceSha1Hashes { get; private set; } = [];

    /// The SHA-1 of the info dictionary in the .torrent
    public byte[] InfoDictSha1 { get; private set; } = [];
    
    /// The length in bytes of each piece
    public Int64 PieceLength { get; private init; }

    private TorrentMetadata() {}

    /// Factory method for parsing a file into a torrent metadata object
    public static TorrentMetadata FromFile(string filepath)
    {
        BDictionary benDict;

        try
        {
            var fstream = File.OpenRead(filepath);
            benDict = new BDictionaryParser(new BencodeParser()).Parse(fstream);
        }
        catch (Exception e)
        {
            Debug.Print(e.Message);
            throw new FileLoadException($"{filepath} inaccessible");
        }
        
        BString announceString;
        try
        {
            announceString = BencodeUtility.GetKeyExcept<BString>(benDict, "announce");
            announceString.Encoding = Encoding.UTF8;
        }
        catch (BencodeException)
        {
            throw new TorrentParsingException(
                "No tracker is defined in the file. Most likely, this network makes use of DHT " +
                "which isn't supported yet.");
        }

        #if DEBUG
        var log = new BencodePrettyPrinter().StringRepresentation(benDict);
        Logger.Instance.Debug("Torrent metadata dictionary:\n{structure}", log);
        #endif
        var infoDict = BencodeUtility.GetKeyExcept<BDictionary>(benDict, "info");
        
        // get pieces SHA-1 hash set
        var hashes = BencodeUtility.GetKeyExcept<BString>(infoDict, "pieces");
        var pieceLength = BencodeUtility.GetKeyExcept<BNumber>(infoDict, "piece length");
        
        // this is for calculating the info dict's SHA1 (needed in tracker requests)
        var infoDictRawBytes = infoDict.EncodeAsBytes()!;
        
        TorrentMetadata meta = new()
        {
            AllFiles = new List<FileEntry>(),
            TrackerUrl = announceString.ToString(),
            PieceSha1Hashes = hashes.Value.ToArray(),
            PieceLength = pieceLength.Value,
        };
        
        using (MemoryStream ms = new MemoryStream(infoDictRawBytes))
        {
            SHA1 infoHash = SHA1.Create();
            infoHash.ComputeHash(ms);
            meta.InfoDictSha1 = infoHash.Hash ?? throw new TorrentParsingException("could not calculate the information dictionary SHA1");
        }

        meta.PopulateFilesWithInfo(infoDict);
        
        return meta;
    }

    private void PopulateFilesWithInfo(BDictionary infoDict)
    {
        // Used to understand if this is a multi or single file torrent (it does not exist on multi file torrents)
        var length = BencodeUtility.GetKey<BNumber>(infoDict, "length");
        
        // in a single file torrent, info -> length exists
        if (length is BNumber fileLength)
        {
            AllFiles.Add(MakeSingle(infoDict, fileLength));
        }
        else
        {
            AllFiles.AddRange(MakeMulti(infoDict));
        }
    }

    private FileEntry MakeSingle(BDictionary benDict, BNumber fileLength)
    {
        var fileName = BencodeUtility.GetKeyExcept<BString>(benDict, "name");
        var pieceLength = BencodeUtility.GetKeyExcept<BNumber>(benDict, "piece length");
        
        return new FileEntry 
        {
            Size = fileLength.Value, // the file size
            SuggestedFilename = fileName.ToString(),
            DirectoryHierarchy = null, // null as this is single-file
            NumPieces = fileLength.Value / pieceLength.Value // the length of the file over the pieces size
        };
    }

    private FileEntry[] MakeMulti(BDictionary benDict)
    {
        var files = BencodeUtility.GetKeyExcept<BList>(benDict, "files");
        
        List<FileEntry> objects = new();
        foreach (var o in files.Value)
        {
            var fileDict = (BDictionary)o;
            
            var hierarchyListFromFile = BencodeUtility.GetKeyExcept<BList>(fileDict, "path");
            var normalisedHierarchy = hierarchyListFromFile
                .Cast<BString>()
                .Select(x => x.ToString()).ToArray();
            
            var fileLen = BencodeUtility.GetKeyExcept<BNumber>(fileDict, "length");

            var pieces = (int) Math.Ceiling((double) fileLen / PieceLength);
            
            objects.Add(
                new FileEntry
                {
                    DirectoryHierarchy = normalisedHierarchy,
                    SuggestedFilename = normalisedHierarchy[^1],
                    Size = fileLen.Value,
                    NumPieces = pieces
                });
        }
        
        return objects.ToArray();
    }

    /// <summary>
    ///  Fetch the SHA1 hash of a specific piece by number
    /// </summary>
    /// <param name="piece">Piece number. Starts from 0</param>
    /// <returns>A readonly slice of bytes</returns>
    public ReadOnlyMemory<byte> GetPieceSha1(int piece)
    {
        long size = AllFiles.Sum(x => x.Size);
        if (piece >= size / PieceLength) throw new ArgumentOutOfRangeException(nameof(piece));
        
        int startOffset = 20 * piece;

        return PieceSha1Hashes.AsMemory().Slice(startOffset, 20);
    }
    
    public bool ProvidesOneFile() => AllFiles.Count == 1;
}