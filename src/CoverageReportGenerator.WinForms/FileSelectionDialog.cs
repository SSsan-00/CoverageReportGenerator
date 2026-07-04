using CoverageReportGenerator.Core.Projects;

namespace CoverageReportGenerator.WinForms;

/// <summary>
/// 解析済みプロジェクト内のソースファイルを絞り込み選択するダイアログ。
/// </summary>
public sealed class FileSelectionDialog : Form
{
    private readonly IReadOnlyList<SourceFile> _files;
    private readonly IReadOnlyList<SourceMember> _members;
    private readonly TextBox _filter = new();
    private readonly DataGridView _grid = new();
    private readonly Button _okButton = new() { Text = "選択", DialogResult = DialogResult.OK };
    private readonly Button _cancelButton = new() { Text = "キャンセル", DialogResult = DialogResult.Cancel };

    /// <summary>
    /// 選択されたソースファイル。
    /// </summary>
    public SourceFile? SelectedSourceFile { get; private set; }

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
        Width = 900;
        Height = 620;

        BuildLayout();
        RefreshGrid();
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
        _filter.TextChanged += (_, _) => RefreshGrid();
        root.Controls.Add(_filter, 0, 0);

        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.RowHeadersVisible = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.MultiSelect = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.Columns.Add("Path", "パス");
        _grid.Columns.Add("Extension", "種類");
        _grid.Columns.Add("Members", "メンバー");
        _grid.Columns["Path"]!.FillWeight = 80;
        _grid.Columns["Extension"]!.FillWeight = 12;
        _grid.Columns["Members"]!.FillWeight = 12;
        _grid.CellDoubleClick += (_, _) => AcceptSelection();
        root.Controls.Add(_grid, 0, 1);

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

    private void RefreshGrid()
    {
        var words = _filter.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        _grid.Rows.Clear();

        foreach (var file in _files
            .Where(file => file.Extension.Equals(".cs", StringComparison.OrdinalIgnoreCase) || file.Extension.Equals(".cshtml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            if (words.Length > 0 && words.Any(word =>
                !file.RelativePath.Contains(word, StringComparison.OrdinalIgnoreCase)
                && !file.Extension.Contains(word, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var row = _grid.Rows.Add(
                file.RelativePath,
                file.Extension,
                _members.Count(member => member.FilePath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase)));
            _grid.Rows[row].Tag = file;
        }

        _okButton.Enabled = _grid.Rows.Count > 0;
        if (_grid.Rows.Count > 0)
        {
            _grid.Rows[0].Selected = true;
        }
    }

    private void AcceptSelection()
    {
        if (_grid.CurrentRow?.Tag is SourceFile file)
        {
            SelectedSourceFile = file;
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
