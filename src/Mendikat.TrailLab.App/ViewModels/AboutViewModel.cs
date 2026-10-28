using System.Reflection;
using Mendikat.TrailLab.App.Resources;

namespace Mendikat.TrailLab.App.ViewModels;

/// <summary>"About" window (APP-09): version, author and links. The version comes from the assembly (Directory.Build.props).</summary>
public sealed class AboutViewModel
{
    public const string Author = "Javier Urrutia";

    public const string AuthorEmail = "javi@mendikat.net";

    public const string WebUrl = "https://www.mendikat.net";

    /// <summary>Where to look for new versions. Provisional: the mendikat.net home page until the download page exists.</summary>
    public const string UpdatesUrl = WebUrl;

    /// <summary>PayPal donation button of mendikat.net (same <c>hosted_button_id</c> as the web form).</summary>
    public const string DonateUrl = "https://www.paypal.com/donate/?hosted_button_id=4BNLU9ACSHTFN";

    public string ProductName => Strings.AppTitle;

    /// <summary>"Versión 0.0.6". The build may append "+commit" to the informational version; it is not shown.</summary>
    public string VersionText => string.Format(Strings.AboutVersion, Version);

    public static string Version
    {
        get
        {
            var informational = typeof(AboutViewModel).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            var plus = informational.IndexOf('+');
            return plus < 0 ? informational : informational[..plus];
        }
    }

    public string AuthorText => string.Format(Strings.AboutAuthor, Author);

    public string AuthorEmailUri => $"mailto:{AuthorEmail}";

    public string AuthorEmailText => AuthorEmail;

    public string WebText => "www.mendikat.net";

    public string Web => WebUrl;
}
