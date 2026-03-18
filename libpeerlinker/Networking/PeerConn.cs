using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using libpeerlinker.Core;
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

   public int BlocksDownloaded { get; set; } = 0;
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
   public BitField BitField { get; set; } = new([]);

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
         Logger.Instance.Debug("{peer} MeChoke = true", Handshake);
      };
      Messages.OnUnchoke = () =>
      {
         MeChoked = false;
         Logger.Instance.Debug("{peer} MeChoke = false", Handshake);
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
   
   
   // This is only really meant for 1 block at a time
   
   // public async Task<Block?> GetBlock(Message requestMessage)
   // {
   //    if (MeChoked)
   //    {
   //       Logger.Instance.Verbose("Peer {peer} choked us, we can't request. GetBlock aborted", Handshake);
   //       return null;
   //    }
   //    
   //    var pieceIdx = BinaryPrimitives.ReadInt32BigEndian(requestMessage.Payload.AsSpan(0,4));
   //    var blockOffset = BinaryPrimitives.ReadInt32BigEndian(requestMessage.Payload.AsSpan(4,4));
   //    var len = BinaryPrimitives.ReadInt32BigEndian(requestMessage.Payload.AsSpan(8,4));
   //
   //    
   //    await SendMessage(requestMessage);
   //
   //    var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
   //    
   //    var pieceMsg = await Messages.BlockUntilRead(MessageType.Piece, cts.Token);
   //    if (pieceMsg is not null)
   //    {
   //       Logger.Instance.Debug("Received block (piece index {pieceIdx}, block offset {blockOffset}, len {len} from {peer}",pieceIdx, blockOffset, len, Handshake);
   //       
   //       return Block.FromPiece(pieceMsg);
   //    }
   //    
   //    Logger.Instance.Debug("Did not receive block (piece index {pieceIdx}, block offset {blockOffset}, len {len} from {peer}",pieceIdx, blockOffset, len, Handshake);
   //    return null;
   // }
   
   private void Dispose(bool disposing)
   {
      if (disposing)
      {
         Connection.Dispose();
      }
   }

   public void Dispose()
   {
      Dispose(true);
      GC.SuppressFinalize(this);
   }
}
