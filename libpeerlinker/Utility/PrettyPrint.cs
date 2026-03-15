using System.Text;
using BencodeNET.Objects;

namespace libpeerlinker.Utility;

public class BencodePrettyPrinter
{
    private readonly StringBuilder _builder = new();
   
    public string StringRepresentation(IBObject obj)
    {
        PrintComplex(obj);
        return _builder.ToString();
    }
    
    private void TabNesting(int nesting)
    {
        foreach (var _ in Enumerable.Range(0, nesting - 1))
        {
            _builder.Append("\t");
        }
    }
    private void PrintComplex(IBObject obj, int nesting = 0)
    {
        if (obj is BDictionary b)
        {
            if (nesting < 50)
            {
                DebugDict(b, nesting + 1);
            }
            else
            {
                _builder.Append("...nesting limit\n");
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
                _builder.Append("...nesting limit\n");
            }
        }
}
    private void DebugDict(BDictionary benDict, int nesting = 0)
    {
        foreach (var key in benDict.Keys)
        {
            TabNesting(nesting);
            var keyVal = benDict[key];

            if (keyVal is BString s)
            {
                if (s.Length > 200)
                {
                    _builder.Append($"{key}: [{s.Length} characters]\n");
                    continue;
                }
            }
            _builder.Append($"{key}: {benDict[key]}\n");

            PrintComplex(benDict[key], nesting);
        }
    }

    private void DebugList(BList list, int nesting = 0)
    {
        var idx = 0;
        foreach (var item in list)
        {
            TabNesting(nesting);
            
            if (item is BString s)
            {
                if (s.Length > 200)
                {
                    _builder.Append($"{idx+1}: [{s.Length}] characters\n");
                    continue;
                }
            }
            if (idx > 300)
            {
                _builder.Append("[too many items]\n");
                break;
            }
            
            _builder.Append($"{idx + 1}: {item}\n");
            
            idx++;
            PrintComplex(item, nesting);
        }
    }
}