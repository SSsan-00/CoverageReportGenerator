using System.Diagnostics;
using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Settings;

namespace CoverageReportGenerator.WinForms;

/// <summary>
/// カバレッジレポート生成のメイン画面。
/// </summary>
public sealed class MainForm : Form
{
    private const string ImplicitIncludePatterns = "*.cs;*.cshtml";
    private const string ImplicitExcludePatterns = "*.g.cs;*.generated.cs;*.Designer.cs;bin;obj";

    private readonly AppSettingsService _settingsService = new();
    private readonly TextBox _projectPath = new() { ReadOnly = true };
    private readonly TextBox _xmlPath = new();
    private readonly TextBox _scopePath = new();
    private readonly TextBox _outputDirectory = new();
    private readonly TextBox _reportTitle = new();
    private readonly RadioButton _projectScope = new() { Text = "プロジェクト", Checked = true };
    private readonly RadioButton _folderScope = new() { Text = "フォルダ" };
    private readonly RadioButton _fileScope = new() { Text = "ファイル" };
    private readonly CheckBox _openAfterGeneration = new() { Text = "生成後に開く", Checked = true, AutoSize = true };
    private readonly CheckBox _overwriteExisting = new() { Text = "既存ファイルを上書き", AutoSize = true };
    private readonly Label _projectStatus = new() { AutoSize = true, Text = "プロジェクト: 未読込" };
    private readonly Label _previewStatus = new() { AutoSize = true, Text = "対象プレビュー" };
    private readonly TextBox _previewFilter = new() { PlaceholderText = "ファイル名・フォルダで絞り込み" };
    private readonly TreeView _folderTree = new();
    private readonly DataGridView _previewGrid = new();
    private readonly RichTextBox _log = new() { ReadOnly = true, BorderStyle = BorderStyle.None };
    private readonly Button _resetButton = new() { Text = "初期化", Height = 36 };
    private readonly Button _excelButton = new() { Text = "Excel出力", Height = 36 };
    private readonly Button _generateButton = new() { Text = "HTML生成", Height = 36 };

    private Button? _scopeBrowseButton;
    private ProjectAnalysis? _analysis;
    private bool _syncingFolderTree;
    private bool _syncingScope;

