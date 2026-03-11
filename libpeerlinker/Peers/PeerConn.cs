using System.Buffers.Binary;
using System.Net.Sockets;
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
   private NetworkStream Ns { get;  }
   
   
   public Handshake? InitialHandshake { get; set; }
   
   // Initially both are choked and no interest
   public bool MeChoked { get; set; } = true;
   public bool PeerChoked { get; set; } = true;
   public bool MeInterest { get; set; } = false;
   public bool PeerInterest { get; set; } = false;
   public byte[] BitField { get; set; } = [];

   public PeerConn(TcpClient conn)
   {
      if (!conn.Connected) throw new ArgumentException("(PeerConn) The TCP Client provided isn't connected to anything yet");

      Connection = conn;
      Ns = Connection.GetStream();
   }
   ~PeerConn()
   {
      Dispose(false);
   }

   public async Task<Message?> RecvMessage()
   {
      try
      {
         var ctsSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));
         var msgLenBytes = new byte[4];
         await Ns.ReadExactlyAsync(msgLenBytes, 0, 4, ctsSource.Token);
         var msgLenAsInt = BinaryPrimitives.ReadInt32BigEndian(msgLenBytes);

         var fullMsg = new byte[msgLenAsInt + 4];
         msgLenBytes.CopyTo(fullMsg, 0);

         await Ns.ReadExactlyAsync(fullMsg, 4, msgLenAsInt, ctsSource.Token);

         var msgObj = MessageFactory.MakeFromBytes(fullMsg);

         if (msgObj.Header.messageID == MessageType.Choke)
         {
            MeChoked = true;
         }
         else if (msgObj.Header.messageID == MessageType.Unchoke)
         {
            MeChoked = false;
         }

         return msgObj;
      }
      catch (OperationCanceledException)
      {
         Console.WriteLine($"Recv timed out, {InitialHandshake}");
         return null;
      }
      catch (EndOfStreamException)
      {
         Console.WriteLine($"Connection closed by peer, {InitialHandshake}");
         return null;
      }
      catch (Exception e)
      {
         Console.WriteLine($"Recv failed: {e.Message} ({e.GetType()}), {InitialHandshake}");
         return null;
      }
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
         Console.WriteLine($"Message timed out ({msg.Header.messageID}) for {InitialHandshake}");
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
      while (true)
      {
         var res = await RecvMessage();
         if (res is null)
            return null;

         if (res.Header.messageID == MessageType.Piece)
            return res.Payload;

         if (res.Header.messageID == MessageType.Choke)
         {
            Console.WriteLine("(GetBlock): Choke received, it's over");
            return null;
         }
         
         Console.WriteLine($"(GetBlock): Skipping {res.Header.messageID} while waiting for Piece");
      }
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