using Microsoft.Playwright;
using static Microsoft.Playwright.Assertions;

namespace FileManagerNet.E2ETests;

public sealed class MultiInstanceTests : E2ETestBase
{
    public MultiInstanceTests(TestHostFixture fx) : base(fx) { }

    [Fact]
    public async Task Two_instances_keep_selection_and_lists_independent()
    {
        await Page.GotoAsync(Fx.BaseUrl + "/Multi");
        var instances = Page.Locator(".fmnet-root");
        await Expect(instances).ToHaveCountAsync(2);

        await Expect(Page.Locator("[data-instance='FM_A'] [data-name='readme.txt']")).ToBeVisibleAsync();
        await Expect(Page.Locator("[data-instance='FM_B'] [data-name='other.txt']")).ToBeVisibleAsync();

        await Page.Locator("[data-instance='FM_A'] [data-name='readme.txt']").ClickAsync();
        await Page.WaitForTimeoutAsync(150);

        var counts = await Page.EvaluateAsync<int[]>(@"() => {
            const els = Array.from(document.querySelectorAll('[data-fmnet]'));
            return els.map(e => e.fmnet.selection.size);
        }");
        Assert.Equal(1, counts[0]);
        Assert.Equal(0, counts[1]);
    }

    [Fact]
    public async Task The_second_instance_works_too()
    {
        await Page.GotoAsync(Fx.BaseUrl + "/Multi");
        await Expect(Page.Locator("[data-instance='FM_B'] [data-name='other.txt']")).ToBeVisibleAsync();

        await Page.Locator("[data-instance='FM_B'] .fm-tree [data-name='second']").ClickAsync();
        await Page.WaitForTimeoutAsync(250);

        var path = await Page.EvaluateAsync<string>(
            "() => document.querySelector('[data-instance=\"FM_B\"]').fmnet.path");
        Assert.Equal("/second", path);
    }
}
