using System.ComponentModel;

namespace MapDescShow;

public sealed class MainForm : Form
{
    private const string DefaultClientRoot = @"M:\沉默世界客户端\GGCM";
    private readonly TextBox _rootBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _mapInfoBox = new() { Dock = DockStyle.Fill, PlaceholderText = "请选择服务端 Mir200\\Envir\\MapInfo.txt" };
    private readonly TextBox _searchBox = new() { Dock = DockStyle.Top, PlaceholderText = "搜索地图文件…" };
    private readonly ListBox _mapList = new() { Dock = DockStyle.Fill };
    private readonly MapCanvas _canvas = new() { Dock = DockStyle.Fill };
    private readonly TextBox _mapKeyBox = new() { Dock = DockStyle.Fill };
    private readonly NumericUpDown _xBox = new() { Maximum = 20000, Dock = DockStyle.Fill };
    private readonly NumericUpDown _yBox = new() { Maximum = 20000, Dock = DockStyle.Fill };
    private readonly TextBox _textBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _colorBox = new() { Text = "$00F0FF", Dock = DockStyle.Fill };
    private readonly Panel _colorPreview = new() { Width = 28, Height = 24, BorderStyle = BorderStyle.FixedSingle };
    private readonly CheckBox _largeModeBox = new() { Text = "大地图 (0)", Checked = true, AutoSize = true };
    private readonly CheckBox _miniModeBox = new() { Text = "小地图 (1)", Checked = true, AutoSize = true };
    private readonly CheckBox _enabledBox = new() { Text = "启用", Checked = true, AutoSize = true };
    private readonly CheckBox _autoBackupBox = new()
    {
        Text = "保存前自动备份 MapDesc1.dat",
        Checked = true,
        AutoSize = true
    };
    private readonly DataGridView _grid = new() { Dock = DockStyle.Fill, AutoGenerateColumns = false };
    private readonly TextBox _entryMapSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索地图名…" };
    private readonly TextBox _entryTextSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "搜索说明…" };
    private readonly CheckBox _exactMapSearch = new()
    {
        Text = "", Checked = false, AutoSize = true, Anchor = AnchorStyles.Left,
        AccessibleName = "地图名完全匹配", Margin = new Padding(4, 3, 2, 3)
    };
    private readonly ToolTip _toolTip = new();
    private readonly ToolStripStatusLabel _status = new() { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
    private readonly ContextMenuStrip _mapContextMenu = new();
    private readonly ToolStripMenuItem _addPointMenuItem = new();
    private readonly List<MapInfoEntry> _allMaps = [];
    private readonly Dictionary<string, string> _preferredMapIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppSettings _settings = AppSettings.Load();
    private MapDescDocument _document = new();
    private MapPreview? _preview;
    private string _mapDirectory = "";
    private string _productionMapDescPath = "";
    private string _mapInfoPath = "";
    private Point _contextCell;
    private bool _selectingMapFromEntry;
    private bool _applyingEntryFilters;

    public MainForm()
    {
        Text = "MapDesc 地图标注编辑器 - 作者QQ8957277";
        Icon? applicationIcon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        if (applicationIcon is not null) Icon = applicationIcon;
        Width = 1500;
        Height = 920;
        MinimumSize = new Size(1100, 700);
        StartPosition = FormStartPosition.CenterScreen;

        _mapContextMenu.Items.Add(_addPointMenuItem);
        _addPointMenuItem.Click += (_, _) => ShowAddDescriptionDialog(_contextCell.X, _contextCell.Y);

        Controls.Add(BuildLayout());
        WireEvents();
        ConfigureGrid();

        string initialRoot = Directory.Exists(_settings.ClientRoot) ? _settings.ClientRoot : DefaultClientRoot;
        _rootBox.Text = initialRoot;
        if (File.Exists(_settings.MapInfoPath))
        {
            _mapInfoPath = _settings.MapInfoPath;
            _mapInfoBox.Text = _mapInfoPath;
        }
        Shown += (_, _) => LoadClientRoot(initialRoot);
        FormClosed += (_, _) => _mapContextMenu.Dispose();
    }

    private Control BuildLayout()
    {
        var rootButton = new Button { Text = "选择客户端目录", AutoSize = true };
        rootButton.Click += (_, _) => ChooseClientRoot();
        var mapInfoButton = new Button { Text = "选择 MapInfo.txt", AutoSize = true };
        mapInfoButton.Click += (_, _) => ChooseMapInfo();
        var loadDescButton = new Button { Text = "重读 MapDesc1.dat", AutoSize = true };
        loadDescButton.Click += (_, _) => LoadMapDesc(_productionMapDescPath);
        var saveButton = new Button { Text = "另存为…", AutoSize = true };
        saveButton.Click += (_, _) => SaveAs();
        var saveClientButton = new Button { Text = "保存到客户端", AutoSize = true };
        saveClientButton.Click += (_, _) => SaveToClient();
        var fitButton = new Button { Text = "适应窗口", AutoSize = true };
        fitButton.Click += (_, _) => _canvas.FitToWindow();

        var top = new TableLayoutPanel { Dock = DockStyle.Top, Height = 76, ColumnCount = 8, RowCount = 2, Padding = new Padding(6) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 2; i < 8; i++) top.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        top.Controls.Add(new Label { Text = "客户端：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        top.Controls.Add(_rootBox, 1, 0);
        top.Controls.Add(rootButton, 2, 0);
        top.Controls.Add(loadDescButton, 3, 0);
        top.Controls.Add(saveButton, 4, 0);
        top.Controls.Add(saveClientButton, 5, 0);
        top.Controls.Add(fitButton, 6, 0);

        top.Controls.Add(new Label { Text = "服务端 MapInfo：", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 1);
        top.Controls.Add(_mapInfoBox, 1, 1);
        top.SetColumnSpan(_mapInfoBox, 4);
        top.Controls.Add(mapInfoButton, 5, 1);

        var left = new Panel { Dock = DockStyle.Fill, Padding = new Padding(5) };
        left.Controls.Add(_mapList);
        left.Controls.Add(_searchBox);

        var editor = BuildEditor();
        var rightSplit = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Horizontal,
            Size = new Size(420, 760), SplitterDistance = 250, SplitterWidth = 7,
            Panel1MinSize = 220, Panel2MinSize = 180, FixedPanel = FixedPanel.Panel1
        };
        rightSplit.Panel1.Controls.Add(editor);
        rightSplit.Panel2.Controls.Add(BuildEntryListPanel());
        Shown += (_, _) => BeginInvoke(() =>
        {
            if (rightSplit.Height >= rightSplit.Panel1MinSize + rightSplit.Panel2MinSize + rightSplit.SplitterWidth)
                rightSplit.SplitterDistance = Math.Min(250,
                    rightSplit.Height - rightSplit.Panel2MinSize - rightSplit.SplitterWidth);
        });

        // 两级分隔条：左侧保持稳定，中间与右侧可由用户拖动调整宽度。
        var centerRight = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            Size = new Size(1100, 700), SplitterDistance = 680, SplitterWidth = 7,
            Panel1MinSize = 320, Panel2MinSize = 340,
            FixedPanel = FixedPanel.Panel2
        };
        centerRight.Panel1.Controls.Add(_canvas);
        centerRight.Panel2.Controls.Add(rightSplit);

        var main = new SplitContainer
        {
            Dock = DockStyle.Fill, Orientation = Orientation.Vertical,
            Size = new Size(1400, 800), SplitterDistance = 300, SplitterWidth = 6,
            Panel1MinSize = 220, Panel2MinSize = 700,
            FixedPanel = FixedPanel.Panel1,
            Padding = new Padding(4)
        };
        main.Panel1.Controls.Add(left);
        main.Panel2.Controls.Add(centerRight);

        var status = new StatusStrip();
        status.Items.Add(_status);
        status.Items.Add(new ToolStripStatusLabel("滚轮缩放 · 右键/中键拖动画布 · 拖动中右分隔条调整编辑区宽度"));

        var host = new Panel { Dock = DockStyle.Fill };
        host.Controls.Add(main);
        host.Controls.Add(top);
        host.Controls.Add(status);
        return host;
    }

    private Control BuildEditor()
    {
        var table = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 5, Padding = new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < table.RowCount; i++)
            table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        void Row(string label, Control control, int row)
        {
            table.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            table.Controls.Add(control, 1, row);
        }
        var identity = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5 };
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        identity.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 90));
        identity.Controls.Add(_mapKeyBox, 0, 0);
        identity.Controls.Add(new Label { Text = "X", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 6, 5, 0) }, 1, 0);
        identity.Controls.Add(_xBox, 2, 0);
        identity.Controls.Add(new Label { Text = "Y", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(10, 6, 5, 0) }, 3, 0);
        identity.Controls.Add(_yBox, 4, 0);
        Row("地图", identity, 0);
        Row("说明", _textBox, 1);

        var colorPanel = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 3, RowCount = 2 };
        colorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        colorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        colorPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var colorButton = new Button { Text = "其他颜色…", AutoSize = true };
        colorButton.Click += (_, _) => ChooseColor();
        colorPanel.Controls.Add(_colorBox, 0, 0);
        colorPanel.Controls.Add(_colorPreview, 1, 0);
        colorPanel.Controls.Add(colorButton, 2, 0);
        var palette = BuildColorPalette();
        colorPanel.Controls.Add(palette, 0, 1);
        colorPanel.SetColumnSpan(palette, 3);
        Row("颜色", colorPanel, 2);
        var modes = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        modes.Controls.AddRange([_largeModeBox, _miniModeBox]);
        Row("模式", modes, 3);
        var state = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true };
        state.Controls.AddRange([_enabledBox, _autoBackupBox]);
        Row("状态", state, 4);
        return table;
    }

    private Control BuildEntryListPanel()
    {
        var add = new Button { Text = "＋ 新增备注", AutoSize = true };
        add.Click += (_, _) => AddEntry();
        var update = new Button { Text = "✓ 更新选中", AutoSize = true };
        update.Click += (_, _) => UpdateSelected();
        var delete = new Button { Text = "－ 删除选中", AutoSize = true };
        delete.Click += (_, _) => DeleteSelected();
        var save = new Button { Text = "保存", AutoSize = true };
        save.Click += (_, _) => SaveToClient();
        var hint = new Label
        {
            Text = "选中行会自动打开对应地图；方向键移动坐标",
            AutoSize = true, Anchor = AnchorStyles.Left, ForeColor = Color.DimGray
        };
        var toolbar = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(3) };
        toolbar.Controls.AddRange([add, update, delete, save, hint]);

        var search = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 5, Padding = new Padding(3, 0, 3, 3) };
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        search.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48));
        search.Controls.Add(new Label { Text = "地图名", AutoSize = true, Anchor = AnchorStyles.Left }, 0, 0);
        search.Controls.Add(_entryMapSearch, 1, 0);
        search.Controls.Add(_exactMapSearch, 2, 0);
        search.Controls.Add(new Label { Text = "说明", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(8, 6, 3, 0) }, 3, 0);
        search.Controls.Add(_entryTextSearch, 4, 0);
        _toolTip.SetToolTip(_exactMapSearch,
            "未勾选：地图名包含搜索文字即可。\n勾选：地图名必须与搜索文字完全一致。\n默认不勾选。");

        var host = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        host.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        host.Controls.Add(toolbar, 0, 0);
        host.Controls.Add(search, 0, 1);
        host.Controls.Add(_grid, 0, 2);
        return host;
    }

    private void ConfigureGrid()
    {
        _grid.AllowUserToAddRows = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.RowHeadersVisible = false;
        _grid.Columns.Add(new DataGridViewCheckBoxColumn { DataPropertyName = nameof(MapDescEntry.Enabled), HeaderText = "启用", Width = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MapDescEntry.MapName), HeaderText = "地图", Width = 90 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MapDescEntry.X), HeaderText = "X", Width = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MapDescEntry.Y), HeaderText = "Y", Width = 45 });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MapDescEntry.Text), HeaderText = "说明", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
        _grid.Columns.Add(new DataGridViewButtonColumn
        {
            DataPropertyName = nameof(MapDescEntry.ColorCode), HeaderText = "颜色", Width = 82,
            FlatStyle = FlatStyle.Popup, UseColumnTextForButtonValue = false,
            ToolTipText = "点击修改此条备注的颜色"
        });
        _grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = nameof(MapDescEntry.Mode), HeaderText = "模式", Width = 45 });
    }

    private void WireEvents()
    {
        _searchBox.TextChanged += (_, _) => FilterMaps();
        _entryMapSearch.TextChanged += (_, _) => ApplyEntryFilters();
        _entryTextSearch.TextChanged += (_, _) => ApplyEntryFilters();
        _exactMapSearch.CheckedChanged += (_, _) => ApplyEntryFilters();
        _rootBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            LoadClientRoot(_rootBox.Text.Trim());
            e.SuppressKeyPress = true;
        };
        _mapInfoBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            LoadMapInfo(_mapInfoBox.Text.Trim());
            e.SuppressKeyPress = true;
        };
        _mapList.SelectedIndexChanged += (_, _) => LoadSelectedMap();
        _canvas.CellClicked += (x, y) => { _xBox.Value = x; _yBox.Value = y; _status.Text = $"选中地图坐标：{x}, {y}"; };
        _canvas.CellContextRequested += ShowAddPointMenu;
        _mapKeyBox.TextChanged += (_, _) => { _canvas.MapKey = _mapKeyBox.Text.Trim(); RefreshEntries(); };
        _largeModeBox.CheckedChanged += (_, _) => { _canvas.ShowLargeMode = _largeModeBox.Checked; RefreshEntries(); };
        _miniModeBox.CheckedChanged += (_, _) => { _canvas.ShowMiniMode = _miniModeBox.Checked; RefreshEntries(); };
        _grid.SelectionChanged += (_, _) =>
        {
            if (!_applyingEntryFilters) PopulateFromSelected();
        };
        _grid.CellValueChanged += (_, _) => RefreshEntries();
        _grid.CellContentClick += GridCellContentClick;
        _grid.CellFormatting += GridCellFormatting;
        _grid.KeyDown += MoveSelectedEntryWithArrowKeys;
        _canvas.KeyDown += MoveSelectedEntryWithArrowKeys;
        _colorBox.TextChanged += (_, _) => UpdateColorPreview();
        _colorBox.Validated += (_, _) => ApplyColor(_colorBox.Text);
        _colorBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode != Keys.Enter) return;
            ApplyColor(_colorBox.Text);
            e.SuppressKeyPress = true;
        };
        UpdateColorPreview();
    }

    private Control BuildColorPalette()
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
            AutoSize = true, Dock = DockStyle.Fill, ColumnCount = 8, RowCount = 3,
            Margin = new Padding(0, 4, 0, 0), GrowStyle = TableLayoutPanelGrowStyle.FixedSize
        };
        for (int i = 0; i < colors.Length; i++)
        {
            Color color = colors[i];
            var swatch = new Button
            {
                BackColor = color, Width = 24, Height = 22, Margin = new Padding(1),
                FlatStyle = FlatStyle.Flat, TabStop = false,
                AccessibleName = $"颜色 {MapDescEntry.FromColor(color)}"
            };
            swatch.FlatAppearance.BorderColor = Color.DimGray;
            swatch.Click += (_, _) => ApplyColor(color);
            palette.Controls.Add(swatch, i % 8, i / 8);
        }
        return palette;
    }

    private void ChooseClientRoot()
    {
        using var dialog = new FolderBrowserDialog { Description = "选择包含 Map 和 data 的客户端根目录", InitialDirectory = _rootBox.Text };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadClientRoot(dialog.SelectedPath);
    }

    private void LoadClientRoot(string root)
    {
        try
        {
            _rootBox.Text = root;
            _mapDirectory = Path.Combine(root, "Map");
            _productionMapDescPath = Path.Combine(root, "data", "MapDesc1.dat");
            if (!Directory.Exists(_mapDirectory)) throw new DirectoryNotFoundException("未找到 Map 目录：" + _mapDirectory);
            // 切换客户端时保留已选择的服务端 MapInfo；尚未选择时才显示物理地图回退列表。
            if (!string.IsNullOrWhiteSpace(_mapInfoPath) && File.Exists(_mapInfoPath))
                LoadMapInfo(_mapInfoPath);
            else
                LoadPhysicalMapFallback();
            if (File.Exists(_productionMapDescPath)) LoadMapDesc(_productionMapDescPath);
            _settings.ClientRoot = Path.GetFullPath(root);
            SaveSettingsQuietly();
            _status.Text = $"只读载入 {_allMaps.Count:N0} 个地图文件";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void FilterMaps()
    {
        string query = _searchBox.Text.Trim();
        _mapList.BeginUpdate();
        _mapList.Items.Clear();
        foreach (MapInfoEntry entry in _allMaps.Where(x =>
                     x.MapName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     x.MapFileId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                     x.MapAlias.Contains(query, StringComparison.OrdinalIgnoreCase)))
            _mapList.Items.Add(entry);
        _mapList.EndUpdate();
    }

    private void LoadSelectedMap()
    {
        if (_mapList.SelectedItem is not MapInfoEntry mapInfo) return;
        _preferredMapIds[mapInfo.MapName] = mapInfo.MapFileId;
        string path = Path.Combine(_mapDirectory, mapInfo.MapFileName);
        try
        {
            Cursor = Cursors.WaitCursor;
            _preview = MapFileReader.ReadWalkability(path);
            _canvas.SetPreview(_preview);
            _mapKeyBox.Text = mapInfo.MapName;
            // 用户从左侧选择地图时，右侧自动只显示该逻辑地图的 MapDesc。
            // 从右侧记录反向定位地图时不重复绑定表格，避免 SelectionChanged 重入。
            if (!_selectingMapFromEntry &&
                !string.Equals(_entryMapSearch.Text, mapInfo.MapName, StringComparison.Ordinal))
                _entryMapSearch.Text = mapInfo.MapName;
            double ratio = _preview.WalkableCount * 100.0 / (_preview.Width * _preview.Height);
            string trailing = _preview.TrailingBytes > 0 ? $" · 忽略尾随 {_preview.TrailingBytes:N0} 字节" : "";
            _status.Text = $"{mapInfo.MapName} → {Path.GetFileName(path)} · {_preview.Width}×{_preview.Height} · {_preview.CellBytes}字节/格{trailing} · " +
                $"可移动 {ratio:F1}% · 禁移/可隔位 {_preview.MoveBlockedCastAllowedCount:N0} · 禁移禁隔位 {_preview.MoveAndCastBlockedCount:N0} · 关闭门 {_preview.ClosedDoorCount:N0}";
        }
        catch (Exception ex) { ShowError(ex); }
        finally { Cursor = Cursors.Default; }
    }

    private void ChooseMapInfo()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "选择服务端 Mir200\\Envir\\MapInfo.txt",
            Filter = "MapInfo.txt|MapInfo.txt|文本文件 (*.txt)|*.txt|所有文件 (*.*)|*.*",
            FileName = string.IsNullOrWhiteSpace(_mapInfoPath) ? "MapInfo.txt" : _mapInfoPath
        };
        if (dialog.ShowDialog(this) == DialogResult.OK) LoadMapInfo(dialog.FileName);
    }

    private void LoadMapInfo(string path)
    {
        try
        {
            _mapInfoPath = Path.GetFullPath(path);
            _mapInfoBox.Text = _mapInfoPath;
            var entries = MapInfoDocument.Load(_mapInfoPath);
            _allMaps.Clear();
            _allMaps.AddRange(entries);
            _settings.MapInfoPath = _mapInfoPath;
            SaveSettingsQuietly();
            FilterMaps();
            int existing = entries.Count(x => File.Exists(Path.Combine(_mapDirectory, x.MapFileName)));
            int aliases = entries.GroupBy(x => x.MapFileId, StringComparer.OrdinalIgnoreCase).Count(x => x.Count() > 1);
            _status.Text = $"MapInfo：{entries.Count:N0} 个逻辑地图；客户端存在 {existing:N0} 个对应 MAP；{aliases:N0} 组共享 MAP";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void LoadPhysicalMapFallback()
    {
        _allMaps.Clear();
        _allMaps.AddRange(Directory.EnumerateFiles(_mapDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(x => string.Equals(Path.GetExtension(x), ".map", StringComparison.OrdinalIgnoreCase))
            .Select(x => Path.GetFileNameWithoutExtension(x))
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .Select((id, index) => new MapInfoEntry(id, id, index + 1, id)));
        FilterMaps();
    }

    private void LoadMapDesc(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return;
        try
        {
            _document = MapDescDocument.Load(path);
            ApplyEntryFilters();
            RefreshEntries();
            _status.Text = $"只读载入 MapDesc：{_document.Entries.Count:N0} 条可解析记录";
        }
        catch (Exception ex) { ShowError(ex); }
    }

    private void RefreshEntries()
    {
        _canvas.MapKey = _mapKeyBox.Text.Trim();
        _canvas.SetEntries(_document.Entries.ToList());
    }

    private void ApplyEntryFilters()
    {
        string mapQuery = _entryMapSearch.Text.Trim();
        string textQuery = _entryTextSearch.Text.Trim();
        var filtered = _document.Entries.Where(x =>
                (mapQuery.Length == 0 || (_exactMapSearch.Checked
                    ? string.Equals(x.MapName, mapQuery, StringComparison.OrdinalIgnoreCase)
                    : x.MapName.Contains(mapQuery, StringComparison.OrdinalIgnoreCase))) &&
                x.Text.Contains(textQuery, StringComparison.OrdinalIgnoreCase))
            .ToList();
        _applyingEntryFilters = true;
        try
        {
            _grid.DataSource = new BindingList<MapDescEntry>(filtered);
            _grid.ClearSelection();
            _grid.CurrentCell = null;
        }
        finally
        {
            _applyingEntryFilters = false;
        }
    }

    private MapDescEntry BuildEntry(int mode) => new()
    {
        MapName = _mapKeyBox.Text.Trim(), X = (int)_xBox.Value, Y = (int)_yBox.Value,
        Text = _textBox.Text.Trim(), ColorCode = _colorBox.Text.Trim(),
        Mode = mode, Enabled = _enabledBox.Checked
    };

    private List<int> SelectedModes()
    {
        var modes = new List<int>(2);
        if (_largeModeBox.Checked) modes.Add(0);
        if (_miniModeBox.Checked) modes.Add(1);
        return modes;
    }

    private void ShowAddPointMenu(int x, int y)
    {
        _contextCell = new Point(x, y);
        _addPointMenuItem.Text = $"在 ({x}, {y}) 添加描述点…";
        _mapContextMenu.Show(_canvas, _canvas.PointToClient(Cursor.Position));
    }

    private void ShowAddDescriptionDialog(int x, int y)
    {
        string mapName = _mapKeyBox.Text.Trim();
        if (mapName.Length == 0)
        {
            MessageBox.Show(this, "请先选择地图。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var dialog = new AddDescriptionDialog(mapName, x, y, ParseEditorColor());
        if (dialog.ShowDialog(this) != DialogResult.OK) return;

        MapDescEntry? first = null;
        foreach (int mode in dialog.SelectedModes)
        {
            var entry = new MapDescEntry
            {
                MapName = mapName, X = x, Y = y, Text = dialog.DescriptionText,
                ColorCode = dialog.SelectedColorCode, Mode = mode, Enabled = true
            };
            _document.Add(entry);
            first ??= entry;
        }
        if (first is null) return;
        ApplyEntryFilters();
        SelectGridEntry(first);
        RefreshEntries();
        _status.Text = $"已添加描述点：{first.Text} ({first.X},{first.Y})，生成 {dialog.SelectedModes.Count} 种模式；尚未保存。";
    }

    private void AddEntry()
    {
        if (string.IsNullOrWhiteSpace(_mapKeyBox.Text) || string.IsNullOrWhiteSpace(_textBox.Text))
        { MessageBox.Show(this, "请填写地图标识和说明文字。", "提示"); return; }
        List<int> modes = SelectedModes();
        if (modes.Count == 0)
        {
            MessageBox.Show(this, "请至少勾选一种显示模式。", "提示");
            return;
        }
        MapDescEntry? first = null;
        foreach (int mode in modes)
        {
            var entry = BuildEntry(mode);
            _document.Add(entry);
            first ??= entry;
        }
        ApplyEntryFilters();
        SelectGridEntry(first!);
        RefreshEntries();
        _status.Text = $"已在内存中添加 {modes.Count} 条模式记录：{first!.MapName} ({first.X},{first.Y}) {first.Text}；尚未保存。";
    }

    private MapDescEntry? SelectedEntry => _grid.CurrentRow?.DataBoundItem as MapDescEntry;

    private void UpdateSelected()
    {
        if (SelectedEntry is not { } entry) return;
        var value = BuildEntry(entry.Mode);
        entry.MapName = value.MapName; entry.X = value.X; entry.Y = value.Y;
        entry.Text = value.Text; entry.ColorCode = value.ColorCode; entry.Mode = value.Mode; entry.Enabled = value.Enabled;
        ApplyEntryFilters();
        SelectGridEntry(entry);
        _grid.Refresh(); RefreshEntries();
    }

    private void DeleteSelected()
    {
        if (SelectedEntry is not { } entry) return;
        _document.Remove(entry);
        ApplyEntryFilters();
        RefreshEntries();
    }

    private void PopulateFromSelected()
    {
        if (SelectedEntry is not { } entry) return;
        _mapKeyBox.Text = entry.MapName;
        _xBox.Value = Math.Clamp(entry.X, 0, (int)_xBox.Maximum);
        _yBox.Value = Math.Clamp(entry.Y, 0, (int)_yBox.Maximum);
        _textBox.Text = entry.Text;
        _colorBox.Text = entry.ColorCode;
        _enabledBox.Checked = entry.Enabled;
        SelectAssociatedMap(entry.MapName);
        _canvas.SelectCell(entry.X, entry.Y);
    }

    private void SelectAssociatedMap(string mapName)
    {
        List<MapInfoEntry> candidates = _allMaps.Where(x =>
            string.Equals(x.MapName, mapName, StringComparison.OrdinalIgnoreCase)).ToList();
        if (candidates.Count == 0)
        {
            _status.Text = $"MapInfo 中找不到逻辑地图“{mapName}”，无法关联 MAP 预览。";
            return;
        }

        // 同名逻辑地图可能对应多个完全不同的物理 MAP。优先保留用户当前选择，
        // 其次使用本次运行中用户为该逻辑地图选过的物理编号，绝不盲选第一个。
        if (_mapList.SelectedItem is MapInfoEntry current &&
            string.Equals(current.MapName, mapName, StringComparison.OrdinalIgnoreCase))
            return;

        MapInfoEntry? target = null;
        if (_preferredMapIds.TryGetValue(mapName, out string? preferredId))
            target = candidates.FirstOrDefault(x =>
                string.Equals(x.MapFileId, preferredId, StringComparison.OrdinalIgnoreCase));
        if (target is null && candidates.Count == 1)
            target = candidates[0];
        if (target is null)
        {
            _status.Text = $"“{mapName}”对应 {candidates.Count} 个物理地图，请在左侧选择正确的地图编号。";
            return;
        }

        int index = FindMapListIndex(target);
        if (index < 0 && _searchBox.TextLength > 0)
        {
            _searchBox.Clear();
            index = FindMapListIndex(target);
        }
        if (index < 0) return;

        if (_mapList.SelectedIndex != index)
        {
            _selectingMapFromEntry = true;
            try { _mapList.SelectedIndex = index; }
            finally { _selectingMapFromEntry = false; }
        }
        _mapList.TopIndex = Math.Max(0, index - 3);
    }

    private int FindMapListIndex(MapInfoEntry target)
    {
        for (int i = 0; i < _mapList.Items.Count; i++)
            if (_mapList.Items[i] is MapInfoEntry item &&
                string.Equals(item.MapName, target.MapName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(item.MapFileId, target.MapFileId, StringComparison.OrdinalIgnoreCase))
                return i;
        return -1;
    }

    private void SelectGridEntry(MapDescEntry entry)
    {
        foreach (DataGridViewRow row in _grid.Rows)
        {
            if (!ReferenceEquals(row.DataBoundItem, entry)) continue;
            _grid.ClearSelection();
            row.Selected = true;
            _grid.CurrentCell = row.Cells[0];
            if (!row.Displayed) _grid.FirstDisplayedScrollingRowIndex = row.Index;
            break;
        }
    }

    private void MoveSelectedEntryWithArrowKeys(object? sender, KeyEventArgs e)
    {
        int dx = e.KeyCode switch { Keys.Left => -1, Keys.Right => 1, _ => 0 };
        int dy = e.KeyCode switch { Keys.Up => -1, Keys.Down => 1, _ => 0 };
        if ((dx == 0 && dy == 0) || SelectedEntry is not { } entry) return;

        int maxX = _preview is null ? (int)_xBox.Maximum : Math.Max(0, _preview.Width - 1);
        int maxY = _preview is null ? (int)_yBox.Maximum : Math.Max(0, _preview.Height - 1);
        int oldX = entry.X;
        int oldY = entry.Y;
        List<MapDescEntry> targets = e.Alt
            ? _document.Entries.Where(x =>
                string.Equals(x.MapName, entry.MapName, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.Text, entry.Text, StringComparison.Ordinal) &&
                x.X == oldX && x.Y == oldY && x.Mode is 0 or 1).ToList()
            : [entry];

        int newX = Math.Clamp(oldX + dx, 0, maxX);
        int newY = Math.Clamp(oldY + dy, 0, maxY);
        foreach (MapDescEntry target in targets)
        {
            target.X = newX;
            target.Y = newY;
        }
        _xBox.Value = entry.X;
        _yBox.Value = entry.Y;
        _canvas.SelectCell(entry.X, entry.Y);
        _grid.Refresh();
        RefreshEntries();
        _status.Text = e.Alt
            ? $"已同步移动模式 0/1 共 {targets.Count} 条记录到 ({entry.X},{entry.Y})；尚未保存。"
            : $"已移动选中备注到 ({entry.X},{entry.Y})；尚未保存。";
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    private void ChooseColor()
    {
        using var dialog = new ColorDialog { FullOpen = true };
        dialog.Color = SelectedEntry?.ToColor() ?? ParseEditorColor();
        if (dialog.ShowDialog(this) == DialogResult.OK) ApplyColor(dialog.Color);
    }

    private void GridCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(MapDescEntry.ColorCode) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not MapDescEntry entry)
            return;

        _grid.CurrentCell = _grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
        using var dialog = new ColorDialog { FullOpen = true, Color = entry.ToColor() };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        entry.ColorCode = MapDescEntry.FromColor(dialog.Color);
        _colorBox.Text = entry.ColorCode;
        _grid.InvalidateCell(e.ColumnIndex, e.RowIndex);
        RefreshEntries();
        _status.Text = $"已修改“{entry.Text}”的颜色为 {entry.ColorCode}；尚未保存。";
    }

    private void GridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0 ||
            _grid.Columns[e.ColumnIndex].DataPropertyName != nameof(MapDescEntry.ColorCode) ||
            _grid.Rows[e.RowIndex].DataBoundItem is not MapDescEntry entry)
            return;

        Color color = entry.ToColor();
        e.Value = entry.ColorCode;
        e.CellStyle.BackColor = color;
        e.CellStyle.SelectionBackColor = color;
        bool dark = color.R * 299 + color.G * 587 + color.B * 114 < 128000;
        e.CellStyle.ForeColor = dark ? Color.White : Color.Black;
        e.CellStyle.SelectionForeColor = dark ? Color.White : Color.Black;
        e.FormattingApplied = true;
    }

    private void ApplyColor(Color color) => ApplyColor(MapDescEntry.FromColor(color));

    private void ApplyColor(string colorCode)
    {
        var probe = new MapDescEntry { ColorCode = colorCode };
        string normalized = probe.ColorCode;
        _colorBox.Text = normalized;
        if (SelectedEntry is { } entry)
        {
            entry.ColorCode = normalized;
            _grid.Refresh();
            RefreshEntries();
            _status.Text = $"已修改选中备注颜色为 {normalized}；尚未保存。";
        }
        UpdateColorPreview();
    }

    private Color ParseEditorColor()
    {
        var probe = new MapDescEntry { ColorCode = _colorBox.Text };
        return probe.ToColor();
    }

    private void UpdateColorPreview()
    {
        _colorPreview.BackColor = ParseEditorColor();
        _colorPreview.Invalidate();
    }

    private void SaveAs()
    {
        using var dialog = new SaveFileDialog
        {
            Title = "另存 MapDesc（不会覆盖生产文件）", Filter = "MapDesc 数据 (*.dat)|*.dat|文本文件 (*.txt)|*.txt",
            FileName = "MapDesc1.preview.dat", InitialDirectory = AppContext.BaseDirectory
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        string full = Path.GetFullPath(dialog.FileName);
        if (string.Equals(full, Path.GetFullPath(_productionMapDescPath), StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, "安全限制：不能覆盖生产环境的 MapDesc1.dat，请选择其他路径。", "已阻止", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        try { _document.SaveAs(full); _status.Text = "已另存为：" + full; }
        catch (Exception ex) { ShowError(ex); }
    }

    private void SaveToClient()
    {
        if (string.IsNullOrWhiteSpace(_productionMapDescPath))
        {
            MessageBox.Show(this, "请先选择客户端目录。", "提示");
            return;
        }

        try
        {
            string? backupPath = null;
            if (_autoBackupBox.Checked && File.Exists(_productionMapDescPath))
            {
                string directory = Path.GetDirectoryName(_productionMapDescPath)!;
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                backupPath = Path.Combine(directory, $"MapDesc1.{stamp}.bak");
                int suffix = 1;
                while (File.Exists(backupPath))
                    backupPath = Path.Combine(directory, $"MapDesc1.{stamp}-{suffix++}.bak");

                // 备份失败会抛出异常，后面的保存不会执行。
                File.Copy(_productionMapDescPath, backupPath, overwrite: false);
            }

            _document.SaveAs(_productionMapDescPath);
            _status.Text = backupPath is null
                ? "已保存到客户端：" + _productionMapDescPath
                : $"已保存；备份：{backupPath}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "保存未完成。原文件不会在备份失败时被覆盖。\n\n" + ex.Message,
                "保存失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ShowError(Exception ex) => MessageBox.Show(this, ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);

    private void SaveSettingsQuietly()
    {
        try { _settings.Save(); }
        catch (Exception ex) { _status.Text = "路径配置保存失败：" + ex.Message; }
    }
}
