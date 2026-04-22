using System.Collections;

namespace libpeerlinker.Utility;

/// <summary>
/// A read-only view over a byte array, treating it as a bitfield
/// where each bit represents whether a piece is available.
/// </summary>
public readonly struct BitField(byte[] byteArray) : IEnumerable<bool>
{
    public bool HasPiece(int index)
    {
        // byte that were in
        int byteIndex = index / 8;
        // the bit that we need counted from rightmost bit
        int bitOffset = 7 - (index % 8); 
        if (byteIndex < 0 || byteIndex >= byteArray.Length)
            return false;
        return (byteArray[byteIndex] & (1 << bitOffset)) != 0;
    }

    // Sets a piece as acquired, if it is already, nothing will change
    public void SetPiece(int index)
    {
        int byteIndex = index / 8;
        int bitOffset = 7 - (index % 8); 
        if (byteIndex < 0 || byteIndex >= byteArray.Length)
            throw new ArgumentOutOfRangeException(nameof(index), "Index out of range for bitfield");

        byteArray[byteIndex] |= (byte)(1 << bitOffset);
    }
    public int Length => byteArray.Length * 8;

    public bool IsEmpty => byteArray.Length == 0;
    
    public IEnumerator<bool> GetEnumerator()
    {
        for (int i = 0; i < byteArray.Length * 8; i++)
        {
            yield return HasPiece(i);
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}