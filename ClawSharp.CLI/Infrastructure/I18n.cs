using System.Globalization;
using System.Reflection;
using System.Resources;
using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClawSharp.CLI.Infrastructure;

public static class I18n
{
    private static readonly ResourceManager ResourceManager = new(
        "ClawSharp.CLI.Resources.Strings",
        Assembly.GetExecutingAssembly());

    private static readonly CultureInfo EnglishCulture = CultureInfo.GetCultureInfo("en-US");
    private static string _currentCulture = EnglishCulture.Name;

    public static void Initialize(IHost host)
    {
        var options = host.Services.GetRequiredService<ClawOptions>();
        SetCulture(options.Runtime.UILanguage);
    }

    public static void InitializeForBootstrap(string? basePath = null)
    {
        var resolvedBasePath = string.IsNullOrWhiteSpace(basePath)
            ? Directory.GetCurrentDirectory()
            : Path.GetFullPath(basePath);

        var config = new ConfigurationBuilder()
            .SetBasePath(resolvedBasePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
            .Build();

        SetCulture(config["Runtime:UILanguage"]);
    }

    public static string T(string key) => T(key, []);

    public static string T(string key, params object[] args)
    {
        var template = Lookup(key);
        return args.Length == 0
            ? template
            : string.Format(CultureInfo.CurrentUICulture, template, args);
    }

    internal static string NormalizeCulture(string? culture)
    {
        if (string.IsNullOrWhiteSpace(culture))
        {
            culture = CultureInfo.CurrentUICulture.Name;
        }

        return culture.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            ? "zh-CN"
            : "en-US";
    }

    internal static void SetCulture(string? culture)
    {
        _currentCulture = NormalizeCulture(culture);
        var cultureInfo = CultureInfo.GetCultureInfo(_currentCulture);
        CultureInfo.CurrentCulture = cultureInfo;
        CultureInfo.CurrentUICulture = cultureInfo;
    }

    internal static string CurrentCultureName => _currentCulture;

    private static string Lookup(string key)
    {
        var localized = ResourceManager.GetString(key, CultureInfo.GetCultureInfo(_currentCulture));
        if (!string.IsNullOrWhiteSpace(localized))
        {
            return localized;
        }

        var english = ResourceManager.GetString(key, EnglishCulture);
        return string.IsNullOrWhiteSpace(english) ? key : english;
    }
}
