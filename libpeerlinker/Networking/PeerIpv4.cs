using System.Net;

namespace libpeerlinker.Peers;

/// <summary>
/// Represents a IPV4 peer
/// </summary>
public record PeerIpv4(IPAddress Ip, UInt16 Port)
{
    public override string ToString() => $"Peer {Ip}:{Port}";
}