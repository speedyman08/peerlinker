using libpeerlinker.FileHandling;
using libpeerlinker.Peers;

namespace libpeerlinker.Peers;

public record PeerTrackerData(
    PeerIpv4[] TrackerPeers,
    TorrentFile[] PickedFiles,
    Int64 Seeders,
    Int64 Leechers
);