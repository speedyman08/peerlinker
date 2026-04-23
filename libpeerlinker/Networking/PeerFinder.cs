using System.Net.Sockets;
using System.Runtime.InteropServices;
using libpeerlinker.Tracking;
using libpeerlinker.Messages;
using libpeerlinker.Utility;

namespace libpeerlinker.Peers;

/// <summary>
/// PeerFinder is a class responsible for finding peers to connect to, given a list of IP addresses
/// It attempts to handshake peers going in chunks of 20.
/// Returns a PeerConn object on succeess, preserving the initial TCP connection made during the handshake sequence.
/// </summary>
public class PeerFinder
{
    // Peers that have responded.
    private readonly List<PeerConn> _knownGoodPeers = new();

    private readonly byte[] _handshakeBytes;

    private Handshake _handshake;
    
    private byte[] MarshalHandshakeAsBytes(Handshake handshake)
    {
        var size = Marshal.SizeOf(handshake);
        var byteBuf = new byte[size];
        MemoryMarshal.Write(byteBuf, handshake);
        return byteBuf;
    }
    
    public PeerFinder(Handshake handshake)
    {
        _handshake = handshake;
        _handshakeBytes = MarshalHandshakeAsBytes(handshake);
    }
    
    public async Task<DiscoveryResult> DiscoveryAsync(List<PeerIpv4> trackerPeers)
    {
        try
        {
            // Handshake our peers given from the tracker in chunks of 20
            // store them in the known good peers list if they respond

            var numPeers = trackerPeers.Count;
            for (int i = 0; i < numPeers; i += 20)
            {
                // The lists of peers and their associated handshake task.
                List<Task<PeerConn?>> handshakeTasks = new();
                List<PeerIpv4> peers = new();

                for (int j = i; j < i + 20 && j < numPeers; j++)
                {
                    handshakeTasks.Add(
                        HandshakeAsync(trackerPeers[j])
                    );
                    peers.Add(trackerPeers[j]);
                }

                while (handshakeTasks.Count > 0)
                {
                    var task = await Task.WhenAny(handshakeTasks);
                    var idx = handshakeTasks.IndexOf(task);

                    if (task.Result is not null)
                    {
                        _knownGoodPeers.Add(task.Result);
                    }

                    peers.RemoveAt(idx);
                    handshakeTasks.RemoveAt(idx);
                }
            }
        }
        catch (TrackerException)
        {
        }

        return _knownGoodPeers.Count == 0
            ? new DiscoveryResult(DiscoveryStatus.NoPeers, _knownGoodPeers)
            : new DiscoveryResult(DiscoveryStatus.Success, _knownGoodPeers);
    }

    /// <summary>
    /// HandshakeAsync returns a PeerConn object if successful handshake, null otherwise.
    /// Keep the connection stored in the PeerConn object alive for later
    /// </summary>
    /// <param name="peer">Object containing IP info</param>
    public async Task<PeerConn?> HandshakeAsync(PeerIpv4 peer)
    {
        try
        {
            var conn = new TcpClient();

            using var connCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await conn.ConnectAsync(peer.Ip.ToString(), peer.Port, connCts.Token);

            using var ioCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            using var _ = ioCts.Token.Register(conn.Dispose);

            var netStream = conn.GetStream();
            byte[] response = new byte[68];

            await netStream.WriteAsync(_handshakeBytes, ioCts.Token);
            await netStream.ReadExactlyAsync(response, 0, response.Length, ioCts.Token);

            Handshake responseHandshake = Handshake.FromBytes(response);

            Logger.Instance.Information("Handshake successful with {PeerIdentifier}", responseHandshake);
            
            PeerConn peerConn = new(conn, responseHandshake);

            return peerConn;
        }
        catch
        {
            return null;
        }
    }
}
