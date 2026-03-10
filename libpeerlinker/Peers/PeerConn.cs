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
   }
   ~PeerConn()
   {
      Dispose(false);
   }

   public async Task<bool> GetBitField()
   {
      try
      {
         var ctsSource = new CancellationTokenSource(TimeSpan.FromSeconds(5));

         var ns = Connection.GetStream();
         var msgLenBytes = new byte[4];
         await ns.ReadExactlyAsync(msgLenBytes, 0, 4, ctsSource.Token);
         var msgLenAsInt = BinaryPrimitives.ReadInt32BigEndian(msgLenBytes);

         if (msgLenAsInt == 0)
         {
            Console.WriteLine("Got a keepalive, ignoring for now im only checking bitfields");
            return false;
         }

         var fullMsg = new byte[msgLenAsInt];
         msgLenBytes.CopyTo(fullMsg, 0);
         
         await ns.ReadExactlyAsync(fullMsg, 4, msgLenAsInt - 4, ctsSource.Token);
         Message bitFieldMsg = MessageFactory.MakeFromBytes(fullMsg);
         BitField = bitFieldMsg.Payload ??
                    throw new NullReferenceException(
                       "Bitfield payload is null, therefore peer message parsed incorrectly");
      }
      catch (OperationCanceledException)
      {
         Console.WriteLine("Bitfield request timed out");
         return false;
      }
      catch (Exception e)
      {
         Console.WriteLine($"Bitfield request failed: {e.Message} ({e.GetType()})");
         return false;
      }

      return true;
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