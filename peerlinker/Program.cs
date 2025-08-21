using libpeerlinker.FileHandling;
using libpeerlinker.Peers;
using libpeerlinker.Tracking;

TorrentMetadata meta = TorrentMetadata.FromFile("fedora.torrent");

Console.WriteLine($"Tracker {meta.TrackerURL}");

// var pickedFiles = meta.AllFiles.Where(file => file.SuggestedFilename == "Fedora-KDE-Live-x86_64-40-1.14.iso")
//     .ToArray();

var tracker = new Tracker(meta, new Version(0,0,1))
{
    Debug = true
};

// start doing handshakes for now

var manager = new PeerManager(tracker, meta)
{
    SaveDirectory = Directory.GetCurrentDirectory(),
};

manager.StartDiscovery();

await Task.Delay(-1);