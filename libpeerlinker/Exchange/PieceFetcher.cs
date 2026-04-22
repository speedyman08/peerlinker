using System.Buffers.Binary;
using System.ComponentModel;
using System.Net.NetworkInformation;
using libpeerlinker.Core;
using libpeerlinker.DiskHandling;
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
    // how many connections we will take from reachable peers.
    private const int MaxConnections = 30;
    private const int PieceAmount = 500;
    private readonly TimeSpan _maxBlockTime = TimeSpan.FromSeconds(5);
    private readonly int _piecesToFetch;
    private readonly int _blockLength = 16384;
    private readonly List<PeerConn> _reachablePeers;
    private readonly TorrentMetadata _meta;
    private BindingList<PeerConn> ActiveConnections { get; } = [];
    private BitField _ourBitField;

    public PieceFetcher(List<PeerConn> reachable, TorrentMetadata meta)
    {
        _reachablePeers = reachable;
        _meta = meta;
        _piecesToFetch = meta.PieceSha1Hashes.Length / 20;
        _ourBitField = new BitField(new byte[(int)Math.Ceiling((double)_piecesToFetch / 8)]);

        ActiveConnections.ListChanged += OnConnect;
    }

    async Task StartPopulatingConns()
    {
        if (ActiveConnections.Count != 0)
            return;

        _reachablePeers.ForEach(i => ActiveConnections.Add(i));
        ActiveConnections.Shuffle();
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

    List<PeerConn> GetMaxPeers()
    {
        return ActiveConnections
            .Take(MaxConnections)
            .ToList();
    }

    async Task MainLoop()
    {
        Logger.Instance.Information("File has {num} pieces.", _piecesToFetch);
        var pieceIndices = Enumerable.Range(0, _piecesToFetch).Shuffle().ToList();
        var blocksInPiece = (int)Math.Ceiling((double)_meta.PieceLength / _blockLength);

        var writer = new DiskWriter(_meta, blocksInPiece, _meta.PieceLength * _piecesToFetch,
            (int)_meta.PieceLength, _ourBitField);

        Logger.Instance.Information("Created a file called {name} and preallocated {size} bytes",
            DiskWriter.TempFileName, _meta.PieceLength * _piecesToFetch);
        Logger.Instance.Information("Starting to fetch pieces");

        for (int i = 0; i < _piecesToFetch; i += PieceAmount)
        {
            Logger.Instance.Information("Getting {pieceAmount} random pieces. Currently, {i}/{remaining}",
                PieceAmount, i, _piecesToFetch);

            var length = Math.Min(PieceAmount, _piecesToFetch - i);
            var blocks = await BlockFetchLoop(pieceIndices.GetRange(i, length), blocksInPiece);
            
            RecalculatePickChances(PieceAmount * blocksInPiece);
            
            // now write this to the disk
            await writer.WriteBlockChunk(blocks.ReceivedBlocks);
        }
        // check pieces

        var badPieceList = await writer.VerifyFile();

        Logger.Instance.Information("Verifying pieces downloaded");
        if (badPieceList.Count > 0)
        {
            Logger.Instance.Error("There are {badPieceList.Count} bad pieces. Redownloading them now.",
                badPieceList.Count);
            var reFetched = await BlockFetchLoop(badPieceList, blocksInPiece);
            await writer.WriteBlockChunk(reFetched.ReceivedBlocks);
        }
        else
        {
            Logger.Instance.Information("{pieces} pieces verified", _piecesToFetch);
        }

        Logger.Instance.Information("Download done. Splitting into files");
        await writer.SplitFile();
    }

    // this will zero out the blocksdownloaded, it's per loop iteration in mainloop
    private void RecalculatePickChances(int blocksDownloaded)
    {
        foreach (var conn in GetMaxPeers())
        {
            conn.CalculatePickChance(blocksDownloaded);
            
            conn.BlocksDownloaded = 0; 
        }
    }
    
    async Task<PieceFullfilmentResult> BlockFetchLoop(List<int> pieceIndices, int blocksInPiece)
    {
        // this is cancelled at any point we have no more peers to split to
        var cancellation = new CancellationTokenSource();
        
        var blocks = await FullFillPieces(pieceIndices, GetMaxPeers(), cancellation);
        
        while (blocks.RemainingRequestsNotSent.Count > 0 && !cancellation.IsCancellationRequested)
        {
            Logger.Instance.Information("Now downloading {nowRequests}/{needed}",
                blocks.RemainingRequestsNotSent.Count, blocksInPiece * PieceAmount);
            
            var remaining = await FullFillRequestMessages(blocks.RemainingRequestsNotSent, GetMaxPeers(), cancellation);
            blocks.ReceivedBlocks.AddRange(remaining.ReceivedBlocks);
        }

        if (cancellation.IsCancellationRequested)
        {
            Logger.Instance.Information("Cancelled fetch loop due to no more peers being available. We got choked too many times probably.");
        }

        return blocks;
    }

    private HashSet<Message> RequestMessagesForPieces(List<int> pieceIndices)
    {
        HashSet<Message> requestMessages = new();

        foreach (int i in pieceIndices)
        {
            // a piece is composed of blocks, which ones do we need exactly?
            var pieceLen = _meta.PieceLength;
            var lastBlockLen = pieceLen % _blockLength;
            var numBlocks = pieceLen / _blockLength;

            foreach (var blockIdx in Enumerable.Range(0, (int)numBlocks))
            {
                requestMessages.Add(MessageFactory.MakeRequest(i, blockIdx * _blockLength, _blockLength));
            }

            if (lastBlockLen != 0)
            {
                requestMessages.Add(MessageFactory.MakeRequest(i, (int)numBlocks * _blockLength,
                    (int)lastBlockLen));
            }
        }

        return requestMessages;
    }

    private bool BlockIsForRequest(Block block, Message request)
    {
        return block.PieceIdx == BinaryPrimitives.ReadInt32BigEndian(request.Payload.AsSpan(0, 4))
               && block.BlockOffset == BinaryPrimitives.ReadInt32BigEndian(request.Payload.AsSpan(4, 4));
    }

    async Task<QueryResult> ProcessRequests(IGrouping<PeerConn, (PeerConn, Message)> requestGroup)
    {
        var cts = new CancellationTokenSource(_maxBlockTime);

        var msgs = requestGroup.Select(p => p.Item2).ToList();
        var received = await BlockReq(requestGroup.Key, msgs, cts.Token);
        if (received is null)
            return new QueryResult
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

    private async Task<PieceFullfilmentResult> FullFillPieces(List<int> indices, List<PeerConn> handles, CancellationTokenSource outCts)
    {
        var requestMessages = RequestMessagesForPieces(indices);
        return await FullFillRequestMessages(requestMessages, handles, outCts);
    }

    private async Task<PieceFullfilmentResult> FullFillRequestMessages(HashSet<Message> requestMessages,
        List<PeerConn> handles, CancellationTokenSource outCts)
    {
        List<Message> requestsWithNoFoundPiece = new();
        List<Block> blocksReceived = new();

        List<(PeerConn, Message)> pendingRequestsToSend = new();

        foreach (var msg in requestMessages)
        {
            var pieceIdx = BinaryPrimitives.ReadInt32BigEndian(msg.Payload.AsSpan(0, 4));
            
            // no more peers to split to, we need to abort
            if (handles.Count == 0)
            {
                await outCts.CancelAsync();
                return new PieceFullfilmentResult
                {
                    ReceivedBlocks = blocksReceived,
                    RemainingRequestsNotSent = requestMessages,
                    RequestsNotInSwarm = requestsWithNoFoundPiece
                };
            }
            // filter to peers who have this piece
            var eligible = handles.Where(h => h.BitField.HasPiece(pieceIdx)).ToList();
    
            if (eligible.Count == 0)
            {
                Logger.Instance.Warning($"No one has the piece {pieceIdx}");
                requestsWithNoFoundPiece.Add(msg);
                continue;
            }

            var totalWeight = eligible.Sum(p => p.PickChance);
            var roll = Random.Shared.NextDouble() * totalWeight;
    
            var cumulative = 0.0;
            PeerConn? chosen = null;
            foreach (var peer in eligible)
            {
                cumulative += peer.PickChance;
                if (roll <= cumulative)
                {
                    chosen = peer;
                    break;
                }
            }
    
            chosen ??= eligible.Last(); // fallback
            pendingRequestsToSend.Add((chosen, msg));
        }

        var results = pendingRequestsToSend.GroupBy(p => p.Item1)
            .Select(ProcessRequests).ToList();

        await Task.WhenAll(results);

        foreach (var taskResult in results)
        {
            blocksReceived.AddRange(taskResult.Result.ReceivedBlocks);
            // remove all messages that were responded to
            requestMessages.RemoveWhere(msg =>
                taskResult.Result.ReceivedBlocks.Any(block => BlockIsForRequest(block, msg)));
        }

        Logger.Instance.Information("Received {blocks} blocks out of {expected} needed", blocksReceived.Count,
            pendingRequestsToSend.Count);
        Logger.Instance.Information("Remaining messages: {remaining}", requestMessages.Count);

        return new PieceFullfilmentResult
        {
            ReceivedBlocks = blocksReceived,
            RemainingRequestsNotSent = requestMessages,
            RequestsNotInSwarm = requestsWithNoFoundPiece.Count == 0 ? null : requestsWithNoFoundPiece
        };
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

        CancellationToken ct = new CancellationTokenSource(TimeSpan.FromSeconds(5)).Token;
        var unchokeMsg = await handle.Messages.BlockUntilRead(MessageType.Unchoke, ct);

        if (unchokeMsg is null)
        {
            Logger.Instance.Fatal("peer {peer} did not unchoke us in 5 seconds", handle.Handshake);
            KillPeer(handle);
            return false;
        }

        return true;
    }

    // Dispatch out the assigned messageds for a peer connections within an interval defined in CT. this method also increments
    // BlocksDownloaded so you can, after the interval has passed, rate peers and priorities some over others.
    // i.e, assign more pieces to the good ones and lesser or even choke bad ones
    // the goal is for this method to complete before CT even expires
    private async Task<List<Block>?> BlockReq(PeerConn handle, List<Message> requestMsgs, CancellationToken ct)
    {
        List<Block> received = new();

        foreach (var msg in requestMsgs)
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

        // Flush any channels we use to remove stale shit
        handle.Messages.FlushChannel(MessageType.Choke);
        handle.Messages.FlushChannel(MessageType.Piece);

        var inPipeline = 0;
        var lastSentIdx = 0;
        // we always want to keep at least 32 requests in there at all times, this can increase throughput
        while (inPipeline < 32)
        {
            if (lastSentIdx >= requestMsgs.Count) break;
            await handle.SendMessage(requestMsgs[lastSentIdx]);
            lastSentIdx++;
            inPipeline++;
        }

        while (inPipeline > 0 && !ct.IsCancellationRequested)
        {
            var pieceChokeCts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

            var pieceTask = handle.Messages.BlockUntilRead(MessageType.Piece, pieceChokeCts.Token);
            var chokeTask = handle.Messages.BlockUntilRead(MessageType.Choke, pieceChokeCts.Token);

            var receive = await Task.WhenAny(chokeTask, pieceTask);
            if (receive == chokeTask)
            {
                Logger.Instance.Information("Peer {peer} choked us after {count}/{total} blocks",
                    handle.Handshake, received.Count, requestMsgs.Count);
                pieceChokeCts.Cancel();
                KillPeer(handle);
                return null;
            }

            pieceChokeCts.Cancel();

            var pieceMsg = await pieceTask;

            if (pieceMsg is null)
            {
                Logger.Instance.Information("Peer {peer} stopped responding after {count}/{total} blocks",
                    handle.Handshake, received.Count, requestMsgs.Count);
                break;
            }

            inPipeline--;
            received.Add(Block.FromPiece(pieceMsg));
            handle.BlocksDownloaded++;

            if (lastSentIdx < requestMsgs.Count && !ct.IsCancellationRequested)
            {
                await handle.SendMessage(requestMsgs[lastSentIdx++]);
                inPipeline++;
            }
        }

        Logger.Instance.Information("Peer {peer} gave us {received}/{needed} within time limit",
            handle.Handshake, received.Count, requestMsgs.Count);

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