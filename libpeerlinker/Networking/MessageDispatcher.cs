using System.Threading.Channels;
using libpeerlinker.Messages;
using libpeerlinker.Utility;

namespace libpeerlinker.Peers;

public class MessageDispatcher(Channel<Message> input, Handshake handshake)
{
    // specific channels for each message type
    public Channel<Message> PieceMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> ChokeMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> UnchokeMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> InterestedMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> NotInterestedMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> HaveMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> BitfieldMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> RequestMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> CancelMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> PortMessages { get; } = Channel.CreateUnbounded<Message>();
    public Channel<Message> KeepAliveMessages { get; } = Channel.CreateUnbounded<Message>();


    // what we execute in peerconn in the case we're choked 
    public Action? OnChoke { get; set; }
    public Action? OnUnchoke { get; set; }

    private Channel<Message>? FetchChannel(MessageType type)
    {
        return type switch
        {
            MessageType.Piece => PieceMessages,
            MessageType.Choke => ChokeMessages,
            MessageType.Unchoke => UnchokeMessages,
            MessageType.Interested => InterestedMessages,
            MessageType.NotInterested => NotInterestedMessages,
            MessageType.Have => HaveMessages,
            MessageType.Bitfield => BitfieldMessages,
            MessageType.Request => RequestMessages,
            MessageType.Cancel => CancelMessages,
            MessageType.Port => PortMessages,
            MessageType.KeepAlive => KeepAliveMessages,
            _ => null
        };
    }

    public async Task RunAsync()
    {
        await foreach (var msg in input.Reader.ReadAllAsync())
        {
            Logger.Instance.Verbose("Received {type} from {handshake}", msg.GetMsgType(), handshake);
            var target = FetchChannel(msg.GetMsgType());

            if (msg.GetMsgType() == MessageType.Choke)
            {
                OnChoke?.Invoke();
            }
            else if (msg.GetMsgType() == MessageType.Unchoke)
            {
                OnUnchoke?.Invoke();
            }

            if (target is not null)
                await target.Writer.WriteAsync(msg);
        }

        Logger.Instance.Debug("Message channel closed for {peer}", handshake);

        PieceMessages.Writer.Complete();
        ChokeMessages.Writer.Complete();
        UnchokeMessages.Writer.Complete();
        InterestedMessages.Writer.Complete();
        NotInterestedMessages.Writer.Complete();
        HaveMessages.Writer.Complete();
        BitfieldMessages.Writer.Complete();
        RequestMessages.Writer.Complete();
        CancelMessages.Writer.Complete();
        PortMessages.Writer.Complete();
        KeepAliveMessages.Writer.Complete();
    }

    public async Task<Message?> BlockUntilRead(MessageType type, CancellationToken token = default)
    {
        try
        {
            var channel = FetchChannel(type);
            if (channel is not null)
                return await channel.Reader.ReadAsync(token);
        }
        catch (OperationCanceledException)
        {
            Logger.Instance.Verbose("Token expired before {type} read", type);
            return null;
        }
        catch (ChannelClosedException)
        {
            Logger.Instance.Verbose(
                "Channel is closed before {type} read (so we received a null recv in MessageReceiver)", type);
            return null;
        }

        return null;
    }
}