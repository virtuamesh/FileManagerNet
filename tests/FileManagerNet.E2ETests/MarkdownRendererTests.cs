using Microsoft.Playwright;

namespace FileManagerNet.E2ETests;

/// <summary>
/// Unit-level checks for the Markdown renderer. The renderer lives in the browser
/// bundle, so these tests call its pure functions directly instead of driving the
/// preview dialog (that flow is covered by <see cref="PreviewTests"/>).
/// </summary>
public sealed class MarkdownRendererTests : E2ETestBase
{
    public MarkdownRendererTests(TestHostFixture fx) : base(fx) { }

    private Task<string> RenderAsync(string markdown) =>
        Page.EvaluateAsync<string>(
            "(text) => window.FileManagerNet.markdown.render(text, (u) => u)",
            markdown);

    private Task<string> RenderWithResolverAsync(string markdown) =>
        Page.EvaluateAsync<string>(
            "(text) => window.FileManagerNet.markdown.render(text, " +
            "(u) => window.FileManagerNet.markdown.resolveUrl('/docs', u, 'API'))",
            markdown);

    [Fact]
    public async Task Headings_paragraphs_and_inline_markup()
    {
        await Fm.GotoAsync();

        var html = await RenderAsync(
            "# Title\n\n## Sub\n\nHello **bold**, *italic*, ~~gone~~ and `code`.");

        Assert.Contains("<h1>Title</h1>", html);
        Assert.Contains("<h2>Sub</h2>", html);
        Assert.Contains("<strong>bold</strong>", html);
        Assert.Contains("<em>italic</em>", html);
        Assert.Contains("<del>gone</del>", html);
        Assert.Contains("<code>code</code>", html);
        Assert.Contains("<p>Hello", html);
    }

    [Fact]
    public async Task Lists_support_nesting_ordering_and_tasks()
    {
        await Fm.GotoAsync();

        var unordered = await RenderAsync("- one\n- two\n  - nested\n- [x] done");
        Assert.Contains("<ul><li>one</li>", unordered);
        Assert.Contains("<ul><li>nested</li></ul>", unordered);
        Assert.Contains("class=\"fm-md-task\"", unordered);
        Assert.Contains("checked", unordered);

        var ordered = await RenderAsync("1. first\n2. second");
        Assert.Contains("<ol><li>first</li><li>second</li></ol>", ordered);
    }

    [Fact]
    public async Task Tables_render_alignment_and_cells()
    {
        await Fm.GotoAsync();

        var html = await RenderAsync(
            "| Name | Value |\n|:-----|------:|\n| alpha | 1 |\n| beta | 2 |");

        Assert.Contains("<table><thead><tr>", html);
        Assert.Contains("<th style=\"text-align:left\">Name</th>", html);
        Assert.Contains("<th style=\"text-align:right\">Value</th>", html);
        Assert.Contains("<td style=\"text-align:left\">alpha</td>", html);
        Assert.Contains("<td style=\"text-align:right\">1</td>", html);
    }

    [Fact]
    public async Task Code_fences_and_raw_html_are_escaped()
    {
        await Fm.GotoAsync();

        var fenced = await RenderAsync("```js\nconst ok = 1 < 2 && \"yes\";\n```");
        Assert.Contains("<pre><code class=\"language-js\">", fenced);
        Assert.Contains("1 &lt; 2 &amp;&amp; &quot;yes&quot;;", fenced);

        var raw = await RenderAsync("<b>hi</b>\n\n<script>alert(1)</script>");
        Assert.Contains("&lt;b&gt;hi&lt;/b&gt;", raw);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", raw);
        Assert.DoesNotContain("<script>", raw);
    }

    [Fact]
    public async Task Links_and_images_resolve_and_block_unsafe_schemes()
    {
        await Fm.GotoAsync();

        var html = await RenderWithResolverAsync(
            "[ok](https://example.com) [bad](javascript:alert(1)) ![img](pic.png)");

        Assert.Contains("<a href=\"https://example.com\"", html);
        Assert.Contains(">ok</a>", html);
        Assert.DoesNotContain("href=\"javascript:", html);
        Assert.Contains("bad", html);   /* label survives without a link */
        Assert.Contains("src=\"API&amp;inline=1&amp;path=%2Fdocs%2Fpic.png\"", html);
    }

    [Fact]
    public async Task Resolve_url_handles_relative_absolute_and_unknown_schemes()
    {
        await Fm.GotoAsync();

        async Task<string> Resolve(string dir, string url) =>
            await Page.EvaluateAsync<string>(
                "([dir, url]) => window.FileManagerNet.markdown.resolveUrl(dir, url, 'API')",
                new[] { dir, url });

        Assert.Equal("API&inline=1&path=%2Fdocs%2Fimg%2Fa.png",
            await Resolve("/docs", "img/a.png"));
        Assert.Equal("API&inline=1&path=%2Fdocs%2Fx.png",
            await Resolve("/docs/sub", "../x.png"));
        Assert.Equal("API&inline=1&path=%2Fetc%2Fpasswd",
            await Resolve("", "../../etc/passwd"));
        Assert.Equal("https://example.com/a?b=1", await Resolve("/docs", "https://example.com/a?b=1"));
        Assert.Equal("#section", await Resolve("/docs", "#section"));
        Assert.Equal("/absolute/path", await Resolve("/docs", "/absolute/path"));
        Assert.Equal("", await Resolve("/docs", "javascript:alert(1)"));
        Assert.Equal("", await Resolve("/docs", "data:text/html;base64,PHNjcmlwdD4="));
    }
}
