using System.IO;
using Microsoft.Win32;

namespace Ember;

public static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Ember";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string;
    }

    public static void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey);
        if (enabled)
            key.SetValue(ValueName, LaunchCommand());
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string LaunchCommand()
    {
        var host = Environment.ProcessPath ?? "Ember.exe";
        if (!Path.GetFileName(host).Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return $"\"{host}\"";

        // Lauf über den .NET-Host (z. B. wegen ASR-Richtlinie): dotnet + dll registrieren
        var dll = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        return dll is { Length: > 0 } ? $"\"{host}\" \"{dll}\"" : $"\"{host}\"";
    }
}
