using System.Net.Sockets;

namespace libpeerlinker.Peers;


/// <summary>
///  A shim for peer connections. Usually long running.
/// </summary>
public class PeerConn
{
   /// <summary>
   /// Heuristic value 0-10 depicting how useful a peer has been to me
   /// </summary>
   public int Priority { get; set; } = 5;
   public required TcpClient Connection { get; init; }
   
   
   // Initially both are choked and no interest
   public bool MeChoked { get; set; }= true;
   public bool PeerChoked { get; set; } = true;
   public bool MeInterest { get; set; } = false;
   public bool PeerInterest { get; set; } = false;
   
   /// <summary>
   /// Constructor
   /// </summary>
   /// <param name="ip">IP of a verified Peer</param>
   /// <exception cref="PeerConnException">Thrown if the connection attempt fails</exception>
   PeerConn(PeerIpv4 ip)
   {
      try
      {
         Connection = new TcpClient();

         Connection.Connect(ip.Ip, ip.Port);
      }
      catch (SocketException)
      {
         throw new PeerConnException("(peerconn) Could not connect to peer");
      }
   }
   
   //public async Task<bool> AttemptFetchPiece(int piece)
}