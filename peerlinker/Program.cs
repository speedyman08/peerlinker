using libpeerlinker.FileHandling;
using libpeerlinker.Tracker;

TorrentMetadata meta = TorrentMetadata.FromFile("fedora.torrent");

Console.WriteLine($"Tracker {meta.TrackerURL}");

var tracker = new Tracker(meta, new Version(0,0,1))
{
    Debug = true
};

await tracker.Announce();