using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using libpeerlinker.Messages;

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
   public MessageDispatcher Messages { get; }
   public Handshake? Handshake { get; set; }
   
   // Initially both are choked and no interest
   public bool MeChoked { get; set; } = true;
   public bool PeerChoked { get; set; } = true;
   public bool MeInterest { get; set; } = false;
   public bool PeerInterest { get; set; } = false;
   public byte[] BitField { get; set; } = [];

   // Don't use this constructor directly, you can get a PeerConn object from the PeerFinder.Handshake method
   public PeerConn(TcpClient conn)
   {
      if (!conn.Connected) throw new ArgumentException("(PeerConn) The TCP Client provided isn't connected to anything yet");

      Connection = conn;
      Ns = Connection.GetStream();

      var mainChannel = Channel.CreateUnbounded<Message>();
      _ = new MessageReceiver(Ns, mainChannel, Handshake.ToString() ?? "").MessageLoop();
      
      Messages = new MessageDispatcher(mainChannel);
      _ = Messages.RunAsync();
   }

   // public async Task<Message?> RecvMessage()
   // {
   //    try
   //    {
   //       var ctsSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
   //       var msgLenBytes = new byte[4];
   //       await Ns.ReadExactlyAsync(msgLenBytes, 0, 4, ctsSource.Token);
   //       var msgLenAsInt = BinaryPrimitives.ReadInt32BigEndian(msgLenBytes);
   //
   //       var fullMsg = new byte[msgLenAsInt + 4];
   //       msgLenBytes.CopyTo(fullMsg, 0);
   //
   //       await Ns.ReadExactlyAsync(fullMsg, 4, msgLenAsInt, ctsSource.Token);
   //
   //       var msgObj = MessageFactory.MakeFromBytes(fullMsg);
   //
   //       if (msgObj.Header.messageID == MessageType.Choke)
   //       {
   //          MeChoked = true;
   //       }
   //       else if (msgObj.Header.messageID == MessageType.Unchoke)
   //       {
   //          MeChoked = false;
   //       }
   //
   //       return msgObj;
   //    }
   //    catch (OperationCanceledException)
   //    {
   //       Console.WriteLine($"Recv timed out, {InitialHandshake}");
   //       return null;
   //    }
   //    catch (EndOfStreamException)
   //    {
   //       Console.WriteLine($"Connection closed by peer, {InitialHandshake}");
   //       return null;
   //    }
   //    catch (Exception e)
   //    {
   //       Console.WriteLine($"Recv failed: {e.Message} ({e.GetType()}), {InitialHandshake}");
   //       return null;
   //    }
   // }

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
         Console.WriteLine($"Message timed out ({msg.Header.messageID}) for {Handshake}");
         return false;
      }
      catch (Exception e)
      {
         Console.WriteLine($"Failed to send message: {e.Message} ({e.GetType()})");
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
      var firstPiece = await Messages.PieceMessages.Reader.ReadAsync(cts.Token);

      return firstPiece.Payload;
   }
   
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