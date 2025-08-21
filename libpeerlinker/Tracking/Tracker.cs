using System.Buffers.Binary;
using System.Net;
using System.Text;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using libpeerlinker.FileHandling;
using libpeerlinker.Peers;

namespace libpeerlinker.Tracking;

/// <summary>
/// A class which represents a tracker, currently allows for initialisation only with the <c>Announce()</c> method.
/// </summary>
public class Tracker
{
    private HttpClient m_httpClient;

    /// Underlying metadata file
    private TorrentMetadata m_torrent;
    
    /// The version sent over in the peer identifier
    private Version m_ver;

    /// A flag for operating in debug mode.
    public bool Debug { get; init; } = false;
    
    /// The files we will be exchanging
    /// Prefer this to meta.AllFiles
    public TorrentFile[] FileSet { get; set; } = [];
    
    /// A unique string to identify our client with length 20. Azureus style as that
    /// makes this information parsable to most other clients
    /// like: -PL0001-(8 random chars)
    public readonly string Identifier;

    public Tracker(TorrentMetadata meta, Version clientVer)
    {
        ValidateVersion(clientVer);
        m_ver = clientVer;

        m_httpClient = new HttpClient()
        {
            BaseAddress = new Uri(meta.TrackerURL)
        };

        m_torrent = meta;
        m_httpClient.DefaultRequestHeaders.Add("User-Agent", "peerlinker/1.0");
        m_httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        Identifier = GenId();
    }

    private void ValidateVersion(Version version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.Major < 0 || version.Major > 9)
            throw new TrackerException("Major must be 0–9");
        if (version.Minor < 0 || version.Minor > 99)
            throw new TrackerException("Minor must be 0–99");
        if (version.Build < 0 || version.Build > 9)
            throw new TrackerException("Build must be 0–9");
    }

    private string GenId()
    {
        // The client identifier prefix with correctly padded minor version.
        // This currently follows the BEP20 standard. looks like -PL0001-
        var header = $"-PL{m_ver.Major}{(m_ver.Minor < 10 ? "0" + m_ver.Minor : m_ver.Minor)}{m_ver.Build}-";

        if (header.Length > 8)
        {
            throw new TrackerException("Tracker ID too long. somehow.");
        }

        // Append this prefix with random characters, also ensure that we are exactly 20 chars.
        StringBuilder id = new(header);
        for (var i = 0; i < 20 - header.Length; i++)
        {
            id.Append((char)('a' + Random.Shared.Next(0, 26)));
        }

        return id.ToString();
    }

    private PeerIpv4[] ParsePeerList(BDictionary bResponse)
    {
        var peersBytes = BencodeHelper.GetKeyExcept<BString>(bResponse, "peers").Value;
        List<PeerIpv4> peers = new();

        for (int i = 6; i < peersBytes.Length + 1; i += 6)
        {
            var ipSlice = peersBytes.Slice(i - 6, 4);
            var portSlice = peersBytes.Slice(i - 2, 2);

            var ip = new IPAddress(ipSlice.Span);
            var port = BinaryPrimitives.ReadUInt16BigEndian(portSlice.Span);

            peers.Add(new PeerIpv4(ip, port));
        }

        return peers.ToArray();
    }

    private Int64 TotalSize()
    {
        if (FileSet.Any())
        {
            return FileSet.Sum(file => file.Size);
        }
        
        // if fileset is empty, default is to consider all files in meta
        return m_torrent.AllFiles.Sum(file => file.Size);
    }

    /// Sends an Announce request to the tracker described in the TorrentMetadata.
    /// You can additionally send over your downloaded and uploaded statistics if you're continuing a download / re announcing
    public async Task<PeerTrackerData> Announce(Int64 downloadedBytes = 0, Int64 uploadedBytes = 0)
    {
        var encodedInfoHash = "";
        var rawHexStr = Convert.ToHexString(m_torrent.InfoDictSHA1);

        for (int i = 0; i < rawHexStr.Length; i += 2)
        {
            encodedInfoHash += string.Concat("%", rawHexStr.AsSpan(i, 2));
        }

        var queryStr =
            $"?info_hash={encodedInfoHash}" +
            $"&peer_id={Identifier}" +
            $"&port=6881" +
            $"&downloaded={downloadedBytes}" +
            $"&uploaded={uploadedBytes}" +
            $"&left={TotalSize()}" +
            $"&numwant=50" +
            $"&event=started" +
            $"&compact=1";

        var fullUri = new Uri(m_httpClient.BaseAddress + queryStr, new UriCreationOptions
        {
            // god, what is this
            DangerousDisablePathAndQueryCanonicalization = true
        });

        Console.WriteLine($"Full URI: {fullUri}");

        HttpRequestMessage announceReq = new(HttpMethod.Get, fullUri);

        BDictionary responseDict;
        try
        {
            var trackerResponse = await m_httpClient.SendAsync(announceReq);

            if (!trackerResponse.IsSuccessStatusCode)
            {
                throw new TrackerException(trackerResponse.ReasonPhrase ?? "Unknown");
            }

            Stream responseContent = trackerResponse.Content.ReadAsStream();

            responseDict = new BDictionaryParser(new BencodeParser()).Parse(responseContent);
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"Tracker {m_torrent.TrackerURL}timed out.");
            throw new TrackerException($"Timed out: {ex.Message}");
        }

        if (Debug)
        {
            var dump = new BDictionary(responseDict.Where(entry => entry.Key != "peers"));
            Console.WriteLine("-- Response Dump --");
            PrettyPrint.DebugDict(dump);
        }

        var peers = ParsePeerList(responseDict);

#if DEBUG
        Console.WriteLine("-- Peer List --");
        foreach (var peerIpv4 in peers)
        {
            Console.WriteLine(peerIpv4);
        }
#endif

        return new PeerTrackerData(
            peers,
            FileSet,
            BencodeHelper.GetKeyExcept<BNumber>(responseDict, "complete").Value,
            BencodeHelper.GetKeyExcept<BNumber>(responseDict, "incomplete").Value
        );
    }
}