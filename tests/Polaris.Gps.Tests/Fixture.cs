namespace Polaris.Gps.Tests;

/// <summary>Access to the test files under Fixtures/ (copied to the output directory).</summary>
internal static class Fixture
{
    public static string PathOf(string relativePath) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath);

    public static FileStream Open(string relativePath) => File.OpenRead(PathOf(relativePath));

    /// <summary>Repository root (folder containing the .slnx), for source-code checks.</summary>
    public static string RepositoryRoot() => FindRoot(AppContext.BaseDirectory) ?? FindRoot(SourceDirectory())
        ?? throw new InvalidOperationException("Repository root not found.");

    /// <summary>Folder of this source file (known at compile time), for runs from outside the repository.</summary>
    private static string SourceDirectory([System.Runtime.CompilerServices.CallerFilePath] string path = "") =>
        System.IO.Path.GetDirectoryName(path)!;

    private static string? FindRoot(string start)
    {
        var directory = new DirectoryInfo(start);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName;
    }
}
