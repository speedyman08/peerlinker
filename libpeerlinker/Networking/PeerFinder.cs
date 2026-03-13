using System.Net.Sockets;
using System.Runtime.InteropServices;
using libpeerlinker.Tracking;
using libpeerlinker.Messages;

namespace libpeerlinker.Peers;

public class PeerFinder
{
    // Peers that have responded.
    private readonly List<PeerConn> _knownGoodPeers = new();

    private readonly byte[] _handshakeBytes;
    
    
    private byte[] MarshalHandshakeAsBytes(Handshake handshake)
    {
        var size = Marshal.SizeOf(handshake);
        var byteBuf = new byte[size];
        MemoryMarshal.Write(byteBuf, handshake);
        return byteBuf;
    }
    
    public PeerFinder(Handshake handshake)
    {
        _handshakeBytes = MarshalHandshakeAsBytes(handshake);
    }
    
    public async Task<DiscoveryResult> DiscoveryAsync(List<PeerIpv4> trackerPeers, Handshake handshake)
    {
        try
        {
            Console.WriteLine($"Using handshake {Convert.ToHexString(_handshakeBytes)}");
            // Handshake our peers given from the tracker in chunks of 20
            // store them in the known good peers list if they respond

            var numPeers = trackerPeers.Count;
            for (int i = 0; i < numPeers; i += 20)
            {
                // The lists of peers and their associated handshake task.
                List<Task<PeerConn?>> handshakeTasks = new();
                List<PeerIpv4> peers = new();

                Console.WriteLine($"-- Handshake Chunk({i}-{i + 20}) --");
                for (int j = i; j < i + 20 && j < numPeers; j++)
                {
                    handshakeTasks.Add(
                        HandshakeAsync(trackerPeers[j])
                    );
                    peers.Add(trackerPeers[j]);
                }

                Console.WriteLine($"-- Results({i}-{i + 20})--");
                while (handshakeTasks.Count > 0)
                {
                    var task = await Task.WhenAny(handshakeTasks);
                    var idx = handshakeTasks.IndexOf(task);

                    if (task.Result is not null)
                    {
                        _knownGoodPeers.Add(task.Result);
                        Console.WriteLine($"{peers[idx]} added to reachable peers");
                    }

                    peers.RemoveAt(idx);
                    handshakeTasks.RemoveAt(idx);
                }
            }
        }
        catch (TrackerException e)
        {
            Console.WriteLine("Did not get a successful announce");
            Console.WriteLine(e.Message);
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
            
            PeerConn peerConn = new(conn)
            {
                Handshake = responseHandshake,
            };

            return peerConn;
        }
        catch (SocketException)
        {
            Console.WriteLine($"{peer} refused the connection");
            return null;
        }
        catch (EndOfStreamException)
        {
            Console.WriteLine($"{peer} closed the connection before handshake");
            return null;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"{peer} timed out / IO too long");
            return null;
        }
        catch (IOException)
        {
            Console.WriteLine($"{peer} IO error");
            return null;
        }
        catch
        {
            Console.WriteLine($"{peer} unknown error");
            return null;
        }
    }
}