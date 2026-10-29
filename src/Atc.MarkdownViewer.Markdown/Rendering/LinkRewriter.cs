namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Resolves links and images against the document (plan MD-004).
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item>Relative images are served from <see cref="RenderContext.AssetBaseUrl"/> when they are inside <see cref="RenderContext.AssetRoot"/>.</item>
/// <item>Relative links get <c>data-href</c> with the original target and <c>href="#"</c>; the host opens them.</item>
/// <item>Targets that do not exist get the class <c>mdv-broken</c>.</item>
/// <item>Only <c>http</c>, <c>https</c> and <c>mailto</c> URLs are kept; other schemes are removed (class <c>mdv-unsafe</c>).</item>
/// </list>
/// </remarks>
internal static class LinkRewriter
{
    public const string BrokenClass = "mdv-broken";
    public const string UnsafeClass = "mdv-unsafe";
    public const string OutsideRootClass = "mdv-outside-root";
    public const string RemoteBlockedClass = "mdv-remote-blocked";

    public static void Rewrite(
        MarkdownDocument document,
        RenderContext context)
    {
        foreach (var node in document.Descendants())
        {
            switch (node)
            {
                case LinkInline link when !string.IsNullOrEmpty(link.Url):
                    link.Url = RewriteUrl(link, link.Url, link.IsImage, context);
                    break;
                case AutolinkInline { IsEmail: false } autolink:
                    autolink.GetAttributes().AddProperty("rel", "noopener noreferrer");
                    break;
            }
        }
    }

    private static string RewriteUrl(
        MarkdownObject node,
        string url,
        bool isImage,
        RenderContext context)
    {
        var attributes = node.GetAttributes();
        if (url.StartsWith('#'))
        {
            return url;
        }

        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute) && !IsRootedLocalPath(url))
        {
            return RewriteAbsolute(attributes, url, absolute, isImage, context);
        }

        if (context.DocumentDirectory is null)
        {
            return url;
        }

        var cut = url.IndexOfAny(['#', '?']);
        var path = Uri.UnescapeDataString(cut < 0 ? url : url[..cut]);
        if (path.Length == 0)
        {
            return url;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(Path.Combine(context.DocumentDirectory, path));
        }
        catch (ArgumentException)
        {
            attributes.AddClass(BrokenClass);
            return url;
        }

        if (!context.FileExists(fullPath))
        {
            attributes.AddClass(BrokenClass);
        }

        if (!isImage)
        {
            attributes.AddProperty("data-href", url);
            return "#";
        }

        if (context.AssetRoot is null || !IsInside(context.AssetRoot, fullPath))
        {
            attributes.AddClass(OutsideRootClass);
            return string.Empty;
        }

        var relative = Path.GetRelativePath(context.AssetRoot, fullPath).Replace('\\', '/');
        var escaped = string.Join('/', relative.Split('/').Select(Uri.EscapeDataString));
        return new Uri(context.AssetBaseUrl, escaped).AbsoluteUri;
    }

    private static string RewriteAbsolute(
        HtmlAttributes attributes,
        string url,
        Uri absolute,
        bool isImage,
        RenderContext context)
    {
        var scheme = absolute.Scheme;
        if (scheme == Uri.UriSchemeHttp || scheme == Uri.UriSchemeHttps)
        {
            if (!isImage)
            {
                attributes.AddProperty("rel", "noopener noreferrer");
                return url;
            }

            if (context.LoadRemoteImages)
            {
                return url;
            }

            attributes.AddClass(RemoteBlockedClass);
            attributes.AddProperty("data-src", url);
            return string.Empty;
        }

        if (scheme == Uri.UriSchemeMailto && !isImage)
        {
            return url;
        }

        attributes.AddClass(UnsafeClass);
        return string.Empty;
    }

    /// <summary>
    /// <c>C:\docs\a.md</c> and <c>/docs/a.md</c> parse as absolute file URIs, but are local paths.
    /// </summary>
    private static bool IsRootedLocalPath(string url)
        => Path.IsPathRooted(url) && !url.Contains("://", StringComparison.Ordinal);

    private static bool IsInside(
        string root,
        string fullPath)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(root), fullPath);
        return !relative.StartsWith("..", StringComparison.Ordinal) && !Path.IsPathRooted(relative);
    }
}