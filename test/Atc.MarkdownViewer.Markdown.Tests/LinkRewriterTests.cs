namespace Atc.MarkdownViewer.Markdown.Tests;

/// <summary>
/// MD-004: links and images are resolved against the document, and unsafe URLs are removed.
/// </summary>
public sealed class LinkRewriterTests
{
    private readonly MarkdownRenderService service = new();

    [Fact]
    public void ExternalLink_GetsNoopener()
        => Render("[x](https://example.com)").Should().Contain("href=\"https://example.com\" rel=\"noopener noreferrer\"");

    [Fact]
    public void RelativeDocLink_GetsDataHrefAndHashHref()
    {
        // Act
        var html = Render("[guide](docs/guide.md#setup)");

        // Assert
        html.Should().Contain("href=\"#\"").And.Contain("data-href=\"docs/guide.md#setup\"").And.NotContain("mdv-broken");
    }

    [Fact]
    public void MissingDocLink_IsMarkedBroken()
        => Render("[gone](docs/missing.md)").Should().Contain("mdv-broken");

    [Fact]
    public void Anchor_IsLeftAlone()
        => Render("[top](#top)").Should().Contain("href=\"#top\"");

    [Theory]
    [InlineData("[x](javascript:alert(1))")]
    [InlineData("[x](vbscript:msgbox)")]
    [InlineData("[x](file:///c:/windows/system.ini)")]
    public void UnsafeScheme_IsRemoved(string markdown)
    {
        // Act
        var html = Render(markdown);

        // Assert
        html.Should().Contain("mdv-unsafe").And.Contain("href=\"\"");
    }

    [Fact]
    public void LocalImage_IsServedFromAssetUrl()
        => Render("![logo](assets/placeholder.svg)").Should().Contain("src=\"https://doc.mdv/assets/placeholder.svg\"");

    [Fact]
    public void ImageWithSpaces_IsEscaped()
        => Render("![x](<assets/my image.png>)").Should().Contain("src=\"https://doc.mdv/assets/my%20image.png\"").And.Contain("mdv-broken");

    [Fact]
    public void ImageOutsideAssetRoot_IsNotLoaded()
    {
        // Act
        var html = Render("![x](../outside.png)");

        // Assert
        html.Should().Contain("mdv-outside-root").And.Contain("src=\"\"");
    }

    [Fact]
    public void RemoteImage_WhenBlocked_KeepsUrlInDataSrc()
    {
        // Arrange
        var context = FixtureFiles.GfmContext with { LoadRemoteImages = false };

        // Act
        var html = service.Render("![x](https://example.com/a.png)", context).Blocks[0].Html;

        // Assert
        html.Should().Contain("mdv-remote-blocked").And.Contain("data-src=\"https://example.com/a.png\"").And.Contain("src=\"\"");
    }

    [Fact]
    public void TextWithoutFile_LeavesRelativeLinksUntouched()
        => service.Render("[x](docs/a.md)", RenderContext.ForText()).Blocks[0].Html.Should().Contain("href=\"docs/a.md\"");

    private string Render(string markdown)
        => service.Render(markdown + "\n", FixtureFiles.GfmContext).Blocks[0].Html;
}