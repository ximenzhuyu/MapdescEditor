namespace MapDescShow;

public sealed class AddDescriptionDialog : Form
{
    private readonly TextBox _textBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _colorBox = new() { Dock = DockStyle.Fill, ReadOnly = true };
    private readonly Panel _colorPreview = new() { Width = 30, Height = 25, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _largeModeBox = new() { Text = "大地图 (0)", Checked = true, AutoSize = true };
    private readonly CheckBox _miniModeBox = new() { Text = "小地图 (1)", Checked = true, AutoSize = true };
    private Color _selectedColor;

    public string DescriptionText => _textBox.Text.Trim();
    public string SelectedColorCode => MapDescEntry.FromColor(_selectedColor);
    public IReadOnlyList<int> SelectedModes
    {
        get
        {
            var modes = new List<int>(2);
            if (_largeModeBox.Checked) modes.Add(0);
            if (_miniModeBox.Checked) modes.Add(1);
            return modes;
        }
    }

    public AddDescriptionDialog(string mapName, int x, int y, Color initialColor)
    {
        Text = "添加地图描述点";
        Width = 460;
        Height = 340;
        MinimumSize = new Size(430, 320);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        SetColor(initialColor);

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 2, RowCount = 6
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        AddRow(table, "地图/坐标", new Label
        {
            Text = $"{mapName}  ({x}, {y})", AutoSize = true, Anchor = AnchorStyles.Left
        }, 0);
        AddRow(table, "说明文字", _textBox, 1);

        var colorTop = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3 };
        colorTop.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        colorTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        colorTop.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var otherColor = new Button { Text = "其他颜色…", AutoSize = true };
        otherColor.Click += (_, _) => ChooseOtherColor();
        colorTop.Controls.Add(_colorBox, 0, 0);
        colorTop.Controls.Add(_colorPreview, 1, 0);
        colorTop.Controls.Add(otherColor, 2, 0);
        AddRow(table, "颜色", colorTop, 2);
        var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        modes.Controls.AddRange([_largeModeBox, _miniModeBox]);
        AddRow(table, "显示模式", modes, 3);

        var palette = BuildPalette();
        table.Controls.Add(palette, 0, 4);
        table.SetColumnSpan(palette, 2);

        var ok = new Button { Text = "添加", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, AutoSize = true };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, AutoSize = true, FlowDirection = FlowDirection.RightToLeft
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);
        table.Controls.Add(buttons, 0, 5);
        table.SetColumnSpan(buttons, 2);
        Controls.Add(table);

        AcceptButton = ok;
        CancelButton = cancel;
        ok.Click += (_, e) =>
        {
            if (DescriptionText.Length > 0 && SelectedModes.Count > 0) return;
            DialogResult = DialogResult.None;
            string message = DescriptionText.Length == 0 ? "请输入说明文字。" : "请至少勾选一种显示模式。";
            MessageBox.Show(this, message, "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            if (DescriptionText.Length == 0) _textBox.Focus();
        };
        Shown += (_, _) => _textBox.Focus();
    }

    private static void AddRow(TableLayoutPanel table, string label, Control control, int row)
    {
        table.Controls.Add(new Label
        {
            Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(3, 7, 8, 3)
        }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private Control BuildPalette()
    {
        Color[] colors =
        [
            Color.Black, Color.Navy, Color.Green, Color.Teal, Color.Maroon, Color.Purple, Color.Olive, Color.Gray,
            Color.Silver, Color.Blue, Color.Lime, Color.Cyan, Color.Red, Color.Magenta, Color.Yellow, Color.White,
            Color.FromArgb(255, 128, 0), Color.FromArgb(255, 128, 160), Color.FromArgb(160, 128, 255),
            Color.FromArgb(80, 160, 255), Color.FromArgb(80, 210, 160), Color.FromArgb(160, 210, 80),
            Color.FromArgb(210, 160, 80), Color.DimGray
        ];
        var palette = new TableLayoutPanel
        {
            AutoSize = true, Anchor = AnchorStyles.None, ColumnCount = 8, RowCount = 3,
            GrowStyle = TableLayoutPanelGrowStyle.FixedSize, Margin = new Padding(3, 12, 3, 8)
        };
        for (int i = 0; i < colors.Length; i++)
        {
            Color color = colors[i];
            var swatch = new Button
            {
                BackColor = color, Width = 36, Height = 28, Margin = new Padding(2),
                FlatStyle = FlatStyle.Flat, TabStop = false,
                AccessibleName = $"颜色 {MapDescEntry.FromColor(color)}"
            };
            swatch.Click += (_, _) => SetColor(color);
            palette.Controls.Add(swatch, i % 8, i / 8);
        }
        return palette;
    }

    private void ChooseOtherColor()
    {
        using var dialog = new ColorDialog { FullOpen = true, Color = _selectedColor };
        if (dialog.ShowDialog(this) == DialogResult.OK) SetColor(dialog.Color);
    }

    private void SetColor(Color color)
    {
        _selectedColor = color;
        _colorBox.Text = MapDescEntry.FromColor(color);
        _colorPreview.BackColor = color;
    }
}
