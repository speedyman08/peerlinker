using libpeerlinker.Core;
using libpeerlinker.Messages;

namespace libpeerlinker.Exchange;

/// <summary>
/// A DTO that represents the results after sending a group of requests to a peer.
/// "RemainingMessages" stores the Request messages not fulfilled due to timeout or other reasons.
/// </summary>
public class QueryResult
{
    public required List<Block> ReceivedBlocks { get; init; }
    public required List<Message> RemainingMessages { get; init; }
}