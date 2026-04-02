using ClawSharp.CLI.Infrastructure;

namespace ClawSharp.Lib.Tests;

public class I18nTests
{
    [Fact]
    public void NormalizeCulture_MapsChineseVariantsToZhCn()
    {
        var normalized = I18n.NormalizeCulture("zh-TW");

        Assert.Equal("zh-CN", normalized);
    }

    [Fact]
    public void T_FallsBackToEnglishResource_WhenLocalizedEntryIsMissing()
    {
        I18n.SetCulture("zh-TW");

        var text = I18n.T("Test.EnglishOnly");

        Assert.Equal("English fallback", text);
        Assert.Equal("zh-CN", I18n.CurrentCultureName);
    }

    [Fact]
    public void T_FormatsMessagesUsingCurrentCultureResources()
    {
        I18n.SetCulture("zh-CN");

        var text = I18n.T("Test.Formatted", "ClawSharp");

        Assert.Equal("你好 ClawSharp", text);
    }
}
