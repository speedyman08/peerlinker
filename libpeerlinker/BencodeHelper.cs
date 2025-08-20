using BencodeNET.Exceptions;
using BencodeNET.Objects;
using libpeerlinker.FileHandling;

namespace libpeerlinker;

public class BencodeHelper
{
    public static T GetKeyExcept<T>(BDictionary benDict, string key)
    {
        var val = GetKey<T>(benDict, key) ?? throw new BencodeException($"key {key} does not exist in the dictionary");
        return val;
    }
    
    
    // returns null instead of throwing on non existent keys
    public static T? GetKey<T>(BDictionary benDict, string key)
    {
        benDict.TryGetValue(key, out var value);
        return (T) value;
    }
}