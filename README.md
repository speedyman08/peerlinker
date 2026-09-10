PeerLinker is my attempt at a BitTorrent library. I work on it when I have free time / when i'm interested. hopefully that explains the 5 month gaps between a surge of commits. I just remember that this repository exists sometimes<br>
My goal is to have a class library of BitTorrent like the C++ library "libtorrent" but in C#, plus a desktop application using eto.forms <br>

At this point in time, peerlinker can download entire files. I tested it with a few linux distro ISOs. It maxed out my available bandwidth, downloading at an average of 25mb/s.

Around ~1500 loc

## Features
- Truly concurrent piece fetching with 30 active connections
- Peer discovery, handshake, etc.
- Support for all message types (the Peer Wire protocol)
- All messages are implemented as unsafe structs. This makes marshaling to and from byte arrays a trivial operation.
- Distribution of requests to peers depending on how useful they are to us, with a weighted random roll to pick them. obviously weighted towards the faster ones
- The architecture of the message recv system is sort of interesting. It's essentially handled with one channel. The incoming messages are dispatched
  to a set of 10 channels, one for each message type, so there's a dispatcher class that encompasses Piece messages, Choke messages, Interest messages etc. Using channels allows us to lazily process messages whenever we actually need to. A consumer could just await the next Unchoke, or the next Piece. it's more flexible
- Request pipelining. 32 requests at any given moment for every connection
- Choke handling. If a peer chokes us mid-transfer, its unfinished requests just go back into the pool and get redistributed to everyone else
- Blocks can arrive in any order from any peer. Everything is written at its exact offset into one preallocated scratch file, so there's no reassembly buffering in memory
- Single and multi-file torrents, split into the final file layout at the end
- .torrent file parsing to fetch metadata
- Bencode pretty printer
- Tracker server connectivity (Announce mechanism)
- BEP20 Azureus peer ID (-PL0001-)
- Dead peer detection
- Pretty logs
- SHA1 verification of pieces, with bad ones automatically re-downloaded
- Writing file to the disk

## Proof of work
https://github.com/user-attachments/assets/c302e353-bfc0-41a8-a179-e2c5c3b01f0c

## How to set it up
- Take a sample .torrent file under /testfiles
- Place it under the build output directory
  (so under peerlinker/bin/Release/net10.0/ for example)
- peerlinker/Program.cs has a variable that allows you to set the torrent file name that peerlinker is configured to use at runtime.

## Wishlist
- This is really more a downloader than a whole bittorrent client. Can't upload pieces yet
- Make it more user friendly. everything in the peerlinker exe could technically be handled in the libpeerlinker library, exposing only the absolutely necessary details.