    /// <summary>
    /// メイン画面を初期化する。
    /// </summary>
    public MainForm()
    {
        Text = "Coverage Report Generator";
        MinimumSize = new Size(1100, 760);
        Width = 1180;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        ApplySettings(_settingsService.Load());
        // 前回の入力内容を表示したうえで、存在するプロジェクトだけ自動解析する。
        Shown += async (_, _) => await LoadSavedProjectIfAvailableAsync();
        FormClosing += (_, _) => SaveCurrentSettings();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(12)
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));
        Controls.Add(root);

        root.Controls.Add(BuildSettingsPanel(), 0, 0);
        root.Controls.Add(BuildPreviewPanel(), 0, 1);
        root.Controls.Add(BuildLogPanel(), 0, 2);

        AcceptButton = _generateButton;
    }

    private Control BuildSettingsPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 4,
            AutoSize = true,
            Padding = new Padding(0, 0, 0, 10)
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 100));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 120));

        AddPathRow(panel, "プロジェクト", _projectPath, "選択", BrowseProjectAsync, "再読込", ReloadProjectAsync);
        panel.Controls.Add(_projectStatus, 1, panel.RowCount++);
        AddPathRow(panel, "DotCover XML", _xmlPath, "選択", BrowseXml, null, null);
        AddScopeRow(panel);
        _scopeBrowseButton = AddPathRow(panel, "対象パス", _scopePath, "選択", BrowseScope, null, null);
        _scopePath.TextChanged += (_, _) => ScopeInputChanged();
        AddPathRow(panel, "出力先", _outputDirectory, "選択", BrowseOutput, null, null);
        AddTextRow(panel, "レポート名", _reportTitle);

        var options = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill, AutoSize = true };
        options.Controls.Add(_openAfterGeneration);
        options.Controls.Add(_overwriteExisting);
        panel.Controls.Add(new Label(), 0, panel.RowCount);
        panel.Controls.Add(options, 1, panel.RowCount);
        var actions = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        _resetButton.Click += (_, _) => ResetToDefaultSettings();
        _excelButton.Click += async (_, _) => await ExportExcelAsync();
        _generateButton.Click += async (_, _) => await GenerateAsync();
        actions.Controls.Add(_generateButton);
        actions.Controls.Add(_excelButton);
        actions.Controls.Add(_resetButton);
        panel.Controls.Add(actions, 2, panel.RowCount);
        panel.SetColumnSpan(actions, 2);
        panel.RowCount++;

        return panel;
    }

    private Control BuildPreviewPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _previewStatus.Padding = new Padding(0, 0, 0, 6);
        panel.Controls.Add(_previewStatus, 0, 0);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            FixedPanel = FixedPanel.Panel1
        };
        const int folderPanelMinimumWidth = 180;
        const int previewPanelMinimumWidth = 240;
        var splitDistanceInitialized = false;
        split.SizeChanged += (_, _) =>
        {
            if (splitDistanceInitialized || split.Width <= folderPanelMinimumWidth + previewPanelMinimumWidth)
            {
                return;
            }

            split.Panel1MinSize = folderPanelMinimumWidth;
            split.Panel2MinSize = previewPanelMinimumWidth;
            split.SplitterDistance = Math.Min(320, split.Width - previewPanelMinimumWidth);
            splitDistanceInitialized = true;
        };

        var folderPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(0, 0, 10, 0)
        };
        folderPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        folderPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        folderPanel.Controls.Add(new Label { Text = "フォルダ", AutoSize = true, Padding = new Padding(0, 0, 0, 6) }, 0, 0);

        _folderTree.Dock = DockStyle.Fill;
        _folderTree.HideSelection = false;
        _folderTree.FullRowSelect = true;
        _folderTree.ShowNodeToolTips = true;
        _folderTree.AfterSelect += FolderTreeAfterSelect;
        folderPanel.Controls.Add(_folderTree, 0, 1);
        split.Panel1.Controls.Add(folderPanel);

        var filePreviewPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        filePreviewPanel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        filePreviewPanel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        _previewFilter.Dock = DockStyle.Top;
        _previewFilter.Margin = new Padding(0, 0, 0, 6);
        _previewFilter.TextChanged += (_, _) => RefreshPreview();
        filePreviewPanel.Controls.Add(_previewFilter, 0, 0);

        _previewGrid.Dock = DockStyle.Fill;
        _previewGrid.ReadOnly = true;
        _previewGrid.AllowUserToAddRows = false;
        _previewGrid.AllowUserToDeleteRows = false;
        _previewGrid.RowHeadersVisible = false;
        _previewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _previewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _previewGrid.CellClick += PreviewGridCellClick;
        _previewGrid.Columns.Add("Included", "対象");
        _previewGrid.Columns.Add("Members", "メンバー");
        _previewGrid.Columns.Add("Path", "パス");
        _previewGrid.Columns.Add("Reason", "理由");
        _previewGrid.Columns["Included"]!.FillWeight = 10;
        _previewGrid.Columns["Members"]!.FillWeight = 14;
        _previewGrid.Columns["Path"]!.FillWeight = 82;
        _previewGrid.Columns["Reason"]!.FillWeight = 40;
        filePreviewPanel.Controls.Add(_previewGrid, 0, 1);
        split.Panel2.Controls.Add(filePreviewPanel);
        panel.Controls.Add(split, 0, 1);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var logPage = new TabPage("ログ");
        _log.Dock = DockStyle.Fill;
        logPage.Controls.Add(_log);
        tabs.TabPages.Add(logPage);
        return tabs;
    }

    private Button AddPathRow(
        TableLayoutPanel panel,
        string label,
        TextBox textBox,
        string buttonText,
        Func<Task>? click,
        string? secondButtonText,
        Func<Task>? secondClick)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        textBox.Dock = DockStyle.Fill;
        panel.Controls.Add(textBox, 1, row);
        var browse = new Button { Text = buttonText, Dock = DockStyle.Fill };
        if (click is not null)
        {
            browse.Click += async (_, _) => await click();
        }

        panel.Controls.Add(browse, 2, row);
        if (secondButtonText is not null)
        {
            var second = new Button { Text = secondButtonText, Dock = DockStyle.Fill };
            if (secondClick is not null)
            {
                second.Click += async (_, _) => await secondClick();
            }

            panel.Controls.Add(second, 3, row);
        }

        return browse;
    }

    private void AddTextRow(TableLayoutPanel panel, string label, TextBox textBox)
    {
        var row = panel.RowCount++;
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        textBox.Dock = DockStyle.Fill;
        panel.Controls.Add(textBox, 1, row);
        panel.SetColumnSpan(textBox, 3);
        textBox.TextChanged += (_, _) => RefreshPreview();
    }

    private void AddScopeRow(TableLayoutPanel panel)
    {
        var row = panel.RowCount++;
        var group = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill, AutoSize = true };
        group.Controls.Add(_projectScope);
        group.Controls.Add(_folderScope);
        group.Controls.Add(_fileScope);
        _projectScope.CheckedChanged += (_, _) => ScopeInputChanged();
        _folderScope.CheckedChanged += (_, _) => ScopeInputChanged();
        _fileScope.CheckedChanged += (_, _) => ScopeInputChanged();

        panel.Controls.Add(new Label { Text = "対象範囲", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        panel.Controls.Add(group, 1, row);
        panel.SetColumnSpan(group, 3);
    }

    private async Task BrowseProjectAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "C# project (*.csproj)|*.csproj", Title = ".csprojを選択" };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _projectPath.Text = dialog.FileName;
        if (string.IsNullOrWhiteSpace(_outputDirectory.Text))
        {
            _outputDirectory.Text = Path.Combine(Path.GetDirectoryName(dialog.FileName)!, "coverage-report");
        }

        if (string.IsNullOrWhiteSpace(_reportTitle.Text))
        {
            _reportTitle.Text = $"{Path.GetFileNameWithoutExtension(dialog.FileName)} カバレッジレポート";
        }

        await LoadProjectAsync();
    }

    private async Task ReloadProjectAsync()
    {
        await LoadProjectAsync();
    }

    private async Task LoadProjectAsync()
    {
        if (!Path.GetExtension(_projectPath.Text).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            Log(".csprojファイルを選択してから読み込んでください。");
            return;
        }

        if (!File.Exists(_projectPath.Text))
        {
            Log("プロジェクトファイルが見つかりません。");
            return;
        }

        try
        {
            SetBusy(true);
            Log("プロジェクトを読み込み中");
            var analyzer = new ProjectAnalyzer();
            _analysis = await analyzer.AnalyzeAsync(_projectPath.Text, new Progress<ProjectAnalysisProgress>(item => Log(item.Message)));
            _projectStatus.Text = $"プロジェクト: {_analysis.ProjectName} · ファイル: {_analysis.SourceFiles.Count} · メンバー: {_analysis.Members.Count} · キャッシュ: {_analysis.CacheStatus}";
            Log($"プロジェクトを読み込みました。キャッシュ: {_analysis.CacheStatus}");
            PopulateFolderTree();
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Log($"エラー: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task LoadSavedProjectIfAvailableAsync()
    {
        if (File.Exists(_projectPath.Text) && Path.GetExtension(_projectPath.Text).Equals(".csproj", StringComparison.OrdinalIgnoreCase))
        {
            await LoadProjectAsync();
        }
    }

    private Task BrowseXml()
    {
        using var dialog = new OpenFileDialog { Filter = "XMLファイル (*.xml)|*.xml|すべてのファイル (*.*)|*.*", Title = "DotCover XMLを選択" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _xmlPath.Text = dialog.FileName;
        }

        return Task.CompletedTask;
    }

    private Task BrowseScope()
    {
        if (_fileScope.Checked)
        {
            using var dialog = new OpenFileDialog { Filter = "ソースファイル (*.cs;*.cshtml)|*.cs;*.cshtml|すべてのファイル (*.*)|*.*", Title = "対象ソースファイルを選択" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                ApplyScopeSelection(CoverageScopeType.File, dialog.FileName);
            }
        }
        else
        {
            using var dialog = new FolderBrowserDialog { Description = "対象フォルダを選択" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                ApplyScopeSelection(CoverageScopeType.Folder, dialog.SelectedPath);
            }
        }
        return Task.CompletedTask;
    }

    private Task BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog { Description = "出力先フォルダを選択" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputDirectory.Text = dialog.SelectedPath;
        }

        return Task.CompletedTask;
    }

    private void ScopeInputChanged()
    {
        if (_syncingFolderTree || _syncingScope)
        {
            return;
        }

        if (_projectScope.Checked)
        {
            ApplyScopeSelection(CoverageScopeType.Project, null);
            return;
        }

        UpdateScopePathState();
        SelectFolderTreeNodeForCurrentScope();
        RefreshPreview();
    }

    private void PreviewGridCellClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || _previewGrid.Rows[e.RowIndex].Tag is not SourceFile file)
        {
            return;
        }

        ApplyScopeSelection(CoverageScopeType.File, file.FullPath, true);
    }

    private void FolderTreeAfterSelect(object? sender, TreeViewEventArgs e)
    {
        if (_syncingFolderTree || e.Node?.Tag is not ProjectFolderNode folder)
        {
            return;
        }

        if (string.IsNullOrEmpty(folder.RelativePath))
        {
            ApplyScopeSelection(CoverageScopeType.Project, null);
            return;
        }

        ApplyScopeSelection(CoverageScopeType.Folder, folder.FullPath);
    }

    private void ApplyScopeSelection(CoverageScopeType scopeType, string? scopePath, bool preservePreviewPosition = false)
    {
        _syncingScope = true;
        try
        {
            _projectScope.Checked = scopeType == CoverageScopeType.Project;
            _folderScope.Checked = scopeType == CoverageScopeType.Folder;
            _fileScope.Checked = scopeType == CoverageScopeType.File;
            _scopePath.Text = scopeType == CoverageScopeType.Project ? string.Empty : scopePath ?? string.Empty;
            UpdateScopePathState();
        }
        finally
        {
            _syncingScope = false;
        }

        SelectFolderTreeNodeForCurrentScope();
        RefreshPreview(preservePreviewPosition, scopeType == CoverageScopeType.File ? scopePath : null);
    }

    private void UpdateScopePathState()
    {
        var projectScope = CurrentScopeType() == CoverageScopeType.Project;
        _scopePath.Enabled = !projectScope;
        if (_scopeBrowseButton is not null)
        {
            _scopeBrowseButton.Enabled = !projectScope;
        }
    }

    private void PopulateFolderTree()
    {
        _folderTree.BeginUpdate();
        _syncingFolderTree = true;
        try
        {
            _folderTree.Nodes.Clear();
            if (_analysis is null)
            {
                return;
            }

            var root = new ProjectFolderTreeBuilder().Build(_analysis);
            var rootNode = CreateFolderTreeNode(root);
            _folderTree.Nodes.Add(rootNode);
            rootNode.Expand();
        }
        finally
        {
            _syncingFolderTree = false;
            _folderTree.EndUpdate();
        }

        SelectFolderTreeNodeForCurrentScope();
    }

    private static TreeNode CreateFolderTreeNode(ProjectFolderNode folder)
    {
        var node = new TreeNode(FolderNodeLabel(folder))
        {
            Tag = folder,
            ToolTipText = string.IsNullOrEmpty(folder.RelativePath) ? "プロジェクト全体" : folder.RelativePath
        };

        foreach (var child in folder.Children)
        {
            node.Nodes.Add(CreateFolderTreeNode(child));
        }

        return node;
    }

    private static string FolderNodeLabel(ProjectFolderNode folder)
    {
        return $"{folder.Name}  ファイル:{folder.SourceFileCount}  メンバー:{folder.MemberCount}";
    }

    private void SelectFolderTreeNodeForCurrentScope()
    {
        if (_syncingFolderTree || _folderTree.Nodes.Count == 0)
        {
            return;
        }

        TreeNode? selected = CurrentScopeType() switch
        {
            CoverageScopeType.Project => _folderTree.Nodes[0],
            CoverageScopeType.Folder when !string.IsNullOrWhiteSpace(_scopePath.Text) => FindFolderTreeNode(_folderTree.Nodes, _scopePath.Text),
            _ => null
        };

        _syncingFolderTree = true;
        try
        {
            _folderTree.SelectedNode = selected;
            selected?.EnsureVisible();
        }
        finally
        {
            _syncingFolderTree = false;
        }
    }

    private static TreeNode? FindFolderTreeNode(TreeNodeCollection nodes, string folderPath)
    {
        foreach (TreeNode node in nodes)
        {
            if (node.Tag is ProjectFolderNode folder && PathsEqual(folder.FullPath, folderPath))
            {
                return node;
            }

            var child = FindFolderTreeNode(node.Nodes, folderPath);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static bool PathsEqual(string left, string right)
    {
        return NormalizePath(left).Equals(NormalizePath(right), StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (ArgumentException)
        {
            return path.Trim();
        }
        catch (NotSupportedException)
        {
            return path.Trim();
        }
    }

    private async Task GenerateAsync()
    {
        try
        {
            SetBusy(true);
            // 生成に失敗しても、直前の入力内容は次回起動時に復元する。
            SaveCurrentSettings();
            var service = new CoverageReportGenerationService();
            var result = await service.GenerateAsync(new CoverageReportGenerationOptions(
                _projectPath.Text,
                _xmlPath.Text,
                _outputDirectory.Text,
                _reportTitle.Text,
                CurrentScopeType(),
                CurrentScopePath(),
                ImplicitIncludePatterns,
                ImplicitExcludePatterns,
                _overwriteExisting.Checked), new Progress<string>(Log));

            Log($"HTMLレポートを生成しました: {result.OutputPath}");
            if (_openAfterGeneration.Checked)
            {
                Process.Start(new ProcessStartInfo(result.OutputPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log($"エラー: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task ExportExcelAsync()
    {
        if (_analysis is null)
        {
            Log("先にプロジェクトを読み込んでください。");
            return;
        }

        if (!File.Exists(_xmlPath.Text))
        {
            Log("DotCover XMLを選択してください。");
            return;
        }

        using var fileDialog = new FileSelectionDialog(_analysis.SourceFiles, _analysis.Members);
        if (fileDialog.ShowDialog(this) != DialogResult.OK || fileDialog.SelectedSourceFiles.Count == 0)
        {
            return;
        }

        var sourceFiles = fileDialog.SelectedSourceFiles.ToList();
        var outputDirectory = string.IsNullOrWhiteSpace(_outputDirectory.Text)
            ? Path.Combine(_analysis.ProjectRoot, "coverage-report")
            : _outputDirectory.Text;
        Directory.CreateDirectory(outputDirectory);

        using var saveDialog = new SaveFileDialog
        {
            Filter = "Excelブック (*.xlsx)|*.xlsx",
            Title = "Excelレポートの保存先を選択",
            FileName = sourceFiles.Count == 1
                ? $"{Path.GetFileNameWithoutExtension(sourceFiles[0].FullPath)}-coverage.xlsx"
                : $"{_analysis.ProjectName}-coverage.xlsx",
            InitialDirectory = outputDirectory,
            OverwritePrompt = true
        };
        if (saveDialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        try
        {
            SetBusy(true);
            SaveCurrentSettings();
            var service = new ExcelReportGenerationService();
            var result = await service.GenerateAsync(new ExcelReportGenerationOptions(
                _projectPath.Text,
                _xmlPath.Text,
                sourceFiles.Select(file => file.FullPath).ToList(),
                saveDialog.FileName,
                ImplicitIncludePatterns,
                ImplicitExcludePatterns), new Progress<string>(Log));

            Log($"Excelレポートを生成しました: {result.OutputPath} ({result.Reports.Count}シート)");
            if (_openAfterGeneration.Checked)
            {
                Process.Start(new ProcessStartInfo(result.OutputPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log($"エラー: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshPreview(bool preserveGridPosition = false, string? preferredFilePath = null)
    {
        if (_analysis is null)
        {
            return;
        }

        var previewState = preserveGridPosition ? CapturePreviewGridState(preferredFilePath) : null;

        var selection = new CoverageSelection(CurrentScopeType(), CurrentScopePath(), ImplicitIncludePatterns, ImplicitExcludePatterns);
        var selected = new CoverageTargetSelector().Select(new ProjectSourceSnapshot(
            _analysis.ProjectPath,
            _analysis.ProjectName,
            _analysis.ProjectRoot,
            _analysis.SourceFiles), selection);
        var selectedSet = selected.IncludedFiles.Select(file => file.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var filteredFiles = _analysis.SourceFiles
            .Where(MatchesPreviewFilter)
            .OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        _previewGrid.SuspendLayout();
        try
        {
            _previewGrid.Rows.Clear();
            foreach (var file in filteredFiles)
            {
                var included = selectedSet.Contains(file.FullPath);
                var members = _analysis.Members.Count(member => member.FilePath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase));
                var rowIndex = _previewGrid.Rows.Add(included ? "○" : "×", members, file.RelativePath, included ? "現在の対象範囲に含まれます" : "対象範囲外です");
                _previewGrid.Rows[rowIndex].Tag = file;
            }

            if (previewState is not null)
            {
                RestorePreviewGridState(previewState);
            }
        }
        finally
        {
            _previewGrid.ResumeLayout();
        }

        _previewStatus.Text = $"対象プレビュー · 対象ファイル: {selected.IncludedFiles.Count} / {_analysis.SourceFiles.Count} · 表示: {filteredFiles.Count} · メンバー: {_analysis.Members.Count}";
    }

    private PreviewGridState CapturePreviewGridState(string? preferredFilePath)
    {
        var selectedPath = preferredFilePath;
        if (string.IsNullOrWhiteSpace(selectedPath) && _previewGrid.CurrentRow?.Tag is SourceFile currentFile)
        {
            selectedPath = currentFile.FullPath;
        }

        var firstDisplayedRowIndex = -1;
        if (_previewGrid.Rows.Count > 0)
        {
            try
            {
                firstDisplayedRowIndex = _previewGrid.FirstDisplayedScrollingRowIndex;
            }
            catch (InvalidOperationException)
            {
                firstDisplayedRowIndex = -1;
            }
        }

        return new PreviewGridState(selectedPath, firstDisplayedRowIndex);
    }

    private void RestorePreviewGridState(PreviewGridState state)
    {
        if (_previewGrid.Rows.Count == 0)
        {
            return;
        }

        DataGridViewRow? selectedRow = null;
        if (!string.IsNullOrWhiteSpace(state.SelectedFilePath))
        {
            selectedRow = _previewGrid.Rows
                .Cast<DataGridViewRow>()
                .FirstOrDefault(row => row.Tag is SourceFile file
                    && file.FullPath.Equals(state.SelectedFilePath, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedRow is not null)
        {
            _previewGrid.ClearSelection();
            selectedRow.Selected = true;
            _previewGrid.CurrentCell = selectedRow.Cells[0];
        }

        if (state.FirstDisplayedRowIndex < 0)
        {
            return;
        }

        var firstDisplayedRowIndex = Math.Min(state.FirstDisplayedRowIndex, _previewGrid.Rows.Count - 1);
        try
        {
            _previewGrid.FirstDisplayedScrollingRowIndex = firstDisplayedRowIndex;
        }
        catch (InvalidOperationException)
        {
        }
    }

    private bool MatchesPreviewFilter(SourceFile file)
    {
        var words = _previewFilter.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return true;
        }

        return words.All(word =>
            file.RelativePath.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            file.FullPath.Contains(word, StringComparison.OrdinalIgnoreCase) ||
            file.Extension.Contains(word, StringComparison.OrdinalIgnoreCase));
    }

    private CoverageScopeType CurrentScopeType()
    {
        if (_folderScope.Checked)
        {
            return CoverageScopeType.Folder;
        }

        return _fileScope.Checked ? CoverageScopeType.File : CoverageScopeType.Project;
    }

    private string? CurrentScopePath()
    {
        return CurrentScopeType() == CoverageScopeType.Project ? null : _scopePath.Text;
    }

    private sealed record PreviewGridState(string? SelectedFilePath, int FirstDisplayedRowIndex);

    private void SetBusy(bool busy)
    {
        _generateButton.Enabled = !busy;
        _excelButton.Enabled = !busy;
        Cursor = busy ? Cursors.WaitCursor : Cursors.Default;
    }

    private void Log(string message)
    {
        if (InvokeRequired)
        {
            BeginInvoke(new Action<string>(Log), message);
            return;
        }

        _log.AppendText($"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");
        _log.SelectionStart = _log.TextLength;
        _log.ScrollToCaret();
    }

    private void ApplySettings(AppSettings settings)
    {
        _projectPath.Text = settings.ProjectPath;
        _xmlPath.Text = settings.DotCoverXmlPath;
        _outputDirectory.Text = settings.OutputDirectory;
        _reportTitle.Text = settings.ReportTitle;
        _openAfterGeneration.Checked = settings.OpenAfterGeneration;
        _overwriteExisting.Checked = settings.OverwriteExisting;
        ApplyScopeSelection(settings.ScopeType, settings.ScopePath);
    }

    private AppSettings CaptureSettings()
    {
        return new AppSettings(
            _projectPath.Text,
            _xmlPath.Text,
            CurrentScopePath() ?? string.Empty,
            ImplicitIncludePatterns,
            ImplicitExcludePatterns,
            _outputDirectory.Text,
            _reportTitle.Text,
            CurrentScopeType(),
            _openAfterGeneration.Checked,
            _overwriteExisting.Checked);
    }

    private void SaveCurrentSettings()
    {
        try
        {
            _settingsService.Save(CaptureSettings());
        }
        catch (Exception ex)
        {
            Log($"設定を保存できませんでした: {ex.Message}");
        }
    }

    private void ResetToDefaultSettings()
    {
        try
        {
            _settingsService.Reset();
        }
        catch (Exception ex)
        {
            Log($"設定を初期化できませんでした: {ex.Message}");
        }

        // 保存済み設定と画面上の解析状態を同時に初期化する。
        _analysis = null;
        ApplySettings(AppSettings.Defaults);
        _projectStatus.Text = "プロジェクト: 未読込";
        _previewStatus.Text = "対象プレビュー";
        _previewFilter.Clear();
        _folderTree.Nodes.Clear();
        _previewGrid.Rows.Clear();
        _log.Clear();
    }
}
