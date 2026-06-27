namespace MediaPlayer;

/// <summary>
/// 应用程序入口点
/// </summary>
internal static class Program
{
    [STAThread]
    static void Main()
    {
        // 启用Windows视觉样式，提供现代化的UI外观
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        // 配置应用程序的高DPI支持，确保在不同分辨率显示器上正确缩放
        Application.SetHighDpiMode(HighDpiMode.SystemAware);

        Application.Run(new MainForm());
    }
}
