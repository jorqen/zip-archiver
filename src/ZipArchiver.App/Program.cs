namespace ZipArchiver.App;

internal static class Program
{
    /// <summary>Главная точка входа приложения.</summary>
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
