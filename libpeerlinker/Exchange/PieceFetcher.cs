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
       chosenPeers.ForEach(i => Console.WriteLine($"Considering {i.Handshake} as first candidate"));
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
             Console.WriteLine($"(PieceFetcher): Never got a bitfield from {handle.Handshake}");
             KillPeer(handle);
             return;
          }
          
          // payload can't be null
          handle.BitField = res.Payload!;
          Console.WriteLine($"(PieceFetcher): Got bitfield for {handle.Handshake}");
          // send keepalive
          await handle.SendKeepAlive();
          Console.WriteLine($"(PieceFetcher): Sent keepalive to {handle.Handshake}, OnConnect is DONE");
       }
    }

    async Task MainLoop()
    {
       var handle = ActiveConnections[Random.Shared.Next(ActiveConnections.Count)];
       
       // try get the first block for now;
       
       Console.WriteLine($"Attempting first piece");
       var interestMsg = MessageFactory.MakeInterested();
       await handle.SendMessage(interestMsg);

       // while (handle.Connection.Available > 0)
       // {
       //    // we have stray messages we don't actually care about
       //    var resStray = await handle.RecvMessage();
       //    if (resStray is null)
       //    {
       //       Console.WriteLine($"No message even though we have data available, {handle.InitialHandshake}");
       //       return;
       //    }
       //    
       //    Console.WriteLine($"(PieceFetcher): Got stray message from {handle.InitialHandshake}");
       //
       //    if (resStray.Header.messageID != MessageType.Choke) continue;
       //    Console.WriteLine($"(PieceFetcher): Got choke from {handle.InitialHandshake}");
       //    Console.WriteLine("I'm giving up");
       //       
       //    KillPeer(handle);
       //    return;
       // }
       
       var res = await handle.GetBlock(0, 0, BlockLength);
       if (res is null)
       {
          Console.WriteLine($"(PieceFetcher): Failed to get block from {handle.Handshake}");
          return;
       }
       
       Console.WriteLine($"Raw Data: {Convert.ToHexString(res)}");
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