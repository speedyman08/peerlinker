using System.ComponentModel;
using libpeerlinker.Messages;
using libpeerlinker.Peers;

namespace libpeerlinker.Exchange;

/// <summary>
/// Main class for exchanging pieces
/// </summary>
public class PieceFetcher
{
    public int BlockLength { get; set; } = 16000;
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
       chosenPeers.ForEach(i => ActiveConnections.Add(i));
    }

    async void OnConnect(object? sender, ListChangedEventArgs e)
    {
       if (e.ListChangedType == ListChangedType.ItemAdded)
       {
          var handle = ActiveConnections[e.NewIndex];
          var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

          Message res;
          
          try {
             res = await handle.Messages.BitfieldMessages.Reader.ReadAsync(cts.Token);
          }
          catch (OperationCanceledException)
          {
             KillPeer(handle);
             return;
          }
          
          // payload can't be null
          handle.BitField = res.Payload!;
          // send keepalive
          await handle.SendKeepAlive();
       }
    }

    async Task MainLoop()
    {
       var handle = ActiveConnections[Random.Shared.Next(ActiveConnections.Count)];
       
       // try get the first block for now
       
       var interestMsg = MessageFactory.MakeInterested();
       await handle.SendMessage(interestMsg);
       
       var res = await handle.GetBlock(0, 0, BlockLength);
       if (res is null)
       {
          return;
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
       await MainLoop();
    }
}
