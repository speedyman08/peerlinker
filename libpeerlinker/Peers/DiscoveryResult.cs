namespace libpeerlinker.Peers;

public enum DiscoveryStatus
{
    Success,
    NoPeers,
}

public record DiscoveryResult(DiscoveryStatus Status, List<PeerIpv4> RespondingPeers);