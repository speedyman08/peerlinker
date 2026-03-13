using System.Threading.Channels;
using libpeerlinker.Messages;

namespace libpeerlinker.Peers;

public class MessageDispatcher(Channel<Message> input)
{
    // specific channels for each message type
    public Channel<Message> PieceMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> ChokeMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> UnchokeMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> HaveMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> BitfieldMessages { get; } = Channel.CreateUnbounded<Message>();

    public async Task RunAsync(CancellationToken ct = default)
    {
        await foreach (var msg in input.Reader.ReadAllAsync(ct))
        {
            var target = msg.Header.messageID switch
            {
                MessageType.Piece   => PieceMessages,
                MessageType.Choke   => ChokeMessages,
                MessageType.Unchoke => UnchokeMessages,
                MessageType.Have    => HaveMessages,
                MessageType.Bitfield => BitfieldMessages,
                _ => null
            };

            if (target is not null)
                await target.Writer.WriteAsync(msg, ct);
        }

        PieceMessages.Writer.Complete();
        ChokeMessages.Writer.Complete();
        UnchokeMessages.Writer.Complete();
        HaveMessages.Writer.Complete();
        BitfieldMessages.Writer.Complete();
    }
}