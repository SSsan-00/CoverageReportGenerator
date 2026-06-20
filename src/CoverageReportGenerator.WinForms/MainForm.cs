using System.Diagnostics;
using CoverageReportGenerator.Core.Projects;
using CoverageReportGenerator.Core.Reports;
using CoverageReportGenerator.Core.Settings;

namespace CoverageReportGenerator.WinForms;

public sealed class MainForm : Form
{
    private readonly AppSettingsService _settingsService = new();
    private readonly TextBox _projectPath = new() { ReadOnly = true };
    private readonly TextBox _xmlPath = new();
    private readonly TextBox _scopePath = new();
    private readonly TextBox _includePatterns = new() { Text = "*.cs;*.cshtml" };
    private readonly TextBox _excludePatterns = new() { Text = "*.g.cs;*.generated.cs;*.Designer.cs;bin;obj" };
    private readonly TextBox _outputDirectory = new();
    private readonly TextBox _reportTitle = new();
    private readonly RadioButton _projectScope = new() { Text = "Project", Checked = true };
    private readonly RadioButton _folderScope = new() { Text = "Folder" };
    private readonly RadioButton _fileScope = new() { Text = "File" };
    private readonly CheckBox _openAfterGeneration = new() { Text = "Open report after generation", Checked = true, AutoSize = true };
    private readonly CheckBox _overwriteExisting = new() { Text = "Overwrite existing report", AutoSize = true };
    private readonly Label _projectStatus = new() { AutoSize = true, Text = "Project: not loaded" };
    private readonly Label _previewStatus = new() { AutoSize = true, Text = "Target Preview" };
    private readonly DataGridView _previewGrid = new();
    private readonly RichTextBox _log = new() { ReadOnly = true, BorderStyle = BorderStyle.None };
    private readonly Button _resetButton = new() { Text = "Reset", Height = 36 };
    private readonly Button _generateButton = new() { Text = "Generate Report", Height = 36 };

    private ProjectAnalysis? _analysis;

    public MainForm()
    {
        Text = "Coverage Report Generator";
        MinimumSize = new Size(1100, 760);
        Width = 1180;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;

        BuildLayout();
        ApplySettings(_settingsService.Load());
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

        AddPathRow(panel, "Project", _projectPath, "Browse", BrowseProjectAsync, "Reload", ReloadProjectAsync);
        panel.Controls.Add(_projectStatus, 1, panel.RowCount++);
        AddPathRow(panel, "DotCover XML", _xmlPath, "Browse", BrowseXml, null, null);
        AddScopeRow(panel);
        AddPathRow(panel, "Scope path", _scopePath, "Browse", BrowseScope, null, null);
        AddTextRow(panel, "Include", _includePatterns);
        AddTextRow(panel, "Exclude", _excludePatterns);
        AddPathRow(panel, "Output", _outputDirectory, "Browse", BrowseOutput, null, null);
        AddTextRow(panel, "Report title", _reportTitle);

        var options = new FlowLayoutPanel { FlowDirection = FlowDirection.LeftToRight, Dock = DockStyle.Fill, AutoSize = true };
        options.Controls.Add(_openAfterGeneration);
        options.Controls.Add(_overwriteExisting);
        panel.Controls.Add(new Label(), 0, panel.RowCount);
        panel.Controls.Add(options, 1, panel.RowCount);
        _resetButton.Click += (_, _) => ResetToDefaultSettings();
        panel.Controls.Add(_resetButton, 2, panel.RowCount);
        _generateButton.Click += async (_, _) => await GenerateAsync();
        panel.Controls.Add(_generateButton, 3, panel.RowCount++);

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

        _previewGrid.Dock = DockStyle.Fill;
        _previewGrid.ReadOnly = true;
        _previewGrid.AllowUserToAddRows = false;
        _previewGrid.AllowUserToDeleteRows = false;
        _previewGrid.RowHeadersVisible = false;
        _previewGrid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _previewGrid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _previewGrid.Columns.Add("Included", "Included");
        _previewGrid.Columns.Add("Coverage", "Coverage");
        _previewGrid.Columns.Add("Members", "Members");
        _previewGrid.Columns.Add("Path", "Path");
        _previewGrid.Columns.Add("Reason", "Reason");
        _previewGrid.Columns["Included"]!.FillWeight = 14;
        _previewGrid.Columns["Coverage"]!.FillWeight = 18;
        _previewGrid.Columns["Members"]!.FillWeight = 14;
        _previewGrid.Columns["Path"]!.FillWeight = 74;
        _previewGrid.Columns["Reason"]!.FillWeight = 40;
        panel.Controls.Add(_previewGrid, 0, 1);
        return panel;
    }

    private Control BuildLogPanel()
    {
        var tabs = new TabControl { Dock = DockStyle.Fill };
        var logPage = new TabPage("Log");
        _log.Dock = DockStyle.Fill;
        logPage.Controls.Add(_log);
        tabs.TabPages.Add(logPage);
        return tabs;
    }

