using libpeerlinker.Core;
using libpeerlinker.Messages;

namespace libpeerlinker.Exchange;

// A DTO that represents the results from one round of piece fulfillment 
public class PieceFulfillmentResult
{
    public List<Block> ReceivedBlocks { get; set; }
    public HashSet<Message> RemainingRequestsNotSent { get; set; }
    // we may have some request messages where no peer has this particular piece, checked by bitfield
    public List<Message>? RequestsNotInSwarm { get; set; }
}