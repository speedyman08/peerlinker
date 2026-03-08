using System.Net.Sockets;
using libpeerlinker.Peers;

namespace libpeerlinker.Exchange;

/// <summary>
/// Main class for exchanging pieces
/// </summary>
public class PieceFetcher(List<PeerIpv4> reachablePeers)
{
    private List<PeerIpv4> _reachablePeers { get; } = reachablePeers; 
    private List<TcpClient> _activeConnections { get; set; }

    async Task StartPopulatingConns()
    {
       // rank by usability 
    }

    async Task Start()
    {
       // a long running task in the background starting some connections we can probably reuse. it contains "Useful" tcp connections
       await Task.Run(StartPopulatingConns);
       
       
    }
}