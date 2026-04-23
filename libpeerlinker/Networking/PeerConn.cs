using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using libpeerlinker.Core;
using libpeerlinker.Messages;
using libpeerlinker.Utility;

namespace libpeerlinker.Peers;


/// <summary>
///  PeerConn is a shim for a TCP connection, it provides various helper methods for sending messages, and proviedes a unified way to
///  handle inbound messages with the Messages property. It also stores the state of relationship between us and the peer
///  like if we're choked or we choked the peer, the peer's BitField, giving us info about what pieces they have
/// </summary>
public class PeerConn : IDisposable
{
   public double PickChance { get; private set; } = 1;
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

   public void CalculatePickChance(int totalBlocksDownloaded)
   {
      PickChance = Math.Pow((float)BlocksDownloaded / totalBlocksDownloaded, 2) + 0.005;
   }
   
   public async Task<bool> SendKeepAlive()
   {
      var msg = MessageFactory.MakeKeepAlive();
      
      return await SendMessage(msg);
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
