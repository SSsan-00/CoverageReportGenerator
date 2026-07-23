using CoverageReportGenerator.Core.Projects;

namespace CoverageReportGenerator.WinForms;

/// <summary>
/// 解析済みプロジェクト内のソースファイルを絞り込み、Excel出力対象として複数選択するダイアログ。
/// </summary>
public sealed class FileSelectionDialog : Form
{
    private readonly IReadOnlyList<SourceFile> _files;
    private readonly IReadOnlyList<SourceMember> _members;
    private readonly List<SourceFile> _selectedFiles = [];
    private readonly TextBox _filter = new();
    private readonly DataGridView _availableGrid = new();
    private readonly DataGridView _selectedGrid = new();
    private readonly Button _addButton = new() { Text = "追加 >" };
    private readonly Button _removeButton = new() { Text = "< 削除" };
    private readonly Button _okButton = new() { Text = "Excel出力", DialogResult = DialogResult.OK };
    private readonly Button _cancelButton = new() { Text = "キャンセル", DialogResult = DialogResult.Cancel };

    /// <summary>
    /// 選択されたソースファイル一覧。
    /// </summary>
    public IReadOnlyList<SourceFile> SelectedSourceFiles => _selectedFiles;

    /// <summary>
    /// ファイル一覧とメンバー情報からダイアログを生成する。
    /// </summary>
    public FileSelectionDialog(IReadOnlyList<SourceFile> files, IReadOnlyList<SourceMember> members)
    {
        _files = files;
        _members = members;

        Text = "Excel出力対象ファイルを選択";
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        Width = 1040;
        Height = 680;

        BuildLayout();
        RefreshAvailableGrid();
        RefreshSelectedGrid();
        UpdateButtonState();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        _filter.Dock = DockStyle.Fill;
        _filter.PlaceholderText = "ファイル名・フォルダ・拡張子で絞り込み";
        _filter.TextChanged += (_, _) => RefreshAvailableGrid();
        root.Controls.Add(_filter, 0, 0);

        var selector = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(0, 8, 0, 8)
        };
        selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        selector.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        selector.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        root.Controls.Add(selector, 0, 1);

