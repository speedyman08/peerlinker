using System.ComponentModel;
using System.Net.Sockets;
using libpeerlinker.Messages;
using libpeerlinker.Peers;

namespace libpeerlinker.Exchange;

/// <summary>
/// Main class for exchanging pieces
/// </summary>
public class PieceFetcher
{
    private readonly List<PeerIpv4> _reachablePeers;
    private readonly PeerFinder _finder;
    private BindingList<PeerConn> ActiveConnections { get; } = [];

    public PieceFetcher(List<PeerIpv4> reachable, Handshake handshake) 
    {
       _finder = new PeerFinder(handshake);
       _reachablePeers = reachable;

       ActiveConnections.ListChanged += OnConnect;
    }
    
    async Task StartPopulatingConns()
    {
       if (ActiveConnections.Count != 0)
          return;

       List<Task<PeerConn?>> connectionAttempts = [];

       for (int i = 0; i < 10; i++)
       {
          int index = Random.Shared.Next(0, _reachablePeers.Count);
          connectionAttempts.Add(_finder.HandshakeAsync(_reachablePeers[index]));
       }

       PeerConn?[] results = await Task.WhenAll(connectionAttempts);

       foreach (var peerConn in results)
       {
          if (peerConn is not null)
             ActiveConnections.Add(peerConn);
       }
    }

    void OnConnect(object? sender, ListChangedEventArgs e)
    {
       if (e.ListChangedType == ListChangedType.ItemAdded)
       {
          var handle = ActiveConnections[e.NewIndex];
          Console.WriteLine($"Got a new long standing connection, we shook with {handle.InitialHandshake}"); 
       }
    }

    //useless peers go away
    void KillPeer(PeerConn conn)
    {
    //    _activeConnections.Remove(conn);
    } 
    void GetBitField()
    {
        
    }
    
    public async Task Start()
    {
       await StartPopulatingConns();
    }
}