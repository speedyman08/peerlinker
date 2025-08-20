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
            Console.WriteLine($"{idx + 1}: {item}");
            idx++;

            PrintComplex(item, nesting);
        }
    }
}