        selector.Controls.Add(BuildAvailablePanel(), 0, 0);
        selector.Controls.Add(BuildMoveButtonsPanel(), 1, 0);
        selector.Controls.Add(BuildSelectedPanel(), 2, 0);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            AutoSize = true
        };
        _okButton.Click += (_, _) => AcceptSelection();
        buttons.Controls.Add(_okButton);
        buttons.Controls.Add(_cancelButton);
        root.Controls.Add(buttons, 0, 2);

        AcceptButton = _okButton;
        CancelButton = _cancelButton;
    }

    private Control BuildAvailablePanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = "ファイル一覧", AutoSize = true }, 0, 0);

        ConfigureGrid(_availableGrid);
        _availableGrid.Columns.Add("Path", "パス");
        _availableGrid.Columns.Add("Extension", "種類");
        _availableGrid.Columns.Add("Members", "メンバー");
        _availableGrid.Columns["Path"]!.FillWeight = 80;
        _availableGrid.Columns["Extension"]!.FillWeight = 12;
        _availableGrid.Columns["Members"]!.FillWeight = 12;
        _availableGrid.CellDoubleClick += (_, _) => AddSelectedFiles();
        _availableGrid.SelectionChanged += (_, _) => UpdateButtonState();
        panel.Controls.Add(_availableGrid, 0, 1);

        return panel;
    }

    private Control BuildMoveButtonsPanel()
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(10, 28, 10, 0)
        };
        _addButton.Width = 96;
        _removeButton.Width = 96;
        _addButton.Click += (_, _) => AddSelectedFiles();
        _removeButton.Click += (_, _) => RemoveSelectedFiles();
        panel.Controls.Add(_addButton);
        panel.Controls.Add(_removeButton);

        return panel;
    }

    private Control BuildSelectedPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 2,
            ColumnCount = 1
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        panel.Controls.Add(new Label { Text = "選択済み", AutoSize = true }, 0, 0);

        ConfigureGrid(_selectedGrid);
        _selectedGrid.Columns.Add("Path", "パス");
        _selectedGrid.Columns.Add("Members", "メンバー");
        _selectedGrid.Columns["Path"]!.FillWeight = 86;
        _selectedGrid.Columns["Members"]!.FillWeight = 14;
        _selectedGrid.CellDoubleClick += (_, _) => RemoveSelectedFiles();
        _selectedGrid.SelectionChanged += (_, _) => UpdateButtonState();
        panel.Controls.Add(_selectedGrid, 0, 1);

        return panel;
    }

    private static void ConfigureGrid(DataGridView grid)
    {
        grid.Dock = DockStyle.Fill;
        grid.ReadOnly = true;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = true;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
    }

    private void RefreshAvailableGrid()
    {
        var words = _filter.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var selectedSet = _selectedFiles.Select(file => file.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        _availableGrid.Rows.Clear();

        foreach (var file in _files
            .Where(IsExcelExportCandidate)
            .Where(file => !selectedSet.Contains(file.FullPath))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (!MatchesFilter(file, words))
            {
                continue;
            }

            var row = _availableGrid.Rows.Add(
                file.RelativePath,
                file.Extension,
                MemberCount(file));
            _availableGrid.Rows[row].Tag = file;
        }

        if (_availableGrid.Rows.Count > 0)
        {
            _availableGrid.Rows[0].Selected = true;
        }

        UpdateButtonState();
    }

    private void RefreshSelectedGrid()
    {
        _selectedGrid.Rows.Clear();

        foreach (var file in _selectedFiles.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var row = _selectedGrid.Rows.Add(file.RelativePath, MemberCount(file));
            _selectedGrid.Rows[row].Tag = file;
        }

        if (_selectedGrid.Rows.Count > 0)
        {
            _selectedGrid.Rows[0].Selected = true;
        }

        UpdateButtonState();
    }

    private void AddSelectedFiles()
    {
        var files = SelectedRows(_availableGrid).ToList();
        if (files.Count == 0 && _availableGrid.CurrentRow?.Tag is SourceFile currentFile)
        {
            files.Add(currentFile);
        }

        foreach (var file in files)
        {
            if (_selectedFiles.Any(selected => selected.FullPath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            _selectedFiles.Add(file);
        }

        RefreshAvailableGrid();
        RefreshSelectedGrid();
    }

    private void RemoveSelectedFiles()
    {
        var files = SelectedRows(_selectedGrid).ToList();
        if (files.Count == 0 && _selectedGrid.CurrentRow?.Tag is SourceFile currentFile)
        {
            files.Add(currentFile);
        }

        foreach (var file in files)
        {
            _selectedFiles.RemoveAll(selected => selected.FullPath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase));
        }

        RefreshAvailableGrid();
        RefreshSelectedGrid();
    }

    private void AcceptSelection()
    {
        if (_selectedFiles.Count == 0)
        {
            DialogResult = DialogResult.None;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void UpdateButtonState()
    {
        _addButton.Enabled = _availableGrid.SelectedRows.Count > 0 || _availableGrid.CurrentRow is not null;
        _removeButton.Enabled = _selectedGrid.SelectedRows.Count > 0 || _selectedGrid.CurrentRow is not null;
        _okButton.Enabled = _selectedFiles.Count > 0;
    }

    private int MemberCount(SourceFile file)
    {
        return _members.Count(member => member.FilePath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExcelExportCandidate(SourceFile file)
    {
        return file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase)
            || file.Extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesFilter(SourceFile file, IReadOnlyList<string> words)
    {
        return words.Count == 0 || words.All(word =>
            file.RelativePath.Contains(word, StringComparison.OrdinalIgnoreCase)
            || file.FullPath.Contains(word, StringComparison.OrdinalIgnoreCase)
            || file.Extension.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<SourceFile> SelectedRows(DataGridView grid)
    {
        return grid.SelectedRows
            .Cast<DataGridViewRow>()
            .OrderBy(row => row.Index)
            .Select(row => row.Tag)
            .OfType<SourceFile>();
    }
}
