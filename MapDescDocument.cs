using System.ComponentModel;
using System.Globalization;
using System.Text;

namespace MapDescShow;

public sealed class MapDescEntry : INotifyPropertyChanged
{
    private string _mapName = "";
    private int _x;
    private int _y;
    private string _text = "";
    private string _colorCode = "$00F0FF";
    private int _mode = 1;
    private bool _enabled = true;

    public string MapName { get => _mapName; set { _mapName = value; Changed(); } }
    public int X { get => _x; set { _x = value; Changed(); } }
    public int Y { get => _y; set { _y = value; Changed(); } }
    public string Text { get => _text; set { _text = value; Changed(); } }
    public string ColorCode { get => _colorCode; set { _colorCode = NormalizeColor(value); Changed(); } }
    public int Mode { get => _mode; set { _mode = value == 0 ? 0 : 1; Changed(); } }
    public bool Enabled { get => _enabled; set { _enabled = value; Changed(); } }
    internal int SourceLine { get; init; } = -1;

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Changed([System.Runtime.CompilerServices.CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public Color ToColor()
    {
        string hex = NormalizeColor(ColorCode).TrimStart('$');
        if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int value))
            return Color.Cyan;
        return Color.FromArgb(value & 0xFF, (value >> 8) & 0xFF, (value >> 16) & 0xFF);
    }

    public static string FromColor(Color color) => $"${color.B:X2}{color.G:X2}{color.R:X2}";

    private static string NormalizeColor(string? value)
    {
        string hex = (value ?? "").Trim().TrimStart('$', '#');
        return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _)
            ? "$" + hex.ToUpperInvariant()
            : "$00F0FF";
    }

    public string Serialize() => $"{(Enabled ? "" : ";")}{MapName},{X},{Y},{Text},{ColorCode},{Mode}";
}

public sealed class MapDescDocument
{
    private sealed record SourceItem(string Raw, MapDescEntry? Entry);
    private readonly List<SourceItem> _sourceItems = [];
    public BindingList<MapDescEntry> Entries { get; } = [];
    public string? SourcePath { get; private set; }

    public static MapDescDocument Load(string path)
    {
        var document = new MapDescDocument { SourcePath = path };
        byte[] bytes;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                   FileShare.ReadWrite | FileShare.Delete))
        {
            bytes = new byte[stream.Length];
            stream.ReadExactly(bytes);
        }

        string text = Encoding.GetEncoding(936).GetString(bytes);
        string[] lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (TryParse(line, i, out var entry))
            {
                document.Entries.Add(entry!);
                document._sourceItems.Add(new SourceItem(line, entry));
            }
            else
            {
                document._sourceItems.Add(new SourceItem(line, null));
            }
        }
        return document;
    }

    public void Add(MapDescEntry entry)
    {
        Entries.Add(entry);
        _sourceItems.Add(new SourceItem("", entry));
    }

    public void Remove(MapDescEntry entry)
    {
        Entries.Remove(entry);
        _sourceItems.RemoveAll(x => ReferenceEquals(x.Entry, entry));
    }

    public void SaveAs(string path)
    {
        var lines = new List<string>(_sourceItems.Count);
        var known = new HashSet<MapDescEntry>(ReferenceEqualityComparer.Instance);
        foreach (var item in _sourceItems)
        {
            if (item.Entry is null)
                lines.Add(item.Raw);
            else if (Entries.Contains(item.Entry))
            {
                lines.Add(item.Entry.Serialize());
                known.Add(item.Entry);
            }
        }
        foreach (var entry in Entries)
            if (!known.Contains(entry))
                lines.Add(entry.Serialize());

        // 只允许“另存为”；调用方负责避免生产路径。
        File.WriteAllText(path, string.Join("\r\n", lines), Encoding.GetEncoding(936));
    }

    private static bool TryParse(string line, int lineNumber, out MapDescEntry? entry)
    {
        entry = null;
        string trimmed = line.Trim();
        if (trimmed.Length == 0) return false;
        bool enabled = !trimmed.StartsWith(';');
        if (!enabled) trimmed = trimmed[1..].TrimStart();
        string[] fields = trimmed.Split(',');
        if (fields.Length != 6 || !int.TryParse(fields[1].Trim(), out int x) ||
            !int.TryParse(fields[2].Trim(), out int y) || !int.TryParse(fields[5].Trim(), out int mode))
            return false;

        entry = new MapDescEntry
        {
            MapName = fields[0].Trim(), X = x, Y = y, Text = fields[3].Trim(),
            ColorCode = fields[4].Trim(), Mode = mode, Enabled = enabled, SourceLine = lineNumber
        };
        return true;
    }
}
