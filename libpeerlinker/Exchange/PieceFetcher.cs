using System.ComponentModel;
using libpeerlinker.Messages;
using libpeerlinker.Peers;

namespace libpeerlinker.Exchange;

/// <summary>
/// Main class for exchanging pieces
/// </summary>
public class PieceFetcher
{
    private readonly List<PeerConn> _reachablePeers;
    private readonly PeerFinder _finder;
    private BindingList<PeerConn> ActiveConnections { get; } = [];

    public PieceFetcher(List<PeerConn> reachable, Handshake handshake) 
    {
       _finder = new PeerFinder(handshake);
       _reachablePeers = reachable;

       ActiveConnections.ListChanged += OnConnect;
    }
    
    async Task StartPopulatingConns()
    {
       if (ActiveConnections.Count != 0)
          return;
       
       var chosenPeers = _reachablePeers.Shuffle().Take(_reachablePeers.Count / 2).ToList();
       chosenPeers.ForEach(i => Console.WriteLine($"Considering {i.InitialHandshake} as first candidate"));
       chosenPeers.ForEach(i => ActiveConnections.Add(i));
    }

    async void OnConnect(object? sender, ListChangedEventArgs e)
    {
       if (e.ListChangedType == ListChangedType.ItemAdded)
       {
          var handle = ActiveConnections[e.NewIndex];
          var res = await handle.GetBitField();
          if (!res)
          {
             Console.WriteLine($"(PieceFetcher): failed to get bitfield for {handle.InitialHandshake}");
             KillPeer(handle);
             return;
          }
          
          Console.WriteLine($"(PieceFetcher): Got bitfield for {handle.InitialHandshake}");
       }
    }

    void KillPeer(PeerConn conn)
    {
       ActiveConnections.Remove(conn);
       conn.Dispose();
    }
    public async Task Start()
    {
       await StartPopulatingConns();
    }
}