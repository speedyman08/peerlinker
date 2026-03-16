using System.ComponentModel;
using System.Threading.Channels;
using libpeerlinker.Messages;
using libpeerlinker.Peers;
using libpeerlinker.Utility;

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
          Logger.Instance.Information("Using peer connection {peer}", handle.Handshake);
          var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

          var res = await handle.Messages.BlockUntilRead(MessageType.Bitfield, cts.Token);
          if (res is null)
          {
             Logger.Instance.Fatal("Did not get bitfield from peer {peer}", handle.Handshake);
             KillPeer(handle);
             return;
          }
          
          // payload can't be null
          handle.BitField = res.Payload!;
          Logger.Instance.Information("Got bitfield from peer {peer}", handle.Handshake);
          // send keepalive
          await handle.SendKeepAlive();
          Logger.Instance.Information("Sent keepalive to peer {peer}", handle.Handshake);
       }
    }

    async Task MainLoop()
    {
       var handle = ActiveConnections[Random.Shared.Next(ActiveConnections.Count)];
       Logger.Instance.Information("Picked random peer {peer}", handle.Handshake);
       
       var interestMsg = MessageFactory.MakeInterested();
       await handle.SendMessage(interestMsg);
       Logger.Instance.Information("Sent interested message to peer {peer}", handle.Handshake);
      
       // for any case we are choked we wait for a fresh unchoke message, after we expressed interest
       if (handle.MeChoked)
       {
          handle.Messages.FlushChannel(MessageType.Unchoke); // flush the channel 
          
          var unchokeToken = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
          var unchokeMsg = await handle.Messages.BlockUntilRead(MessageType.Unchoke, unchokeToken);
          if (unchokeMsg is null)
          {
             Logger.Instance.Fatal("peer {peer} did not unchoke us in time", handle.Handshake);
             KillPeer(handle);
             return;
          }
          Logger.Instance.Information("Received our unchoke from {peer}, proceeding with piece", handle.Handshake);
       }
       
       // handle.mechoked should hopefully be false now
       var res = await handle.GetBlock(0, 0, BlockLength);
       if (res is null)
       {
          Logger.Instance.Fatal("Failed to get block from peer {peer}", handle.Handshake);
          KillPeer(handle);
          return;
       }
       Logger.Instance.Information("Got the first block from peer {peer}", handle.Handshake);
       Logger.Instance.Debug("Block bytes: {bytes}", Convert.ToHexString(res));
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
