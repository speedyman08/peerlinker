using libpeerlinker.Messages;
using libpeerlinker.Peers;
using libpeerlinker.Tracking;
using libpeerlinker.Messages;

TorrentMetadata meta = TorrentMetadata.FromFile("debian.torrent");

ReadOnlyMemory<byte> bytes = meta.GetPieceSha1(50);

var msgRawBytes = new byte[]
{
    0, 0, 0, 9, 4, 0, 0, 0, 100
};

var msg = MessageFactory.MakeFromBytes(msgRawBytes);


Console.WriteLine($"Tracker {meta.TrackerUrl}");

var tracker = new TrackerHandle(meta, new Version(0, 0, 1));

var res = await tracker.Announce();

// we need an initial message
var handshake = new Handshake(meta, tracker.Identifier);

PeerFinder finder = new();

var discoveryResult = await finder.DiscoveryAsync(res.TrackerPeers, handshake);

if (discoveryResult.Status == DiscoveryStatus.NoPeers)
{
    Console.WriteLine("No peers found. Are we cooked?");
}

discoveryResult.RespondingPeers.ForEach(Console.WriteLine);

// using CancellationTokenSource cts = new(20000);
//
// try
// {
//     // temp tcpClient to listen on 6881
//     var server = new TcpListener(IPAddress.Any, 6881);
//     server.Start();
//     Console.WriteLine("Listening on 6881");
//     
//     var device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts.Token);
//     await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 6881, 6881, "PeerLinker temporary"));
//     Console.WriteLine($"6881 mapping opened with device {device.HostEndPoint}");
// }
// catch (NatDeviceNotFoundException ex)
// {
//     Console.WriteLine("Port forwarding failed. This is required for BitTorrent");
//     Console.WriteLine($"Reason: {ex.Message}");
//     Environment.Exit(1);
// }
//
// await Task.Delay(-1);




