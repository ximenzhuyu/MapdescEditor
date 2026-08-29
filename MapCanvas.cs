using System.Drawing.Drawing2D;
using System.ComponentModel;

namespace MapDescShow;

public sealed class MapCanvas : Control
{
    // 传奇地图单元为 48×32；小地图以 32×32 为基准投影。
    private const float MiniMapScaleX = 48f / 32f;
    private const float MiniMapScaleY = 32f / 32f;
    private MapPreview? _preview;
    private float _zoom = 1f;
    private PointF _pan;
    private Point _dragStart;
    private PointF _panStart;
    private bool _dragging;
    private bool _dragMoved;
    private Point? _selectedCell;
    private IReadOnlyList<MapDescEntry> _entries = [];

    public event Action<int, int>? CellClicked;
    public event Action<int, int>? CellContextRequested;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public string MapKey { get; set; } = "";
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowLargeMode { get; set; } = true;
    [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool ShowMiniMode { get; set; } = true;
    public float Zoom => _zoom;

    public MapCanvas()
    {
        DoubleBuffered = true;
        TabStop = true;
        BackColor = Color.FromArgb(24, 24, 24);
        SetStyle(ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
    }

    public void SetPreview(MapPreview? preview)
    {
        _preview?.Bitmap.Dispose();
        _preview = preview;
        _selectedCell = null;
        FitToWindow();
    }

    public void SetEntries(IReadOnlyList<MapDescEntry> entries)
    {
        _entries = entries;
        Invalidate();
    }

    public void SelectCell(int x, int y)
    {
        _selectedCell = new Point(x, y);
        Invalidate();
    }

    public void FitToWindow()
    {
        if (_preview is null || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        float displayWidth = _preview.Width * MiniMapScaleX;
        float displayHeight = _preview.Height * MiniMapScaleY;
        _zoom = Math.Max(0.05f, Math.Min((ClientSize.Width - 30f) / displayWidth,
            (ClientSize.Height - 30f) / displayHeight));
        _pan = new PointF((ClientSize.Width - displayWidth * _zoom) / 2f,
            (ClientSize.Height - displayHeight * _zoom) / 2f);
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_preview is null)
        {
            TextRenderer.DrawText(e.Graphics, "请选择客户端 Map 目录中的 .map 文件",
                Font, ClientRectangle, Color.Gainsboro,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            return;
        }

        var g = e.Graphics;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.DrawImage(_preview.Bitmap, new RectangleF(_pan.X, _pan.Y,
            _preview.Width * MiniMapScaleX * _zoom,
            _preview.Height * MiniMapScaleY * _zoom));

        foreach (var entry in _entries)
        {
            if (!entry.Enabled || (entry.Mode == 0 && !ShowLargeMode) ||
                (entry.Mode == 1 && !ShowMiniMode) ||
                !string.Equals(entry.MapName, MapKey, StringComparison.OrdinalIgnoreCase)) continue;
            DrawEntry(g, entry);
        }

        if (_selectedCell is Point p)
        {
            float x = MapToScreenX(p.X);
            float y = MapToScreenY(p.Y);
            using var outline = new Pen(Color.Black, 4);
            using var pen = new Pen(Color.Red, 1);
            g.DrawLine(outline, x - 10, y, x + 10, y);
            g.DrawLine(outline, x, y - 10, x, y + 10);
            g.DrawLine(pen, x - 10, y, x + 10, y);
            g.DrawLine(pen, x, y - 10, x, y + 10);
        }
    }

    private void DrawEntry(Graphics g, MapDescEntry entry)
    {
        float x = MapToScreenX(entry.X);
        float y = MapToScreenY(entry.Y);
        Color color = entry.ToColor();
        using var font = new Font(Font.FontFamily, 9, FontStyle.Bold);
        Point point = GetCenteredTextOrigin(g, entry.Text, font, x, y);
        TextRenderer.DrawText(g, entry.Text, font, new Point(point.X + 1, point.Y + 1), Color.Black,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        TextRenderer.DrawText(g, entry.Text, font, point, color,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
    }

    private static Point GetCenteredTextOrigin(Graphics graphics, string text, Font font,
        float centerX, float centerY)
    {
        Size textSize = TextRenderer.MeasureText(graphics, text, font, Size.Empty,
            TextFormatFlags.NoPadding | TextFormatFlags.NoPrefix);
        return new Point(
            (int)Math.Round(centerX - textSize.Width / 2f),
            (int)Math.Round(centerY - textSize.Height / 2f));
    }

    private float MapToScreenX(float mapX) => _pan.X + mapX * MiniMapScaleX * _zoom;
    private float MapToScreenY(float mapY) => _pan.Y + mapY * MiniMapScaleY * _zoom;

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        if (_preview is null) return;
        float oldZoom = _zoom;
        float factor = e.Delta > 0 ? 1.25f : .8f;
        _zoom = Math.Clamp(_zoom * factor, .05f, 40f);
        float mapX = (e.X - _pan.X) / oldZoom;
        float mapY = (e.Y - _pan.Y) / oldZoom;
        _pan = new PointF(e.X - mapX * _zoom, e.Y - mapY * _zoom);
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        Focus();
        if (e.Button is MouseButtons.Middle or MouseButtons.Right)
        {
            _dragging = true;
            _dragMoved = false;
            _dragStart = e.Location;
            _panStart = _pan;
            Cursor = Cursors.Hand;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        if (_dragging)
        {
            if (Math.Abs(e.X - _dragStart.X) > 2 || Math.Abs(e.Y - _dragStart.Y) > 2)
                _dragMoved = true;
            _pan = new PointF(_panStart.X + e.X - _dragStart.X, _panStart.Y + e.Y - _dragStart.Y);
            Invalidate();
        }
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        if (_dragging)
        {
            bool showContext = e.Button == MouseButtons.Right && !_dragMoved;
            _dragging = false;
            Cursor = Cursors.Default;
            if (showContext && TryGetCell(e.Location, out int contextX, out int contextY))
            {
                SelectCell(contextX, contextY);
                CellClicked?.Invoke(contextX, contextY);
                CellContextRequested?.Invoke(contextX, contextY);
            }
            return;
        }
        if (e.Button != MouseButtons.Left || _preview is null) return;
        if (!TryGetCell(e.Location, out int x, out int y)) return;
        SelectCell(x, y);
        CellClicked?.Invoke(x, y);
    }

    private bool TryGetCell(Point location, out int x, out int y)
    {
        x = y = -1;
        if (_preview is null) return false;
        x = (int)Math.Floor((location.X - _pan.X) / (_zoom * MiniMapScaleX));
        y = (int)Math.Floor((location.Y - _pan.Y) / (_zoom * MiniMapScaleY));
        return x >= 0 && y >= 0 && x < _preview.Width && y < _preview.Height;
    }
}
