using libpeerlinker.Exchange;
using libpeerlinker.Messages;
using libpeerlinker.Peers;
using libpeerlinker.Tracking;
using libpeerlinker.Utility; 

Logger.Instance.Verbose("A");
Logger.Instance.Debug("A");
Logger.Instance.Information("Parsing torrent file debian.torrent");
Logger.Instance.Warning("A");
Logger.Instance.Error("A");
Logger.Instance.Fatal("A");
TorrentMetadata meta = TorrentMetadata.FromFile("debian.torrent");

ReadOnlyMemory<byte> bytes = meta.GetPieceSha1(50);

var msgRawBytes = new byte[]
{
    0, 0, 0, 9, 4, 0, 0, 0, 100
};

var msg = MessageFactory.MakeFromBytes(msgRawBytes);

var tracker = new Tracker(meta, new Version(0, 0, 1));

var res = await tracker.Announce();

// we need an initial message
var handshake = new Handshake(meta, tracker.Identifier);

PeerFinder finder = new(handshake);

var discoveryResult = await finder.DiscoveryAsync(res.TrackerPeers);

var fetcher = new PieceFetcher(discoveryResult.RespondingPeers, handshake);

await fetcher.Start();

// using CancellationTokenSource cts = new(20000);
//
// try
// {
//     // temp tcpClient to listen on 6881
//     var server = new TcpListener(IPAddress.Any, 6881);
//     server.Start();
//     
//     var device = await OpenNat.Discoverer.DiscoverDeviceAsync(PortMapper.Upnp, cts.Token);
//     await device.CreatePortMapAsync(new Mapping(Protocol.Tcp, 6881, 6881, "PeerLinker temporary"));
// }
// catch (NatDeviceNotFoundException ex)
// {
//     Environment.Exit(1);
// }
//
// await Task.Delay(-1);



