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
   public bool MeInterest { get; set; } = false;
   public bool PeerInterest { get; set; } = false;
   public byte[] BitField { get; set; } = [];

   // Don't use this constructor directly, you can get a PeerConn object from the PeerFinder.Handshake method
   public PeerConn(TcpClient conn, Handshake handshake)
   {
      if (!conn.Connected) throw new ArgumentException("(PeerConn) The TCP Client provided isn't connected to anything yet");

      Connection = conn;
      Ns = Connection.GetStream();
      Handshake = handshake;

      
      // initiate the message pump and the dispatcher to channels
      _ = new MessageReceiver(Ns, MessageChannel, handshake).MessageLoop();
      
      Messages = new MessageDispatcher(MessageChannel, handshake);
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
      var msg = MessageFactory.MakeRequest(pieceIdx, blockOffset, blockLength);
      await SendMessage(msg);

      var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

      try
      {
         var firstPiece = await Messages.PieceMessages.Reader.ReadAsync(cts.Token);

         return firstPiece.Payload;
      }
      catch (OperationCanceledException)
      {
         Logger.Instance.Verbose("Timed out waiting for block from peer {peer}", Handshake);
         return null;
      }
      catch (ChannelClosedException) // this may happen if the network stream received EOF, where then the dispatcher closes all channels
      {
         Logger.Instance.Verbose("Peer {peer} closed channel before piece message", Handshake);
         return null;
      }
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
