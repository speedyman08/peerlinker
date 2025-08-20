using System.Net;

namespace libpeerlinker.Peers;

public record PeerIpv4(IPAddress Ip, UInt16 Port)
{
    public override string ToString() => $"Peer {Ip}:{Port}";
}