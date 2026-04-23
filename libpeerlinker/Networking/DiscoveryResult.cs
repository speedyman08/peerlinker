namespace libpeerlinker.Peers;

public enum DiscoveryStatus
{
    Success,
    NoPeers,
}

/// <summary>
/// What we get after attempting peer discovery
/// </summary>
/// <param name="Status">State of the discovery attempt, we either have peers or not</param>
/// <param name="RespondingPeers">List of peers that we did a handshake with, if any</param>
public record DiscoveryResult(DiscoveryStatus Status, List<PeerConn> RespondingPeers);