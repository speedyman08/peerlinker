using libpeerlinker.Peers;

namespace libpeerlinker.Messages;

public record AnnounceResponse(
    List<PeerIpv4> TrackerPeers,
    Int64 Seeders,
    Int64 Leechers
);