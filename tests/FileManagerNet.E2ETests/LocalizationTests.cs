using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class LocalizationTests : E2ETestBase
{
    public LocalizationTests(TestHostFixture fx) : base(fx) { }

    [Theory]
    [InlineData("zh-CN", "复制")]
    [InlineData("ja", "コピー")]
    [InlineData("de", "Kopieren")]
    [InlineData("en", "Copy")]
    public async Task Locale_query_switches_the_ui_language(string locale, string copyLabel)
    {
        await Fm.GotoAsync("/Files", "?locale=" + locale);
        await Expect(Page.Locator(".fm-ribbon-body:not([hidden]) [data-act='copy'] span"))
            .ToHaveTextAsync(copyLabel);
    }
}
