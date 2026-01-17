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
        catch (TrackerException)
        {
            Console.WriteLine("Did not get a successful announce");
        }
    }

    private async Task<bool> Handshake(PeerIpv4 peer)
    {
        using var peerConn = new TcpClient();

        try
        {
            using var connCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await peerConn.ConnectAsync(peer.Ip.ToString(), peer.Port, connCts.Token);

            using var ioCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            using var _ = ioCts.Token.Register(peerConn.Dispose);

            using var netStream = peerConn.GetStream();
            byte[] response = new byte[68];

            await netStream.WriteAsync(m_handshake, ioCts.Token);
            await netStream.ReadExactlyAsync(response, 0, response.Length, ioCts.Token);

            Console.WriteLine($"{peer} Response: {Convert.ToHexString(response)}");
            return true;
        }
        catch (SocketException)
        {
            Console.WriteLine($"{peer} refused the connection");
            return false;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"{peer} timed out / IO too long");
            return false;
        }
        catch (IOException)
        {
            Console.WriteLine($"{peer} IO error");
            return false;
        }
    }
}