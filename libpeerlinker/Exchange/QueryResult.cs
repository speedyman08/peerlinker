using libpeerlinker.Core;
using libpeerlinker.Messages;

namespace libpeerlinker.Exchange;

public class QueryResult
{
    public required List<Block> ReceivedBlocks { get; init; }
    public required List<Message> RemainingMessages { get; init; }
}