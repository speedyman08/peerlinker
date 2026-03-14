using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using libpeerlinker.Messages;

namespace libpeerlinker.Peers;

public class MessageReceiver(NetworkStream ns, Channel<Message> output)
{
    private readonly CancellationTokenSource _timerSource = new(TimeSpan.FromMinutes(2));

    public async Task<Message?> Recv()
    {
        try
        {
            var msgLenBytes = new byte[4];
            await ns.ReadExactlyAsync(msgLenBytes, 0, 4, _timerSource.Token);
            var msgLenAsInt = BinaryPrimitives.ReadInt32BigEndian(msgLenBytes);

            var fullMsg = new byte[msgLenAsInt + 4];
            msgLenBytes.CopyTo(fullMsg, 0);

            await ns.ReadExactlyAsync(fullMsg, 4, msgLenAsInt, _timerSource.Token);

            var msgObj = MessageFactory.MakeFromBytes(fullMsg);

            if (msgObj.Header.messageID == MessageType.KeepAlive)
            {
                _timerSource.TryReset();
            }
            
            return msgObj;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }
    }

    public async Task MessageLoop()
    {
        while (!_timerSource.IsCancellationRequested)
        {
            var msg = await Recv();
            if (msg is null)
            {
                break;
            }

            await output.Writer.WriteAsync(msg, _timerSource.Token);
        }

        output.Writer.Complete(); 
    }
}
