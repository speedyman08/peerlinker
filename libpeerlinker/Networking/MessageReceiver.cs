using System.Buffers.Binary;
using System.Net.Sockets;
using System.Threading.Channels;
using libpeerlinker.Messages;
using libpeerlinker.Utility;

namespace libpeerlinker.Peers;

public class MessageReceiver(NetworkStream ns, Channel<Message> output, Handshake handshake)
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
            _timerSource.TryReset();
            
            var msgObj = MessageFactory.MakeFromBytes(fullMsg);
            return msgObj;
        }
        catch (OperationCanceledException)
        {
            Logger.Instance.Debug("Message receive for {identifier} timed out with no keepalives sent", handshake);
            return null;
        }
        catch (EndOfStreamException)
        {
            Logger.Instance.Debug("Peer {identifier} disconnected :(", handshake);
            return null;
        }
        catch (IOException)
        {
            Logger.Instance.Debug("Stream closed for {identifier} (disposed locally)", handshake);
            return null;
        }
        catch(Exception e)
        {
            Logger.Instance.Debug("Unknown error occured while receiving message: {error}, type {type}", e.Message, e.GetType());
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

        Logger.Instance.Debug("Message pump completed for {identifier}", handshake);
        output.Writer.Complete(); 
    }
}
