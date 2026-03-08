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
    public List<FileEntry> AllFiles { get; set; }
    /// The tracker's URL
    public required string TrackerURL { get; init; }
    
    /// Array representing the SHA-1 of every single piece concatenated.
    public byte[] PieceSHA1Hashes{ get; set; }
    
    /// The SHA-1 of the info dictionary in the .torrent
    public byte[] InfoDictSHA1 { get; set; }
    
    /// The length in bytes of each piece
    public required Int64 PieceLength { get; set; }

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
        
        #if DEBUG
        Console.WriteLine($"-- {filepath} --");
        PrettyPrint.DebugDict(benDict);
        
        #endif

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

        var infoDict = BencodeUtility.GetKeyExcept<BDictionary>(benDict, "info");
        
        // get pieces SHA-1 hash set
        var hashes = BencodeUtility.GetKeyExcept<BString>(infoDict, "pieces");
        var pieceLength = BencodeUtility.GetKeyExcept<BNumber>(infoDict, "piece length");
        
        // this is for calculating the info dict's SHA1 (needed in tracker requests)
        var infoDictRawBytes = infoDict.EncodeAsBytes()!;
        
        TorrentMetadata meta = new()
        {
            TrackerURL = announceString.ToString(),
            PieceSHA1Hashes = hashes.Value.ToArray(),
            PieceLength = pieceLength.Value,
        };
        
        using (MemoryStream ms = new MemoryStream(infoDictRawBytes))
        {
            SHA1 infoHash = SHA1.Create();
            infoHash.ComputeHash(ms);
            meta.InfoDictSHA1 = infoHash.Hash ?? throw new TorrentParsingException("could not calculate the information dictionary SHA1");
        }

        meta.PopulateWithInfo(infoDict);
        
        return meta;
    }

    private void PopulateWithInfo(BDictionary infoDict)
    {
        var files = new List<FileEntry>();
        
        // Used to understand if this is a multi or single file torrent (it does not exist on multi file torrents)
        var length = BencodeUtility.GetKey<BNumber>(infoDict, "length");
        
        // in a single file torrent, info -> length exists
        if (length is BNumber fileLength)
        {
            files.Add(MakeSingle(infoDict, fileLength));
        }
        else
        {
            files.AddRange(MakeMulti(infoDict));
        }

        AllFiles = files;
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

        return PieceSHA1Hashes.AsMemory().Slice(startOffset, 20);
    }
    
    public bool ProvidesOneFile() => AllFiles.Count == 1;
}