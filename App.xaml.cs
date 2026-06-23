using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;

namespace shiftUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "shiftUI", "error.log");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 全局 UI 线程异常处理
        DispatcherUnhandledException += (s, args) =>
        {
            LogException("UI 线程未处理异常", args.Exception);
            MessageBox.Show(
                $"程序发生错误：\n{args.Exception.Message}\n\n详细信息已写入：\n{LogPath}",
                "ShiftUI 错误",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            args.Handled = true;
        };

        // 后台线程异常处理
        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                LogException("后台线程未处理异常", ex);
            }
        };

        // 任务异常处理
        TaskScheduler.UnobservedTaskException += (s, args) =>
        {
            LogException("未观察到的任务异常", args.Exception);
            args.SetObserved();
        };
    }

    private static void LogException(string context, Exception ex)
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (dir != null) Directory.CreateDirectory(dir);

            var log = new System.Text.StringBuilder();
            log.AppendLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {context}");

            // 递归记录所有 InnerException
            int level = 0;
            Exception? current = ex;
            while (current != null)
            {
                log.AppendLine($"  [{level}] 类型: {current.GetType().FullName}");
                log.AppendLine($"  [{level}] 消息: {current.Message}");
                log.AppendLine($"  [{level}] 堆栈:\n{current.StackTrace}");
                current = current.InnerException;
                level++;
            }

            log.AppendLine("---");
            File.AppendAllText(LogPath, log.ToString());
        }
        catch
        {
            Debug.WriteLine($"无法写入日志文件: {LogPath}");
        }
    }
}

