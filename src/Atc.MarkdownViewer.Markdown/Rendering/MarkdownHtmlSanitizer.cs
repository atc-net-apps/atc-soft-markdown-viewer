namespace Atc.MarkdownViewer.Markdown.Rendering;

/// <summary>
/// Sanitizes render units that contain raw HTML (plan SEC-001, MD-008). The web view's CSP blocks scripts,
/// event handlers and <c>javascript:</c> URLs as a second line of defence.
/// </summary>
internal static class MarkdownHtmlSanitizer
{
    private static readonly string[] ExtraTags =
    [
        "details", "summary", "kbd", "sup", "sub", "picture", "source", "input", "svg", "path",
    ];

    private static readonly string[] ExtraAttributes =
    [
        "class", "id", "rel", "srcset", "checked", "disabled", "type", "start", "align",
        "aria-hidden", "viewbox", "d", "fill-rule", "version", "width", "height", "open",
    ];

    public static HtmlSanitizer Create()
    {
        var sanitizer = new HtmlSanitizer { AllowDataAttributes = true };
        foreach (var tag in ExtraTags)
        {
            sanitizer.AllowedTags.Add(tag);
        }

        foreach (var attribute in ExtraAttributes)
        {
            sanitizer.AllowedAttributes.Add(attribute);
        }

        sanitizer.AllowedSchemes.Add(Uri.UriSchemeMailto);
        return sanitizer;
    }
}