    private void AddPathRow(
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
        _projectScope.CheckedChanged += (_, _) => RefreshPreview();
        _folderScope.CheckedChanged += (_, _) => RefreshPreview();
        _fileScope.CheckedChanged += (_, _) => RefreshPreview();

        panel.Controls.Add(new Label { Text = "Scope", AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        panel.Controls.Add(group, 1, row);
        panel.SetColumnSpan(group, 3);
    }

    private async Task BrowseProjectAsync()
    {
        using var dialog = new OpenFileDialog { Filter = "C# project (*.csproj)|*.csproj", Title = "Select .csproj" };
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
            _reportTitle.Text = $"{Path.GetFileNameWithoutExtension(dialog.FileName)} Coverage Report";
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
            Log("Select a .csproj file before loading the project.");
            return;
        }

        if (!File.Exists(_projectPath.Text))
        {
            Log("Project file was not found.");
            return;
        }

        try
        {
            SetBusy(true);
            Log("Loading project");
            var analyzer = new ProjectAnalyzer();
            _analysis = await analyzer.AnalyzeAsync(_projectPath.Text, new Progress<ProjectAnalysisProgress>(item => Log(item.Message)));
            _projectStatus.Text = $"Project: {_analysis.ProjectName} · Files: {_analysis.SourceFiles.Count} · Members: {_analysis.Members.Count} · Cache: {_analysis.CacheStatus}";
            Log($"Project loaded. Cache: {_analysis.CacheStatus}");
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
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
        using var dialog = new OpenFileDialog { Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*", Title = "Select DotCover XML" };
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
            using var dialog = new OpenFileDialog { Filter = "Source files (*.cs;*.cshtml)|*.cs;*.cshtml|All files (*.*)|*.*", Title = "Select source file" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _scopePath.Text = dialog.FileName;
            }
        }
        else
        {
            using var dialog = new FolderBrowserDialog { Description = "Select folder scope" };
            if (dialog.ShowDialog(this) == DialogResult.OK)
            {
                _scopePath.Text = dialog.SelectedPath;
                _folderScope.Checked = true;
            }
        }

        RefreshPreview();
        return Task.CompletedTask;
    }

    private Task BrowseOutput()
    {
        using var dialog = new FolderBrowserDialog { Description = "Select output folder" };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _outputDirectory.Text = dialog.SelectedPath;
        }

        return Task.CompletedTask;
    }

    private async Task GenerateAsync()
    {
        try
        {
            SetBusy(true);
            SaveCurrentSettings();
            var service = new CoverageReportGenerationService();
            var result = await service.GenerateAsync(new CoverageReportGenerationOptions(
                _projectPath.Text,
                _xmlPath.Text,
                _outputDirectory.Text,
                _reportTitle.Text,
                CurrentScopeType(),
                CurrentScopePath(),
                _includePatterns.Text,
                _excludePatterns.Text,
                _overwriteExisting.Checked), new Progress<string>(Log));

            Log($"Report generated: {result.OutputPath}");
            if (_openAfterGeneration.Checked)
            {
                Process.Start(new ProcessStartInfo(result.OutputPath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            Log($"Error: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void RefreshPreview()
    {
        if (_analysis is null)
        {
            return;
        }

        var selection = new CoverageSelection(CurrentScopeType(), CurrentScopePath(), _includePatterns.Text, _excludePatterns.Text);
        var selected = new CoverageTargetSelector().Select(new ProjectSourceSnapshot(
            _analysis.ProjectPath,
            _analysis.ProjectName,
            _analysis.ProjectRoot,
            _analysis.SourceFiles), selection);
        var selectedSet = selected.IncludedFiles.Select(file => file.FullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);

        _previewGrid.Rows.Clear();
        foreach (var file in _analysis.SourceFiles.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var included = selectedSet.Contains(file.FullPath);
            var members = _analysis.Members.Count(member => member.FilePath.Equals(file.FullPath, StringComparison.OrdinalIgnoreCase));
            _previewGrid.Rows.Add(included ? "Yes" : "No", "Pending", members, file.RelativePath, included ? "Included by current scope" : "Filtered out");
        }

        _previewStatus.Text = $"Target Preview · Files: {selected.IncludedFiles.Count} / {_analysis.SourceFiles.Count} · Members: {_analysis.Members.Count}";
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

    private void SetBusy(bool busy)
    {
        _generateButton.Enabled = !busy;
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
        _scopePath.Text = settings.ScopePath;
        _includePatterns.Text = settings.IncludePatterns;
        _excludePatterns.Text = settings.ExcludePatterns;
        _outputDirectory.Text = settings.OutputDirectory;
        _reportTitle.Text = settings.ReportTitle;
        _openAfterGeneration.Checked = settings.OpenAfterGeneration;
        _overwriteExisting.Checked = settings.OverwriteExisting;
        _projectScope.Checked = settings.ScopeType == CoverageScopeType.Project;
        _folderScope.Checked = settings.ScopeType == CoverageScopeType.Folder;
        _fileScope.Checked = settings.ScopeType == CoverageScopeType.File;
    }

    private AppSettings CaptureSettings()
    {
        return new AppSettings(
            _projectPath.Text,
            _xmlPath.Text,
            _scopePath.Text,
            _includePatterns.Text,
            _excludePatterns.Text,
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
            Log($"Settings were not saved: {ex.Message}");
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
            Log($"Settings were not reset: {ex.Message}");
        }

        _analysis = null;
        ApplySettings(AppSettings.Defaults);
        _projectStatus.Text = "Project: not loaded";
        _previewStatus.Text = "Target Preview";
        _previewGrid.Rows.Clear();
        _log.Clear();
    }
}
