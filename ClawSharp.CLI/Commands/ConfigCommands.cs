using System.CommandLine;
using ClawSharp.CLI.Infrastructure;
using ClawSharp.Lib.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Spectre.Console;

namespace ClawSharp.CLI.Commands;

public static class ConfigCommands
{
    public static Command Create(IHost host)
    {
        var configCommand = new Command("config", I18n.T("Config.Description"));

        configCommand.AddCommand(CreateList(host));
        configCommand.AddCommand(CreateGet(host));
        configCommand.AddCommand(CreateSet(host));
        configCommand.AddCommand(CreateReset(host));

        return configCommand;
    }

    private static Command CreateList(IHost host)
    {
        var command = new Command("list", I18n.T("Config.List.Description"));
        var allOption = new Option<bool>("--all", I18n.T("Config.List.AllOption"));
        command.AddOption(allOption);

        command.SetHandler(async (showAll) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                var allConfigs = await configManager.GetAllAsync();
                
                var table = new Table().Border(TableBorder.Rounded);
                table.AddColumn(I18n.T("Chat.Config.Column.Key"));
                table.AddColumn(I18n.T("Common.Value"));

                var keysToShow = showAll 
                    ? await configManager.GetSupportedKeysAsync() 
                    : allConfigs.Keys.OrderBy(k => k);

                foreach (var key in keysToShow.OrderBy(k => k))
                {
                    // 过滤掉 IConfiguration 自动生成的一些内部键（如 : 分隔符产生的空键）
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    string? displayValue;
                    if (allConfigs.TryGetValue(key, out var val))
                    {
                        displayValue = configManager.IsSecret(key) ? "********" : val;
                    }
                    else
                    {
                        displayValue = $"[grey]{I18n.T("Common.NotSet")}[/]";
                    }

                    table.AddRow(key.EscapeMarkup(), displayValue?.EscapeMarkup() ?? $"[grey]{I18n.T("Common.Null")}[/]");
                }

                AnsiConsole.Write(table);
                return 0;
            });
        }, allOption);

        return command;
    }

    private static Command CreateGet(IHost host)
    {
        var command = new Command("get", I18n.T("Config.Get.Description"));
        var keyArg = new Argument<string>("key", I18n.T("Config.Get.KeyArg"));
        command.AddArgument(keyArg);

        command.SetHandler(async (key) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                var value = configManager.Get(key);
                
                if (value == null)
                {
                    AnsiConsole.MarkupLine(I18n.T("Config.Get.KeyMissing", key.EscapeMarkup()));
                }
                else
                {
                    var displayValue = configManager.IsSecret(key) ? "********" : value;
                    AnsiConsole.WriteLine(displayValue);
                }
                return 0;
            });
        }, keyArg);

        return command;
    }

    private static Command CreateSet(IHost host)
    {
        var command = new Command("set", I18n.T("Config.Set.Description"));
        var keyArg = new Argument<string>("key", I18n.T("Config.Set.KeyArg"));
        var valueArg = new Argument<string?>("value", () => null, I18n.T("Config.Set.ValueArg"));
        command.AddArgument(keyArg);
        command.AddArgument(valueArg);

        command.SetHandler(async (key, value) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                var configManager = host.Services.GetRequiredService<IConfigManager>();
                
                var finalValue = value;
                if (string.IsNullOrEmpty(finalValue) && configManager.IsSecret(key))
                {
                    finalValue = AnsiConsole.Prompt(
                        new TextPrompt<string>(I18n.T("Config.Set.Prompt", key.EscapeMarkup()))
                            .Secret());
                }

                if (finalValue == null)
                {
                     AnsiConsole.MarkupLine(I18n.T("Config.Set.ValueRequired"));
                     return 1;
                }

                await configManager.SetAsync(key, finalValue);
                AnsiConsole.MarkupLine(I18n.T("Config.Set.Success", key.EscapeMarkup()));
                return 0;
            });
        }, keyArg, valueArg);

        return command;
    }

    private static Command CreateReset(IHost host)
    {
        var command = new Command("reset", I18n.T("Config.Reset.Description"));
        var allOption = new Option<bool>("--all", I18n.T("Config.Reset.AllOption"));
        var keyOption = new Option<string?>("--key", I18n.T("Config.Reset.KeyOption"));
        var forceOption = new Option<bool>("--force", I18n.T("Config.Reset.ForceOption"));
        
        command.AddOption(allOption);
        command.AddOption(keyOption);
        command.AddOption(forceOption);

        command.SetHandler(async (all, key, force) =>
        {
            await CliErrorHandler.ExecuteWithHandlingAsync(async () =>
            {
                if (!all && string.IsNullOrEmpty(key))
                {
                    AnsiConsole.MarkupLine(I18n.T("Config.Reset.MissingTarget"));
                    return 1;
                }

                if (!force)
                {
                    var message = all
                        ? I18n.T("Config.Reset.ConfirmAll")
                        : I18n.T("Config.Reset.ConfirmKey", key!.EscapeMarkup());
                    if (!AnsiConsole.Confirm(message))
                    {
                        AnsiConsole.MarkupLine(I18n.T("Config.Reset.Cancelled"));
                        return 0;
                    }
                }

                var configManager = host.Services.GetRequiredService<IConfigManager>();
                await configManager.ResetAsync(all, key, force);
                AnsiConsole.MarkupLine(I18n.T("Config.Reset.Success"));
                return 0;
            });
        }, allOption, keyOption, forceOption);

        return command;
    }
}
