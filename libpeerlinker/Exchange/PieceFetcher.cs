using System.ComponentModel;
using System.Net.Sockets;
using libpeerlinker.Peers;

namespace libpeerlinker.Exchange;

/// <summary>
/// Main class for exchanging pieces
/// </summary>
public class PieceFetcher(List<PeerIpv4> reachablePeers)
{
    private List<PeerIpv4> _reachablePeers { get; } = reachablePeers;
    private BindingList<PeerConn> _activeConnections { get; } = new();

    //async Task StartPopulatingConns()
    //{
     //  if (_activeConnections.Count == 0)
      // {
       //    for (int i = 0; i < 10; i++)
        //   {
               // take random
         //      await Task.Run(() =>
               //{
          //         var peer = _reachablePeers[Random.Shared.Next(0, _reachablePeers.Count)];
          //         _activeConnections.Add(new PeerConn(peer));
           //    });
          // } 
       //}
    //}

    //useless peers go away
    private void KillPeer(PeerConn conn)
    {
    //    _activeConnections.Remove(conn);
    }
    private void GetBitField()
    {
        
    }
    
    async Task Start()
    {
       //await Task.Run(StartPopulatingConns);
    }
}