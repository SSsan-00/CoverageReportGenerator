namespace CoverageReportGenerator.WinForms;

/// <summary>
/// WinFormsアプリのエントリポイント。
/// </summary>
static class Program
{
    /// <summary>
    /// アプリケーションを起動する。
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
