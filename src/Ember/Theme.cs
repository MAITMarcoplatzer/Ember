using Microsoft.Win32;
using Color = System.Windows.Media.Color;

namespace Ember;

public sealed record Palette(
    Color Background,
    Color Text,
    Color TextMuted,
    Color Surface,
    Color Border)
{
    public static readonly Color Accent = Color.FromRgb(0xD8, 0x5A, 0x30);
    public static readonly Color Bar = Color.FromRgb(0xF0, 0x99, 0x7B);
}

public static class Theme
{
    public static bool IsLight()
    {
        using var key = Registry.CurrentUser.OpenSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is not int v || v != 0;
    }

    public static Palette Current() => IsLight()
        ? new Palette(
            Background: Color.FromRgb(0xF9, 0xF9, 0xF9),
            Text: Color.FromRgb(0x1A, 0x1A, 0x1A),
            TextMuted: Color.FromRgb(0x5F, 0x5E, 0x5A),
            Surface: Color.FromRgb(0xEC, 0xEC, 0xEC),
            Border: Color.FromRgb(0xD8, 0xD8, 0xD8))
        : new Palette(
            Background: Color.FromRgb(0x2B, 0x2B, 0x2B),
            Text: Color.FromRgb(0xF5, 0xF5, 0xF5),
            TextMuted: Color.FromRgb(0xB4, 0xB2, 0xA9),
            Surface: Color.FromRgb(0x3A, 0x3A, 0x3A),
            Border: Color.FromRgb(0x4A, 0x4A, 0x4A));
}
