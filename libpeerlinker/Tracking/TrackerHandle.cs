using System.Buffers.Binary;
using System.Net;
using System.Text;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using libpeerlinker.FileHandling;
using libpeerlinker.Peers;
using libpeerlinker.Messages;
using libpeerlinker.Utility;

namespace libpeerlinker.Tracking;

/// <summary>
/// A class which represents a tracker, currently allows for initialisation only with the <c>Announce()</c> method.
/// </summary>
public class TrackerHandle
{
    private HttpClient m_httpClient;

    /// Underlying metadata file
    private TorrentMetadata m_torrent;

    /// The version sent over in the peer identifier
    private Version m_ver;

    public List<FileEntry> PickedFiles
    {
        get => field.Count == 0 ? m_torrent.AllFiles : field;
        set;
    } = new();

    /// A unique string to identify our client with length 20. Azureus style as that
    /// makes this information parsable to most other clients
    /// like: -PL0001-(8 random chars)
    public readonly string Identifier;

    /// Initialize our handle
    /// We need a metadata file and the client version to report to the tracker
    /// client verison thing will probably be replaced with configuration injection
    /// <param name="clientVer">A version to report to the tracker and other peers.</param>
    /// <param name="meta">Object describing metadata. Needed for info dictionary hash computation</param>
    
    public TrackerHandle(TorrentMetadata meta, Version clientVer)
    {
        ValidateVersion(clientVer);
        m_ver = clientVer;

        m_httpClient = new HttpClient
        {
            BaseAddress = new Uri(meta.TrackerUrl)
        };

        m_httpClient.DefaultRequestHeaders.Add("User-Agent", "peerlinker/1.0");
        m_httpClient.DefaultRequestHeaders.Add("Accept", "application/json");
        m_torrent = meta;
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

    private List<PeerIpv4> ParsePeerList(BDictionary bResponse)
    {
        var peersBytes = BencodeUtility.GetKeyExcept<BString>(bResponse, "peers").Value;
        List<PeerIpv4> peers = new();

        for (int i = 6; i < peersBytes.Length + 1; i += 6)
        {
            var ipSlice = peersBytes.Slice(i - 6, 4);
            var portSlice = peersBytes.Slice(i - 2, 2);

            var ip = new IPAddress(ipSlice.Span);
            var port = BinaryPrimitives.ReadUInt16BigEndian(portSlice.Span);

            peers.Add(new PeerIpv4(ip, port));
        }

        return peers;
    }

    private Int64 TotalSize()
    {
        if (PickedFiles.Any())
        {
            return PickedFiles.Sum(file => file.Size);
        }

        // if fileset is empty, default is to consider all files in meta
        return m_torrent.AllFiles.Sum(file => file.Size);
    }

    /// Sends an Announce request to the tracker described in the TorrentMetadata.
    /// You can additionally send over your downloaded and uploaded statistics if you're continuing a download / re announcing
    public async Task<AnnounceResponse> Announce(Int64 downloadedBytes = 0, Int64 uploadedBytes = 0)
    {
        var encodedInfoHash = "";
        var rawHexStr = Convert.ToHexString(m_torrent.InfoDictSha1);

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

            var failure = BencodeUtility.GetKey<BString>(responseDict, "failure reason");

            if (failure != null)
            {
                throw new TrackerException(failure.EncodeAsString());
            }
        }
        catch (TaskCanceledException ex)
        {
            Console.WriteLine($"Tracker {m_torrent.TrackerUrl}timed out.");
            throw new TrackerException($"Timed out: {ex.Message}");
        }

#if DEBUG
        var dump = new BDictionary(responseDict.Where(entry => 
                                                                  entry.Key != "peers"
                                                               && entry.Key != "peers6"));

        Console.WriteLine("-- Response Dump --");
        PrettyPrint.DebugDict(dump);
#endif
        var peers = ParsePeerList(responseDict);

#if DEBUG
        Console.WriteLine("-- Peer List --");
        foreach (var peerIpv4 in peers)
        {
            Console.WriteLine(peerIpv4);
        }
#endif

        // Sometimes, trackers will only send the interval and the compact peer list.
        // so we can just zero out these optionals
        var seeders = BencodeUtility.GetKey<BNumber>(responseDict, "complete");
        var leechers = BencodeUtility.GetKey<BNumber>(responseDict, "incomplete");

        seeders ??= 0;
        leechers ??= 0;

        return new AnnounceResponse(
            peers,
            seeders, leechers
        );
    }
}