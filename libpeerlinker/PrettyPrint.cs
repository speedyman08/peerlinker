using BencodeNET.Objects;

namespace libpeerlinker;

public class PrettyPrint
{
    private static void TabNesting(int nesting)
    {
        foreach (var _ in Enumerable.Range(0, nesting))
        {
            Console.Write("\t");
        }
    }
    private static void PrintComplex(IBObject obj, int nesting = 0)
    {
        if (obj is BDictionary b)
        {
            if (nesting < 50)
            {
                DebugDict(b, nesting + 1);
            }
            else
            {
                Console.WriteLine("...nesting limit");
            }
        }
        else if (obj is BList ls)
        {
            if (nesting < 50)
            {
                DebugList(ls, nesting + 1);
            }
            else
            {
                Console.WriteLine("...nesting limit");
            }
        }
}
    public static void DebugDict(BDictionary benDict, int nesting = 0)
    {
        foreach (var key in benDict.Keys)
        {
            TabNesting(nesting);
            var keyVal = benDict[key];

            if (keyVal is BString s)
            {
                if (s.Length > 200)
                {
                    Console.WriteLine($"{key}: [too long]");
                    continue;
                }
            }
            Console.WriteLine($"{key}: {benDict[key]}");

            PrintComplex(benDict[key], nesting);
        }
    }

    public static void DebugList(BList list, int nesting = 0)
    {
        var idx = 0;
        foreach (var item in list)
        {
            TabNesting(nesting);
            
            if (item is BString s)
            {
                if (s.Length > 200)
                {
                    Console.WriteLine($"{idx+1}: too long");
                    continue;
                }
            }
            if (idx > 300)
            {
                Console.WriteLine("[too many items]");
                break;
            }
            
            Console.WriteLine($"{idx + 1}: {item}");
            
            idx++;
            PrintComplex(item, nesting);
        }
    }
}