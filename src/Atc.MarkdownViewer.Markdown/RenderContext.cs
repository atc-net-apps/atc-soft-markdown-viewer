namespace Atc.MarkdownViewer.Markdown;

/// <summary>
/// Describes where a document lives, so relative links and images can be resolved (plan MD-004).
/// </summary>
/// <param name="DocumentDirectory">The folder of the document, or <see langword="null"/> for stdin.</param>
/// <param name="AssetRoot">The folder served to the web view at <paramref name="AssetBaseUrl"/>. Images outside it are not loaded.</param>
/// <param name="AssetBaseUrl">The URL that <paramref name="AssetRoot"/> is served under.</param>
/// <param name="LoadRemoteImages">Whether <c>http</c> and <c>https</c> images are loaded.</param>
/// <param name="FileExists">Checks whether a resolved local path exists.</param>
public sealed record RenderContext(
    string? DocumentDirectory,
    string? AssetRoot,
    Uri AssetBaseUrl,
    bool LoadRemoteImages,
    Func<string, bool> FileExists)
{
    /// <summary>
    /// Gets the default asset URL that the host maps to <see cref="AssetRoot"/>.
    /// </summary>
    public static Uri DefaultAssetBaseUrl { get; } = new("https://doc.mdv/");

    /// <summary>
    /// Creates a context for a document file, using its folder as the asset root.
    /// </summary>
    /// <param name="documentPath">The full path of the document.</param>
    /// <returns>The context.</returns>
    public static RenderContext ForFile(string documentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(documentPath);
        var directory = Path.GetDirectoryName(Path.GetFullPath(documentPath));
        return new RenderContext(directory, directory, DefaultAssetBaseUrl, LoadRemoteImages: true, File.Exists);
    }

    /// <summary>
    /// Creates a context for text without a file, such as stdin. Relative links are left untouched.
    /// </summary>
    /// <returns>The context.</returns>
    public static RenderContext ForText()
        => new(DocumentDirectory: null, AssetRoot: null, DefaultAssetBaseUrl, LoadRemoteImages: true, _ => false);
}