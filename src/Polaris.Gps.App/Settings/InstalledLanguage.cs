using System.IO;
using System.Security;
using Microsoft.Win32;

namespace Polaris.Gps.App.Settings;

/// <summary>
/// Language chosen in the installer, saved in HKLM\SOFTWARE\Mendikat\Polaris GPS, value "Language". It is the
/// interface language the first time a user starts the application; from then on Settings decides.
/// </summary>
public static class InstalledLanguage
{
    public const string RegistryKey = @"SOFTWARE\Mendikat\Polaris GPS";
    public const string RegistryValue = "Language";

    /// <summary>The installer's language code, or null when there is none (zip copy) or it is not a known language.</summary>
    public static string? Read()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(RegistryKey);
            return Validate(key?.GetValue(RegistryValue) as string);
        }
        catch (Exception ex) when (ex is SecurityException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>The code if it is one of <see cref="AppLanguage.All"/>, in its canonical form; otherwise null.</summary>
    public static string? Validate(string? code) =>
        AppLanguage.All.FirstOrDefault(l => string.Equals(l.Code, code?.Trim(), StringComparison.OrdinalIgnoreCase))?.Code;
}
