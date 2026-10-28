using System.Globalization;

namespace Mendikat.TrailLab.App.Settings;

/// <summary>A language of the user interface. Texts come from Strings.resx (Spanish) and Strings.{Code}.resx.</summary>
public sealed record AppLanguage(string Code, string NativeName, string CultureName)
{
    public const string DefaultCode = "es";

    /// <summary>Spanish first: it is the default and the language of any text not yet translated.</summary>
    public static IReadOnlyList<AppLanguage> All { get; } =
    [
        new("es", "Español", "es-ES"),
        new("ca", "Català", "ca-ES"),
        new("eu", "Euskara", "eu-ES"),
        new("fr", "Français", "fr-FR"),
        new("pt", "Português", "pt-PT"),
    ];

    /// <summary>The language with that code, or Spanish for an unknown or missing one.</summary>
    public static AppLanguage FromCode(string? code) =>
        All.FirstOrDefault(l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase)) ?? All[0];

    public CultureInfo Culture => CultureInfo.GetCultureInfo(CultureName);
}
