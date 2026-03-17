namespace libpeerlinker.Utility;

/// <summary>
/// A read-only view over a byte array, treating it as a bitfield
/// where each bit represents whether a piece is available.
/// </summary>
public readonly struct BitField(byte[] data)
{
    public bool HasPiece(int index)
    {
        // byte that were in
        int byteIndex = index / 8;
        // the bit that we need counted from rightmost bit
        int bitOffset = 7 - (index % 8); 
        if (byteIndex < 0 || byteIndex >= data.Length)
            return false;
        return (data[byteIndex] & (1 << bitOffset)) != 0;
    }

    public int Length => data.Length * 8;

    public bool IsEmpty => data.Length == 0;
}