using System.Text;

namespace MapDescShow;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException += (_, e) => ReportCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            ReportCrash(e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString()));
        Application.Run(new MainForm());
    }

    private static void ReportCrash(Exception exception)
    {
        string? logPath = null;
        try
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "MapDescShow", "Logs");
            Directory.CreateDirectory(directory);
            logPath = Path.Combine(directory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss-fff}.log");
            File.WriteAllText(logPath, exception.ToString(), Encoding.UTF8);
        }
        catch
        {
            // 日志失败时仍显示原始错误，不让报告过程再次引发异常。
        }

        string detail = logPath is null ? "" : $"\n\n错误日志：{logPath}";
        MessageBox.Show($"程序发生错误：\n{exception.Message}{detail}",
            "MapDesc 地图标注编辑器", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
