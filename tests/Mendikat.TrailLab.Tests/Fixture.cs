namespace Mendikat.TrailLab.Tests;

/// <summary>Access to the test files under Fixtures/ (copied to the output directory).</summary>
internal static class Fixture
{
    public static string PathOf(string relativePath) =>
        System.IO.Path.Combine(AppContext.BaseDirectory, "Fixtures", relativePath);

    public static FileStream Open(string relativePath) => File.OpenRead(PathOf(relativePath));

    /// <summary>Repository root (folder containing the .slnx), for source-code checks.</summary>
    public static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && directory.GetFiles("*.slnx").Length == 0)
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root not found.");
    }
}
