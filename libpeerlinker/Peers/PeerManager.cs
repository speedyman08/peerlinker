using System.Net.Sockets;
using System.Runtime.InteropServices;
using libpeerlinker.FileHandling;
using libpeerlinker.Packets;
using libpeerlinker.Tracking;

namespace libpeerlinker.Peers;

public class PeerManager
{
    // Associated tracker
    private Tracker m_tracker;

    /// Data from the torrent file
    private TorrentMetadata m_torrent;

    /// Struct representing the handshake packet
    private byte[] m_handshake;

    // Peers we have handshaked with
    private List<PeerIpv4> m_knownGoodPeers = new();
    public required string SaveDirectory { get; init; }

    public PeerManager(Tracker tracker, TorrentMetadata meta)
    {
        m_tracker = tracker;
        m_torrent = meta;
        var handshake = new HandshakeMessage(m_torrent.InfoDictSHA1, m_tracker.Identifier);

        var size = Marshal.SizeOf(handshake);
        var bytebuf = new byte[size];
        MemoryMarshal.Write(bytebuf, handshake);
        m_handshake = bytebuf;
    }

    public async Task StartDiscovery()
    {
        try
        {
            var initialTrackerInfo = await m_tracker.Announce();

            Console.WriteLine($"Requesting pieces for file {m_tracker.FileSet[0]}");
            Console.WriteLine($"Using handshake (hex dump) {Convert.ToHexString(m_handshake)}");
            // Handshake our peers given from the tracker in chunks of 20
            // store them in the known good peers list if they respond
            for (int i = 0; i < initialTrackerInfo.TrackerPeers.Length; i += 20)
            {
                // The lists of peers and their associated handshake task.
                List<Task<bool>> handShakes = new();
                List<PeerIpv4> peers = new();

                Console.WriteLine($"-- Handshake Chunk({i}-{i + 20}) --");
                for (int j = i; j < i + 20 && j < initialTrackerInfo.TrackerPeers.Length; j++)
                {
                    handShakes.Add(
                        Handshake(initialTrackerInfo.TrackerPeers[j])
                    );
                    peers.Add(initialTrackerInfo.TrackerPeers[j]);
                }

                Console.WriteLine($"-- Results({i}-{i + 20})--");
                while (handShakes.Count > 0)
                {
                    var task = await Task.WhenAny(handShakes);
                    var idx = handShakes.IndexOf(task);

                    if (task.Result)
                    {
                        m_knownGoodPeers.Add(peers[idx]);
                        Console.WriteLine($"{peers[idx]} added to known good peers list");
                    }

                    peers.RemoveAt(idx);
                    handShakes.RemoveAt(idx);
                }
            }

            Console.WriteLine($"Handshakes are FINISHED. Opening TCP Connections to {m_knownGoodPeers.Count} peers");
        }
        catch (TrackerException e)
        {
            Console.WriteLine("Did not get a successful announce");
        }
    }

    private async Task<bool> Handshake(PeerIpv4 peer)
    {
#if DEBUG
        Console.WriteLine($"Initiating handshake for {peer}");
#endif
        TcpClient peerConn = new();

        try
        {
            CancellationTokenSource connCts = new(TimeSpan.FromSeconds(10));

            await peerConn.ConnectAsync(peer.Ip.ToString(), peer.Port, connCts.Token);
            connCts.Dispose();
        }
        catch (SocketException)
        {
            peerConn.Dispose();
            Console.WriteLine($"{peer} refused the connection");
            return false;
        }
        catch (OperationCanceledException)
        {
            peerConn.Dispose();
            Console.WriteLine($"{peer} timed out connection");
            return false;
        }

        CancellationTokenSource ctsForIo = new(TimeSpan.FromSeconds(20));
        // we need to force it to close the stream, because ReadAsync and WriteAsync will
        // sometimes behave badly and not care about our cancellation causing a deadlock
        // read and write will fail because it's disposed
        
        ctsForIo.Token.Register(() =>
        {
            peerConn.Dispose();
            Console.WriteLine($"{peer} is TAKING TOO LONG (Forced close)");
        });
        
        try
        {
            var netStream = peerConn.GetStream();
            // the handshake must be 68 bytes long
            byte[] response = new Byte[68];
            
            await netStream.WriteAsync(new ReadOnlyMemory<byte>(m_handshake), ctsForIo.Token);
            await netStream.ReadExactlyAsync(response, 0, response.Length, ctsForIo.Token);

            ctsForIo.Dispose();
            await netStream.DisposeAsync();

            Console.WriteLine($"{peer} Response: {Convert.ToHexString(response)}");
        }
        catch (OperationCanceledException)
        {
            peerConn.Dispose();
            ctsForIo.Dispose();
            Console.WriteLine($"{peer} is TAKING TOO LONG");
            return false;
        }
        catch (EndOfStreamException)
        {
            peerConn.Dispose();
            ctsForIo.Dispose();
            Console.WriteLine($"{peer} sent an EOF (probably does not have this info hash)");
            return false;
        }
        catch (IOException)
        {
            peerConn.Dispose();
            ctsForIo.Dispose();
            Console.WriteLine($"{peer} refused the connection");
            return false;
        }

        peerConn.Dispose();

        return true;
    }
}