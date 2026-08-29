using System.Text;
using System.Text.RegularExpressions;

namespace MapDescShow;

public sealed record MapInfoEntry(string MapFileId, string MapName, int LineNumber, string RawLine,
    string MapAlias = "")
{
    public string MapFileName => MapFileId.EndsWith(".map", StringComparison.OrdinalIgnoreCase)
        ? MapFileId : MapFileId + ".map";
    public override string ToString() => MapAlias.Length == 0
        ? $"{MapName}   →   {MapFileName}"
        : $"{MapName}   [{MapAlias}]   →   {MapFileName}";
}

public static partial class MapInfoDocument
{
    [GeneratedRegex(@"^\s*\[\s*([^\]\s]+)\s+([^\]]+?)\s*\]", RegexOptions.Compiled)]
    private static partial Regex EntryRegex();

    public static List<MapInfoEntry> Load(string path)
    {
        byte[] bytes;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }

        string text = Encoding.GetEncoding(936).GetString(bytes);
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var result = new List<MapInfoEntry>();
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line.TrimStart().StartsWith(';')) continue;
            Match match = EntryRegex().Match(line);
            if (!match.Success) continue;
            string mapToken = match.Groups[1].Value.Trim();
            string mapName = match.Groups[2].Value.Trim();
            string mapAlias = "";
            string mapFile = mapToken;
            int separator = mapToken.LastIndexOf('|');
            if (separator >= 0)
            {
                mapAlias = mapToken[..separator].Trim();
                mapFile = mapToken[(separator + 1)..].Trim();
            }
            if (mapFile.Length > 0 && mapName.Length > 0)
                result.Add(new MapInfoEntry(mapFile, mapName, i + 1, line, mapAlias));
        }
        return result;
    }
}
