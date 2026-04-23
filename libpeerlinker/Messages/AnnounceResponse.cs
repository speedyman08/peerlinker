using libpeerlinker.Peers;

namespace libpeerlinker.Messages;

// The deserialized response a tracker gives after an "Announce" request. 
public record AnnounceResponse(
    List<PeerIpv4> TrackerPeers,
    Int64 Seeders,
    Int64 Leechers
);