using System.Runtime.InteropServices;
using System.Text;

namespace libpeerlinker.Packets;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct HandshakeMessage
{
    public byte identLen;
    public fixed byte protocolIdent[19];
    // 8 null bytes
    // later, this is treated like a byte field indicating features
    public fixed byte reservedBytes[8];
    public fixed byte infoDictHash[20];
    public fixed byte clientIdentifier[20];
    
    public HandshakeMessage(byte[] hash, string identifier)
    {
        identLen = 19;
        
        if (hash.Length != 20){
            throw new ArgumentOutOfRangeException("hash", "The info SHA1 is supposed to be 20 bytes long exactly.");
        } else if (identifier.Length != 20)
        {
            throw new ArgumentOutOfRangeException("identifier", "The client ID is supposed to be 20 bytes long exactly.");
        }

        var identifierBytes = Encoding.ASCII.GetBytes(identifier);

        for (int i = 0; i < 19; i++)
        {
            protocolIdent[i] = (byte)"BitTorrent protocol"[i];
        }
        
        for (int i = 0; i < 20; i++)
        {
            infoDictHash[i] = hash[i];
            clientIdentifier[i] = identifierBytes[i];
        }
    }
}