using System.Collections.Specialized;
using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using BencodeNET.Objects;
using BencodeNET.Parsing;
using libpeerlinker.FileHandling;
using Microsoft.VisualBasic.CompilerServices;

namespace libpeerlinker.Tracker;

/// <summary>
/// A class which represents a tracker, currently allows for initialisation only with the <c>Announce()</c> method.
/// </summary>
public class Tracker
{
    private HttpClient m_httpClient;
    /// Underlying metadata file
    private TorrentMetadata m_torrent;
    /// A unique string to identify ourselves
    private string m_identifier;

    /// The version sent over in the peer identifier
    private Version m_ver;
    /// A flag for operating in debug mode.
    /// In debug mode, the initial bencoded peer list (with compact=1) is requested for easier development, and all responses from peers and the tracker
    /// will be dumped.
    /// Without this, the initial list is binary encoded.
    public bool Debug { get; init; } = false;
    
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
        m_identifier = GenId();
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

    public async Task Announce()
    {
        var encodedInfoHash = "";
        var rawHexStr = Convert.ToHexString(m_torrent.InfoDictSHA1);

        for (int i = 0; i < rawHexStr.Length; i += 2)
        {
            encodedInfoHash += string.Concat("%", rawHexStr.AsSpan(i, 2));
        }

        var queryStr = $"?info_hash={encodedInfoHash}" +
                       $"&peer_id={m_identifier}" +
                       $"&port=6881" +
                       $"&downloaded=0" +
                       $"&uploaded=0" +
                       $"&left={TotalPieces()}" +
                       $"&event=started";


        var fullUri = new Uri(m_httpClient.BaseAddress + queryStr, new UriCreationOptions
        {
            // god, what is this
            DangerousDisablePathAndQueryCanonicalization = true
        });

        Console.WriteLine($"Full URI: {fullUri}");
        
        HttpRequestMessage announceReq = new(HttpMethod.Get, fullUri);
        
        try
        {
            var trackerResponse = await m_httpClient.SendAsync(announceReq);

            if (!trackerResponse.IsSuccessStatusCode)
            {
                throw new TrackerException(trackerResponse.ReasonPhrase ?? "Unknown");
            }
            
            Stream responseContent = trackerResponse.Content.ReadAsStream();
        
            BDictionary contentBencoded = new BDictionaryParser(new BencodeParser()).Parse(responseContent);
        
            Console.WriteLine("Response Dump");
            PrettyPrint.DebugDict(contentBencoded);
        }
        catch (TaskCanceledException)
        {
            Console.WriteLine($"Tracker {m_torrent.TrackerURL}timed out.");
        }
    }

    private Int64 TotalPieces()
    {
        return m_torrent.AllFiles.Aggregate(0l, (pieces, file) => pieces += file.NumPieces);
    }
}