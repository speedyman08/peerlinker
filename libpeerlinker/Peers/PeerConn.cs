using System.Buffers.Binary;
using System.Net.Sockets;
using libpeerlinker.Messages;

namespace libpeerlinker.Peers;


/// <summary>
///  A shim for a TCP connection. Long running.
/// </summary>
public class PeerConn
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
      Connection.Close();
      Connection.Dispose();
   }

   public async Task GetBitField()
   {
      var ns = Connection.GetStream();
      byte[] msgLenBytes = [];
      await ns.ReadExactlyAsync(msgLenBytes, 0, 4);
      var msgLenAsInt = BinaryPrimitives.ReadInt32BigEndian(msgLenBytes);

      byte[] fullMsg = [];
      await ns.ReadExactlyAsync(fullMsg, 0, msgLenAsInt);
      Message bitFieldMsg = MessageFactory.MakeFromBytes(fullMsg);
      BitField = bitFieldMsg.Payload ?? throw new NullReferenceException("Bitfield payload is null, therefore peer message parsed incorrectly");
   }
   //public async Task<bool> AttemptFetchPiece(int piece)
}