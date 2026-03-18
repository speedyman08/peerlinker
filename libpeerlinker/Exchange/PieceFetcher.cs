using System.Buffers.Binary;
using System.ComponentModel;
using libpeerlinker.Core;
using libpeerlinker.Messages;
using libpeerlinker.Peers;
using libpeerlinker.Tracking;
using libpeerlinker.Utility;

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

        var blocks = await FullFillPieces(pieceIndices.Slice(0, 1500));
        Logger.Instance.Information("Finis.");
    }

    private List<Message> RequestMessagesForPieces(List<int> pieceIndices)
    {
        List<Message> requestMessages = new();
        
        foreach (int i in pieceIndices)
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

        return requestMessages;
    }

    private bool BlockIsForRequest(Block block, Message request)
    {
        return block.PieceIdx == BinaryPrimitives.ReadInt32BigEndian(request.Payload.AsSpan(0, 4))
               && block.BlockOffset == BinaryPrimitives.ReadInt32BigEndian(request.Payload.AsSpan(4, 4));
    }

    async Task<QueryResult> ProcessRequest(IGrouping<PeerConn, (PeerConn, Message)> requestGroup, SemaphoreSlim tasksRunning)
    {
        // everyone gets like 20 seconds to give us their pieces
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await tasksRunning.WaitAsync();
        try
        {
            var msgs = requestGroup.Select(p => p.Item2).ToList();
            var received = await BlockReq(requestGroup.Key, msgs, cts.Token);
            if (received is null) return new QueryResult
            {
                ReceivedBlocks = new List<Block>(),
                RemainingMessages = msgs,
            };
            var remaining = msgs.Where(req => !received.Any(block => BlockIsForRequest(block, req))).ToList();
            
            return new QueryResult
            {
                ReceivedBlocks = received,
                RemainingMessages = remaining,
            };
        }
        finally
        {
            tasksRunning.Release();
        } 
    }
    
    private async Task<List<Block>> FullFillPieces(List<int> indices)
    {
        List<PeerConn> handles = ActiveConnections.Shuffle().ToList();
        List<Message> requestMessages = RequestMessagesForPieces(indices);
        HashSet<Message> assigned = new();
        List<Block> blocksReceived = new();
        List<(PeerConn, Message)> pendingRequests = new();


        // assign requests to peers
        requestMessages.ForEach(msg =>
        {
            var pieceIdx = BinaryPrimitives.ReadInt32BigEndian(msg.Payload.AsSpan(0, 4));

            foreach (var handle in handles)
            {
                if (!handle.BitField.HasPiece(pieceIdx))
                    continue;

                pendingRequests.Add((handle, msg));
                assigned.Add(msg);
                handles.Remove(handle);
                handles.Add(handle);

                break;
            }
        });

        // don't want to run too many BlockReq tasks as some tasks could be starved and waiting times for unchokes are depleted
        var tasksRunning = new SemaphoreSlim(30);
        var results = pendingRequests.GroupBy(p => p.Item1)
            .Select(g => ProcessRequest(g, tasksRunning)).ToList();

        await Task.WhenAll(results);
        
        foreach (var taskResult in results)
        {
            blocksReceived.AddRange(taskResult.Result.ReceivedBlocks);
            // remove all messages that were responded to
            assigned.RemoveWhere(msg => taskResult.Result.ReceivedBlocks.Any(block => BlockIsForRequest(block, msg)));
        }
        
        Logger.Instance.Information("Received {blocks} blocks out of {expected} needed", blocksReceived.Count,
            requestMessages.Count);
        Logger.Instance.Information("Remaining messages: {remaining}", assigned.Count);
        Logger.Instance.Information("At this point i would retry fullfill pieces and consider these messages in the next round");
        // Logger.Instance.Information("{num} messages remaining to sent, they were not responded to", requestMessages.Count);

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

        var unchokeToken = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;
        var unchokeMsg = await handle.Messages.BlockUntilRead(MessageType.Unchoke, unchokeToken);
        if (unchokeMsg is null)
        {
            Logger.Instance.Fatal("peer {peer} did not unchoke us in 20 seconds", handle.Handshake);
            return false;
        }

        return true;
    }


    private async Task<List<Block>?> BlockReq(PeerConn handle, List<Message> blockMsgs, CancellationToken ct)
    {
        List<Block> received = new();
        
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

        var inPipeline = 0;
        var lastSentIdx = 0;
        // initially saturate messages
        while (inPipeline < 5)
        {
            if (lastSentIdx >= blockMsgs.Count) break;
            await handle.SendMessage(blockMsgs[lastSentIdx]);
            lastSentIdx++;
            inPipeline++;
        }


        while (inPipeline > 0 && !ct.IsCancellationRequested)
        {
            if (handle.MeChoked)
            {
                Logger.Instance.Debug("Peer {peer} choked us mid-transfer after {count}/{total} blocks",
                    handle.Handshake, received.Count, blockMsgs.Count);
                break;
            }   
            var token = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
            var pieceMsg = await handle.Messages.BlockUntilRead(MessageType.Piece, token);
            if (pieceMsg is null)
            {
                Logger.Instance.Debug("Peer {peer} stopped responding after {count}/{total} blocks",
                    handle.Handshake, received.Count, blockMsgs.Count);
                break;
            }

            inPipeline--;
            received.Add(Block.FromPiece(pieceMsg));
            handle.BlocksDownloaded++;

            if (lastSentIdx < blockMsgs.Count && !ct.IsCancellationRequested)
            {
                await handle.SendMessage(blockMsgs[lastSentIdx++]);
                inPipeline++;
            }
        }
        
        Logger.Instance.Information("Peer {peer} gave us {received}/{needed} within time limit of 20 sec", handle.Handshake, received.Count, blockMsgs.Count);
        
        return received;
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