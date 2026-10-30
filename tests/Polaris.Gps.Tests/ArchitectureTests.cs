using Polaris.Gps.Domain.Documents;

namespace Polaris.Gps.Tests;

public sealed class ArchitectureTests
{
    private static readonly string[] UiAssemblies =
        ["PresentationFramework", "PresentationCore", "WindowsBase", "System.Xaml", "System.Windows.Forms"];

    [Fact]
    public void Domain_does_not_reference_any_ui_framework()
    {
        var references = typeof(TrailDocument).Assembly.GetReferencedAssemblies().Select(a => a.Name);

        Assert.DoesNotContain(references, name => UiAssemblies.Contains(name));
    }
}
