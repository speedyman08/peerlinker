using libpeerlinker.FileHandling;
using libpeerlinker.Packets;
using libpeerlinker.Peers;
using libpeerlinker.Tracking;

TorrentMetadata meta = TorrentMetadata.FromFile("fedora.torrent");

Console.WriteLine($"Tracker {meta.TrackerURL}");

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




