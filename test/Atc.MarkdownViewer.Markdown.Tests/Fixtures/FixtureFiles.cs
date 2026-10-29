namespace Atc.MarkdownViewer.Markdown.Tests.Fixtures;

/// <summary>
/// Locates the GFM fixtures and their snapshots. Fixtures are read with LF line endings, so results do not
/// depend on how git checked them out.
/// </summary>
public static class FixtureFiles
{
    public static string GfmDirectory
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "gfm");

    public static string SpecFile
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", "spec", "gfm-spec.txt");

    public static IReadOnlyList<string> GfmNames
        => Directory.GetFiles(GfmDirectory, "*.md", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileNameWithoutExtension)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();

    public static TheoryData<string> GfmNamesData => new(GfmNames);

    public static string GfmPath(string name)
        => Path.Combine(GfmDirectory, name + ".md");

    public static string ReadGfm(string name)
        => File.ReadAllText(GfmPath(name)).ReplaceLineEndings("\n");

    /// <summary>
    /// Gets the snapshot folder in the source tree, so <c>UPDATE_SNAPSHOTS=1</c> writes where git sees it.
    /// </summary>
    public static string SourceSnapshotDirectory
        => Path.Combine(ProjectDirectory(), "Snapshots");

    /// <summary>
    /// Gets the spec folder in the source tree, for the generated Markdig-versus-GitHub report.
    /// </summary>
    public static string SourceSpecDirectory
        => Path.Combine(ProjectDirectory(), "Fixtures", "spec");

    /// <summary>
    /// Gets the test project folder. Called only from this file, so the compiler fills in this file's path
    /// (<c>Fixtures/FixtureFiles.cs</c>), not the calling test's.
    /// </summary>
    private static string ProjectDirectory(
        [CallerFilePath] string thisFilePath = "")
        => Path.GetDirectoryName(Path.GetDirectoryName(thisFilePath)!)!;

    /// <summary>
    /// Gets a context that resolves links against the GFM fixture folder.
    /// </summary>
    public static RenderContext GfmContext
        => new(GfmDirectory, GfmDirectory, RenderContext.DefaultAssetBaseUrl, LoadRemoteImages: true, File.Exists);
}