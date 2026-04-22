using libpeerlinker.Exchange;
using libpeerlinker.Messages;
using libpeerlinker.Peers;
using libpeerlinker.Tracking;
using libpeerlinker.Utility; 

Logger.Instance.Information("Parsing torrent structure of fedora2.torrent");
TorrentMetadata meta = TorrentMetadata.FromFile("fedora2.torrent");

var tracker = new Tracker(meta, new Version(0, 0, 1));

Logger.Instance.Information("Sending Announce request to tracker {tracker}", meta.TrackerUrl);
var res = await tracker.Announce();

// we need an initial message
var handshake = new Handshake(meta, tracker.Identifier);

PeerFinder finder = new(handshake);

Logger.Instance.Information("Initiating handshakes with peers");
var discoveryResult = await finder.DiscoveryAsync(res.TrackerPeers);

var fetcher = new PieceFetcher(discoveryResult.RespondingPeers, meta);

Logger.Instance.Information("Starting main piece exchange service");
await fetcher.Start();