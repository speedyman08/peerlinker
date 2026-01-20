using libpeerlinker.FileHandling;
using libpeerlinker.Peers;

namespace libpeerlinker.Peers;

public record AnnounceResponse(
    List<PeerIpv4> TrackerPeers,
    Int64 Seeders,
    Int64 Leechers
);