using System.Net.Sockets;

namespace libpeerlinker.Peers;


/// <summary>
///  A shim for peer connections. Usually long running.
/// </summary>
public class PeerConn(TcpClient conn)
{
   /// <summary>
   /// Heuristic value 0-10 depicting how useful a peer has been to me
   /// </summary>
   public int Priority { get; set; } = 5;

   public TcpClient Connection { get; init; } = conn;
   // Initially both are choked and no interest
   public bool MeChoked { get; set; } = true;
   public bool PeerChoked { get; set; } = true;
   public bool MeInterest { get; set; } = false;
   public bool PeerInterest { get; set; } = false;
   public byte[] BitField { get; set; } = [];
   
   ~PeerConn()
   {
      Connection.Close();
      Connection.Dispose();
   }

   public void GetBitField()
   {
      
   }
   //public async Task<bool> AttemptFetchPiece(int piece)
}