using System;
using System.IO;
using Avalonia;

namespace OmniDrive.App;

internal static class Program
{
    private static readonly string LogDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "OmniDrive");
    private static readonly string LogPath = Path.Combine(LogDirectory, "startup.log");

    [STAThread]
    public static void Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (_, eventArgs) =>
            LogException("Unhandled exception", eventArgs.ExceptionObject as Exception);

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogException("Fatal startup exception", ex);
            ShowStartupError(ex);
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();

    private static void LogException(string context, Exception? exception)
    {
        try
        {
            Directory.CreateDirectory(LogDirectory);
            File.AppendAllText(LogPath,
                $"[{DateTimeOffset.Now:O}] {context}{Environment.NewLine}{exception}{Environment.NewLine}{Environment.NewLine}");
        }
        catch
        {
            // Logging must not cause a second startup failure.
        }
    }

    private static void ShowStartupError(Exception exception)
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                var message = "O OmniDrive encontrou um erro ao iniciar.\n\n" +
                              exception.GetType().Name + ": " + exception.Message +
                              "\n\nO detalhe foi salvo em: " + LogPath;
                MessageBoxW(IntPtr.Zero, message, "Erro ao iniciar o OmniDrive", 0x10);
            }
        }
        catch
        {
            // The log file remains the fallback if a native dialog cannot be shown.
        }
    }

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);
}