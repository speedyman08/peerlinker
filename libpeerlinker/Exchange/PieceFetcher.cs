using System.Buffers.Binary;
using System.ComponentModel;
using System.Threading.Channels;
using libpeerlinker.Core;
using libpeerlinker.Messages;
using libpeerlinker.Peers;
using libpeerlinker.Tracking;
using libpeerlinker.Utility;
using Spectre.Console;

namespace libpeerlinker.Exchange;

/// <summary>
/// Main class for exchanging pieces
/// </summary>
public class PieceFetcher
{
    public int BlockLength { get; set; } = 16000;
    private readonly List<PeerConn> _reachablePeers;
    private readonly PeerFinder _finder;
    private readonly TorrentMetadata _meta;
    private BindingList<PeerConn> ActiveConnections { get; } = [];

    public PieceFetcher(List<PeerConn> reachable, TorrentMetadata meta, Handshake handshake)
    {
        _finder = new PeerFinder(handshake);
        _reachablePeers = reachable;
        _meta = meta;

        ActiveConnections.ListChanged += OnConnect;
    }

    async Task StartPopulatingConns()
    {
        if (ActiveConnections.Count != 0)
            return;

        _reachablePeers.ForEach(i => ActiveConnections.Add(i));
    }

    async void OnConnect(object? sender, ListChangedEventArgs e)
    {
        if (e.ListChangedType == ListChangedType.ItemAdded)
        {
            var handle = ActiveConnections[e.NewIndex];
            Logger.Instance.Information("Using peer connection {peer}", handle.Handshake);
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var res = await handle.Messages.BlockUntilRead(MessageType.Bitfield, cts.Token);
            if (res is null)
            {
                Logger.Instance.Fatal("Did not get bitfield from peer {peer}", handle.Handshake);
                KillPeer(handle);
                return;
            }

            // payload can't be null
            handle.BitField = new BitField(res.Payload!);
            Logger.Instance.Information("Got bitfield from peer {peer}", handle.Handshake);
            // send keepalive
            await handle.SendKeepAlive();
            Logger.Instance.Information("Sent keepalive to peer {peer}", handle.Handshake);
        }
    }

    async Task MainLoop()
    {
        var numPieces = _meta.PieceSha1Hashes.Length / 20;
        Logger.Instance.Information("File has {num}", numPieces);
        var pieceIndices = Enumerable.Range(0, numPieces).Shuffle().ToList();


        var blocks = await FullFillPieces(pieceIndices.Slice(0, 50));
        Logger.Instance.Information("Finis.");
    }

    private async Task<List<Block>> FullFillPieces(List<int> indices)
    {
        List<PeerConn> handles = ActiveConnections.Shuffle().ToList();
        List<Message> requestMessages = new();
        List<Block> blocksReceived = new();
        List<(PeerConn, Message)> pendingRequests = new();

        foreach (int i in indices)
        {
            // a piece is composed of blocks, which ones do we need exactly?
            var pieceLen = _meta.PieceLength;
            var lastBlockLen = pieceLen % BlockLength;
            var numBlocks = pieceLen / BlockLength;

            foreach (var blockIdx in Enumerable.Range(0, (int)numBlocks))
            {
                requestMessages.Add(MessageFactory.MakeRequest(i, blockIdx * BlockLength, BlockLength));
            }

            if (lastBlockLen != 0)
            {
                requestMessages.Add(MessageFactory.MakeRequest(i, (int)numBlocks * BlockLength, (int)lastBlockLen));
            }
        }

        requestMessages.ForEach(msg =>
        {
            var pieceIdx = BinaryPrimitives.ReadInt32BigEndian(msg.Payload.AsSpan(0, 4));

            foreach (var handle in handles)
            {
                if (!handle.BitField.HasPiece(pieceIdx))
                    continue;

                pendingRequests.Add((handle, msg));

                handles.Remove(handle);
                handles.Add(handle);

                break;
            }
        });

        // don't want to run too many BlockReq tasks as some tasks could be starved and waiting times for unchokes are depleted
        var concurrentPeerLimit = new SemaphoreSlim(5);
        var requestTasks = pendingRequests.GroupBy(p => p.Item1)
            .Select(async g =>
            {
                await concurrentPeerLimit.WaitAsync();
                try
                {
                    return await BlockReq(g.Key, g.Select(p => p.Item2).ToList());
                }
                finally
                {
                    concurrentPeerLimit.Release();
                }
                
            }).ToList();

        foreach (var task in requestTasks)
        {
            if (task.Result is not null)
            {
                blocksReceived.AddRange(task.Result);
            }
        }

        Logger.Instance.Information("Received {blocks} blocks out of {expected} needed", blocksReceived.Count,
            requestMessages.Count);

        return blocksReceived;
    }

    // false when we are not unchoked
    // true when we are
    private async Task<bool> TryUnchoke(PeerConn handle)
    {
        if (!handle.MeChoked) return true;

        handle.Messages.FlushChannel(MessageType
            .Unchoke); // we need a fresh unchoke message, older ones could be stale so we consume them
        
        var interestMsg = MessageFactory.MakeInterested();
        await handle.SendMessage(interestMsg);
        Logger.Instance.Information("Sent interested message to peer {peer}", handle.Handshake);

        var unchokeToken = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
        var unchokeMsg = await handle.Messages.BlockUntilRead(MessageType.Unchoke, unchokeToken);
        if (unchokeMsg is null)
        {
            Logger.Instance.Fatal("peer {peer} did not unchoke us in 10 seconds", handle.Handshake);
            return false;
        }

        return true;
    }

    private async Task<List<Block>?> BlockReq(PeerConn handle, List<Message> blockMsgs)
    {
        foreach (var msg in blockMsgs)
        {
            if (msg.GetMsgType() != MessageType.Request)
            {
                throw new ArgumentException("Message is not a request");
            }
        }

        if (handle.MeChoked)
        {
            var unchokeSuccess = await TryUnchoke(handle);
            if (!unchokeSuccess) return null;
        }

        // fire everything at once then look at our piece channel
        foreach (var msg in blockMsgs)
            await handle.SendMessage(msg);

        var blocks = new List<Block>();


        for (int i = 0; i < blockMsgs.Count; i++)
        {
            if (handle.MeChoked)
            {
                Logger.Instance.Debug("Peer {peer} choked us mid-transfer after {count}/{total} blocks",
                    handle.Handshake, i, blockMsgs.Count);
                break;
            }
            
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            var pieceMsg = await handle.Messages.BlockUntilRead(MessageType.Piece, cts.Token);
            if (pieceMsg is null)
            {
                Logger.Instance.Debug("Peer {peer} stopped responding after {count}/{total} blocks",
                    handle.Handshake, i, blockMsgs.Count);
                break;
            }

            blocks.Add(Block.FromPiece(pieceMsg));
        }

        return blocks;
    }


    void KillPeer(PeerConn conn)
    {
        ActiveConnections.Remove(conn);
        conn.Dispose();
    }

    public async Task Start()
    {
        await StartPopulatingConns();
        await MainLoop();
    }
}