using libpeerlinker.Core;
using libpeerlinker.Messages;

namespace libpeerlinker.Exchange;

public class PieceFullfilmentResult
{
    public List<Block> ReceivedBlocks { get; set; }
    public HashSet<Message> RemainingRequestsNotSent { get; set; }
    // we may have some request messages where no peer has this particular piece, checked by bitfield
    public List<Message>? RequestsNotInSwarm { get; set; }
}