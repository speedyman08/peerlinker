using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using libpeerlinker.Messages;
using libpeerlinker.Utility;

namespace libpeerlinker.Peers;


/// <summary>
///  A shim for a TCP connection. Long running.
/// </summary>
public class PeerConn : IDisposable
{
   /// <summary>
   /// Heuristic value 0-10 depicting how useful a peer has been to me
   /// </summary>
   public int Priority { get; set; } = 5;
   public TcpClient Connection { get; init; }
   private NetworkStream Ns { get; }
   private Channel<Message> MessageChannel { get; } = Channel.CreateUnbounded<Message>();
   public MessageDispatcher Messages { get; }
   
   public Handshake? Handshake { get; set; }
   
   // Initially both are choked and no interest
   public bool MeChoked { get; set; } = true;
   public bool PeerChoked { get; set; } = true;
   public bool MeInterest { get; set; } 
   public bool PeerInterest { get; set; }
   public byte[] BitField { get; set; } = [];

   // Don't use this constructor directly, you can get a PeerConn object from the PeerFinder.Handshake method
   public PeerConn(TcpClient conn, Handshake handshake)
   {
      if (!conn.Connected)
         throw new ArgumentException("(PeerConn) The TCP Client provided isn't connected to anything yet");

      Connection = conn;
      Ns = Connection.GetStream();
      Handshake = handshake;


      // initiate the message pump and the dispatcher to channels
      _ = new MessageReceiver(Ns, MessageChannel, handshake).MessageLoop();

      Messages = new MessageDispatcher(MessageChannel, handshake);
      
      Messages.OnChoke = () =>
      {
         MeChoked = true;
         Logger.Instance.Debug("Peer {peer} choked us", Handshake);
      };
      Messages.OnUnchoke = () =>
      {
         MeChoked = false;
         Logger.Instance.Debug("Peer {peer} unchoked us", Handshake);
      };

   _ = Messages.RunAsync();
   }
   
   public async Task<bool> SendMessage(Message msg)
   {
      try
      {
         var ctsSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

         var msgBytes = msg.EncodeAsBytes();
         await Ns.WriteAsync(msgBytes, 0, msgBytes.Length, ctsSource.Token);
         return true;
      }
      catch (OperationCanceledException)
      {
         return false;
      }
      catch (Exception)
      {
         return false;
      }
   }

   public async Task<bool> SendKeepAlive()
   {
      var msg = MessageFactory.MakeKeepAlive();
      
      return await SendMessage(msg);
   }
   
   public async Task<byte[]?> GetBlock(int pieceIdx, int blockOffset, int blockLength)
   {
      if (MeChoked)
      {
         Logger.Instance.Verbose("Peer {peer} choked us, we can't request. GetBlock aborted", Handshake);
         return null;
      }
      var msg = MessageFactory.MakeRequest(pieceIdx, blockOffset, blockLength);
      await SendMessage(msg);

      var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
      
      var pieceMsg = await Messages.BlockUntilRead(MessageType.Piece, cts.Token);
      if (pieceMsg is not null) return pieceMsg.Payload;
      
      Logger.Instance.Debug("Did not receive block (piece index {pieceIdx}, block offset {blockOffset} from {peer}",pieceIdx, blockOffset, Handshake);
      return null;
   }
   
   private void Dispose(bool disposing)
   {
      if (disposing)
      {
         Connection.Dispose();
         MessageChannel.Writer.Complete();
      }
   }

   public void Dispose()
   {
      Dispose(true);
      GC.SuppressFinalize(this);
   }
}
